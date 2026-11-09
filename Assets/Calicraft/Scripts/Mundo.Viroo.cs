using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using Viroo.Api;
using Viroo.Teleport;

// Calicraft dentro de VIROO (sala con Room Player, Single Player o escritorio).
// En la sala nadie camina con la palanca: cada uno se mueve caminando de verdad, y el teletransporte
// (palanca derecha adelante + gatillo, o el menú de VIROO) mueve a TODO el grupo. Por eso:
//   - el jugador lo pone VIROO (cámara, manos, avatar); aquí no hay CharacterController propio;
//   - el mundo es el mismo para todos (semilla fija) y los cambios viajan por CalicraftRed;
//   - hay un punto de teletransporte en cada monumento y en cada casa del terremoto;
//   - modo cooperativo: sin monstruos ni vidas; las memorias y las casas son del grupo.
public partial class Mundo
{
    public static Mundo Instancia { get; private set; }
    public Camera Camara => cam;

    const int SEMILLA = 1925;            // el terremoto de 1925 (La Ermita): el mismo mundo en todos los PC
    const int BLOQUES = 7;               // de tipos[]: Piedra…Vidrio (sin TNT ni antorcha: no se sincronizan)
    const float GATILLO = 0.7f;

    static readonly List<(string datos, bool efectos)> pendientes = new List<(string, bool)>();
    bool silencio;                       // reconstruyendo el estado de la sesión: sin sonidos ni paneles
    bool esVR;
    Transform manoIzq, manoDer;
    readonly Dictionary<int, float> memoriaPedida = new Dictionary<int, float>();

    TextMeshPro hud;
    Transform panel, fondoPanel;
    TextMeshPro panelTitulo, panelTexto;
    float tPanel;
    Transform marco;
    bool gatilloAntes, agarreAntes, agarreIzqAntes, botonAntes, botonIzqAntes;
    float tTeleport, tBuscarManos;

    void Awake() => Instancia = this;

    // ---------------------------------------------------------------------------------------------
    // La cámara del rig de VIROO (espera a que VIROO termine de arrancar)
    async Task<Camera> CamaraViroo()
    {
        float limite = Time.realtimeSinceStartup + 40f;
        bool avisado = false;
        while (Time.realtimeSinceStartup < limite)
        {
            try
            {
                var api = VirooApi.Instance;
                if (api != null && api.IsInitialized)
                {
                    var ctx = api.Context();
                    esVR = ctx.IsVr;
                    BuscarManos();
                    var c = ctx.VirooCamera?.OriginalCamera;
                    if (c != null) return c;
                }
            }
            catch (Exception e)
            {
                if (!avisado) Debug.LogWarning("[Calicraft] VIROO todavía no está listo: " + e.Message);
                avisado = true;
            }
            await Task.Yield();
        }
        Debug.LogWarning("[Calicraft] No llegó la cámara de VIROO; uso Camera.main");
        return Camera.main;
    }

    void BuscarManos()
    {
        try
        {
            var ctx = VirooApi.Instance.Context();
            if (ctx.RightHand) manoDer = ctx.RightHand.transform;
            if (ctx.LeftHand) manoIzq = ctx.LeftHand.transform;
        }
        catch (Exception) { /* VIROO aún arrancando: se reintenta */ }
    }

    // el "cuerpo" local: un punto en los pies que sigue a la cabeza (para tocar los cubos caminando)
    void CrearJugadorViroo()
    {
        jugador = new GameObject("JugadorLocal");
        jugador.transform.SetParent(raiz, false);
        jugador.layer = CAPA_JUGADOR;
        cc = null;
        tipoSel = Mathf.Min(tipoSel, BLOQUES - 1);
    }

    // se llama al terminar de construir el mundo
    void PrepararViroo()
    {
        ZonasDeTeletransporte();
        ColocarPuntosDeTeletransporte();
        if (hud == null) CrearHud();
        if (panel == null) CrearPanel();
        if (marco == null) CrearMarco();
        CrearPantallas();   // las pantallas de video de los monumentos (Mundo.Videos.cs)

        // lo que llegó por la red mientras se construía el mundo (incluye lo de quien entra tarde)
        var lista = new List<(string, bool)>(pendientes);
        pendientes.Clear();
        foreach (var (datos, _) in lista) Aplicar(datos, false);
    }

    // ---------------------------------------------------------------------------------------------
    // cada cuadro
    void ActualizarViroo(float dt)
    {
        if (cam == null) return;
        var cabeza = cam.transform.position;
        int gx = Mathf.RoundToInt(cabeza.x), gz = Mathf.RoundToInt(cabeza.z);
        float suelo = terreno.AlturaSuelo(gx, gz);
        jugador.transform.position = new Vector3(cabeza.x, Mathf.Max(suelo, cabeza.y - 1.6f), cabeza.z);

        if (esVR && (manoDer == null || manoIzq == null) && (tBuscarManos -= dt) <= 0) { tBuscarManos = 1; BuscarManos(); }

        var rayo = esVR && manoDer != null ? new Ray(manoDer.position, manoDer.forward) : new Ray(cabeza, cam.transform.forward);
        bool romper = false, poner = false, cambiar = false;

        if (esVR) LeerMandos(ref romper, ref poner, ref cambiar, dt);
        else
        {
            // escritorio (probar en el editor o VIROO en PC): F rompe, G pone, C o 1-7 cambian el bloque
            romper = Input.GetKeyDown(KeyCode.F);
            poner = Input.GetKeyDown(KeyCode.G);
            cambiar = Input.GetKeyDown(KeyCode.C);
            for (int k = 0; k < BLOQUES; k++) if (Input.GetKeyDown(KeyCode.Alpha1 + k)) { tipoSel = k; Sonar("clic"); }
        }

        if (cambiar) { tipoSel = (tipoSel + 1) % BLOQUES; Sonar("clic"); Aviso("Bloque: " + tipos[tipoSel].nombre); }
        if (romper && !TocarBotonVideo(rayo) && !TocarMemoriaConRayo(rayo)) RomperRed(rayo);
        if (poner) PonerRed(rayo);

        MostrarMarco(rayo);
        ActualizarHud();
        ActualizarPanel(dt);
    }

    void LeerMandos(ref bool romper, ref bool poner, ref bool cambiar, float dt)
    {
        try
        {
            var input = VirooApi.Instance.Input();
            var der = input.RightController;
            var izq = input.LeftController;
            if (tTeleport > 0) tTeleport -= dt;
            if (der != null)
            {
                // palanca derecha adelante = teletransporte de VIROO; su gatillo confirma el salto, no rompe bloques
                if (der.Primary2DAxis().y > 0.5f) tTeleport = 0.6f;
                bool g = der.Trigger() > GATILLO;
                if (g && !gatilloAntes && tTeleport <= 0) romper = true;
                gatilloAntes = g;
                bool a = der.Grip() > GATILLO;
                if (a && !agarreAntes) poner = true;
                agarreAntes = a;
                bool b = der.PrimaryButton();
                if (b && !botonAntes) cambiar = true;
                botonAntes = b;
            }
            if (izq != null)
            {
                bool a = izq.Grip() > GATILLO;
                if (a && !agarreIzqAntes) poner = true;
                agarreIzqAntes = a;
                bool b = izq.PrimaryButton();
                if (b && !botonIzqAntes) cambiar = true;
                botonIzqAntes = b;
            }
        }
        catch (Exception) { /* mandos aún no disponibles */ }
    }

    // ---------------------------------------------------------------------------------------------
    // apuntar: solo cuenta lo que es del mundo de Calicraft (no el rig ni los avatares de VIROO)
    bool ApuntarMundo(Ray r, out RaycastHit hit)
    {
        hit = default;
        var hits = Physics.RaycastAll(r, ALCANCE, ~0, QueryTriggerInteraction.Ignore);
        float mejor = float.MaxValue;
        bool hay = false;
        foreach (var h in hits)
        {
            if (h.distance >= mejor || raiz == null || !h.collider.transform.IsChildOf(raiz)) continue;
            mejor = h.distance; hit = h; hay = true;
        }
        return hay;
    }

    void RomperRed(Ray r)
    {
        if (!ApuntarMundo(r, out var hit) || hit.collider.GetComponent<TrozoTerreno>() == null) return;
        var c = Celda(hit.point - hit.normal * 0.5f);
        if (terreno.Get(c.x, c.y, c.z) == B.Aire) return;
        Enviar($"r|{c.x}|{c.y}|{c.z}");
    }

    void PonerRed(Ray r)
    {
        if (!ApuntarMundo(r, out var hit)) return;
        var c = Celda(hit.point + hit.normal * 0.5f);
        var centro = new Vector3(c.x, c.y + 0.5f, c.z);
        if (Vector3.Distance(centro, cam.transform.position) < 0.8f) return;    // no en tu propia cara
        if (terreno.Get(c.x, c.y, c.z) != B.Aire) return;
        Enviar($"p|{c.x}|{c.y}|{c.z}|{(int)tipos[tipoSel].bloque}");
    }

    // apuntar a un cubo de memoria y apretar el gatillo también lo descubre
    bool TocarMemoriaConRayo(Ray r)
    {
        for (int i = 0; i < cubos.Count; i++)
        {
            var c = cubos[i];
            if (c.obj == null) continue;
            var v = c.obj.position - r.origin;
            float t = Vector3.Dot(v, r.direction);
            if (t < 0 || t > ALCANCE) continue;
            if ((v - r.direction * t).magnitude > 0.7f) continue;
            PedirMemoria(i);
            return true;
        }
        return false;
    }

    // se pide una vez; si en 3 s no volvió por la red, se puede pedir de nuevo
    void PedirMemoria(int i)
    {
        if (memoriaPedida.TryGetValue(i, out float t) && Time.time - t < 3f) return;
        memoriaPedida[i] = Time.time;
        Enviar("m|" + i);
    }

    // ---------------------------------------------------------------------------------------------
    // red
    void Enviar(string datos)
    {
        var red = CalicraftRed.Instancia;
        if (red != null && red.IsInitialized)
        {
            try { red.Execute(datos); return; }
            catch (Exception e) { Debug.LogWarning("[Calicraft] No se pudo enviar por la red: " + e.Message); }
        }
        Aplicar(datos, true);   // sin red (pruebas sueltas): se aplica solo aquí
    }

    public static void Recibir(string datos, bool efectos)
    {
        var m = Instancia;
        if (m == null || m.cargando || m.terreno == null || m.hud == null) { pendientes.Add((datos, efectos)); return; }
        m.Aplicar(datos, efectos);
    }

    void Aplicar(string datos, bool efectos)
    {
        silencio = !efectos;
        try
        {
            var p = datos.Split('|');
            switch (p[0])
            {
                case "r": AplicarRomper(Celda3(p)); break;
                case "p": AplicarPoner(Celda3(p), (B)int.Parse(p[4], CultureInfo.InvariantCulture)); break;
                case "m": RecogerMemoria(int.Parse(p[1], CultureInfo.InvariantCulture)); break;
                case "v": ReproducirVideo(int.Parse(p[1], CultureInfo.InvariantCulture), int.Parse(p[2], CultureInfo.InvariantCulture)); break;
                default: Debug.LogWarning("[Calicraft] Mensaje desconocido: " + datos); break;
            }
        }
        catch (Exception e) { Debug.LogWarning($"[Calicraft] Mensaje inválido '{datos}': {e.Message}"); }
        finally { silencio = false; }
    }

    static Vector3Int Celda3(string[] p) => new Vector3Int(
        int.Parse(p[1], CultureInfo.InvariantCulture), int.Parse(p[2], CultureInfo.InvariantCulture), int.Parse(p[3], CultureInfo.InvariantCulture));

    void AplicarRomper(Vector3Int c)
    {
        var b = terreno.Get(c.x, c.y, c.z);
        if (b == B.Aire || !terreno.Set(c.x, c.y, c.z, B.Aire)) return;
        if (silencio) return;
        Efectos.Escombros(new Vector3(c.x, c.y + 0.5f, c.z), ColorDe(b));
        Sonar("clic");
    }

    void AplicarPoner(Vector3Int c, B b)
    {
        if (terreno.Get(c.x, c.y, c.z) != B.Aire) return;
        if (terreno.Set(c.x, c.y, c.z, b)) RevisarHueco(c);
    }

    // ---------------------------------------------------------------------------------------------
    // teletransporte: todo el terreno es zona válida (capa de interacción "Teleport", la de la plantilla)
    void ZonasDeTeletransporte()
    {
        foreach (var t in terreno.GetComponentsInChildren<TrozoTerreno>(true))
        {
            if (t.GetComponent<TeleportationArea>() != null) continue;
            var area = t.gameObject.AddComponent<TeleportationArea>();
            area.interactionLayers = new InteractionLayerMask { value = unchecked((int)0x80000000) };
        }
    }

    // los puntos se crean en el editor (VIROO → Calicraft → Configurar escena) con su x,z;
    // aquí se bajan al suelo real, frente a su monumento o a la puerta de su casa
    void ColocarPuntosDeTeletransporte()
    {
        foreach (var tp in FindObjectsByType<TeleportPoint>(FindObjectsSortMode.None))
        {
            string id = tp.Identifier ?? "";
            if (id == "inicio")
            {
                tp.transform.SetPositionAndRotation(new Vector3(0, terreno.AlturaSuelo(0, 0), 0), Quaternion.identity);
            }
            else if (id.StartsWith("mem_"))
            {
                var c = cubos.Find(k => "mem_" + k.m.id == id);
                if (c == null) continue;
                var haciaCentro = new Vector3(-c.lugar.x, 0, -c.lugar.z);
                if (haciaCentro.sqrMagnitude < 1) haciaCentro = Vector3.back;
                var p = c.lugar + haciaCentro.normalized * 3f;
                int x = Mathf.RoundToInt(p.x), z = Mathf.RoundToInt(p.z);
                var mira = new Vector3(c.lugar.x - x, 0, c.lugar.z - z);
                tp.transform.SetPositionAndRotation(new Vector3(x, terreno.AlturaSuelo(x, z), z),
                    mira.sqrMagnitude > 0.01f ? Quaternion.LookRotation(mira) : Quaternion.identity);
            }
            else if (id.StartsWith("casa_") && int.TryParse(id.Substring(5), out int i) && i < casas.Count)
            {
                var cen = casas[i].centro;
                int x = Mathf.RoundToInt(cen.x), z = Mathf.RoundToInt(cen.z) - 6;   // frente a la puerta (lado -z)
                tp.transform.SetPositionAndRotation(new Vector3(x, terreno.AlturaSuelo(x, z), z), Quaternion.identity);
            }
        }
    }

    // ---------------------------------------------------------------------------------------------
    // textos en el mundo (en VR no existe la pantalla: OnGUI no se ve en las gafas)
    // TextMeshPro 3D: fontSize 10 ≈ 1 metro de alto por línea
    TextMeshPro Texto3D(string nombre, Transform padre, float metros, Vector2 tam, TextAlignmentOptions al)
    {
        var g = new GameObject(nombre);
        g.transform.SetParent(padre, false);
        var t = g.AddComponent<TextMeshPro>();
        t.fontSize = metros * 10f;
        t.rectTransform.sizeDelta = tam;
        t.alignment = al;
        t.textWrappingMode = TextWrappingModes.Normal;
        return t;
    }

    void CrearHud()
    {
        hud = Texto3D("HUD", cam.transform, 0.03f, new Vector2(1.4f, 0.3f), TextAlignmentOptions.Bottom);
        hud.transform.localPosition = new Vector3(0, -0.34f, 0.9f);
        hud.transform.localRotation = Quaternion.Euler(20, 0, 0);
        hud.outlineWidth = 0.2f;
        hud.outlineColor = new Color32(0, 0, 0, 220);
    }

    void ActualizarHud()
    {
        string linea = $"Memorias {recogidos.Count}/{MEMORIAS.Length}   ·   Casas {CasasListas}/{casas.Count}   ·   Bloque: {tipos[tipoSel].nombre}";
        if (tAviso > 0 && aviso != "") linea = $"<color=#FFE066>{aviso}</color>\n" + linea;
        if (termino) linea = $"<color=#7FE9FF>{tituloFin}</color>\n" + linea;
        hud.text = linea;
    }

    void CrearPanel()
    {
        panel = new GameObject("PanelConsigna").transform;
        panel.SetParent(raiz, false);
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(q.GetComponent<Collider>());
        q.name = "Fondo";
        q.transform.SetParent(panel, false);
        q.GetComponent<Renderer>().sharedMaterial = new Material(Shaders.Buscar("Universal Render Pipeline/Unlit"))
            { color = new Color(0.055f, 0.07f, 0.125f) };
        fondoPanel = q.transform;
        panelTitulo = Texto3D("Titulo", panel, 0.16f, new Vector2(2.3f, 0.3f), TextAlignmentOptions.TopLeft);
        panelTexto = Texto3D("Texto", panel, 0.1f, new Vector2(2.3f, 2f), TextAlignmentOptions.TopLeft);
        panel.gameObject.SetActive(false);
    }

    // la consigna aparece sobre su monumento, para todos los que estén cerca
    void MostrarPanel(string titulo, string texto, Color col, Vector3 donde)
    {
        if (panel == null) return;
        panelTitulo.text = titulo;
        panelTitulo.color = col;
        panelTexto.text = texto;
        float alto = panelTexto.GetPreferredValues(texto, 2.3f, 0).y;
        panelTitulo.transform.localPosition = new Vector3(0, alto / 2 + 0.2f, -0.01f);
        panelTexto.transform.localPosition = new Vector3(0, -0.15f, -0.01f);
        panelTexto.rectTransform.sizeDelta = new Vector2(2.3f, alto + 0.05f);
        fondoPanel.localScale = new Vector3(2.6f, alto + 0.75f, 1);
        fondoPanel.localPosition = new Vector3(0, 0.05f, 0);
        panel.position = donde + Vector3.up * (1.4f + alto / 2);
        panel.gameObject.SetActive(true);
        tPanel = 25f;
    }

    void ActualizarPanel(float dt)
    {
        if (panel == null || !panel.gameObject.activeSelf) return;
        if ((tPanel -= dt) <= 0) { panel.gameObject.SetActive(false); return; }
        var hacia = panel.position - cam.transform.position;   // de frente a quien lo mira
        hacia.y = 0;
        if (hacia.sqrMagnitude > 0.01f) panel.rotation = Quaternion.LookRotation(hacia);
    }

    // un cubo transparente marca el bloque al que apuntas
    void CrearMarco()
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(g.GetComponent<Collider>());
        g.name = "Marco";
        marco = g.transform;
        marco.SetParent(raiz, false);
        marco.localScale = Vector3.one * 1.03f;
        var m = new Material(ShaderBase()) { color = Color.white };
        Transparente(m, 0.22f);
        g.GetComponent<Renderer>().sharedMaterial = m;
        g.SetActive(false);
    }

    void MostrarMarco(Ray r)
    {
        if (marco == null) return;
        if (ApuntarMundo(r, out var hit) && hit.collider.GetComponent<TrozoTerreno>() != null)
        {
            var c = Celda(hit.point - hit.normal * 0.5f);
            marco.position = new Vector3(c.x, c.y + 0.5f, c.z);
            marco.gameObject.SetActive(true);
        }
        else marco.gameObject.SetActive(false);
    }
}

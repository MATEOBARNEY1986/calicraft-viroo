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
//   - sin monstruos ni vidas; las casas son del grupo, pero cada MEMORIA es de quien llega primero:
//     solo esa persona puede poner su video y al final sale la tabla de quién recuperó más.
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
    bool sinViroo;                       // VIROO no arrancó (p. ej. el editor en Linux): modo de prueba
    float yawPrueba, pitchPrueba;
    int puntoPrueba = -1;
    Transform manoIzq, manoDer;
    readonly Dictionary<int, float> memoriaPedida = new Dictionary<int, float>();

    // quién soy y de quién es cada memoria (el identificador es el ClientId de VIROO)
    string miId = "", miNombre = "";
    readonly Dictionary<int, string> duenoMemoria = new Dictionary<int, string>();      // memoria -> jugador
    readonly Dictionary<string, string> nombreJugador = new Dictionary<string, string>(); // jugador -> nombre
    readonly Dictionary<string, int> memoriasDe = new Dictionary<string, int>();        // jugador -> cuántas

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
        float limite = Time.realtimeSinceStartup + 15f;
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
        Debug.LogWarning("[Calicraft] VIROO no arrancó: MODO DE PRUEBA (WASD caminar, flechas o Q/E girar, T siguiente estación)");
        sinViroo = true;
        var c0 = Camera.main;
        if (c0 == null)
        {
            c0 = new GameObject("CamaraPrueba").AddComponent<Camera>();
            c0.gameObject.AddComponent<AudioListener>();
            c0.tag = "MainCamera";
        }
        return c0;
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
        if (sinViroo) IrAEstacion(true);

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
        if (sinViroo) MoverSinViroo(dt);
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

    // ---------------------------------------------------------------------------------------------
    // MODO DE PRUEBA (sin VIROO): para recorrer el mundo desde el editor en Linux.
    // WASD caminar (Shift corre); girar: flechas ← → o Q/E; mirar arriba/abajo: flechas ↑ ↓
    // (pensado para portátil, sin mouse); también clic derecho + mouse. T = siguiente estación
    // (punto de teletransporte), Y = volver al inicio. Se camina sobre el terreno: sube y baja con él.
    void MoverSinViroo(float dt)
    {
        var t = cam.transform;
        if (Input.GetMouseButton(1))
        {
            yawPrueba += Input.GetAxis("Mouse X") * 3f;
            pitchPrueba = Mathf.Clamp(pitchPrueba - Input.GetAxis("Mouse Y") * 3f, -80f, 80f);
        }
        float giro = (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.E) ? 1 : 0) - (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.Q) ? 1 : 0);
        float cabeceo = (Input.GetKey(KeyCode.DownArrow) ? 1 : 0) - (Input.GetKey(KeyCode.UpArrow) ? 1 : 0);
        yawPrueba += giro * 90f * dt;
        pitchPrueba = Mathf.Clamp(pitchPrueba + cabeceo * 60f * dt, -80f, 80f);
        if (Input.GetKeyDown(KeyCode.T) || Input.GetKeyDown(KeyCode.Y)) IrAEstacion(Input.GetKeyDown(KeyCode.Y));
        t.rotation = Quaternion.Euler(pitchPrueba, yawPrueba, 0);
        float h = (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0);
        float v = (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0);
        var adelante = t.forward; adelante.y = 0; adelante.Normalize();
        var derecha = t.right; derecha.y = 0; derecha.Normalize();
        float vel = Input.GetKey(KeyCode.LeftShift) ? 10f : 4.5f;
        var p = t.position + (adelante * v + derecha * h) * vel * dt;
        float suelo = terreno.AlturaSuelo(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.z));
        p.y = Mathf.Lerp(p.y, suelo + 1.65f, Mathf.Clamp01(10f * dt));
        t.position = p;
    }

    void IrAEstacion(bool alInicio)
    {
        var puntos = new List<TeleportPoint>(FindObjectsByType<TeleportPoint>(FindObjectsSortMode.None));
        if (puntos.Count == 0) return;
        puntos.Sort((a, b) => a.Index.CompareTo(b.Index));
        puntoPrueba = alInicio ? 0 : (puntoPrueba + 1) % puntos.Count;
        var tp = puntos[puntoPrueba];
        cam.transform.position = tp.transform.position + Vector3.up * 1.65f;
        yawPrueba = tp.transform.eulerAngles.y;
        pitchPrueba = 5;
        Aviso("Estación: " + tp.Label);
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

    // se pide una vez; si en 3 s no volvió por la red, se puede pedir de nuevo.
    // El mensaje lleva quién la pide: el servidor los ordena y la memoria es del primero que llega.
    void PedirMemoria(int i)
    {
        if (memoriaPedida.TryGetValue(i, out float t) && Time.time - t < 3f) return;
        memoriaPedida[i] = Time.time;
        Identificarme();
        Enviar($"m|{i}|{miId}|{miNombre}");
    }

    // mi identificador en la sesión (ClientId de VIROO) y el nombre de mi avatar, si tiene
    void Identificarme()
    {
        if (miId != "") return;
        try
        {
            var yo = VirooApi.Instance.Players().GetLocalPlayer();
            if (yo != null && yo.PlayerData != null && !string.IsNullOrEmpty(yo.PlayerData.ClientId))
            {
                miId = LimpiarTexto(yo.PlayerData.ClientId, 64);
                try { miNombre = LimpiarTexto(yo.PlayerAvatar?.GetAvatarName(), 16); }
                catch (Exception) { miNombre = ""; }
            }
        }
        catch (Exception e) { Debug.LogWarning("[Calicraft] No pude leer el jugador de VIROO: " + e.Message); }
        if (miId == "") miId = sinViroo ? "local" : "j" + Guid.NewGuid().ToString("N").Substring(0, 8);
    }

    // los mensajes van separados por "|": el texto no puede traerlo
    static string LimpiarTexto(string s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.Replace("|", " ").Replace("\n", " ").Replace("\r", " ").Replace("<", "").Replace(">", "").Trim();
        return s.Length > max ? s.Substring(0, max) : s;
    }

    // Anota de quién es la memoria. Sin nombre de avatar queda "Jugador N", por orden de llegada de su
    // primera memoria: como los mensajes llegan a todos en el mismo orden, el número es igual en todos.
    void AnotarDueno(int i, string id, string nombre)
    {
        if (string.IsNullOrEmpty(id)) id = "?";
        if (!nombreJugador.ContainsKey(id))
            nombreJugador[id] = string.IsNullOrEmpty(nombre) ? "Jugador " + (nombreJugador.Count + 1) : nombre;
        duenoMemoria[i] = id;
        memoriasDe[id] = (memoriasDe.TryGetValue(id, out int n) ? n : 0) + 1;
    }

    string NombreDe(string id) => id != null && nombreJugador.TryGetValue(id, out var n) ? n : "otro jugador";
    int MisMemorias => miId != "" && memoriasDe.TryGetValue(miId, out int n) ? n : 0;

    // la tabla del final: quién recuperó más memorias (hasta 10 renglones)
    string TablaDeMemorias()
    {
        var lista = new List<KeyValuePair<string, int>>(memoriasDe);
        lista.Sort((a, b) => b.Value != a.Value ? b.Value.CompareTo(a.Value) : string.CompareOrdinal(NombreDe(a.Key), NombreDe(b.Key)));
        var sb = new System.Text.StringBuilder();
        for (int k = 0; k < lista.Count && k < 10; k++)
        {
            bool yo = lista[k].Key == miId;
            string renglon = $"{k + 1}. {NombreDe(lista[k].Key)}{(yo ? " (tú)" : "")}: {lista[k].Value}";
            sb.Append(yo ? $"<color=#FFE066>{renglon}</color>" : renglon).Append(k % 2 == 0 ? "      " : "\n");
        }
        return sb.ToString().TrimEnd();
    }

    // ---------------------------------------------------------------------------------------------
    // red
    void Enviar(string datos)
    {
        // modo de prueba: el simulador de VIROO del editor acepta el envío pero nunca lo devuelve,
        // así que el cambio se aplica directo en este PC
        if (sinViroo) { Aplicar(datos, true); return; }
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
                case "m": RecogerMemoria(int.Parse(p[1], CultureInfo.InvariantCulture), p.Length > 2 ? p[2] : "", p.Length > 3 ? p[3] : ""); break;
                case "v": ReproducirVideo(int.Parse(p[1], CultureInfo.InvariantCulture), int.Parse(p[2], CultureInfo.InvariantCulture), p.Length > 3 ? p[3] : ""); break;
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

    // los puntos se crean en el editor (menú Calicraft → Configurar escena VIROO) con su x,z;
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
        hud.transform.localPosition = new Vector3(0, -0.24f, 0.9f);   // dentro de la vista también en escritorio
        hud.transform.localRotation = Quaternion.Euler(15, 0, 0);
        hud.outlineWidth = 0.2f;
        hud.outlineColor = new Color32(0, 0, 0, 220);
        if (!esVR)   // escritorio: una mira en el centro (F y G apuntan ahí)
        {
            var mira = Texto3D("Mira", cam.transform, 0.04f, new Vector2(0.2f, 0.2f), TextAlignmentOptions.Center);
            mira.transform.localPosition = new Vector3(0, 0, 0.9f);
            mira.text = "+";
            mira.outlineWidth = 0.25f;
            mira.outlineColor = new Color32(0, 0, 0, 220);
        }
    }

    void ActualizarHud()
    {
        string linea = $"Tus memorias {MisMemorias}   ·   Grupo {recogidos.Count}/{MEMORIAS.Length}   ·   Casas {CasasListas}/{casas.Count}   ·   Bloque: {tipos[tipoSel].nombre}";
        if (tAviso > 0 && aviso != "") linea = $"<color=#FFE066>{aviso}</color>\n" + linea;
        if (termino) linea = $"<color=#7FE9FF>{tituloFin}</color>\n{TablaDeMemorias()}\n" + linea;
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

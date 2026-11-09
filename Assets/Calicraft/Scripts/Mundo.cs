// Calicraft (versión Unity): antes Mindcraft. Empezó como el mismo juego de Godot de la Sesión 9
// (mindcraft_estudiante_s9) y creció para mostrar lo que se puede hacer con un motor:
// mundo generado por código, día y noche, monstruos, física (TNT), partículas, combate,
// minimapa e inventario. Se construye SOLO al darle Play.
//
// Está dividido en varios archivos, uno por sistema:
//   Mundo.cs             reglas y flujo de la partida (este archivo)
//   Mundo.Personajes.cs  los personajes del curso (.glb), hacia dónde miran y su tamaño
//   Mundo.Escenario.cs   piezas del mapa: aldea de Godot, gemas, lava, antorchas, TNT, monstruos
//   Mundo.Pantallas.cs   pantallas y HUD: inicio, fin, inventario, minimapa, ayuda
//   Mundo.Utilidades.cs  cajas, materiales y texturas
//   Terreno.cs           el mundo de bloques generado (montañas, cuevas, minerales, lagos)
//   CicloDia.cs          sol, luna, cielo y estrellas
//   Efectos.cs           partículas (chispas, explosiones, humo, brillos)
//   Enemigo.cs · Monstruos.cs · Tnt.cs · Proyectil.cs · Antorcha.cs · Empujador.cs
//
// Controles: tecla H dentro del juego.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class Mundo : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Arranque()
    {
        if (FindFirstObjectByType<Mundo>() != null) return;
        new GameObject("Mundo").AddComponent<Mundo>();
    }

    // ---------- estado de la partida (como mundo.gd) ----------
    int gemas, meta, vidas = 3, puntos, derrotados;
    public bool termino, jugando = true;
    public float rapidez = 1f;           // multiplica la velocidad de los enemigos
    float invencible;                    // segundos sin perder vida después de un golpe
    Vector3 inicio = Vector3.zero;       // donde reapareces
    bool yaEmpezo;                       // al jugar otra vez no se repite la pantalla de inicio
    int elegido, dificultad = 1, semilla;

    enum Pantalla { Portada, Mision, Eleccion, Ninguna }
    Pantalla pantalla = Pantalla.Portada;
    float tMision, tFin, golpeAlpha;
    string tituloFin = "";
    bool cargando = true;
    string diag = "";
    string aviso = "";
    float tAviso, tAyuda;
    bool ayuda, mapaGrande;

    // ---------- inventario (teclas 1-9) ----------
    class TipoBloque { public string nombre, icono; public B bloque; public bool tnt, antorcha; }
    readonly TipoBloque[] tipos = {
        new TipoBloque { nombre = "Piedra",   icono = "piedra.png",      bloque = B.Piedra },
        new TipoBloque { nombre = "Pasto",    icono = "pasto_lado.png",  bloque = B.Pasto },
        new TipoBloque { nombre = "Tierra",   icono = "tierra.png",      bloque = B.Tierra },
        new TipoBloque { nombre = "Tronco",   icono = "tronco_lado.png", bloque = B.Tronco },
        new TipoBloque { nombre = "Tablas",   icono = "tablas.png",      bloque = B.Tablas },
        new TipoBloque { nombre = "Ladrillo", icono = "ladrillo.png",    bloque = B.Ladrillo },
        new TipoBloque { nombre = "Vidrio",   icono = "vidrio.png",      bloque = B.Vidrio },
        new TipoBloque { nombre = "TNT (clic izq para encender)", icono = "tnt.png", tnt = true },
        new TipoBloque { nombre = "Antorcha", icono = "tronco_arriba.png", antorcha = true },
    };
    int tipoSel;

    // ---------- objetos del mundo ----------
    Transform raiz, modelos;
    GameObject jugador;
    CharacterController cc;
    Terreno terreno;
    CicloDia ciclo;
    Camera cam, camMapa;
    RenderTexture rtMapa;
    readonly List<Enemigo> enemigos = new List<Enemigo>();
    readonly List<GameObject> listaModelos = new List<GameObject>();
    readonly List<Transform> gemasObj = new List<Transform>();
    readonly List<Transform> corazones = new List<Transform>();
    readonly List<Transform> giran = new List<Transform>();
    float yaw, pitch = 15f, velY, sacudida, tLanzar;
    int cuadroMapa;
    readonly Vector3 LAVA = new Vector3(6, 0, 0);
    static readonly Vector3 CAM_DETRAS = new Vector3(0, 2.5f, -6f);
    const int CAPA_JUGADOR = 2, CAPA_CIELO = 1;   // 2 = Ignore Raycast: el rayo no te toca
    static int SinJugador => ~(1 << CAPA_JUGADOR);

    Font fuente;
    AudioSource audioSrc;
    readonly Dictionary<string, AudioClip> sonidos = new Dictionary<string, AudioClip>();

    const float VEL = 5f, SALTO = 5.5f, GRAVEDAD = 12f, SENS = 2f, ALCANCE = 15f;

    public bool EsDeNoche => ciclo != null && ciclo.EsDeNoche;
    public Vector3 PosJugador => jugador ? jugador.transform.position : Vector3.zero;
    public int MaxMonstruos => new[] { 3, 6, 9 }[dificultad];

    // Godot usa -Z como "adelante"; Unity +Z. Se invierte z para que el mapa quede igual.
    static Vector3 P(float x, float y, float z) => new Vector3(x, y, -z);

    // =====================================================================
    async void Start()
    {
        // Apaga cámara/luz/oído que trae la escena vacía para que no choquen con las del juego
        foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.enabled = false;
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) l.enabled = false;
        foreach (var a in FindObjectsByType<AudioListener>(FindObjectsSortMode.None)) a.enabled = false;

        fuente = Resources.Load<Font>("PressStart2P-Regular");
        audioSrc = gameObject.AddComponent<AudioSource>();
        foreach (var s in new[] { "gema", "golpe", "gameover", "ganar", "clic" })
        {
            var clip = Resources.Load<AudioClip>("sonidos/" + s);
            if (clip != null) sonidos[s] = clip;
        }

        var sol = Luz("Sol", LightType.Directional, Color.white, 1.1f);
        sol.shadows = LightShadows.Hard;
        sol.shadowStrength = 0.6f;
        var luna = Luz("Luna", LightType.Directional, new Color(0.6f, 0.7f, 1f), 0.2f);
        luna.renderMode = LightRenderMode.ForceVertex;   // luz liviana: no repite el dibujo de todo el mundo
        RenderSettings.sun = sol;
        QualitySettings.shadowDistance = 45;
        QualitySettings.pixelLightCount = 4;

        cam = new GameObject("Camara").AddComponent<Camera>();
        cam.gameObject.tag = "MainCamera";   // para que los rótulos de los monumentos la miren
        cam.gameObject.AddComponent<AudioListener>();
        cam.fieldOfView = 75;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 160;
        cam.cullingMask = ~0;

        ciclo = gameObject.AddComponent<CicloDia>();
        ciclo.sol = sol;
        ciclo.luna = luna;
        ciclo.cam = cam;
        ciclo.cielo = Cielo();
        CrearMinimapa();

        await PrecargarTexturas();
        await CargarPersonajes();
        Construir();
    }

    // ---------- arma el mundo ----------
    void Construir() => StartCoroutine(ConstruirRutina());

    IEnumerator ConstruirRutina()
    {
        cargando = true;
        yield return null;   // deja ver "CARGANDO..." mientras se genera el mundo

        raiz = new GameObject("Escenario").transform;
        gemas = meta = puntos = derrotados = 0;
        termino = false; invencible = 0; golpeAlpha = 0; sacudida = 0; tituloFin = "";
        gemasObj.Clear(); corazones.Clear(); cuerposEnemigos.Clear(); giran.Clear();
        listaModelos.Clear(); enemigos.Clear();
        semilla = Random.Range(1, 100000);

        terreno = new GameObject("Terreno").AddComponent<Terreno>();
        terreno.transform.SetParent(raiz, false);
        terreno.Generar(semilla, (archivo, transparente) => Mat(archivo, Vector2.one, transparente));
        Bordes();
        AldeaDeGodot();

        // Calicraft: en vez de gemas, 19 cubos de memoria y 5 casas del terremoto (Mundo.Calicraft.cs)
        CrearCalicraft();
        meta = MEMORIAS.Length;

        Lava(LAVA);
        // Sin enemigos de día: los malos pensamientos solo aparecen de noche (los crea Monstruos.cs).
        Corazon(P(-4, 1, 4));
        for (int i = 0; i < 3; i++) Corazon(LugarAlAzar());
        var mons = raiz.gameObject.AddComponent<Monstruos>();
        mons.mundo = this;
        mons.terreno = terreno;

        CrearJugador();

        AplicarDificultad();
        if (yaEmpezo)
        {
            pantalla = Pantalla.Ninguna;
            jugando = true;
            VistaDeInicio(false);
            Pausar(false);
            tAyuda = 10;
        }
        else
        {
            pantalla = Pantalla.Portada;
            jugando = false;
            VistaDeInicio(false);
            Pausar(true);
        }
        cargando = false;
    }

    // =====================================================================
    void Update()
    {
        float dt = Time.deltaTime;
        foreach (var t in giran) if (t) t.Rotate(0, 2f * Mathf.Rad2Deg * dt, 0, Space.World);
        if (golpeAlpha > 0) golpeAlpha = Mathf.Max(0, golpeAlpha - dt * 1.25f);
        if (cargando || jugador == null) return;

        if (termino)
        {
            tFin += dt;
            if (Input.GetKeyDown(KeyCode.R)) Reiniciar();
            return;
        }
        if (pantalla == Pantalla.Mision) tMision += dt;
        if (pantalla == Pantalla.Eleccion) modelos.Rotate(0, 1.2f * Mathf.Rad2Deg * dt, 0);
        if (!jugando) return;

        ciclo.velocidad = Input.GetKey(KeyCode.N) ? 30f : 1f;
        if (invencible > 0) invencible -= dt;
        if (tAviso > 0) tAviso -= dt;
        if (tAyuda > 0) tAyuda -= dt;
        if (tLanzar > 0) tLanzar -= dt;
        ActualizarCalicraft(dt);
        if (termino) return;
        if (jugador.transform.position.y < Terreno.Y_MIN - 10) { PerderVida(); if (termino) return; }

        if (Input.GetKeyDown(KeyCode.G)) GirarPersonaje();
        if (Input.GetKeyDown(KeyCode.H)) { ayuda = !(ayuda || tAyuda > 0); tAyuda = 0; }
        if (Input.GetKeyDown(KeyCode.M)) mapaGrande = !mapaGrande;
        if (Input.GetKeyDown(KeyCode.Escape)) Pausar(Cursor.lockState == CursorLockMode.Locked);

        for (int k = 0; k < tipos.Length; k++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + k)) tipoSel = k;
        float rueda = Input.mouseScrollDelta.y;
        if (rueda != 0) tipoSel = (tipoSel + (rueda > 0 ? -1 : 1) + tipos.Length) % tipos.Length;

        if (Cursor.lockState == CursorLockMode.Locked)
        {
            yaw += Input.GetAxisRaw("Mouse X") * SENS;
            pitch = Mathf.Clamp(pitch - Input.GetAxisRaw("Mouse Y") * SENS, -23, 69);
            if (Input.GetMouseButtonDown(0)) Romper();
            if (Input.GetMouseButtonDown(1)) Poner();
            if (Input.GetKeyDown(KeyCode.F) || Input.GetMouseButtonDown(2)) Lanzar();
        }
        else if (Input.GetMouseButtonDown(0) && !MouseEnPanel()) Pausar(false);   // clic en el panel = abrir enlace
        jugador.transform.rotation = Quaternion.Euler(0, yaw, 0);
        cam.transform.localRotation = Quaternion.Euler(pitch, 0, 0);

        // caminar, saltar y nadar (personaje.gd)
        float h = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 : 0)
                - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1 : 0);
        float v = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1 : 0)
                - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1 : 0);
        var tr = jugador.transform;
        bool enAgua = terreno.EsAgua(tr.position + Vector3.up * 0.6f);
        Vector3 mov = (tr.forward * v + tr.right * h).normalized * (enAgua ? VEL * 0.55f : VEL);
        if (enAgua)
        {
            velY = Mathf.Max(velY - GRAVEDAD * 0.25f * dt, -2f);
            if (Input.GetKey(KeyCode.Space)) velY = 2.5f;
        }
        else if (cc.isGrounded)
        {
            velY = -1;
            if (Input.GetKeyDown(KeyCode.Space)) velY = SALTO;
        }
        else velY -= GRAVEDAD * dt;
        mov.y = velY;
        cc.Move(mov * dt);
        ColocarCamara();

        // gemas, corazones y lava (las Area3D de Godot)
        for (int i = gemasObj.Count - 1; i >= 0; i--)
            if (Toca(gemasObj[i].position, 0.7f))
            {
                Efectos.Destello(gemasObj[i].position, new Color(0.4f, 0.95f, 1f));
                Destroy(gemasObj[i].gameObject);
                gemasObj.RemoveAt(i);
                Recoger();
                if (termino) return;
            }
        for (int i = corazones.Count - 1; i >= 0; i--)
            if (Toca(corazones[i].position, 0.5f))
            {
                Efectos.Destello(corazones[i].position, new Color(1, 0.3f, 0.5f));
                Destroy(corazones[i].gameObject);
                corazones.RemoveAt(i);
                GanarVida();
            }
        var p = tr.position;
        if (Mathf.Abs(p.x - LAVA.x) < 2.4f && Mathf.Abs(p.z - LAVA.z) < 2.4f && p.y < 1f) PerderVida();
    }

    // la cámara va detrás, pero si hay una montaña o pared en medio se acerca (no la atraviesa)
    void ColocarCamara()
    {
        var t = jugador.transform;
        Vector3 cabeza = t.position + Vector3.up * 1.6f;
        Vector3 deseada = t.TransformPoint(CAM_DETRAS);
        Vector3 dir = deseada - cabeza;
        float dist = dir.magnitude;
        dir /= dist;
        if (Physics.SphereCast(cabeza, 0.25f, dir, out var hit, dist, SinJugador, QueryTriggerInteraction.Ignore))
            cam.transform.position = cabeza + dir * Mathf.Max(0.5f, hit.distance - 0.1f);
        else
            cam.transform.localPosition = CAM_DETRAS;
        if (sacudida > 0)
        {
            cam.transform.position += Random.insideUnitSphere * 0.3f * sacudida;
            sacudida -= Time.deltaTime * 2f;
        }
    }

    // la cápsula del jugador (radio 0.4) toca una esfera de radio r en 'pos'
    bool Toca(Vector3 pos, float r)
    {
        var c = jugador.transform.position;
        float y = Mathf.Clamp(pos.y, c.y + 0.4f, c.y + 1.3f);
        return Vector3.Distance(pos, new Vector3(c.x, y, c.z)) < r + 0.4f;
    }

    // ---------- poner / romper / lanzar ----------
    static Vector3Int Celda(Vector3 p) => new Vector3Int(Mathf.RoundToInt(p.x), Mathf.FloorToInt(p.y), Mathf.RoundToInt(p.z));

    bool Apuntar(out RaycastHit hit) =>
        Physics.Raycast(cam.transform.position, cam.transform.forward, out hit, ALCANCE, SinJugador, QueryTriggerInteraction.Ignore);

    void Poner()
    {
        if (!Apuntar(out var hit)) return;
        var c = Celda(hit.point + hit.normal * 0.5f);
        var centro = new Vector3(c.x, c.y + 0.5f, c.z);
        if (Vector3.Distance(centro, PosJugador + Vector3.up * 0.9f) < 1.2f) return;   // no encima de ti
        foreach (var e in enemigos)
            if (e && Vector3.Distance(centro, e.transform.position + Vector3.up * 0.9f) < 1.1f) return;
        var t = tipos[tipoSel];
        if (t.tnt) CrearTnt(centro);
        else if (t.antorcha) PonerAntorcha(new Vector3(c.x, c.y, c.z));
        else if (terreno.Get(c.x, c.y, c.z) == B.Aire && terreno.Set(c.x, c.y, c.z, t.bloque)) RevisarHueco(c);
    }

    void Romper()
    {
        if (!Apuntar(out var hit)) return;
        var tnt = hit.collider.GetComponentInParent<Tnt>();
        if (tnt) { tnt.Encender(); return; }
        var antorcha = hit.collider.GetComponentInParent<Antorcha>();
        if (antorcha) { Destroy(antorcha.gameObject); return; }
        if (hit.collider.GetComponent<TrozoTerreno>() == null) return;   // la casa y los árboles de Godot no se rompen

        var c = Celda(hit.point - hit.normal * 0.5f);
        var b = terreno.Get(c.x, c.y, c.z);
        if (b == B.Aire || !terreno.Set(c.x, c.y, c.z, B.Aire)) return;
        Efectos.Escombros(new Vector3(c.x, c.y + 0.5f, c.z), ColorDe(b));
        int p = b switch { B.Carbon => 2, B.Hierro => 4, B.Oro => 8, B.Diamante => 15, _ => 0 };
        if (p > 0)
        {
            puntos += p;
            Sonar("gema");
            string nombre = b switch { B.Carbon => "carbon", B.Hierro => "hierro", B.Oro => "oro", _ => "diamante" };
            Aviso($"+{p} {nombre}");
        }
    }

    void Lanzar()
    {
        if (tLanzar > 0) return;
        tLanzar = 0.25f;
        var origen = PosJugador + Vector3.up * 1.4f + jugador.transform.forward * 0.6f;
        Vector3 objetivo = Physics.Raycast(cam.transform.position, cam.transform.forward, out var h, 80f, SinJugador, QueryTriggerInteraction.Ignore)
            ? h.point : cam.transform.position + cam.transform.forward * 80f;
        var dir = (objetivo - origen).normalized;
        var bola = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bola.name = "BolaDeNieve";
        bola.transform.SetParent(raiz, false);
        bola.transform.position = origen;
        bola.transform.localScale = Vector3.one * 0.3f;
        bola.GetComponent<Renderer>().sharedMaterial = MatColor(new Color(0.92f, 0.96f, 1f));
        var rb = bola.AddComponent<Rigidbody>();
        rb.mass = 0.2f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.linearVelocity = dir * 26f + Vector3.up;
        Physics.IgnoreCollision(bola.GetComponent<Collider>(), cc);
        bola.AddComponent<Proyectil>();
        Sonar("clic");
    }

    // la explosión de la TNT
    public void Explosion(Vector3 pos, float radio)
    {
        var quitados = terreno.Explotar(pos, radio);
        for (int i = 0; i < Mathf.Min(18, quitados.Count); i++)   // escombros que salen volando
        {
            var q = quitados[Random.Range(0, quitados.Count)];
            var d = GameObject.CreatePrimitive(PrimitiveType.Cube);
            d.name = "Escombro";
            d.transform.SetParent(raiz, false);
            d.transform.position = new Vector3(q.celda.x, q.celda.y + 0.5f, q.celda.z);
            d.transform.localScale = Vector3.one * 0.45f;
            d.GetComponent<Renderer>().sharedMaterial = Mat(Terreno.TexturaLado(q.tipo), Vector2.one);
            var rb = d.AddComponent<Rigidbody>();
            rb.mass = 0.5f;
            rb.AddExplosionForce(9f, pos, radio * 2.5f, 1.5f, ForceMode.Impulse);
            Destroy(d, Random.Range(3f, 5f));
        }
        foreach (var col in Physics.OverlapSphere(pos, radio * 2f))
        {
            var rb = col.attachedRigidbody;
            if (rb && !rb.isKinematic) rb.AddExplosionForce(12f, pos, radio * 2.5f, 1.5f, ForceMode.Impulse);
            var otra = col.GetComponentInParent<Tnt>();
            if (otra) otra.Encender(Random.Range(0.2f, 0.5f));     // reacción en cadena
            var en = col.GetComponentInParent<Enemigo>();
            if (en && Vector3.Distance(en.transform.position, pos) < radio * 1.6f) en.Danio(5, en.transform.position - pos);
        }
        if (Vector3.Distance(PosJugador + Vector3.up * 0.9f, pos) < radio + 0.8f) PerderVida();
        Efectos.Explosion(pos);
        Sonar("golpe");
        sacudida = Mathf.Max(sacudida, Mathf.Clamp01(1.2f - Vector3.Distance(PosJugador, pos) / 25f));
    }

    // ---------- reglas (mundo.gd) ----------
    void Recoger()
    {
        gemas++;
        puntos += 25;
        Sonar("gema");
        if (gemas >= meta) Terminar("MISION CUMPLIDA");
    }

    public void PerderVida()
    {
        if (termino || invencible > 0) return;
        vidas--;
        invencible = 1.5f;
        Sonar("golpe");
        golpeAlpha = 0.5f;          // pantallazo rojo
        if (vidas <= 0) { Terminar("GAME OVER"); return; }
        cc.enabled = false;         // reaparecer en el inicio
        jugador.transform.position = inicio;
        cc.enabled = true;
        velY = 0;
    }

    void GanarVida() { vidas++; Sonar("gema"); }

    public void MonstruoDerrotado(bool conPuntos)
    {
        if (!conPuntos) return;   // los que se queman con el sol no dan puntos
        puntos += 10;
        derrotados++;
        Sonar("gema");
        Aviso("+10 monstruo");
    }

    void Aviso(string texto) { aviso = texto; tAviso = 1.8f; }

    void AplicarDificultad()
    {
        vidas = new[] { 5, 3, 1 }[dificultad];
        rapidez = new[] { 0.7f, 1f, 1.5f }[dificultad];
    }

    void Terminar(string texto)
    {
        termino = true;
        Pausar(true);
        Sonar(texto == "GAME OVER" ? "gameover" : "ganar");
        tituloFin = texto;
        tFin = 0;
    }

    void Empezar()
    {
        yaEmpezo = true;
        jugando = true;
        pantalla = Pantalla.Ninguna;
        VistaDeInicio(false);
        Pausar(false);
        tAyuda = 12;   // la ayuda de controles se ve al empezar
    }

    void Pausar(bool pausa)
    {
        Cursor.lockState = pausa ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = pausa;
    }

    // jugar otra vez: un mundo nuevo (otra semilla)
    void Reiniciar()
    {
        cam.transform.SetParent(null, false);
        Destroy(raiz.gameObject);
        jugador = null;
        Construir();
    }

    // en el navegador una página no se puede cerrar sola: el botón vuelve a la portada
    void Salir()
    {
        if (EnNavegador) { yaEmpezo = false; Reiniciar(); return; }
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void Sonar(string nombre)
    {
        if (sonidos.TryGetValue(nombre, out var clip)) audioSrc.PlayOneShot(clip);
    }
}

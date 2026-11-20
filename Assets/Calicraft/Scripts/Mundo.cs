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

// VERSIÓN VIROO: Mundo ya no se crea solo; vive en la escena Calicraft (dentro de Root), junto a
// CalicraftRed. El jugador, la cámara y el movimiento los pone VIROO (ver Mundo.Viroo.cs).
public partial class Mundo : MonoBehaviour
{
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
        // Marcas de arranque: en el Player.log de la sala muestran en qué paso se queda.
        Debug.Log($"[Calicraft] 1/4 Arranca la escena (Unity {Application.unityVersion}, {Application.productName})");
        // (VIROO) no se apagan cámaras ni luces de la escena: son las del rig de VIROO
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

        // la cámara es la del rig de VIROO (gafas o escritorio); solo se le alarga la vista
        cam = await CamaraViroo();
        if (cam != null && cam.farClipPlane < 160) cam.farClipPlane = 160;
        Debug.Log("[Calicraft] 2/4 Cámara: " + (cam != null ? cam.name + " (display " + cam.targetDisplay + ")" : "NINGUNA"));

        ciclo = gameObject.AddComponent<CicloDia>();
        ciclo.sol = sol;
        ciclo.luna = luna;
        ciclo.cam = cam;
        ciclo.cielo = Cielo();
        // sin minimapa (no hay pantalla en VR) ni personajes del curso (VIROO pone los avatares)

        await PrecargarTexturas();
        Debug.Log("[Calicraft] 3/4 Texturas cargadas desde " + Application.streamingAssetsPath);
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
        semilla = SEMILLA;                 // (VIROO) el mismo mundo en todos los PC de la sala
        Random.InitState(semilla);         // y los objetos puestos "al azar" también caen igual

        terreno = new GameObject("Terreno").AddComponent<Terreno>();
        terreno.transform.SetParent(raiz, false);
        terreno.Generar(semilla, (archivo, transparente) => Mat(archivo, Vector2.one, transparente));
        Bordes();
        AldeaDeGodot();

        // Calicraft: en vez de gemas, 19 cubos de memoria y 5 casas del terremoto (Mundo.Calicraft.cs)
        CrearCalicraft();
        meta = MEMORIAS.Length;

        Lava(LAVA);   // se ve, pero en la versión de la sala no quita vidas
        // (VIROO) modo cooperativo: sin malos pensamientos ni corazones (en la sala nadie puede correr)

        CrearJugadorViroo();

        AplicarDificultad();
        pantalla = Pantalla.Ninguna;       // sin portada ni elección de personaje: se entra jugando
        jugando = true;
        yaEmpezo = true;
        cargando = false;
        Debug.Log("[Calicraft] 4/4 Mundo armado");
        PrepararViroo();
        IniciarPruebaSiPide();             // solo con -calicraftPrueba (Mundo.Prueba.cs)
    }

    // =====================================================================
    void Update()
    {
        float dt = Time.deltaTime;
        foreach (var t in giran) if (t) t.Rotate(0, 2f * Mathf.Rad2Deg * dt, 0, Space.World);
        if (cargando || jugador == null) return;
        if (tAviso > 0) tAviso -= dt;
        ActualizarCalicraft(dt);   // tocar los cubos de memoria caminando
        ActualizarViroo(dt);       // mandos (o teclado), red, textos en el mundo (Mundo.Viroo.cs)
    }

    // la cápsula del jugador (radio 0.4) toca una esfera de radio r en 'pos'
    bool Toca(Vector3 pos, float r)
    {
        var c = jugador.transform.position;
        float y = Mathf.Clamp(pos.y, c.y + 0.4f, c.y + 1.3f);
        return Vector3.Distance(pos, new Vector3(c.x, y, c.z)) < r + 0.4f;
    }

    // celda de bloque que contiene el punto p
    static Vector3Int Celda(Vector3 p) => new Vector3Int(Mathf.RoundToInt(p.x), Mathf.FloorToInt(p.y), Mathf.RoundToInt(p.z));

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
        // (VIROO) modo cooperativo: no hay game over ni se reaparece (el jugador lo mueve VIROO)
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
        termino = true;              // (VIROO) el mundo sigue abierto; el HUD muestra el título
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
        if (silencio) return;   // reconstruyendo la sesión de quien entra tarde
        if (sonidos.TryGetValue(nombre, out var clip)) audioSrc.PlayOneShot(clip);
    }
}

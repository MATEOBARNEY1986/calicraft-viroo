using UnityEngine;
using UnityEngine.Rendering;

// Las piezas del mapa: el cielo, la aldea de Godot en el centro (casa, árboles, rocas, colina,
// lago, lava), las gemas, los corazones, las antorchas, la TNT y los monstruos.
public partial class Mundo
{
    Material Cielo()
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 30;
        RenderSettings.fogEndDistance = 120;
        var sh = Shaders.Buscar("Skybox/Procedural");
        if (sh == null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            return null;
        }
        var m = new Material(sh);
        m.SetColor("_SkyTint", new Color(0.35f, 0.6f, 1f));
        m.SetColor("_GroundColor", new Color(0.45f, 0.55f, 0.45f));
        m.SetFloat("_AtmosphereThickness", 0.8f);
        RenderSettings.skybox = m;
        cam.clearFlags = CameraClearFlags.Skybox;
        return m;
    }

    Light Luz(string nombre, LightType tipo, Color color, float intensidad)
    {
        var l = new GameObject(nombre).AddComponent<Light>();
        l.type = tipo;
        l.color = color;
        l.intensity = intensidad;
        return l;
    }

    // bordes invisibles en la orilla del mundo generado
    void Bordes()
    {
        float m = Terreno.MITAD;
        Invisible(new Vector3(0, 30, m), new Vector3(2 * m + 2, 100, 1));
        Invisible(new Vector3(0, 30, -m - 1), new Vector3(2 * m + 2, 100, 1));
        Invisible(new Vector3(m, 30, 0), new Vector3(1, 100, 2 * m + 2));
        Invisible(new Vector3(-m - 1, 30, 0), new Vector3(1, 100, 2 * m + 2));
    }

    // el mapa del juego de Godot (Sesión 9), en la zona plana del centro
    void AldeaDeGodot()
    {
        foreach (var a in new[] { (-25, -28), (-30, -20), (-20, -35), (-35, -32), (-28, -40), (5, -30),
                                  (-8, 25), (-25, 15), (-32, 26), (30, 32), (36, 15), (-40, 0) })
            Arbol(P(a.Item1, 0, a.Item2));

        Caja("Roca", P(6, 0.6f, -6), new Vector3(2, 1.2f, 1.6f), "piedra.png", true, null, false, -20);
        Caja("Roca", P(-5, 0.6f, 8), new Vector3(2, 1.2f, 1.6f), "piedra.png");
        Caja("Roca", P(28, 0.6f, 5), new Vector3(2, 1.2f, 1.6f), "piedra.png", true, null, false, -45);
        Caja("Roca", P(-20, 0.6f, 30), new Vector3(2, 1.2f, 1.6f), "piedra.png", true, null, false, -70);

        Caja("Orilla", P(14, 0.03f, 10), new Vector3(14, 0.06f, 10), "arena.png", false);
        Caja("Lago", P(14, 0.05f, 10), new Vector3(12, 0.1f, 8), "agua.png", false, null, true);

        Casa(P(-12, 0, -12));

        var colina = P(22, 0, -20);   // tres niveles de pasto
        Caja("Colina", colina + new Vector3(0, 0.5f, 0), new Vector3(10, 1, 10), "pasto_arriba.png");
        Caja("Colina", colina + new Vector3(0, 1.5f, 0), new Vector3(7, 1, 7), "pasto_arriba.png");
        Caja("Colina", colina + new Vector3(0, 2.5f, 0), new Vector3(4, 1, 4), "pasto_arriba.png");

        // antorchas en la aldea (se encienden al anochecer)
        foreach (var a in new[] { P(-15.5f, 0, -8.5f), P(-8.5f, 0, -8.5f), P(-15.5f, 0, -15.5f), P(-8.5f, 0, -15.5f),
                                  P(2, 0, 2), P(-2, 0, 2), P(14, 0, 4), P(22, 3, -20) })
            PonerAntorcha(a);
    }

    void Arbol(Vector3 pos)
    {
        var a = new GameObject("Arbol").transform;
        a.SetParent(raiz, false);
        a.localPosition = pos;
        Caja("Tronco", new Vector3(0, 1.5f, 0), new Vector3(1, 3, 1), "tronco_lado.png", true, a);
        Caja("Copa", new Vector3(0, 4.5f, 0), new Vector3(3, 3, 3), "hojas.png", true, a);
    }

    void Casa(Vector3 pos)
    {
        var c = new GameObject("Casa").transform;
        c.SetParent(raiz, false);
        c.localPosition = pos;
        const string L = "ladrillo.png";
        Caja("Pared", new Vector3(0, 2, 2.85f), new Vector3(6, 4, 0.3f), L, true, c);              // atrás
        Caja("Pared", new Vector3(-1.85f, 2, -2.85f), new Vector3(2.3f, 4, 0.3f), L, true, c);     // puerta
        Caja("Pared", new Vector3(1.85f, 2, -2.85f), new Vector3(2.3f, 4, 0.3f), L, true, c);
        Caja("Dintel", new Vector3(0, 3.2f, -2.85f), new Vector3(1.4f, 1.6f, 0.3f), L, true, c);
        Caja("Pared", new Vector3(-2.85f, 2, 0), new Vector3(0.3f, 4, 5.4f), L, true, c);          // lisa
        Caja("Pared", new Vector3(2.85f, 2, -1.65f), new Vector3(0.3f, 4, 2.1f), L, true, c);      // ventana
        Caja("Pared", new Vector3(2.85f, 2, 1.65f), new Vector3(0.3f, 4, 2.1f), L, true, c);
        Caja("Pared", new Vector3(2.85f, 0.8f, 0), new Vector3(0.3f, 1.6f, 1.2f), L, true, c);
        Caja("Pared", new Vector3(2.85f, 3.4f, 0), new Vector3(0.3f, 1.2f, 1.2f), L, true, c);
        Caja("Techo", new Vector3(0, 4.2f, 0), new Vector3(7, 0.4f, 7), "tablas.png", true, c);
    }

    void Gema(Vector3 pos, bool faro = false)
    {
        var piv = new GameObject("Gema").transform;
        piv.SetParent(raiz, false);
        piv.localPosition = pos;
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(piv, false);
        g.transform.localScale = Vector3.one * 0.5f;
        g.transform.localRotation = Quaternion.Euler(45, 0, 35);
        var m = Mat("mineral_diamante.png", Vector2.one);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", new Color(0.4f, 0.9f, 1f) * 0.5f);
        g.GetComponent<Renderer>().sharedMaterial = m;
        Efectos.Brillo(piv);
        if (faro) Efectos.Faro(piv, new Color(0.4f, 0.9f, 1f, 0.3f));
        gemasObj.Add(piv);
        giran.Add(piv);
    }

    void Corazon(Vector3 pos)
    {
        var h = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        h.name = "Corazon";
        Destroy(h.GetComponent<Collider>());
        h.transform.SetParent(raiz, false);
        h.transform.localPosition = pos;
        h.transform.localScale = Vector3.one * 0.7f;
        h.GetComponent<Renderer>().sharedMaterial = MatColor(new Color(1, 0.2f, 0.45f), 0.3f);
        corazones.Add(h.transform);
        giran.Add(h.transform);
    }

    void Lava(Vector3 pos)
    {
        var l = GameObject.CreatePrimitive(PrimitiveType.Cube);
        l.name = "Lava";
        Destroy(l.GetComponent<Collider>());
        l.transform.SetParent(raiz, false);
        l.transform.localPosition = pos + new Vector3(0, 0.05f, 0);
        l.transform.localScale = new Vector3(4, 0.1f, 4);
        l.GetComponent<Renderer>().sharedMaterial = MatColor(new Color(1, 0.35f, 0), 1f);
        var chispas = new GameObject("Chispas").transform;
        chispas.SetParent(raiz, false);
        chispas.position = pos + Vector3.up * 0.1f;
        Efectos.ChispasLava(chispas);
        // la lava alumbra de noche
        var luz = Luz("LuzLava", LightType.Point, new Color(1f, 0.45f, 0.1f), 2f);
        luz.transform.SetParent(chispas, false);
        luz.transform.localPosition = Vector3.up * 1.2f;
        luz.range = 9;
        var a = chispas.gameObject.AddComponent<Antorcha>();
        a.luz = luz; a.ciclo = ciclo;
    }

    void PonerAntorcha(Vector3 basePos)
    {
        var a = new GameObject("Antorcha");
        a.transform.SetParent(raiz, false);
        a.transform.position = basePos;
        Caja("Palo", new Vector3(0, 0.3f, 0), new Vector3(0.14f, 0.6f, 0.14f), "tronco_lado.png", false, a.transform);
        var fuego = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(fuego.GetComponent<Collider>());
        fuego.transform.SetParent(a.transform, false);
        fuego.transform.localPosition = new Vector3(0, 0.68f, 0);
        fuego.transform.localScale = Vector3.one * 0.18f;
        fuego.GetComponent<Renderer>().sharedMaterial = MatColor(new Color(1f, 0.6f, 0.15f), 1.5f);
        var col = a.AddComponent<BoxCollider>();
        col.center = new Vector3(0, 0.4f, 0);
        col.size = new Vector3(0.4f, 0.8f, 0.4f);
        var luz = Luz("Luz", LightType.Point, new Color(1f, 0.65f, 0.3f), 1.6f);
        luz.transform.SetParent(a.transform, false);
        luz.transform.localPosition = new Vector3(0, 0.9f, 0);
        luz.range = 10;
        var an = a.AddComponent<Antorcha>();
        an.luz = luz; an.ciclo = ciclo;
        Efectos.Llama(a.transform, new Vector3(0, 0.75f, 0));
    }

    void CrearTnt(Vector3 centro)
    {
        var t = GameObject.CreatePrimitive(PrimitiveType.Cube);
        t.name = "TNT";
        t.transform.SetParent(raiz, false);
        t.transform.position = centro;
        t.transform.localScale = Vector3.one * 0.98f;
        t.GetComponent<Renderer>().sharedMaterial = Mat("tnt.png", Vector2.one);
        var rb = t.AddComponent<Rigidbody>();
        rb.mass = 2f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        t.AddComponent<Tnt>().mundo = this;
    }

    public Enemigo CrearEnemigo(Vector3 pos, string villano, bool nocturno = false)
    {
        var e = new GameObject(nocturno ? "Monstruo" : "Enemigo");
        e.transform.SetParent(raiz, false);
        e.transform.localPosition = pos;
        var ctrl = e.AddComponent<CharacterController>();
        ctrl.height = 1.7f; ctrl.radius = 0.4f; ctrl.center = new Vector3(0, 0.85f, 0);

        // el monstruo es un personaje del curso; si su .glb falla, queda el bloque rojo de Godot
        var cuerpo = new GameObject(villano);
        cuerpo.transform.SetParent(e.transform, false);
        if (Clonar(villano, cuerpo.transform))
        {
            Orientar(cuerpo, villano);
            NormalizarTamano(e.transform, cuerpo, 1.7f);
            cuerposEnemigos.Add((cuerpo, e.transform));
        }
        else
        {
            var caja = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(caja.GetComponent<Collider>());
            caja.transform.SetParent(cuerpo.transform, false);
            caja.transform.localPosition = new Vector3(0, 0.8f, 0);
            caja.transform.localScale = new Vector3(0.8f, 1.6f, 0.8f);
            caja.GetComponent<Renderer>().sharedMaterial = MatColor(new Color(0.8f, 0.1f, 0.1f));
        }
        if (nocturno) Violeta(cuerpo);   // los malos pensamientos
        var en = e.AddComponent<Enemigo>();
        en.mundo = this;
        en.cc = ctrl;
        en.nocturno = nocturno;
        if (nocturno) en.vida = 2;
        enemigos.Add(en);
        return en;
    }

    public int MonstruosNocturnos()
    {
        enemigos.RemoveAll(e => e == null);
        int n = 0;
        foreach (var e in enemigos) if (e.nocturno) n++;
        return n;
    }

    // un punto al azar en tierra firme lejos de la aldea (para corazones extra)
    Vector3 LugarAlAzar()
    {
        for (int i = 0; i < 30; i++)
        {
            int x = Random.Range(-Terreno.MITAD + 8, Terreno.MITAD - 8), z = Random.Range(-Terreno.MITAD + 8, Terreno.MITAD - 8);
            if (Terreno.Lejania(x, z) < 0.9f || terreno.EsAguaColumna(x, z)) continue;
            int y = terreno.AlturaSuelo(x, z);
            if (terreno.Get(x, y - 1, z) == B.Hojas) continue;
            return new Vector3(x, y + 1f, z);
        }
        return new Vector3(0, 1, 10);
    }
}

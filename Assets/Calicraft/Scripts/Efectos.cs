using UnityEngine;

// Partículas hechas por código: chispas de la lava, brillo de las gemas, llamas de las
// antorchas, escombros al romper un bloque, explosiones, humo y el faro de luz de las gemas lejanas.
public static class Efectos
{
    static Material mat;
    static Material Mat() => mat != null ? mat : (mat = new Material(Shaders.Buscar("Sprites/Default")));

    static ParticleSystem Nuevo(string nombre, Vector3 pos, Transform padre)
    {
        var go = new GameObject(nombre);
        if (padre) go.transform.SetParent(padre, false);
        go.transform.position = pos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);   // se configura antes de arrancar
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = Mat();
        return ps;
    }

    static Gradient Desvanecer()
    {
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                  new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 0.6f), new GradientAlphaKey(0, 1) });
        return g;
    }

    static void Configurar(ParticleSystem ps, Color c1, Color c2, float vel, float tam, float vida, float gravedad, int max)
    {
        var m = ps.main;
        m.startLifetime = new ParticleSystem.MinMaxCurve(vida * 0.5f, vida);
        m.startSpeed = new ParticleSystem.MinMaxCurve(vel * 0.4f, vel);
        m.startSize = new ParticleSystem.MinMaxCurve(tam * 0.5f, tam);
        m.startColor = new ParticleSystem.MinMaxGradient(c1, c2);
        m.gravityModifier = gravedad;
        m.simulationSpace = ParticleSystemSimulationSpace.World;
        m.maxParticles = max;
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(Desvanecer());
    }

    // un estallido de una sola vez (se borra solo al terminar)
    public static void Rafaga(Vector3 pos, Color c1, Color c2, int n, float vel, float tam, float vida, float gravedad, float radio = 0.3f)
    {
        var ps = Nuevo("Rafaga", pos, null);
        Configurar(ps, c1, c2, vel, tam, vida, gravedad, n);
        var m = ps.main;
        m.duration = 1;
        m.loop = false;
        m.stopAction = ParticleSystemStopAction.Destroy;
        var e = ps.emission;
        e.rateOverTime = 0;
        e.SetBursts(new[] { new ParticleSystem.Burst(0, (short)n) });
        var s = ps.shape;
        s.shapeType = ParticleSystemShapeType.Sphere;
        s.radius = radio;
        ps.Play();
    }

    // un emisor que no para (pegado a un objeto)
    static ParticleSystem Emisor(string nombre, Transform padre, Vector3 local, Color c1, Color c2, float porSegundo,
                                 float vel, float tam, float vida, float gravedad, Vector3 caja, bool haciaArriba)
    {
        var ps = Nuevo(nombre, padre.TransformPoint(local), padre);
        Configurar(ps, c1, c2, vel, tam, vida, gravedad, Mathf.CeilToInt(porSegundo * vida * 1.5f) + 4);
        var m = ps.main;
        m.loop = true;
        var e = ps.emission;
        e.rateOverTime = porSegundo;
        var s = ps.shape;
        s.shapeType = ParticleSystemShapeType.Box;
        s.scale = caja;
        if (haciaArriba) s.rotation = new Vector3(-90, 0, 0);
        ps.Play();
        return ps;
    }

    public static void Escombros(Vector3 pos, Color c) =>
        Rafaga(pos, c, c * 0.7f, 14, 4f, 0.18f, 0.8f, 1.5f);

    public static void Explosion(Vector3 pos)
    {
        Rafaga(pos, new Color(1f, 0.55f, 0.1f), new Color(1f, 0.9f, 0.3f), 70, 13f, 0.8f, 0.6f, -0.1f, 0.6f);
        Rafaga(pos, new Color(0.25f, 0.25f, 0.25f), new Color(0.6f, 0.6f, 0.6f), 40, 4f, 1.6f, 1.8f, -0.15f, 1.2f);
    }

    public static void Humo(Vector3 pos) =>
        Rafaga(pos, new Color(0.3f, 0.3f, 0.3f), new Color(0.65f, 0.65f, 0.65f), 18, 1.5f, 0.6f, 1.5f, -0.2f);

    public static void Destello(Vector3 pos, Color c) =>
        Rafaga(pos, c, Color.white, 16, 3f, 0.2f, 0.5f, 0.5f);

    public static void ChispasLava(Transform lava) =>
        Emisor("Chispas", lava, Vector3.zero, new Color(1f, 0.4f, 0f), new Color(1f, 0.85f, 0.2f),
               25, 3f, 0.14f, 1.2f, 0.35f, new Vector3(4, 4, 0.1f), true);

    public static void Brillo(Transform gema) =>
        Emisor("Brillo", gema, Vector3.zero, new Color(0.5f, 0.95f, 1f), Color.white,
               5, 0.25f, 0.12f, 1f, -0.05f, new Vector3(0.9f, 0.9f, 0.9f), false);

    public static void Llama(Transform antorcha, Vector3 local) =>
        Emisor("Llama", antorcha, local, new Color(1f, 0.45f, 0.05f), new Color(1f, 0.9f, 0.3f),
               14, 0.5f, 0.16f, 0.6f, -0.25f, new Vector3(0.12f, 0.12f, 0.05f), true);

    // columna de luz que se ve desde lejos (para encontrar las gemas de las montañas)
    public static void Faro(Transform gema, Color c)
    {
        var f = GameObject.CreatePrimitive(PrimitiveType.Cube);
        f.name = "Faro";
        Object.Destroy(f.GetComponent<Collider>());
        f.transform.SetParent(gema, false);
        f.transform.localPosition = new Vector3(0, 40, 0);
        f.transform.localScale = new Vector3(0.35f, 80, 0.35f);
        var m = new Material(Shaders.Buscar("Sprites/Default")) { color = c };
        f.GetComponent<Renderer>().sharedMaterial = m;
    }
}

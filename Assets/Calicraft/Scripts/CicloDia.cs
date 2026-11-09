using UnityEngine;

// Día y noche: el sol da la vuelta, el cielo y la niebla cambian de color, al atardecer
// todo se pone naranja y de noche salen la luna y las estrellas.
public class CicloDia : MonoBehaviour
{
    public Light sol, luna;
    public Material cielo;             // el cielo procedural de Unity
    public Camera cam;
    public float duracion = 240f;      // segundos que dura un día completo
    public float hora = 0.3f;          // 0.25 amanece · 0.5 mediodía · 0.75 anochece
    public float velocidad = 1f;       // la tecla N lo acelera

    public float Luz { get; private set; }        // 0 = noche cerrada, 1 = pleno día
    public bool EsDeNoche => Luz < 0.25f;

    ParticleSystem estrellas;
    Material matEstrellas;

    void Start() => CrearEstrellas();

    void Update()
    {
        hora = Mathf.Repeat(hora + Time.deltaTime * velocidad * (EsDeNoche ? 2f : 1f) / duracion, 1f);   // la noche pasa más rápido
        float ang = (hora - 0.25f) * 360f;                       // 0° al amanecer, 90° al mediodía
        sol.transform.rotation = Quaternion.Euler(ang, -30, 0);
        luna.transform.rotation = Quaternion.Euler(ang + 180, -30, 0);

        float altura = Mathf.Sin(ang * Mathf.Deg2Rad);           // > 0 de día
        Luz = Mathf.Clamp01(altura * 2.5f + 0.3f);
        float naranja = Mathf.Clamp01(1 - Mathf.Abs(altura) * 4f);   // cerca del horizonte

        sol.intensity = 1.15f * Luz;
        sol.enabled = Luz > 0.02f;
        sol.color = Color.Lerp(Color.white, new Color(1f, 0.6f, 0.35f), naranja);
        luna.intensity = 0.25f * (1 - Luz);
        luna.enabled = Luz < 0.98f;

        RenderSettings.ambientLight = Color.Lerp(new Color(0.08f, 0.09f, 0.16f), new Color(0.55f, 0.6f, 0.7f), Luz);
        var niebla = Color.Lerp(new Color(0.04f, 0.05f, 0.1f), new Color(0.7f, 0.85f, 1f), Luz);
        RenderSettings.fogColor = Color.Lerp(niebla, new Color(0.95f, 0.6f, 0.4f), naranja * 0.5f);
        if (cielo) cielo.SetFloat("_Exposure", Mathf.Lerp(0.08f, 1.3f, Luz));
        if (cam && cam.clearFlags == CameraClearFlags.SolidColor) cam.backgroundColor = RenderSettings.fogColor;
        if (matEstrellas) matEstrellas.color = new Color(1, 1, 1, Mathf.Clamp01(1 - Luz * 1.6f));
    }

    void LateUpdate()
    {
        if (estrellas && cam) estrellas.transform.position = cam.transform.position;   // el cielo te sigue
    }

    // 500 puntitos en media esfera alrededor de la cámara
    void CrearEstrellas()
    {
        var go = new GameObject("Estrellas") { layer = 1 };   // capa 1: no sale en el minimapa
        estrellas = go.AddComponent<ParticleSystem>();
        estrellas.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var m = estrellas.main;
        m.loop = false;
        m.playOnAwake = false;
        m.startLifetime = 1e6f;
        m.startSpeed = 0;
        m.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.3f);
        m.maxParticles = 600;
        m.simulationSpace = ParticleSystemSimulationSpace.Local;
        var e = estrellas.emission; e.enabled = false;
        var s = estrellas.shape;
        s.shapeType = ParticleSystemShapeType.Hemisphere;
        s.radius = 140;
        s.radiusThickness = 0;
        s.rotation = new Vector3(-90, 0, 0);
        matEstrellas = new Material(Shader.Find("Sprites/Default"));
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = matEstrellas;
        estrellas.Play();
        estrellas.Emit(500);
    }
}

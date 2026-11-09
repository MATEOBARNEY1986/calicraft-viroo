using UnityEngine;

// Luz que titila como fuego. Se apaga de día para no gastar (las luces cuestan en el motor).
public class Antorcha : MonoBehaviour
{
    public Light luz;
    public CicloDia ciclo;
    float baseIntensidad, semilla;

    void Start()
    {
        baseIntensidad = luz.intensity;
        semilla = Random.value * 10f;
    }

    void Update()
    {
        luz.enabled = ciclo == null || ciclo.Luz < 0.7f;
        if (luz.enabled) luz.intensity = baseIntensidad * (0.8f + 0.35f * Mathf.PerlinNoise(Time.time * 6f, semilla));
    }
}

using UnityEngine;

// Bloque de TNT con física (cae, se puede empujar). Al encenderlo parpadea y explota:
// abre un cráter en el terreno, lanza escombros volando, empuja lo que tenga física,
// lastima a los monstruos y enciende las otras TNT cercanas (reacción en cadena).
public class Tnt : MonoBehaviour
{
    public Mundo mundo;
    bool encendida;
    float mecha;
    Material mat;

    public void Encender(float segundos = 2.5f)
    {
        if (encendida) return;
        encendida = true;
        mecha = segundos;
        mat = GetComponent<Renderer>().material;   // copia propia para parpadear
        mat.EnableKeyword("_EMISSION");
        mundo.Sonar("clic");
    }

    void Update()
    {
        if (!encendida) return;
        mecha -= Time.deltaTime;
        bool blanco = Mathf.Repeat(Time.time * 6f, 1f) < 0.5f;
        mat.SetColor("_EmissionColor", blanco ? Color.white * 0.9f : Color.black);
        transform.localScale = Vector3.one * (1f + 0.1f * Mathf.Abs(Mathf.Sin(Time.time * 18f)));
        if (mecha <= 0)
        {
            mundo.Explosion(transform.position, 3.5f);
            Destroy(gameObject);
        }
    }
}

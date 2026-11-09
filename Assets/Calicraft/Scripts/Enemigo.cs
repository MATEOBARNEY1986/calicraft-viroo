using UnityEngine;

// Un monstruo (zombi, momia, científico loco...). Te persigue si te ve (de noche ve más lejos),
// si no deambula; salta los escalones de un bloque; aguanta varios golpes y se va hacia atrás
// cuando le pegas. Los que salen de noche se queman con el sol.
// (empezó como enemigo.gd de Godot)
public class Enemigo : MonoBehaviour
{
    public Mundo mundo;
    public CharacterController cc;
    public int vida = 3;
    public bool nocturno;
    public float velocidad = 2.5f, alcance = 12f;
    const float GRAVEDAD = 12f;

    float velY, tDeambular, tQuema;
    Vector3 empuje, rumbo;

    void Update()
    {
        if (mundo == null || mundo.termino || !mundo.jugando) return;   // quieto antes de empezar y al terminar
        float dt = Time.deltaTime;
        velY = cc.isGrounded ? -1 : velY - GRAVEDAD * dt;

        Vector3 hacia = mundo.PosJugador - transform.position;
        hacia.y = 0;
        float d = hacia.magnitude;
        if (d < 1.3f) mundo.Golpe(this);   // de noche te quita la última memoria

        Vector3 v = Vector3.zero;
        float ve = alcance * (mundo.EsDeNoche ? 1.5f : 1f);
        if (d < ve && d > 1f)
        {
            hacia /= d;
            v = hacia * velocidad * mundo.rapidez;
            transform.rotation = Quaternion.LookRotation(hacia);
        }
        else
        {
            tDeambular -= dt;
            if (tDeambular <= 0)
            {
                tDeambular = Random.Range(2f, 5f);
                var r = Random.insideUnitCircle.normalized;
                rumbo = Random.value < 0.3f ? Vector3.zero : new Vector3(r.x, 0, r.y);
            }
            if (rumbo != Vector3.zero)
            {
                v = rumbo * velocidad * 0.4f;
                transform.rotation = Quaternion.LookRotation(rumbo);
            }
        }
        // si hay un bloque adelante, salta
        if (cc.isGrounded && v.sqrMagnitude > 0.01f &&
            Physics.Raycast(transform.position + Vector3.up * 0.5f, v.normalized, 0.8f, ~(1 << 2), QueryTriggerInteraction.Ignore))
            velY = 6f;

        v += empuje;
        empuje = Vector3.Lerp(empuje, Vector3.zero, dt * 4f);
        v.y = velY;
        cc.Move(v * dt);

        if (nocturno && !mundo.EsDeNoche)
        {
            tQuema -= dt;
            if (tQuema <= 0) { tQuema = 0.6f; Efectos.Humo(transform.position + Vector3.up * 1.6f); Danio(1, Vector3.zero, false); }
        }
        if (transform.position.y < Terreno.Y_MIN - 20) Destroy(gameObject);
    }

    public void Danio(int n, Vector3 direccion, bool conPuntos = true)
    {
        if (vida <= 0) return;
        vida -= n;
        direccion.y = 0;
        empuje = direccion.normalized * 8f;
        velY = 4f;
        Efectos.Destello(transform.position + Vector3.up, new Color(1f, 0.25f, 0.2f));
        if (vida > 0) return;
        Efectos.Rafaga(transform.position + Vector3.up, new Color(0.4f, 0.8f, 0.3f), new Color(0.2f, 0.2f, 0.2f), 30, 5f, 0.35f, 0.9f, 0.8f, 0.5f);
        mundo.MonstruoDerrotado(conPuntos);
        Destroy(gameObject);
    }
}

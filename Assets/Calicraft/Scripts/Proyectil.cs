using UnityEngine;

// Bola de nieve (tecla F): vuela con física, le quita una vida al monstruo que toca
// y enciende la TNT si le pega.
public class Proyectil : MonoBehaviour
{
    float vida = 4f;

    void Update()
    {
        vida -= Time.deltaTime;
        if (vida <= 0) Destroy(gameObject);
    }

    void OnCollisionEnter(Collision c)
    {
        var e = c.collider.GetComponentInParent<Enemigo>();
        if (e) e.Danio(1, c.transform.position - transform.position);
        var t = c.collider.GetComponentInParent<Tnt>();
        if (t) t.Encender(0.6f);
        Efectos.Rafaga(transform.position, Color.white, new Color(0.8f, 0.9f, 1f), 12, 3f, 0.15f, 0.5f, 1f);
        Destroy(gameObject);
    }
}

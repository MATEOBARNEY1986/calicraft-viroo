using UnityEngine;

// De noche aparecen monstruos cerca de ti (lejos de la vista, entre 16 y 28 bloques).
// Cuántos depende de la dificultad. Al amanecer se queman (ver Enemigo).
public class Monstruos : MonoBehaviour
{
    public Mundo mundo;
    public Terreno terreno;
    static readonly string[] villanos = { "zombie_walter", "momia_shelly", "cientifico" };
    float espera;

    void Update()
    {
        if (!mundo.jugando || mundo.termino || !mundo.EsDeNoche) return;
        espera -= Time.deltaTime;
        if (espera > 0) return;
        espera = 3f;
        if (mundo.MonstruosNocturnos() >= mundo.MaxMonstruos) return;

        var p = mundo.PosJugador;
        for (int intento = 0; intento < 8; intento++)
        {
            var r = Random.insideUnitCircle.normalized * Random.Range(16f, 28f);
            int x = Mathf.RoundToInt(p.x + r.x), z = Mathf.RoundToInt(p.z + r.y);
            if (Mathf.Abs(x) >= Terreno.MITAD - 2 || Mathf.Abs(z) >= Terreno.MITAD - 2) continue;
            if (terreno.EsAguaColumna(x, z)) continue;
            int y = terreno.AlturaSuelo(x, z);
            if (terreno.Get(x, y - 1, z) == B.Hojas) continue;   // no encima de los árboles
            var pos = new Vector3(x, y, z);
            mundo.CrearEnemigo(pos, villanos[Random.Range(0, villanos.Length)], true);
            Efectos.Humo(pos + Vector3.up * 0.5f);
            break;
        }
    }
}

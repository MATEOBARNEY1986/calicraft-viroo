// Creaciones de los estudiantes de Modelado Digital (las mismas de la web Calicraft, creaciones.js).
// Los datos están en Mundo.Creaciones.Datos.cs (GENERADO por herramientas/creaciones_unity.py).
// Tres modos, igual que en la web:
//   Bloques: se arma con bloques del mundo (se puede caminar encima y romper)
//   Figura:  cajas a su tamaño y color reales (piezas más finas que un bloque: la vaca, el puesto de empanadas)
//   Modelo:  el .glb que hizo el estudiante (Tinkercad o Blender), importado por glTFast en Resources/creaciones
using System.Collections.Generic;
using UnityEngine;

public partial class Mundo
{
    enum ModoCreacion { Bloques, Figura, Modelo }

    class PiezaCaja
    {
        public float x, y, z, sx, sy, sz; public string m; public bool resta;
        public PiezaCaja(float x, float y, float z, float sx, float sy, float sz, string m, bool resta)
        { this.x = x; this.y = y; this.z = z; this.sx = sx; this.sy = sy; this.sz = sz; this.m = m; this.resta = resta; }
    }

    class Extra
    {
        public string piezas; public float ex, ez, escala;
        public Extra(string piezas, float ex, float ez, float escala) { this.piezas = piezas; this.ex = ex; this.ez = ez; this.escala = escala; }
    }

    class Creacion
    {
        public string id, titulo, autor, modelo, rotulo;
        public ModoCreacion modo;
        public int x, z, plano, pisoX, pisoZ;
        public float escala = 1, giro, alto;
        public bool hayPiso;
        public PiezaCaja[] cajas;
        public Extra[] extras;
    }

    int creacionesPuestas, creacionesModeloOk, creacionesModeloMal;
    readonly List<(string, Vector3)> vistasCreaciones = new List<(string, Vector3)>();

    // los mismos colores y bloques que en la web
    static readonly Dictionary<string, B> BLOQUE_CREACION = new Dictionary<string, B> {
        { "hojas", B.Hojas }, { "piedra", B.Piedra }, { "tronco", B.Tronco }, { "oro", B.Amarillo }, { "tablas", B.Tablas },
        { "ladrillo", B.Ladrillo }, { "arena", B.Arena }, { "agua", B.Azul } };
    static readonly Dictionary<string, string> COLOR_CREACION = new Dictionary<string, string> {
        { "hojas", "#4f8f2f" }, { "piedra", "#888888" }, { "tronco", "#6b4a2a" }, { "oro", "#ffd23a" }, { "tablas", "#b08850" },
        { "ladrillo", "#a04a3a" }, { "arena", "#e6d38a" }, { "agua", "#3f7fd0" } };

    static int RedondeoJs(float v) => Mathf.FloorToInt(v + 0.5f);     // Math.round de JavaScript (no el redondeo bancario)

    static (float rx, float alto, float rz) Extension(PiezaCaja[] cajas)
    {
        float rx = 0, alto = 0, rz = 0;
        foreach (var c in cajas)
        {
            if (c.resta) continue;
            rx = Mathf.Max(rx, Mathf.Abs(c.x) + c.sx / 2); alto = Mathf.Max(alto, c.y + c.sy / 2); rz = Mathf.Max(rz, Mathf.Abs(c.z) + c.sz / 2);
        }
        return (rx, alto, rz);
    }

    void CrearCreaciones()
    {
        foreach (var c in CREACIONES)
        {
            try
            {
                if (c.modo == ModoCreacion.Bloques) CreacionBloques(c);
                else if (c.modo == ModoCreacion.Figura) CreacionFigura(c);
                else CreacionModelo(c);
                creacionesPuestas++;
            }
            catch (System.Exception e) { Debug.LogError($"[Calicraft] No se pudo poner la creación {c.id}: {e.Message}"); }
        }
    }

    void CreacionBloques(Creacion c)
    {
        var (rx, alto, rz) = Extension(c.cajas);
        int i0 = Mathf.FloorToInt(c.x - rx) - 3, i1 = Mathf.CeilToInt(c.x + rx) + 3, k0 = Mathf.FloorToInt(c.z - rz) - 3, k1 = Mathf.CeilToInt(c.z + rz) + 3;
        int b = terreno.Aplanar(i0, i1, k0, k1);
        (int, int) Tramo(float centro, float lado) { int a = RedondeoJs(centro - lado / 2); return (a, Mathf.Max(RedondeoJs(centro + lado / 2), a + 1)); }
        for (int pasada = 0; pasada < 2; pasada++)
            foreach (var k in c.cajas)
            {
                if (k.resta != (pasada == 1)) continue;
                var t = k.resta ? B.Aire : (BLOQUE_CREACION.TryGetValue(k.m, out var bb) ? bb : B.Piedra);
                var (a0, a1) = Tramo(c.x + k.x, k.sx); var (c0, c1) = Tramo(c.z + k.z, k.sz);
                if (k.sy < .3f) { for (int i = a0; i < a1; i++) for (int q = c0; q < c1; q++) terreno.Set(i, b - 1, q, t); continue; }   // losa: cambia el piso
                var (b0, b1) = Tramo(k.y, k.sy);
                for (int i = a0; i < a1; i++) for (int j = Mathf.Max(0, b0); j < b1; j++) for (int q = c0; q < c1; q++) terreno.Set(i, b + j, q, t);
            }
        Rotulo($"{c.titulo} · {c.autor}", c.x, b + Mathf.Min(alto, 30) + 4, c.z);
        vistasCreaciones.Add((c.titulo, new Vector3(c.x, b, c.z)));
    }

    Material MatCreacion(string m)
    {
        if (m == "vaca") return Mat("vaca.png", Vector2.one);
        string hex = m.StartsWith("#") ? m : (COLOR_CREACION.TryGetValue(m, out var h) ? h : "#888888");
        return MatColor(ColorUtility.TryParseHtmlString(hex, out var col) ? col : Color.gray);
    }

    Transform PiezasDeFigura(PiezaCaja[] cajas, float escala, Transform padre, Vector3 pos)
    {
        var g = new GameObject("Figura").transform; g.SetParent(padre, false); g.position = pos;
        foreach (var k in cajas)
        {
            if (k.resta) continue;
            var cubo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(cubo.GetComponent<Collider>());
            cubo.transform.SetParent(g, false);
            cubo.transform.localPosition = new Vector3(k.x, k.y, k.z); cubo.transform.localScale = new Vector3(k.sx, k.sy, k.sz);
            cubo.GetComponent<Renderer>().sharedMaterial = MatCreacion(k.m);
        }
        g.localScale = Vector3.one * escala;
        return g;
    }

    void CreacionFigura(Creacion c)
    {
        var (rx, alto, rz) = Extension(c.cajas);
        float r = Mathf.Max(rx, rz) * c.escala + 2;
        if (c.extras != null) foreach (var e in c.extras) r = Mathf.Max(r, Mathf.Max(rx, rz) * c.escala + 2 + Mathf.Sqrt(e.ex * e.ex + e.ez * e.ez));
        int b = terreno.Aplanar(Mathf.FloorToInt(c.x - r), Mathf.CeilToInt(c.x + r), Mathf.FloorToInt(c.z - r), Mathf.CeilToInt(c.z + r));
        var piv = new GameObject("Creacion_" + c.id).transform; piv.SetParent(raiz, false);
        PiezasDeFigura(c.cajas, c.escala, piv, new Vector3(c.x, b, c.z));
        if (c.extras != null)
            foreach (var e in c.extras)
                if (PIEZAS_CREACIONES.TryGetValue(e.piezas, out var p)) PiezasDeFigura(p, e.escala, piv, new Vector3(c.x + e.ex, b, c.z + e.ez));
        if (c.rotulo != null) Rotulo(c.rotulo, c.x, b + alto * c.escala + 0.9f, c.z + rz * c.escala + 0.1f);
        Rotulo($"{c.titulo} · {c.autor}", c.x, b + alto * c.escala + 4.5f, c.z);
        vistasCreaciones.Add((c.titulo, new Vector3(c.x, b, c.z)));
    }

    void CreacionModelo(Creacion c)
    {
        int b = c.plano > 0 ? terreno.Aplanar(c.x - c.plano, c.x + c.plano, c.z - c.plano, c.z + c.plano)
                            : terreno.AlturaSuelo(c.hayPiso ? c.pisoX : c.x, c.hayPiso ? c.pisoZ : c.z);
        Rotulo($"{c.titulo} · {c.autor}", c.x, b + c.alto + 3.5f, c.z);
        vistasCreaciones.Add((c.titulo, new Vector3(c.x, b, c.z)));
        var pref = Resources.Load<GameObject>("creaciones/" + c.modelo);
        if (pref == null) { creacionesModeloMal++; Debug.LogError($"[Calicraft] No está el modelo creaciones/{c.modelo}"); return; }
        var piv = new GameObject("Creacion_" + c.id).transform; piv.SetParent(raiz, false);
        var o = Instantiate(pref, piv).transform;
        o.localRotation = Quaternion.Euler(0, c.giro, 0);
        var caja = Limites(o);
        if (caja.size.y < 1e-4f) { creacionesModeloMal++; return; }
        o.localScale *= c.alto / caja.size.y;
        caja = Limites(o);
        o.position += new Vector3(c.x - caja.center.x, b - caja.min.y, c.z - caja.center.z);
        foreach (var col in o.GetComponentsInChildren<Collider>()) Destroy(col);
        creacionesModeloOk++;
    }

    static Bounds Limites(Transform t)
    {
        var rs = t.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(t.position, Vector3.zero);
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }
}

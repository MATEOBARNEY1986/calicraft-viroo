using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Tipos de bloque del terreno
public enum B : byte
{
    Aire, Pasto, Tierra, Piedra, Arena, Tronco, Hojas, Ladrillo, Vidrio, Tablas,
    Carbon, Hierro, Oro, Diamante, Roca,
    // los colores de los monumentos de Calicraft (los mismos del juego VR)
    Bronce, Marmol, Rosa, Amarillo, Azul, Negro, Rojo, Ventanal, Crema
}

// El mundo de bloques generado por código (generación procedural), como en Minecraft:
// montañas y valles con ruido Perlin, cuevas, minerales, árboles y lagos. Cada semilla da
// un mundo distinto. Guarda qué bloque hay en cada casilla y dibuja el mundo por trozos
// (chunks) de 32x32: solo se dibujan las caras que dan al aire, así un mundo enorme sigue
// siendo liviano.
public class Terreno : MonoBehaviour
{
    public const int MITAD = 96, LADO = MITAD * 2;     // x y z van de -96 a 95
    public const int Y_MIN = -16, Y_MAX = 40, ALTO = Y_MAX - Y_MIN;
    public const int TROZO = 32;
    public const int PLANO = 55;                       // el centro queda plano: ahí está el mapa de Godot
    public const float NIVEL_AGUA = 0f;

    static readonly string[] TEX = {
        "pasto_arriba.png", "pasto_lado.png", "tierra.png", "piedra.png", "arena.png",
        "tronco_arriba.png", "tronco_lado.png", "hojas.png", "ladrillo.png", "vidrio.png", "tablas.png",
        "mineral_carbon.png", "mineral_hierro.png", "mineral_oro.png", "mineral_diamante.png", "roca_base.png",
        "bronce.png", "marmol.png", "rosa.png", "amarillo.png", "azul.png", "negro.png", "rojo.png", "ventanal.png", "crema.png" };
    // textura de arriba, de los lados y de abajo de cada tipo de bloque (índices de TEX)
    static readonly int[,] CARAS = {
        { 0, 0, 0 },    // Aire
        { 0, 1, 2 },    // Pasto
        { 2, 2, 2 },    // Tierra
        { 3, 3, 3 },    // Piedra
        { 4, 4, 4 },    // Arena
        { 5, 6, 5 },    // Tronco
        { 7, 7, 7 },    // Hojas
        { 8, 8, 8 },    // Ladrillo
        { 9, 9, 9 },    // Vidrio
        { 10, 10, 10 }, // Tablas
        { 11, 11, 11 }, // Carbón
        { 12, 12, 12 }, // Hierro
        { 13, 13, 13 }, // Oro
        { 14, 14, 14 }, // Diamante
        { 15, 15, 15 }, // Roca del fondo (no se rompe)
        { 16, 16, 16 }, { 17, 17, 17 }, { 18, 18, 18 }, { 19, 19, 19 }, { 20, 20, 20 },   // Bronce, Mármol, Rosa, Amarillo, Azul
        { 21, 21, 21 }, { 22, 22, 22 }, { 23, 23, 23 }, { 24, 24, 24 },                     // Negro, Rojo, Ventanal, Crema
    };
    public static string TexturaLado(B b) => TEX[CARAS[(int)b, 1]];

    // las 6 caras de un cubo: dirección y sus 4 esquinas (en orden para que Unity las vea de frente)
    static readonly Vector3Int[] DIR = {
        Vector3Int.up, Vector3Int.down, Vector3Int.right, Vector3Int.left,
        new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1) };
    static readonly Vector3[][] ESQUINAS = {
        new[] { new Vector3(0, 1, 0), new Vector3(0, 1, 1), new Vector3(1, 1, 1), new Vector3(1, 1, 0) }, // arriba
        new[] { new Vector3(1, 0, 0), new Vector3(1, 0, 1), new Vector3(0, 0, 1), new Vector3(0, 0, 0) }, // abajo
        new[] { new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(1, 1, 1), new Vector3(1, 0, 1) }, // +x
        new[] { new Vector3(0, 0, 1), new Vector3(0, 1, 1), new Vector3(0, 1, 0), new Vector3(0, 0, 0) }, // -x
        new[] { new Vector3(1, 0, 1), new Vector3(1, 1, 1), new Vector3(0, 1, 1), new Vector3(0, 0, 1) }, // +z
        new[] { new Vector3(0, 0, 0), new Vector3(0, 1, 0), new Vector3(1, 1, 0), new Vector3(1, 0, 0) }, // -z
    };
    const float M = 0.002f;   // margen para que no se cuele el borde de la textura
    static readonly Vector2[] UV = { new Vector2(M, M), new Vector2(M, 1 - M), new Vector2(1 - M, 1 - M), new Vector2(1 - M, M) };

    byte[] celdas;
    int[] alturas;              // altura original de cada columna (para los lagos)
    int semilla;
    float ox, oz;
    Material[] mats;
    Material matAgua;
    Trozo[,] trozos;

    class Trozo
    {
        public int cx, cz;
        public bool sucio = true;
        public Mesh malla;
        public MeshFilter mf;
        public MeshRenderer mr;
        public MeshCollider mc;
    }

    // ---------- generación ----------
    public void Generar(int semillaMundo, System.Func<string, bool, Material> material)
    {
        semilla = semillaMundo;
        var rnd = new System.Random(semilla);
        ox = rnd.Next(-20000, 20000);
        oz = rnd.Next(-20000, 20000);
        celdas = new byte[LADO * ALTO * LADO];
        alturas = new int[LADO * LADO];
        mats = new Material[TEX.Length];
        for (int i = 0; i < TEX.Length; i++) mats[i] = material(TEX[i], TEX[i] == "vidrio.png");
        matAgua = material("agua.png", true);

        for (int x = -MITAD; x < MITAD; x++)
            for (int z = -MITAD; z < MITAD; z++)
            {
                int h = AlturaGenerada(x, z);
                alturas[Col(x, z)] = h;
                float lejos = Lejania(x, z);
                bool playa = h <= 0 && lejos > 0.5f;
                for (int y = Y_MIN; y < h; y++)
                {
                    B b;
                    if (y == Y_MIN) b = B.Roca;
                    else if (y == h - 1) b = playa ? B.Arena : (h >= 24 ? B.Piedra : B.Pasto);
                    else if (y >= h - 4) b = playa ? B.Arena : B.Tierra;
                    else b = Mineral(x, y, z);
                    // cuevas: solo lejos de la aldea; en las montañas asoman a la superficie
                    if (lejos > 0.85f && y > Y_MIN + 1 && y < h - (h > 10 ? 0 : 3) && Cueva(x, y, z)) b = B.Aire;
                    celdas[I(x, y, z)] = (byte)b;
                }
            }

        for (int x = -MITAD + 3; x < MITAD - 3; x++)
            for (int z = -MITAD + 3; z < MITAD - 3; z++)
            {
                int h = alturas[Col(x, z)];
                if (h < 1 || h > 20 || Lejania(x, z) < 0.95f) continue;
                if (Get(x, h - 1, z) != B.Pasto || Hash(x, z, 7) > 0.012f) continue;
                ArbolDeBloques(x, h, z);
            }

        int n = LADO / TROZO;
        trozos = new Trozo[n, n];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
            {
                var t = new Trozo { cx = i, cz = j };
                var go = new GameObject($"Trozo_{i}_{j}");
                go.transform.SetParent(transform, false);
                go.AddComponent<TrozoTerreno>();
                t.mf = go.AddComponent<MeshFilter>();
                t.mr = go.AddComponent<MeshRenderer>();
                t.mc = go.AddComponent<MeshCollider>();
                t.malla = new Mesh { indexFormat = IndexFormat.UInt32, name = go.name };
                trozos[i, j] = t;
                Mallar(t);
            }
        CrearAgua();
    }

    float Ruido(float x, float z, float f) => Mathf.PerlinNoise((x + ox) * f, (z + oz) * f);

    int AlturaGenerada(int x, int z)
    {
        float lomas = (Ruido(x, z, 0.012f) + Ruido(x, z, 0.03f) * 0.5f + Ruido(x, z, 0.08f) * 0.15f) / 1.65f;
        float montes = Mathf.Pow(Mathf.Clamp01(Ruido(x + 500, z - 300, 0.01f) * 1.6f - 0.5f), 1.5f);
        float h = -6 + lomas * 16 + montes * 26;
        h = Mathf.Lerp(0, h, Lejania(x, z));
        return Mathf.Clamp(Mathf.RoundToInt(h), Y_MIN + 3, Y_MAX - 8);
    }

    // 0 en la zona plana del centro, 1 lejos (con una transición suave)
    public static float Lejania(float x, float z)
    {
        float d = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
        return Mathf.SmoothStep(0, 1, Mathf.InverseLerp(PLANO, PLANO + 18, d));
    }

    bool Cueva(int x, int y, int z)
    {
        const float f = 0.07f;   // ruido "3D" armado con tres ruidos 2D cruzados
        float a = Mathf.PerlinNoise((x + ox) * f, (y + oz) * f);
        float b = Mathf.PerlinNoise((y + oz) * f + 31, (z + ox) * f);
        float c = Mathf.PerlinNoise((z + ox) * f + 77, (x + oz) * f + 11);
        return (a + b + c) / 3f > 0.61f;
    }

    B Mineral(int x, int y, int z)
    {
        float r = Hash3(x, y, z);
        if (y < -10 && r < 0.006f) return B.Diamante;
        if (y < -4 && r < 0.012f) return B.Oro;
        if (y < 4 && r < 0.03f) return B.Hierro;
        if (r < 0.05f) return B.Carbon;
        return B.Piedra;
    }

    void ArbolDeBloques(int x, int y, int z)
    {
        int alto = 4 + (int)(Hash(x, z, 3) * 3);
        for (int dy = -2; dy <= 1; dy++)
        {
            int r = dy >= 0 ? 1 : 2;
            for (int dx = -r; dx <= r; dx++)
                for (int dz = -r; dz <= r; dz++)
                {
                    if (r == 2 && Mathf.Abs(dx) == 2 && Mathf.Abs(dz) == 2) continue;
                    if (Get(x + dx, y + alto + dy, z + dz) == B.Aire) SetCrudo(x + dx, y + alto + dy, z + dz, B.Hojas);
                }
        }
        for (int i = 0; i < alto; i++) SetCrudo(x, y + i, z, B.Tronco);
    }

    float Hash(int x, int z, int k) => Hash3(x, k * 977, z);

    float Hash3(int x, int y, int z)
    {
        unchecked
        {
            uint h = (uint)x * 73856093u ^ (uint)y * 19349663u ^ (uint)z * 83492791u ^ (uint)semilla * 2654435761u;
            h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15;
            return (h & 0xFFFFFF) / 16777216f;
        }
    }

    // ---------- leer y cambiar bloques ----------
    static int I(int x, int y, int z) => ((x + MITAD) * ALTO + (y - Y_MIN)) * LADO + (z + MITAD);
    static int Col(int x, int z) => (x + MITAD) * LADO + (z + MITAD);
    public static bool Dentro(int x, int y, int z) =>
        x >= -MITAD && x < MITAD && z >= -MITAD && z < MITAD && y >= Y_MIN && y < Y_MAX;

    public B Get(int x, int y, int z) => Dentro(x, y, z) ? (B)celdas[I(x, y, z)] : B.Aire;

    void SetCrudo(int x, int y, int z, B b) { if (Dentro(x, y, z)) celdas[I(x, y, z)] = (byte)b; }

    public bool Set(int x, int y, int z, B b)
    {
        if (!Dentro(x, y, z) || Get(x, y, z) == B.Roca) return false;   // el fondo no se rompe
        celdas[I(x, y, z)] = (byte)b;
        MarcarSucio(x, z);
        return true;
    }

    // altura del suelo (encima del primer bloque desde arriba)
    public int AlturaSuelo(int x, int z)
    {
        for (int y = Y_MAX - 1; y >= Y_MIN; y--) if (Get(x, y, z) != B.Aire) return y + 1;
        return Y_MIN;
    }

    public bool EsAguaColumna(int x, int z) =>
        x >= -MITAD && x < MITAD && z >= -MITAD && z < MITAD && alturas[Col(x, z)] < 0;

    public bool EsAgua(Vector3 p) =>
        EsAguaColumna(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.z)) && p.y < NIVEL_AGUA - 0.3f;

    // quita los bloques en una esfera (la explosión de la TNT); devuelve lo que quitó
    public List<(Vector3Int celda, B tipo)> Explotar(Vector3 c, float r)
    {
        var quitados = new List<(Vector3Int, B)>();
        int R = Mathf.CeilToInt(r);
        int cx = Mathf.RoundToInt(c.x), cy = Mathf.FloorToInt(c.y), cz = Mathf.RoundToInt(c.z);
        for (int dx = -R; dx <= R; dx++)
            for (int dy = -R; dy <= R; dy++)
                for (int dz = -R; dz <= R; dz++)
                {
                    if (dx * dx + dy * dy + dz * dz > r * r) continue;
                    int x = cx + dx, y = cy + dy, z = cz + dz;
                    var b = Get(x, y, z);
                    if (b == B.Aire || b == B.Roca) continue;
                    celdas[I(x, y, z)] = (byte)B.Aire;
                    MarcarSucio(x, z);
                    quitados.Add((new Vector3Int(x, y, z), b));
                }
        return quitados;
    }

    // las cumbres más altas, separadas entre sí (ahí van las gemas lejanas)
    public List<Vector3> Cumbres(int cuantas, float separacion)
    {
        var cols = new List<(int h, int x, int z)>();
        for (int x = -MITAD + 6; x < MITAD - 6; x += 2)
            for (int z = -MITAD + 6; z < MITAD - 6; z += 2)
                if (Lejania(x, z) > 0.95f) cols.Add((AlturaSuelo(x, z), x, z));
        cols.Sort((a, b) => b.h.CompareTo(a.h));
        var lista = new List<Vector3>();
        foreach (var c in cols)
        {
            var p = new Vector3(c.x, c.h + 1f, c.z);
            bool lejos = true;
            foreach (var q in lista) if ((q - p).sqrMagnitude < separacion * separacion) { lejos = false; break; }
            if (lejos) lista.Add(p);
            if (lista.Count >= cuantas) break;
        }
        return lista;
    }

    // ---------- dibujar (mallas por trozo) ----------
    void MarcarSucio(int x, int z)
    {
        Sucio(x, z); Sucio(x + 1, z); Sucio(x - 1, z); Sucio(x, z + 1); Sucio(x, z - 1);
    }

    void Sucio(int x, int z)
    {
        if (x < -MITAD || x >= MITAD || z < -MITAD || z >= MITAD) return;
        trozos[(x + MITAD) / TROZO, (z + MITAD) / TROZO].sucio = true;
    }

    void LateUpdate()
    {
        if (trozos == null) return;
        foreach (var t in trozos) if (t.sucio) Mallar(t);   // se rehace solo el trozo que cambió
    }

    readonly List<Vector3> vs = new List<Vector3>();
    readonly List<Vector3> ns = new List<Vector3>();
    readonly List<Vector2> uvs = new List<Vector2>();
    List<int>[] tris;
    readonly List<int> usadas = new List<int>();

    void Mallar(Trozo t)
    {
        if (tris == null) { tris = new List<int>[TEX.Length]; for (int i = 0; i < tris.Length; i++) tris[i] = new List<int>(); }
        vs.Clear(); ns.Clear(); uvs.Clear();
        foreach (var l in tris) l.Clear();

        int x0 = -MITAD + t.cx * TROZO, z0 = -MITAD + t.cz * TROZO;
        for (int x = x0; x < x0 + TROZO; x++)
            for (int z = z0; z < z0 + TROZO; z++)
                for (int y = Y_MIN; y < Y_MAX; y++)
                {
                    var b = (B)celdas[I(x, y, z)];
                    if (b == B.Aire) continue;
                    for (int f = 0; f < 6; f++)
                    {
                        var d = DIR[f];
                        int nx = x + d.x, ny = y + d.y, nz = z + d.z;
                        if (ny < Y_MIN || nx < -MITAD || nx >= MITAD || nz < -MITAD || nz >= MITAD) continue;
                        var v = Get(nx, ny, nz);
                        if (v != B.Aire && !(v == B.Vidrio && b != B.Vidrio)) continue;   // cara tapada
                        int tex = CARAS[(int)b, f == 0 ? 0 : (f == 1 ? 2 : 1)];
                        int base0 = vs.Count;
                        var origen = new Vector3(x - 0.5f, y, z - 0.5f);
                        for (int k = 0; k < 4; k++)
                        {
                            vs.Add(origen + ESQUINAS[f][k]);
                            ns.Add(d);
                            uvs.Add(UV[k]);
                        }
                        var tr = tris[tex];
                        tr.Add(base0); tr.Add(base0 + 1); tr.Add(base0 + 2);
                        tr.Add(base0); tr.Add(base0 + 2); tr.Add(base0 + 3);
                    }
                }

        var m = t.malla;
        m.Clear();
        m.SetVertices(vs);
        m.SetNormals(ns);
        m.SetUVs(0, uvs);
        usadas.Clear();
        for (int k = 0; k < tris.Length; k++) if (tris[k].Count > 0) usadas.Add(k);
        m.subMeshCount = usadas.Count;
        var ms = new Material[usadas.Count];
        for (int s = 0; s < usadas.Count; s++) { m.SetTriangles(tris[usadas[s]], s); ms[s] = mats[usadas[s]]; }
        m.RecalculateBounds();
        t.mf.sharedMesh = m;
        t.mr.sharedMaterials = ms;
        t.mc.sharedMesh = null;
        t.mc.sharedMesh = vs.Count > 0 ? m : null;
        t.sucio = false;
    }

    // deja plana una zona (como el aplanar del juego VR): devuelve la altura del suelo
    public int Aplanar(int x0, int x1, int z0, int z1)
    {
        var cuenta = new Dictionary<int, int>();
        for (int x = x0; x <= x1; x += 3)
            for (int z = z0; z <= z1; z += 3)
            {
                int h = AlturaSuelo(x, z);
                cuenta.TryGetValue(h, out int c); cuenta[h] = c + 1;
            }
        int bas = 0, mejor = -1;
        foreach (var kv in cuenta) if (kv.Value > mejor) { mejor = kv.Value; bas = kv.Key; }
        for (int x = x0; x <= x1; x++)
            for (int z = z0; z <= z1; z++)
            {
                if (x < -MITAD || x >= MITAD || z < -MITAD || z >= MITAD) continue;
                for (int y = bas; y < Y_MAX; y++) Set(x, y, z, B.Aire);
                for (int y = Y_MIN + 1; y < bas - 1; y++) if (Get(x, y, z) == B.Aire) Set(x, y, z, B.Tierra);
                Set(x, bas - 1, z, B.Pasto);
                if (x >= -MITAD && x < MITAD && z >= -MITAD && z < MITAD) alturas[Col(x, z)] = bas;
            }
        return bas;
    }

    // un cerro redondo con la cima plana (Cristo Rey, Las Tres Cruces, el oso): devuelve la cima
    public int Cerro(int cx, int cz, int R, int H)
    {
        var borde = new List<int>();
        for (int a = 0; a < 16; a++)
            borde.Add(AlturaSuelo(Mathf.RoundToInt(cx + Mathf.Cos(a * Mathf.PI / 8) * R), Mathf.RoundToInt(cz + Mathf.Sin(a * Mathf.PI / 8) * R)));
        borde.Sort();
        int bas = borde[8], top = bas + H;
        for (int i = cx - R; i <= cx + R; i++)
            for (int k = cz - R; k <= cz + R; k++)
            {
                if (i < -MITAD || i >= MITAD || k < -MITAD || k >= MITAD) continue;
                float d = Mathf.Sqrt((i - cx) * (i - cx) + (k - cz) * (k - cz));
                if (d > R) continue;
                int h = d < 4 ? top : bas + Mathf.RoundToInt(H * Mathf.Cos(Mathf.Min(1f, (d - 4) / (float)(R - 4)) * Mathf.PI / 2));
                for (int y = h; y < Y_MAX; y++) Set(i, y, k, B.Aire);
                if (AlturaSuelo(i, k) > h) continue;
                for (int y = bas; y < h; y++) { var t = Get(i, y, k); if (t == B.Aire || t == B.Hojas || t == B.Tronco || t == B.Pasto) Set(i, y, k, B.Tierra); }
                Set(i, h - 1, k, B.Pasto);
                if (i >= -MITAD && i < MITAD && k >= -MITAD && k < MITAD) alturas[Col(i, k)] = h;
            }
        return top;
    }

    // una laguna redonda (la de la ballena y el pescador): se cava y se vuelve a dibujar el agua
    public void Laguna(int cx, int cz, int R)
    {
        for (int x = cx - R - 2; x <= cx + R + 2; x++)
            for (int z = cz - R - 2; z <= cz + R + 2; z++)
            {
                if (x < -MITAD || x >= MITAD || z < -MITAD || z >= MITAD) continue;
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (z - cz) * (z - cz));
                if (d > R + 2) continue;
                bool agua = d < R;
                int fondo = agua ? -4 : 0;
                for (int y = fondo; y < Y_MAX; y++) Set(x, y, z, B.Aire);
                for (int y = fondo - 1; y >= fondo - 3; y--) Set(x, y, z, B.Arena);
                for (int y = fondo - 4; y > Y_MIN; y--) if (Get(x, y, z) == B.Aire) Set(x, y, z, B.Piedra);
                alturas[Col(x, z)] = agua ? -4 : 0;
            }
        var viejo = transform.Find("Agua");
        if (viejo) Destroy(viejo.gameObject);
        CrearAgua();
    }

    // los lagos: una sola malla transparente al nivel del agua, sin choque (se puede nadar)
    void CrearAgua()
    {
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var tr = new List<int>();
        float y = NIVEL_AGUA - 0.1f;
        for (int x = -MITAD; x < MITAD; x++)
            for (int z = -MITAD; z < MITAD; z++)
            {
                if (alturas[Col(x, z)] >= 0) continue;
                int b0 = v.Count;
                for (int k = 0; k < 4; k++)
                {
                    var e = ESQUINAS[0][k];
                    v.Add(new Vector3(x - 0.5f + e.x, y, z - 0.5f + e.z));
                    uv.Add(new Vector2(x + e.x, z + e.z));
                }
                tr.Add(b0); tr.Add(b0 + 1); tr.Add(b0 + 2); tr.Add(b0); tr.Add(b0 + 2); tr.Add(b0 + 3);
            }
        if (v.Count == 0) return;
        var go = new GameObject("Agua");
        go.transform.SetParent(transform, false);
        var m = new Mesh { indexFormat = IndexFormat.UInt32, name = "Agua" };
        m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tr, 0);
        m.RecalculateNormals(); m.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh = m;
        go.AddComponent<MeshRenderer>().sharedMaterial = matAgua;
    }
}

// marca para saber que el rayo tocó el terreno (y no la casa o un árbol de Godot)
public class TrozoTerreno : MonoBehaviour { }

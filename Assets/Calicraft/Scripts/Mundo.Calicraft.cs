using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

// Calicraft (versión Unity): la misma misión del juego de realidad virtual.
// - 19 cubos de memoria y biopatrimonio de Cali y el Valle; al tocarlos dan su consigna
//   (los textos y enlaces están en Mundo.Memorias.Datos.cs, generado desde el juego VR).
// - 5 casas que tumbó el terremoto: poner un bloque en cada hueco amarillo las reconstruye.
// - De noche llegan los malos pensamientos (monstruos violeta): te quitan la última memoria.
// Se gana con todas las memorias y todas las casas.
public partial class Mundo
{
    public enum Cat { Naturaleza, Patrimonio, Cultura, Resistencia, Creacion }

    public class Enlace
    {
        public string texto, url;
        public Enlace(string t, string u) { texto = t; url = u; }
    }

    public class Memoria
    {
        public string id, nombre, consigna;
        public Cat cat;
        public int x, z;           // dónde está su monumento en el mundo
        public Enlace[] enlaces;
        public Memoria(string i, string n, Cat c, int x, int z, string co, params Enlace[] e)
        { id = i; nombre = n; cat = c; this.x = x; this.z = z; consigna = co; enlaces = e; }
    }

    static Color ColorCat(Cat c) => c switch
    {
        Cat.Naturaleza => new Color(0.18f, 0.44f, 0.84f),
        Cat.Patrimonio => new Color(0.83f, 0.63f, 0.16f),
        Cat.Cultura => new Color(0.94f, 0.42f, 0.66f),
        Cat.Creacion => new Color(0.21f, 0.65f, 0.32f),       // creaciones de los estudiantes (verde, como en la web)
        _ => new Color(0.78f, 0.06f, 0.18f),
    };

    const string CONSIGNA_CASA = "Un terremoto tumbó parte de esta casa. Reconstruir juntos también es un acto de memoria: así la comunidad se levanta de nuevo.";

    // ---------- estado ----------
    class Cubo { public Memoria m; public Vector3 lugar; public Transform obj; }
    readonly List<Cubo> cubos = new List<Cubo>();
    readonly List<Cubo> recogidos = new List<Cubo>();     // en orden: el mal pensamiento quita el último

    class CasaRota { public Vector3 centro; public List<Vector3Int> huecos = new List<Vector3Int>(); public List<GameObject> fantasmas = new List<GameObject>(); public bool lista; }
    readonly List<CasaRota> casas = new List<CasaRota>();
    int CasasListas { get { int n = 0; foreach (var c in casas) if (c.lista) n++; return n; } }

    Memoria consignaVista;     // la consigna que se está mostrando
    float tConsigna;
    Rect rectPanel;            // dónde quedó el panel (para no volver a bloquear el mouse al hacer clic en él)

    // ---------- arma la misión ----------
    void CrearCalicraft()
    {
        cubos.Clear(); recogidos.Clear(); casas.Clear();
        duenoMemoria.Clear(); nombreJugador.Clear(); memoriasDe.Clear();
        consignaVista = null; tConsigna = 0;
        ciclo.duracion = 540f;     // día de 9 minutos; la noche pasa al doble de rápido (CicloDia)
        ciclo.hora = 0.28f;

        // los monumentos de bloques (el centro de Cali, el río, los cerros, el biopatrimonio…)
        ConstruirMonumentos();
        CrearCreaciones();         // las creaciones de los estudiantes (Mundo.Creaciones.cs), antes de poner los cubos

        // sobre cada monumento, su cubo de memoria con el rayo de luz
        foreach (var m in MEMORIAS)
        {
            var lugar = TierraFirmeCerca(m.x, m.z);
            var c = new Cubo { m = m, lugar = lugar };
            PonerCubo(c);
            cubos.Add(c);
        }

        foreach (var (x, z) in new[] { (-34, -26), (26, -30), (-10, -38), (12, 52), (-32, 34) })
            casas.Add(CasaDelTerremoto(x, z));
    }

    Vector3 TierraFirmeCerca(int x0, int z0)
    {
        for (int d = 0; d < 12; d++)
            for (int dx = -d; dx <= d; dx++)
                for (int dz = -d; dz <= d; dz++)
                {
                    int x = Mathf.Clamp(x0 + dx, -Terreno.MITAD + 6, Terreno.MITAD - 6), z = Mathf.Clamp(z0 + dz, -Terreno.MITAD + 6, Terreno.MITAD - 6);
                    if (terreno.EsAguaColumna(x, z)) continue;
                    int y = terreno.AlturaSuelo(x, z);
                    if (terreno.Get(x, y - 1, z) == B.Hojas) continue;
                    return new Vector3(x, y + 1.1f, z);
                }
        return new Vector3(x0, terreno.AlturaSuelo(x0, z0) + 1.1f, z0);
    }

    void PonerCubo(Cubo c)
    {
        var col = ColorCat(c.m.cat);
        var piv = new GameObject("Memoria_" + c.m.id).transform;
        piv.SetParent(raiz, false);
        piv.position = c.lugar;
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(piv, false);
        g.transform.localScale = Vector3.one * 0.7f;
        g.GetComponent<Renderer>().sharedMaterial = MatColor(col, 0.6f);
        Efectos.Brillo(piv);
        Efectos.Faro(piv, new Color(col.r, col.g, col.b, 0.35f));
        c.obj = piv;
        giran.Add(piv);
    }

    // una casa de ladrillo de 7x7 con la pared caída en 7 partes (huecos amarillos)
    CasaRota CasaDelTerremoto(int cx, int cz)
    {
        var casa = new CasaRota();
        var lugar = TierraFirmeCerca(cx, cz);
        cx = (int)lugar.x; cz = (int)lugar.z;
        int y0 = terreno.AlturaSuelo(cx, cz);
        casa.centro = new Vector3(cx, y0, cz);
        const int R = 3, ALTO = 4;
        // limpia el espacio y rellena el piso
        for (int x = cx - R - 1; x <= cx + R + 1; x++)
            for (int z = cz - R - 1; z <= cz + R + 1; z++)
            {
                for (int y = y0; y < y0 + ALTO + 4; y++) terreno.Set(x, y, z, B.Aire);
                for (int y = y0 - 1; y >= y0 - 6 && terreno.Get(x, y, z) == B.Aire; y--) terreno.Set(x, y, z, B.Piedra);
                terreno.Set(x, y0 - 1, z, B.Tablas);
            }
        var muro = new List<Vector3Int>();
        for (int x = cx - R; x <= cx + R; x++)
            for (int z = cz - R; z <= cz + R; z++)
            {
                bool borde = Mathf.Abs(x - cx) == R || Mathf.Abs(z - cz) == R;
                if (!borde) continue;
                for (int y = y0; y < y0 + ALTO; y++)
                {
                    bool puerta = z == cz - R && x == cx && y < y0 + 2;
                    if (puerta) continue;
                    terreno.Set(x, y, z, B.Ladrillo);
                    if (y > y0 && !(Mathf.Abs(x - cx) == R && Mathf.Abs(z - cz) == R)) muro.Add(new Vector3Int(x, y, z));
                }
            }
        // 7 huecos repartidos por los muros (siempre los mismos para cada casa)
        var azar = new System.Random(cx * 73856093 ^ cz * 19349663);
        while (casa.huecos.Count < 7 && muro.Count > 0)
        {
            int k = azar.Next(muro.Count);
            var h = muro[k];
            muro.RemoveAt(k);
            terreno.Set(h.x, h.y, h.z, B.Aire);
            casa.huecos.Add(h);
            var f = GameObject.CreatePrimitive(PrimitiveType.Cube);
            f.name = "Hueco";
            Destroy(f.GetComponent<Collider>());
            f.transform.SetParent(raiz, false);
            f.transform.position = new Vector3(h.x, h.y + 0.5f, h.z);
            f.transform.localScale = Vector3.one * 0.9f;
            var m = MatColor(new Color(1f, 0.85f, 0.15f), 0.8f);
            Transparente(m, 0.35f);
            f.GetComponent<Renderer>().sharedMaterial = m;
            casa.fantasmas.Add(f);
        }
        // escombros alrededor
        for (int i = 0; i < 6; i++)
        {
            int x = cx + azar.Next(-R - 2, R + 3), z = cz + azar.Next(-R - 2, R + 3);
            if (Mathf.Abs(x - cx) <= R && Mathf.Abs(z - cz) <= R) continue;
            terreno.Set(x, terreno.AlturaSuelo(x, z), z, i % 2 == 0 ? B.Ladrillo : B.Piedra);
        }
        return casa;
    }

    // se llama después de poner un bloque: ¿tapó un hueco de una casa?
    void RevisarHueco(Vector3Int c)
    {
        foreach (var casa in casas)
        {
            int i = casa.huecos.IndexOf(c);
            if (i < 0) continue;
            casa.huecos.RemoveAt(i);
            Efectos.Destello(new Vector3(c.x, c.y + 0.5f, c.z), new Color(1f, 0.85f, 0.2f));
            Destroy(casa.fantasmas[i]);
            casa.fantasmas.RemoveAt(i);
            puntos += 10;
            Sonar("clic");
            if (casa.huecos.Count == 0 && !casa.lista)
            {
                casa.lista = true;
                puntos += 100;
                Sonar("ganar");
                MostrarConsigna(new Memoria("casa", "Casa reconstruida", Cat.Patrimonio, 0, 0, CONSIGNA_CASA), casa.centro + Vector3.up * 4);
                RevisarVictoria();
            }
            else Aviso($"Faltan {casa.huecos.Count} huecos en esta casa");
            return;
        }
    }

    // ---------- cada cuadro ----------
    void ActualizarCalicraft(float dt)
    {
        if (tConsigna > 0) tConsigna -= dt;
        for (int i = cubos.Count - 1; i >= 0; i--)
        {
            var c = cubos[i];
            if (c.obj == null) continue;
            c.obj.position = c.lugar + Vector3.up * (Mathf.Sin(Time.time * 2f + i) * 0.15f);
            if (Toca(c.obj.position, 0.8f)) PedirMemoria(i);   // (VIROO) se pide por la red; vuelve en RecogerMemoria
        }
    }

    // (VIROO) llega por la red a todos los jugadores, en el mismo orden: la memoria es de quien llegó primero
    void RecogerMemoria(int i, string quien, string nombre)
    {
        if (i < 0 || i >= cubos.Count) return;
        var c = cubos[i];
        if (c.obj == null) return;              // ya la había encontrado otro: se queda con su dueño
        AnotarDueno(i, quien, nombre);
        bool mia = quien == miId && miId != "";
        if (!silencio) Efectos.Destello(c.obj.position, ColorCat(c.m.cat));
        giran.Remove(c.obj);
        Destroy(c.obj.gameObject);
        c.obj = null;
        recogidos.Add(c);
        puntos += 50;
        Sonar("gema");
        MostrarConsigna(c.m, c.lugar);
        if (!silencio) Aviso(mia ? "¡Es tuya! " + c.m.nombre : $"{NombreDe(quien)} encontró: {c.m.nombre}");
        RevisarVictoria();
    }

    void MostrarConsigna(Memoria m, Vector3 donde)
    {
        if (silencio) return;
        consignaVista = m;
        tConsigna = m.enlaces != null && m.enlaces.Length > 0 ? 25f : 14f;
        // (VIROO) en las gafas no se abren páginas: los videos están en la pantalla de al lado
        // (Mundo.Videos.cs) y el cómic y los micrositios quedan con su dirección para visitarlos después
        string texto = m.consigna;
        if (m.enlaces != null)
        {
            bool hayVideo = false;
            var webs = new System.Collections.Generic.List<string>();
            foreach (var e in m.enlaces)
                if (IdYouTube(e.url) != null) hayVideo = true;
                else webs.Add($"{e.texto}: {e.url.Replace("https://", "").Replace("/preview", "").TrimEnd('/')}");
            if (hayVideo) texto += "\n\n<color=#F06AA8>Mira el video en la pantalla de al lado.</color>";
            if (webs.Count > 0) texto += "\n\n<color=#F06AA8>Para visitar después:\n" + string.Join("\n", webs) + "</color>";
        }
        MostrarPanel(m.nombre, texto, ColorCat(m.cat), donde);
    }

    void RevisarVictoria()
    {
        if (recogidos.Count >= MEMORIAS.Length && CasasListas >= casas.Count) Terminar("RECUPERARON LA MEMORIA DE CALI Y EL VALLE");
    }

    // un monstruo te alcanzó: los malos pensamientos (de noche) te quitan la última memoria
    public void Golpe(Enemigo e)
    {
        if (termino || invencible > 0) return;
        if (e.nocturno && recogidos.Count > 0)
        {
            var c = recogidos[recogidos.Count - 1];
            recogidos.RemoveAt(recogidos.Count - 1);
            PonerCubo(c);                      // el cubo vuelve a su lugar: hay que ir por él otra vez
            invencible = 1.5f;
            golpeAlpha = 0.4f;
            Sonar("golpe");
            Aviso("Un mal pensamiento te quitó: " + c.m.nombre);
            return;
        }
        PerderVida();
    }

    // los malos pensamientos son violeta
    static void Violeta(GameObject cuerpo)
    {
        foreach (var r in cuerpo.GetComponentsInChildren<Renderer>())
            foreach (var m in r.materials)
            {
                if (m.HasProperty("_Color")) m.color = Color.Lerp(m.color, new Color(0.55f, 0.25f, 0.9f), 0.65f);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.Lerp(m.GetColor("_BaseColor"), new Color(0.55f, 0.25f, 0.9f), 0.65f));
            }
    }

    // ---------- enlaces (videos, cómic) ----------
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void AbrirEnlace(string url);
#else
    static void AbrirEnlace(string url) => Application.OpenURL(url);
#endif

    // ---------- pantalla ----------
    void PanelConsigna(float W, float H)
    {
        if (consignaVista == null || tConsigna <= 0) { rectPanel = Rect.zero; return; }
        var m = consignaVista;
        var col = ColorCat(m.cat);
        bool hayEnlaces = m.enlaces != null && m.enlaces.Length > 0;
        float ancho = Mathf.Min(720, W - 40);
        var st = new GUIStyle(GUI.skin.label) { font = fuente, fontSize = 12, wordWrap = true };
        float alto = st.CalcHeight(new GUIContent(m.consigna), ancho - 36);
        float total = 24 + 26 + alto + (hayEnlaces ? 74 : 16);
        var r = new Rect(W / 2 - ancho / 2, H - 120 - total, ancho, total);
        rectPanel = r;
        float a = Mathf.Clamp01(tConsigna);
        Pintar(r, new Color(0.055f, 0.07f, 0.125f, 0.92f * a));
        Marco(r, new Color(col.r, col.g, col.b, a), 3);
        Texto(new Rect(r.x + 18, r.y + 14, ancho - 36, 22), m.nombre, 14, new Color(col.r, col.g, col.b, a), TextAnchor.UpperLeft);
        Texto(new Rect(r.x + 18, r.y + 44, ancho - 36, alto + 4), m.consigna, 12, new Color(1, 1, 1, a), TextAnchor.UpperLeft, false, true);
        if (!hayEnlaces) return;
        float x = r.x + 18, y = r.y + 44 + alto + 12;
        bool libre = Cursor.lockState != CursorLockMode.Locked;
        foreach (var e in m.enlaces)
        {
            float w = Mathf.Min(260, 40 + e.texto.Length * 11);
            var b = new Rect(x, y, w, 34);
            Pintar(b, new Color(0.78f, 0.06f, 0.18f, a));
            Texto(b, e.texto, 10, Color.white, TextAnchor.MiddleCenter, false);
            // se abre al soltar el clic (así el navegador no lo bloquea como ventana emergente)
            if (libre && Event.current.type == EventType.MouseDown && b.Contains(Event.current.mousePosition))
            {
                Sonar("clic");
                AbrirEnlace(e.url);
                Event.current.Use();
            }
            x += w + 10;
        }
        if (!libre) Texto(new Rect(x, y, r.xMax - x - 10, 34), "ESC: suelta el mouse para abrir", 8, new Color(1, 1, 1, 0.75f), TextAnchor.MiddleLeft, false, true);
    }

    // ¿el mouse está sobre el panel? (en coordenadas de pantalla de Unity)
    bool MouseEnPanel()
    {
        if (rectPanel.width <= 0) return false;
        float esc = Screen.height / 648f;
        var p = new Vector2(Input.mousePosition.x / esc, (Screen.height - Input.mousePosition.y) / esc);
        return rectPanel.Contains(p);
    }
}

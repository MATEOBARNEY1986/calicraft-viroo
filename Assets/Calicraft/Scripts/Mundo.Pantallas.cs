using UnityEngine;

// Pantallas y HUD (en Godot eran Control/Label/Button): inicio (portada, misión, elegir
// personaje), fin (GAME OVER / MISIÓN CUMPLIDA), marcador, reloj, inventario, minimapa y ayuda.
public partial class Mundo
{
    // ---------- minimapa: una segunda cámara mira desde arriba y dibuja en una textura ----------
    void CrearMinimapa()
    {
        rtMapa = new RenderTexture(256, 256, 16) { filterMode = FilterMode.Point };
        camMapa = new GameObject("CamaraMapa").AddComponent<Camera>();
        camMapa.orthographic = true;
        camMapa.enabled = false;   // se dibuja a mano cada pocos cuadros (ahorra trabajo)
        camMapa.targetTexture = rtMapa;
        camMapa.clearFlags = CameraClearFlags.SolidColor;
        camMapa.backgroundColor = new Color(0.08f, 0.12f, 0.1f);
        camMapa.cullingMask = ~((1 << CAPA_JUGADOR) | (1 << CAPA_CIELO));
        camMapa.nearClipPlane = 0.3f;
        camMapa.farClipPlane = 140;
        camMapa.transform.rotation = Quaternion.Euler(90, 0, 0);   // norte arriba
    }

    void LateUpdate()
    {
        if (cargando || jugador == null || camMapa == null) return;
        if (++cuadroMapa % 3 != 0) return;
        var p = jugador.transform.position;
        camMapa.orthographicSize = mapaGrande ? 80 : 32;
        camMapa.transform.position = new Vector3(p.x, p.y + 60, p.z);
        bool niebla = RenderSettings.fog;
        var sombras = QualitySettings.shadows;
        RenderSettings.fog = false;                       // el mapa se ve sin niebla ni sombras
        QualitySettings.shadows = ShadowQuality.Disable;
        camMapa.Render();
        RenderSettings.fog = niebla;
        QualitySettings.shadows = sombras;
    }

    // =====================================================================
    void OnGUI()
    {
        float esc = Screen.height / 648f;                       // como el viewport 1152x648 de Godot
        GUI.matrix = Matrix4x4.Scale(new Vector3(esc, esc, 1));
        float W = Screen.width / esc, H = 648;

        if (cargando)
        {
            Pintar(new Rect(0, 0, W, H), new Color(0.04f, 0.06f, 0.12f, 1));
            Texto(new Rect(0, 0, W, H), "CARGANDO...", 24, Color.white, TextAnchor.MiddleCenter);
            Texto(new Rect(0, H / 2 + 30, W, 30), "Generando un mundo nuevo", 12, new Color(1, 1, 1, 0.7f), TextAnchor.MiddleCenter);
            if (diag != "") Texto(new Rect(10, H - 70, W - 20, 64), diag, 9, Color.white, TextAnchor.LowerLeft, true, true);
            return;
        }
        if (diag != "") Texto(new Rect(10, H - 70, W - 20, 64), diag, 9, Color.white, TextAnchor.LowerLeft, true, true);

        Texto(new Rect(20, 16, W, 30), $"Memorias: {recogidos.Count} / {meta}   Casas: {CasasListas} / {casas.Count}   Vidas: {vidas}   Puntos: {puntos}", 14, Color.white, TextAnchor.UpperLeft);
        if (jugando && !termino) { Hud(W, H); PanelConsigna(W, H); }
        if (golpeAlpha > 0) Pintar(new Rect(0, 0, W, H), new Color(1, 0, 0, golpeAlpha));

        if (termino) { PantallaFin(W, H); return; }
        if (!yaEmpezo) PantallaInicio(W, H);
    }

    void Hud(float W, float H)
    {
        float hh = ciclo.hora * 24f;
        int h = (int)hh, m = (int)((hh - h) * 60);
        bool noche = EsDeNoche;
        Texto(new Rect(20, 44, W, 20), $"{(noche ? "NOCHE" : "DIA")}  {h:00}:{m:00}   (N = adelantar el tiempo)", 11,
              noche ? new Color(0.6f, 0.75f, 1f) : new Color(1f, 0.9f, 0.4f), TextAnchor.UpperLeft);
        if (noche) Texto(new Rect(20, 64, W, 20), "Es de noche: llegan los malos pensamientos", 10, new Color(0.75f, 0.55f, 1f), TextAnchor.UpperLeft);

        Mira(W / 2, H / 2);
        Inventario(W, H);
        Mapa(W, H);
        if (tAviso > 0) Texto(new Rect(0, H / 2 + 40, W, 30), aviso, 14, new Color(1, 0.85f, 0.2f), TextAnchor.MiddleCenter);
        if (ayuda || tAyuda > 0) Ayuda();
        else Texto(new Rect(20, 84, W, 20), "H = ayuda", 9, new Color(1, 1, 1, 0.7f), TextAnchor.UpperLeft);
    }

    void Inventario(float W, float H)
    {
        const float S = 46, G = 6;
        float x0 = W / 2 - (tipos.Length * S + (tipos.Length - 1) * G) / 2, y = H - S - 14;
        Texto(new Rect(0, y - 24, W, 20), tipos[tipoSel].nombre, 12, Color.white, TextAnchor.MiddleCenter);
        for (int i = 0; i < tipos.Length; i++)
        {
            var r = new Rect(x0 + i * (S + G), y, S, S);
            Pintar(r, new Color(0, 0, 0, 0.55f));
            if (tipos[i].antorcha)
            {
                Pintar(new Rect(r.center.x - 3, r.y + 18, 6, 22), new Color(0.45f, 0.3f, 0.15f));
                Pintar(new Rect(r.center.x - 6, r.y + 7, 12, 12), new Color(1f, 0.65f, 0.15f));
            }
            else GUI.DrawTexture(new Rect(r.x + 6, r.y + 6, S - 12, S - 12), Tex(tipos[i].icono));
            Marco(r, i == tipoSel ? new Color(1, 0.85f, 0.2f) : new Color(1, 1, 1, 0.3f), i == tipoSel ? 3 : 1);
            Texto(new Rect(r.x + 3, r.y + 2, 20, 12), (i + 1).ToString(), 8, Color.white, TextAnchor.UpperLeft);
        }
    }

    void Mapa(float W, float H)
    {
        float tam = mapaGrande ? 420 : 170;
        var r = mapaGrande ? new Rect(W / 2 - tam / 2, H / 2 - tam / 2, tam, tam) : new Rect(W - tam - 16, 16, tam, tam);
        Pintar(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), new Color(0, 0, 0, 0.6f));
        if (rtMapa) GUI.DrawTexture(r, rtMapa);
        float alcance = camMapa.orthographicSize;
        var p = PosJugador;
        foreach (var cu in cubos) if (cu.obj) Punto(r, p, cu.obj.position, alcance, ColorCat(cu.m.cat), 7, true);
        foreach (var ca in casas) if (!ca.lista) Punto(r, p, ca.centro, alcance, new Color(1f, 0.85f, 0.15f), 9, true);
        foreach (var e in enemigos) if (e) Punto(r, p, e.transform.position, alcance, new Color(1, 0.2f, 0.2f), 6, false);
        // tú: cuadro amarillo con una rayita hacia donde miras
        var c = r.center;
        var f = jugador.transform.forward;
        for (int i = 1; i <= 3; i++) Pintar(new Rect(c.x + f.x * i * 4 - 2, c.y - f.z * i * 4 - 2, 4, 4), Color.yellow);
        Pintar(new Rect(c.x - 4, c.y - 4, 8, 8), Color.yellow);
        Marco(r, new Color(1, 0.85f, 0.2f), 2);
        Texto(new Rect(r.x, r.yMax + 4, r.width, 14), mapaGrande ? "M = cerrar el mapa" : "M = mapa grande", 8, Color.white, TextAnchor.MiddleCenter);
    }

    // un punto en el mapa; las gemas que quedan fuera se ven en el borde para orientarte
    void Punto(Rect r, Vector3 centro, Vector3 w, float alcance, Color c, float s, bool alBorde)
    {
        float u = (w.x - centro.x) / (2 * alcance) + 0.5f, v = 0.5f - (w.z - centro.z) / (2 * alcance);
        bool fuera = u < 0 || u > 1 || v < 0 || v > 1;
        if (fuera && !alBorde) return;
        u = Mathf.Clamp01(u); v = Mathf.Clamp01(v);
        Pintar(new Rect(r.x + u * r.width - s / 2, r.y + v * r.height - s / 2, s, s), fuera ? new Color(c.r, c.g, c.b, 0.55f) : c);
    }

    void Ayuda()
    {
        string[] l = {
            "WASD: caminar      ESPACIO: saltar / nadar",
            "MOUSE: mirar",
            "CLIC IZQ: romper bloque / encender TNT",
            "CLIC DER: poner bloque",
            "1-9 o RUEDA: elegir bloque",
            "F: lanzar bola de nieve",
            "N (mantener): adelantar el tiempo",
            "M: mapa grande     G: girar personaje",
            "ESC: soltar el mouse (para abrir videos)",
            "H: mostrar / ocultar esta ayuda" };
        var r = new Rect(16, 84, 420, 20 + l.Length * 18);
        Pintar(r, new Color(0, 0, 0, 0.55f));
        for (int i = 0; i < l.Length; i++)
            Texto(new Rect(r.x + 10, r.y + 10 + i * 18, r.width, 16), l[i], 9, Color.white, TextAnchor.UpperLeft, false);
    }

    void PantallaFin(float W, float H)
    {
        float a = Mathf.Clamp01(tFin / 0.5f);                  // la pantalla aparece
        Pintar(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.75f * a));
        if (a < 1) return;
        float y = H / 2 - 170;
        float letras = Mathf.Clamp01((tFin - 0.5f) / 0.8f);   // el título, letra por letra
        string titulo = tituloFin.Substring(0, Mathf.RoundToInt(tituloFin.Length * letras));
        var col = tituloFin == "GAME OVER" ? new Color(1, 0.25f, 0.2f) : new Color(1, 0.85f, 0.2f);
        int tamT = Mathf.Min(64, Mathf.FloorToInt(W * 0.9f / Mathf.Max(1, tituloFin.Length)));
        Texto(new Rect(0, y, W, 72), titulo, tamT, col, TextAnchor.MiddleCenter); y += 64 + 28;
        Texto(new Rect(0, y, W, 20), $"Memorias: {recogidos.Count} / {meta}     Casas: {CasasListas} / {casas.Count}     Puntos: {puntos}", 16, Color.white, TextAnchor.MiddleCenter); y += 16 + 28;
        if (Boton(new Rect(W / 2 - 220, y, 440, 72), "JUGAR OTRA VEZ")) Reiniciar(); y += 72 + 20;
        Texto(new Rect(0, y, W, 16), "(en un mundo nuevo)", 10, new Color(1, 1, 1, 0.7f), TextAnchor.MiddleCenter); y += 16 + 20;
        if (Boton(new Rect(W / 2 - 220, y, 440, 72), EnNavegador ? "MENU" : "SALIR")) Salir();
    }

    void PantallaInicio(float W, float H)
    {
        if (pantalla == Pantalla.Portada)
        {
            Pintar(new Rect(0, 0, W, H), new Color(0.04f, 0.06f, 0.12f, 0.85f));
            float y = H / 2 - 120;
            Texto(new Rect(0, y, W, 72), "CALICRAFT", 64, new Color(1, 0.85f, 0.2f), TextAnchor.MiddleCenter); y += 64 + 28;
            Texto(new Rect(0, y, W, 20), "Cali y el Valle - memoria y biopatrimonio", 16, Color.white, TextAnchor.MiddleCenter); y += 16 + 10;
            Texto(new Rect(0, y, W, 16), $"Mundo #{semilla}", 10, new Color(1, 1, 1, 0.6f), TextAnchor.MiddleCenter); y += 16 + 24;
            if (Boton(new Rect(W / 2 - 220, y, 440, 72), "JUGAR")) { pantalla = Pantalla.Mision; tMision = 0; }
        }
        else if (pantalla == Pantalla.Mision)
        {
            Pintar(new Rect(0, 0, W, H), new Color(0.04f, 0.06f, 0.12f, 0.85f));
            string historia = $"La memoria y el biopatrimonio de Cali y el Valle están repartidos en {meta} cubos de colores: azul naturaleza, dorado patrimonio, rosa cultura viva y rojo memoria y resistencia. Tócalos para descubrir su historia. Sigue los rayos de luz y el mapa.\n\n"
                + "Hay 5 casas que tumbó el terremoto: pon bloques en los huecos amarillos para reconstruirlas.\n\n"
                + "De noche llegan los malos pensamientos: te quitan la última memoria. Defiéndete con bolas de nieve. La lava quema.";
            int n = Mathf.RoundToInt(historia.Length * Mathf.Clamp01(tMision / 4f));   // letra por letra
            float y = H / 2 - 210;
            Texto(new Rect(0, y, W, 48), "LA MISION", 40, new Color(1, 0.85f, 0.2f), TextAnchor.MiddleCenter); y += 48 + 24;
            Texto(new Rect(W / 2 - 380, y, 760, 250), historia.Substring(0, n), 14, Color.white, TextAnchor.UpperLeft, true, true); y += 250 + 20;
            if (Boton(new Rect(W / 2 - 220, y, 440, 72), "SIGUIENTE")) { pantalla = Pantalla.Eleccion; VistaDeInicio(true); }
        }
        else if (pantalla == Pantalla.Eleccion)
        {
            float y = H - 30 - 272;
            Texto(new Rect(0, y, W, 30), "ELIGE TU PERSONAJE", 24, Color.white, TextAnchor.MiddleCenter); y += 24 + 16;
            float fx = W / 2 - (72 + 16 + 420 + 16 + 72) / 2f;
            if (Boton(new Rect(fx, y, 72, 72), "<", 24)) MostrarPersonaje(elegido - 1);
            Texto(new Rect(fx + 88, y, 420, 72), nombres[elegido], 24, new Color(1, 0.85f, 0.2f), TextAnchor.MiddleCenter);
            if (Boton(new Rect(fx + 88 + 420 + 16, y, 72, 72), ">", 24)) MostrarPersonaje(elegido + 1);
            y += 72 + 16;
            string[] dif = { "FACIL", "NORMAL", "DIFICIL" };
            float dx = W / 2 - (3 * 200 + 2 * 12) / 2f;
            for (int i = 0; i < 3; i++)
                if (Boton(new Rect(dx + i * 212, y, 200, 56), dif[i], 16, dificultad == i)) { dificultad = i; AplicarDificultad(); }
            y += 56 + 16;
            if (Boton(new Rect(W / 2 - 220, y, 440, 72), "A JUGAR")) Empezar();
        }
    }

    // ---------- piezas de dibujo ----------
    // la mira (+): dos barras amarillas con borde negro
    void Mira(float x, float y)
    {
        const float L = 14, G = 3, BO = 2;   // largo, grueso y borde
        Pintar(new Rect(x - L - BO, y - G / 2 - BO, 2 * (L + BO), G + 2 * BO), Color.black);
        Pintar(new Rect(x - G / 2 - BO, y - L - BO, G + 2 * BO, 2 * (L + BO)), Color.black);
        Pintar(new Rect(x - L, y - G / 2, 2 * L, G), Color.yellow);
        Pintar(new Rect(x - G / 2, y - L, G, 2 * L), Color.yellow);
    }

    void Pintar(Rect r, Color c)
    {
        var antes = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = antes;
    }

    void Marco(Rect r, Color c, float g)
    {
        Pintar(new Rect(r.x, r.y, r.width, g), c);
        Pintar(new Rect(r.x, r.yMax - g, r.width, g), c);
        Pintar(new Rect(r.x, r.y, g, r.height), c);
        Pintar(new Rect(r.xMax - g, r.y, g, r.height), c);
    }

    void Texto(Rect r, string s, int tam, Color col, TextAnchor anc, bool borde = true, bool wrap = false)
    {
        var st = new GUIStyle(GUI.skin.label) { font = fuente, fontSize = tam, alignment = anc, wordWrap = wrap };
        if (borde)
        {
            st.normal.textColor = new Color(0, 0, 0, col.a);
            float o = Mathf.Max(1, tam / 10f);
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if (dx != 0 || dy != 0) GUI.Label(new Rect(r.x + dx * o, r.y + dy * o, r.width, r.height), s, st);
        }
        st.normal.textColor = col;
        GUI.Label(r, s, st);
    }

    bool Boton(Rect r, string s, int tam = 20, bool marcado = false)
    {
        bool encima = r.Contains(Event.current.mousePosition);
        Pintar(r, encima ? new Color(0.22f, 0.22f, 0.3f, 0.95f) : new Color(0.12f, 0.12f, 0.17f, 0.95f));
        Marco(r, marcado ? new Color(1, 0.85f, 0.2f) : new Color(1, 1, 1, encima ? 0.85f : 0.35f), marcado ? 4 : 2);
        Texto(r, s, tam, marcado ? new Color(1, 0.85f, 0.2f) : Color.white, TextAnchor.MiddleCenter, false);
        if (GUI.Button(r, GUIContent.none, GUIStyle.none)) { Sonar("clic"); return true; }
        return false;
    }
}

using System.Collections.Generic;
using UnityEngine;

// Los monumentos de Calicraft, hechos con bloques dentro del terreno (portado del juego VR).
// El centro histórico de Cali, el río con el Gato de Tejada, los cerros con Cristo Rey y
// las Tres Cruces, el biopatrimonio del Pacífico (alabaos, parteras, macetas, oso, ballena,
// pescador, aves) y la memoria y resistencia (La Gaitana y Benkos Biohó).
public partial class Mundo
{
    // ---------- pincel: dibuja en coordenadas propias (+z es el frente); r = cuartos de vuelta ----------
    struct Pincel
    {
        public Terreno t; public int ox, oy, oz, r;
        public void P(int x, int y, int z, B b)
        {
            int a, c;
            switch (r)
            {
                case 1: a = z; c = -x; break;
                case 2: a = -x; c = -z; break;
                case 3: a = -z; c = x; break;
                default: a = x; c = z; break;
            }
            t.Set(ox + a, oy + y, oz + c, b);
        }
        public void Caja(int x0, int x1, int y0, int y1, int z0, int z1, B b)
        {
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                    for (int z = z0; z <= z1; z++) P(x, y, z, b);
        }
    }
    Pincel Pin(int ox, int oy, int oz, int r = 0) => new Pincel { t = terreno, ox = ox, oy = oy, oz = oz, r = r };

    const B AIRE = B.Aire;

    // ---------- arma todos los monumentos ----------
    void ConstruirMonumentos()
    {
        Cali();
        CentroDeCali();
        BiopatrimonioMon();
        Gaitana(-40, -115);
        Benkos(60, 60);
        // estaciones de los micrositios transmedia de los estudiantes
        VocesDelRio(78, 33);
        BarrioObrero(-78, 30);
    }

    // ================= estaciones de los micrositios de los estudiantes =================
    // Voces del río (Guardianas de oro): un río con las barequeras de Guachené sacando oro a mano
    void VocesDelRio(int cx, int cz)
    {
        int Bz = terreno.Aplanar(cx - 12, cx + 12, cz - 10, cz + 10);
        var p = Pin(cx, Bz, cz, 0);
        p.Caja(-11, 11, -1, -1, -9, 9, B.Arena);                 // la playa de arena (el barequeo)
        terreno.Laguna(cx, cz + 4, 7);                            // el río (agua real)
        foreach (var xz in new[] { (-7, -3), (-3, -5), (2, -4), (6, -2), (-5, 1), (4, 1), (8, -5), (-9, -2) })
            p.P(xz.Item1, 0, xz.Item2, B.Amarillo);              // pepitas de oro en la arena
        // tres barequeras junto al agua, cada una con su batea con oro
        var sitios = new[] { (-6, -3, (B)B.Rojo, (B)B.Amarillo), (0, -4, B.Azul, B.Rosa), (6, -3, B.Crema, B.Rojo) };
        foreach (var s in sitios)
        {
            Figura(cx + s.Item1, Bz, cz + s.Item2, s.Item3, s.Item4, false, 2);
            var q = Pin(cx + s.Item1, Bz, cz + s.Item2 - 1, 0);
            q.Caja(-1, 1, 0, 0, 0, 0, B.Bronce); q.P(0, 0, 0, B.Amarillo);   // la batea
        }
        Rotulo("Voces del río: Guardianas de oro", cx, Bz + 12, cz);
    }

    // Barrio Obrero: casas del barrio, una tarima de salsa y su gente
    void BarrioObrero(int cx, int cz)
    {
        int Bz = terreno.Aplanar(cx - 13, cx + 13, cz - 11, cz + 11);
        Pin(cx, Bz, cz, 0).Caja(-12, 12, -1, -1, -10, 10, B.Piedra);   // la calle empedrada
        CasaBarrio(cx - 8, cz - 4, Bz);
        CasaBarrio(cx + 7, cz - 4, Bz);
        CasaBarrio(cx - 1, cz + 6, Bz);
        // tarima de salsa en la esquina (tablas, parlantes y luces de colores)
        var t = Pin(cx + 8, Bz, cz + 6, 0);
        t.Caja(-2, 2, 0, 0, -2, 2, B.Tablas);
        t.Caja(-2, -2, 1, 3, -2, 2, B.Negro); t.Caja(2, 2, 1, 3, -2, 2, B.Negro); t.Caja(-2, 2, 4, 4, -2, 2, B.Negro);
        t.P(-1, 3, 2, B.Rosa); t.P(0, 3, 2, B.Amarillo); t.P(1, 3, 2, B.Azul);
        // gente del barrio
        Figura(cx - 3, Bz, cz, B.Rojo, B.Negro, true, 0);
        Figura(cx + 2, Bz, cz - 1, B.Azul, B.Crema, false, 0);
        Figura(cx + 4, Bz, cz + 3, B.Amarillo, B.Rojo, true, 2);
        Rotulo("Memorias Vivas del Barrio Obrero", cx, Bz + 12, cz);
    }

    void CasaBarrio(int cx, int cz, int Bz)
    {
        var p = Pin(cx, Bz, cz, 0);
        p.Caja(-2, 2, 0, 3, -2, 2, B.Ladrillo);     // paredes
        p.Caja(-1, 1, 0, 2, -1, 1, B.Aire);         // interior hueco
        p.Caja(-3, 3, 4, 4, -3, 3, B.Tablas);       // techo saliente
        p.Caja(0, 0, 0, 1, -2, -2, B.Aire);         // puerta
        p.P(2, 2, 0, B.Ventanal); p.P(-2, 2, 0, B.Ventanal);   // ventanas
    }

    // ================= el centro histórico =================
    void Palmera(Pincel p)
    {
        p.Caja(0, 0, 0, 8, 0, 0, B.Tronco); p.P(0, 9, 0, B.Hojas);
        foreach (var d in new[] { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, 1), (1, -1), (-1, -1) })
        { p.P(d.Item1, 9, d.Item2, B.Hojas); p.P(2 * d.Item1, 9, 2 * d.Item2, B.Hojas); p.P(3 * d.Item1, 8, 3 * d.Item2, B.Hojas); }
    }
    void Estatua(Pincel p)   // Joaquín de Caicedo y Cuero, en bronce
    {
        p.Caja(-2, 2, 0, 2, -2, 2, B.Piedra);
        p.Caja(-1, -1, 3, 4, -1, 0, B.Bronce); p.Caja(1, 1, 3, 4, -1, 0, B.Bronce);
        p.Caja(-1, 1, 5, 7, -1, 0, B.Bronce); p.Caja(-2, -2, 5, 7, -1, 0, B.Bronce); p.Caja(2, 2, 6, 9, -1, 0, B.Bronce);
        p.Caja(0, 0, 8, 9, -1, 0, B.Bronce);
    }
    void Teatro(Pincel p)    // el Teatro Municipal: fachada neoclásica con columnas y frontón
    {
        B C = B.Crema, M = B.Marmol;
        p.Caja(-8, 8, 0, 9, -14, 0, C); p.Caja(-7, 7, 1, 8, -13, -1, AIRE);
        p.Caja(-8, 8, 0, 0, 1, 4, M);
        foreach (var x in new[] { -7, -4, 4, 7 }) p.Caja(x, x, 1, 8, 4, 4, M);
        p.Caja(-8, 8, 9, 9, 1, 4, M); p.Caja(-8, 8, 10, 10, -14, 4, M);
        for (int n = 0; n <= 4; n++) p.Caja(-8 + 2 * n, 8 - 2 * n, 11 + n, 11 + n, 3, 4, C);
        p.Caja(-1, 1, 1, 5, 0, 0, AIRE);
        foreach (var x in new[] { -6, -3, 3, 6 }) p.Caja(x, x, 5, 7, 0, 0, B.Ventanal);
    }
    void TorreDeCali(Pincel p, int alto)
    {
        p.Caja(-3, 3, 0, alto, -3, 3, B.Ventanal);
        for (int y = 4; y < alto; y += 4) p.Caja(-3, 3, y, y, -3, 3, B.Marmol);
        p.Caja(-3, 3, 0, 3, -3, 3, B.Marmol); p.Caja(-1, 1, 0, 2, 3, 3, AIRE);
        p.Caja(-2, 2, alto + 1, alto + 2, -2, 2, B.Marmol); p.Caja(0, 0, alto + 3, alto + 8, 0, 0, B.Negro);
    }
    void Diablo(Pincel p)    // el Diablo del América de Cali, con su tridente
    {
        B R = B.Rojo, N = B.Negro, Y = B.Amarillo;
        p.Caja(-3, 3, 0, 1, -2, 2, B.Piedra);
        p.Caja(-2, -1, 2, 5, -1, 0, R); p.Caja(1, 2, 2, 5, -1, 0, R);
        p.Caja(-2, 2, 6, 10, -1, 0, R); p.Caja(-3, -3, 7, 10, -1, 0, R); p.Caja(3, 3, 7, 10, -1, 0, R);
        p.Caja(-2, 2, 11, 14, -1, 0, R);
        p.P(-1, 13, 0, Y); p.P(1, 13, 0, Y); p.Caja(-1, 1, 11, 11, 0, 0, N); p.P(-2, 12, 0, N); p.P(2, 12, 0, N);
        foreach (var x in new[] { -2, 2 }) { p.P(x, 15, -1, R); p.P(x + (int)Mathf.Sign(x), 16, -1, R); p.P(x + (int)Mathf.Sign(x), 17, -1, N); }
        foreach (var yz in new[] { (6, -2), (5, -3), (4, -4), (4, -5), (5, -6) }) p.P(0, yz.Item1, yz.Item2, R);
        p.Caja(-1, 1, 6, 6, -6, -6, R); p.P(0, 7, -6, R);
        p.Caja(4, 4, 3, 17, 0, 0, Y); p.Caja(3, 5, 15, 15, 0, 0, Y); p.P(3, 16, 0, Y); p.P(5, 16, 0, Y); p.P(3, 17, 0, Y); p.P(5, 17, 0, Y);
    }
    void Tarima(Pincel p)    // la tarima del Petronio: marimba, cununos, luces y pista de baile
    {
        p.Caja(-7, 7, 0, 1, -5, 1, B.Tablas);
        p.Caja(-7, 7, 2, 9, -5, -5, B.Negro); p.Caja(-7, -7, 2, 9, 1, 1, B.Negro); p.Caja(7, 7, 2, 9, 1, 1, B.Negro);
        p.Caja(-7, 7, 9, 9, -5, 1, B.Negro);
        B[] luces = { B.Rosa, B.Amarillo, B.Azul, B.Rojo };
        for (int x = -6; x <= 6; x += 2) p.P(x, 8, 1, luces[((x + 6) / 2) % 4]);
        p.Caja(-6, -5, 2, 4, 0, 1, B.Negro); p.Caja(5, 6, 2, 4, 0, 1, B.Negro);
        p.P(-3, 2, -3, B.Tronco); p.P(3, 2, -3, B.Tronco); p.Caja(-3, 3, 3, 3, -3, -2, B.Tablas);
        foreach (var x in new[] { -2, 2 }) { p.Caja(x, x, 2, 3, 0, 0, B.Tronco); p.P(x, 4, 0, B.Crema); }
        for (int x = -7; x <= 7; x++) for (int z = 2; z <= 9; z++) p.P(x, -1, z, luces[(x + 7 + z) % 4]);
    }
    void CentroDeCali()
    {
        int Bz = terreno.Aplanar(-22, 40, -112, -46);
        var plaza = Pin(10, Bz, -70, 0);
        plaza.Caja(-10, 10, -1, -1, -10, 10, B.Piedra);
        foreach (var xz in new[] { (-9, -9), (9, -9), (-9, 9), (9, 9), (0, -9), (0, 9), (-9, 0), (9, 0) })
            Palmera(Pin(10 + xz.Item1, Bz, -70 + xz.Item2, 0));
        Estatua(Pin(10, Bz, -70, 2));
        Teatro(Pin(10, Bz, -88, 0));
        int alto = Mathf.Min(38, Terreno.Y_MAX - 3 - Bz);
        TorreDeCali(Pin(33, Bz, -100, 0), alto);
        Diablo(Pin(-8, Bz, -70, 1));
        Tarima(Pin(-6, Bz, -100, 0));
        Rotulo("Plaza de Caicedo", 10, Bz + 13, -70);
        Rotulo("Teatro Municipal", 10, Bz + 19, -91);
        Rotulo("Torre de Cali", 33, Bz + alto + 12, -100);
        Rotulo("El Diablo del América", -11, Bz + 21, -71);
        Rotulo("Tarima del Petronio y la salsa", -6, Bz + 13, -100);
    }

    // ================= el río Cali, el Gato de Tejada, La Ermita y los cerros =================
    void GatoDelRio(Pincel p)    // El Gato del Río, de Hernando Tejada
    {
        B Bz = B.Bronce, N = B.Negro;
        p.Caja(-3, 3, 0, 0, -3, 4, B.Piedra);
        p.Caja(-2, 2, 1, 3, -2, 0, Bz); p.Caja(-2, 2, 4, 4, -1, 0, Bz);
        p.Caja(-1, 1, 1, 3, 1, 1, Bz);
        p.Caja(-1, -1, 1, 4, 2, 2, Bz); p.Caja(1, 1, 1, 4, 2, 2, Bz);
        p.Caja(-1, 1, 4, 8, -1, 1, Bz);
        p.Caja(-2, 2, 9, 10, -1, 2, Bz); p.Caja(-1, 1, 11, 11, -1, 2, Bz); p.Caja(-1, 1, 9, 9, 3, 3, Bz);
        foreach (var x in new[] { -2, 2 }) { p.Caja(x, x, 11, 12, 0, 1, Bz); p.P(x, 13, 0, Bz); }
        p.P(-1, 10, 2, B.Amarillo); p.P(1, 10, 2, B.Amarillo); p.P(0, 9, 3, N);
        foreach (var xz in new[] { (3, -2), (4, -1), (4, 0), (4, 1), (4, 2), (3, 3), (2, 3) }) p.P(xz.Item1, 1, xz.Item2, Bz);
    }
    void Gata(Pincel p, B color)
    {
        p.Caja(-1, 2, 0, 0, -1, 2, B.Piedra);
        p.Caja(0, 1, 1, 2, 0, 1, color); p.Caja(0, 1, 3, 4, 0, 1, color);
        p.P(0, 5, 0, color); p.P(1, 5, 0, color); p.P(0, 4, 1, B.Negro); p.P(1, 4, 1, B.Negro);
        p.P(2, 1, 0, color); p.P(2, 2, 0, color);
    }
    void Ermita(Pincel p)    // La Ermita, la iglesia gótica blanca a la orilla del río
    {
        B M = B.Marmol, G = B.Piedra, A = B.Azul;
        p.Caja(-4, 4, 0, 7, -17, 0, M); p.Caja(-3, 3, 0, 6, -16, -1, AIRE);
        for (int n = 0; n <= 4; n++) p.Caja(-4 + n, 4 - n, 8 + n, 8 + n, -17, 0, G);
        for (int z = -15; z <= -3; z += 3) foreach (var x in new[] { -4, 4 }) p.Caja(x, x, 2, 5, z, z, A);
        p.Caja(-2, 2, 0, 14, 0, 4, M); p.Caja(-1, 1, 0, 3, 0, 4, AIRE); p.P(0, 4, 4, AIRE);
        p.Caja(-1, 1, 8, 10, 4, 4, A); p.P(0, 11, 4, A);
        p.Caja(-2, 2, 15, 15, 0, 4, G); p.Caja(-1, 1, 16, 18, 1, 3, G); p.Caja(0, 0, 19, 24, 2, 2, G);
        p.Caja(0, 0, 25, 27, 2, 2, M); p.Caja(-1, 1, 26, 26, 2, 2, M);
        foreach (var x in new[] { -4, 4 }) { p.Caja(x, x, 0, 11, 1, 1, M); p.Caja(x, x, 12, 14, 1, 1, G); }
    }
    void CristoRey(Pincel p)
    {
        B M = B.Marmol;
        p.Caja(-2, 2, 0, 2, -2, 2, B.Piedra);
        p.Caja(-2, 2, 3, 6, -1, 1, M); p.Caja(-1, 1, 7, 12, -1, 0, M);
        p.Caja(-5, 5, 12, 12, -1, 0, M); p.Caja(-7, -6, 11, 11, -1, 0, M); p.Caja(6, 7, 11, 11, -1, 0, M);
        p.P(0, 13, 0, M); p.P(0, 13, -1, M);
        p.Caja(-1, 1, 14, 15, -1, 0, M); p.P(0, 16, 0, M); p.P(0, 16, -1, M);
    }
    void TresCruces(Pincel p)
    {
        foreach (var x in new[] { -7, 0, 7 }) { int h = x != 0 ? 9 : 12; p.Caja(x, x, 0, h, 0, 0, B.Marmol); p.Caja(x - 2, x + 2, h - 3, h - 3, 0, 0, B.Marmol); }
    }
    void Cali()
    {
        int b = terreno.Aplanar(42, 92, -52, 30);
        for (int i = 56; i <= 64; i++)
            for (int k = -52; k <= 30; k++)
            {
                if (i >= 58 && i <= 62) { terreno.Set(i, b - 2, k, B.Arena); terreno.Set(i, b - 1, k, B.Azul); }
                else terreno.Set(i, b - 1, k, B.Piedra);
            }
        for (int i = 57; i <= 63; i++) { terreno.Set(i, b - 1, -33, B.Ladrillo); terreno.Set(i, b, -34, B.Ladrillo); terreno.Set(i, b, -32, B.Ladrillo); }
        GatoDelRio(Pin(50, b, -5, 3));
        Gata(Pin(52, b, 8, 1), B.Rosa); Gata(Pin(52, b, 20, 1), B.Amarillo); Gata(Pin(67, b, 5, 3), B.Azul); Gata(Pin(67, b, 18, 3), B.Rosa);
        Ermita(Pin(74, b, -20, 3));
        Rotulo("El Gato del Río (Gato de Tejada)", 50, b + 17, -5);
        Rotulo("La Ermita", 80, b + 31, -20);
        int c1 = terreno.Cerro(-66, 8, 26, 14); CristoRey(Pin(-66, c1, 8, 1));
        Rotulo("Cristo Rey", -66, c1 + 21, 8);
        int c2 = terreno.Cerro(-48, -60, 20, 11); TresCruces(Pin(-48, c2, -60, 1));
        Rotulo("Las Tres Cruces", -48, c2 + 16, -60);
    }

    // ================= el biopatrimonio del Pacífico =================
    // figura humana (8 de alto), piel morena (afropacífica); r = hacia dónde mira
    void Figura(int ax, int Bz, int az, B vestido, B turbante, bool canta, int r = 0)
    {
        var p = Pin(ax, Bz, az, r); B P = B.Bronce;
        p.Caja(-1, -1, 0, 1, 0, 0, P); p.Caja(1, 1, 0, 1, 0, 0, P);
        p.Caja(-1, 1, 2, 4, 0, 0, vestido);
        p.Caja(-1, 1, 5, 5, 0, 0, P);
        p.Caja(-1, 1, 6, 7, 0, 0, turbante);
        if (canta) foreach (var x in new[] { -2, 2 }) { p.P(x, 4, 0, vestido); p.P(x, 5, 0, vestido); p.P(x, 6, 0, P); }
        else foreach (var x in new[] { -2, 2 }) { p.P(x, 4, 0, vestido); p.P(x, 3, 0, vestido); p.P(x, 2, 0, P); }
    }
    void Alabaos(int cx, int cz, int Bz)   // el gualí: altar con el angelito, velas y cantadoras en ronda
    {
        var p = Pin(cx, Bz, cz, 0);
        p.Caja(-11, 11, -1, -1, -11, 11, B.Tablas);
        p.Caja(-2, 2, 0, 2, -2, 2, B.Marmol); p.Caja(-1, 1, 3, 4, -1, 1, B.Crema);
        foreach (var xz in new[] { (-3, -3), (3, -3), (-3, 3), (3, 3) }) { p.Caja(xz.Item1, xz.Item1, 0, 2, xz.Item2, xz.Item2, B.Tronco); p.P(xz.Item1, 3, xz.Item2, B.Amarillo); }
        foreach (var xz in new[] { (-6, 0), (6, 0), (0, 6), (0, -6), (-5, -5), (5, 5), (-5, 5), (5, -5) }) p.P(xz.Item1, 0, xz.Item2, B.Rosa);
        B[] turb = { B.Rosa, B.Amarillo, B.Azul, B.Rojo, B.Crema, B.Amarillo, B.Rosa, B.Azul };
        var sitios = new[] { (-9, -4), (-5, -8), (0, -9), (5, -8), (9, -4), (-9, 4), (9, 4), (0, 9) };
        for (int i = 0; i < sitios.Length; i++)
        {
            int x = sitios[i].Item1, z = sitios[i].Item2;
            int r = Mathf.Abs(x) > Mathf.Abs(z) ? (x < 0 ? 1 : 3) : (z < 0 ? 0 : 2);
            Figura(cx + x, Bz, cz + z, B.Marmol, turb[i], true, r);
        }
    }
    void Parteras(int cx, int cz, int Bz)
    {
        var p = Pin(cx, Bz, cz, 0);
        p.Caja(-9, 10, -1, -1, -8, 8, B.Pasto);
        Figura(cx, Bz, cz, B.Azul, B.Amarillo, false);
        p.Caja(-1, 1, 3, 3, 1, 1, B.Marmol); p.P(0, 4, 1, B.Bronce);
        var bu = Pin(cx + 6, Bz, cz + 1, 0); bu.Caja(-1, 1, 0, 1, -1, 0, B.Tronco);
        Figura(cx + 6, Bz + 2, cz + 1, B.Rosa, B.Rosa, false, 3);
        foreach (var xz in new[] { (-6, -5), (-7, 2), (-3, 6), (6, -6), (9, 6), (3, 7) })
        { var q = Pin(cx + xz.Item1, Bz, cz + xz.Item2, 0); q.P(0, 0, 0, B.Tronco); q.P(0, 1, 0, B.Tronco); q.P(0, 2, 0, B.Hojas); q.P(-1, 2, 0, B.Hojas); q.P(1, 2, 0, B.Hojas); q.P(0, 3, 0, B.Hojas); }
    }
    void Maceta(Pincel p, int A)   // palo de maguey, figuras de alfeñique, cintas y banderita
    {
        B BL = B.Marmol, PM = B.Crema;
        p.Caja(-1, 1, 0, 0, -1, 1, B.Tronco); p.Caja(0, 0, 1, A, 0, 0, PM);
        var pisos = new (int, (int, int)[])[] {
            (3, new[]{(2,0),(-2,0),(0,2),(0,-2)}),
            (5, new[]{(2,2),(-2,-2),(2,-2),(-2,2)}),
            (7, new[]{(1,0),(-1,0),(0,1),(0,-1)}) };
        B[] cinta = { B.Rosa, B.Azul, B.Amarillo, B.Rojo };
        foreach (var piso in pisos)
            if (piso.Item1 < A)
            {
                var ramas = piso.Item2;
                for (int i = 0; i < ramas.Length; i++)
                { int dx = ramas[i].Item1, dz = ramas[i].Item2, y = piso.Item1; p.P((int)Mathf.Sign(dx), y, (int)Mathf.Sign(dz), PM); p.P(dx, y, dz, BL); p.P(dx, y + 1, dz, BL); p.P(dx, y - 1, dz, cinta[i % 4]); }
            }
        p.P(0, A + 1, 0, B.Amarillo); p.P(-1, A + 1, 0, B.Rosa); p.P(1, A + 1, 0, B.Azul); p.P(0, A + 2, 0, B.Rojo); p.P(0, A, 1, B.Rosa);
        p.P(1, A - 1, 0, PM); p.P(2, A - 1, 0, B.Amarillo); p.P(2, A - 2, 0, B.Azul); p.P(2, A - 3, 0, B.Rojo);
    }
    void Macetas(int cx, int cz, int Bz)
    {
        var p = Pin(cx, Bz, cz, 0);
        p.Caja(-8, 8, -1, -1, -3, 3, B.Piedra); p.Caja(-8, 8, 0, 0, -2, 2, B.Tablas);
        Maceta(Pin(cx, Bz + 1, cz, 0), 10); Maceta(Pin(cx - 5, Bz + 1, cz, 0), 7); Maceta(Pin(cx + 5, Bz + 1, cz, 0), 7);
    }
    void OsoAnteojos(Pincel p)
    {
        B N = B.Negro, C = B.Crema;
        foreach (var xz in new[] { (-2, -4), (1, -4), (-2, 1), (1, 1) }) p.Caja(xz.Item1, xz.Item1 + 1, 0, 1, xz.Item2, xz.Item2 + 1, N);
        p.Caja(-2, 2, 2, 5, -4, 2, N); p.Caja(-1, 1, 2, 3, 2, 2, C); p.P(0, 4, -5, N);
        p.Caja(-2, 2, 3, 6, 3, 5, N);
        p.P(-2, 5, 5, C); p.P(0, 5, 5, C); p.P(2, 5, 5, C); p.P(-2, 4, 5, C); p.P(2, 4, 5, C); p.P(-1, 5, 5, N); p.P(1, 5, 5, N);
        p.Caja(-1, 1, 3, 4, 6, 6, C); p.P(0, 4, 7, N); p.P(-2, 7, 4, N); p.P(2, 7, 4, N);
    }
    void Ballena(Pincel p)
    {
        B N = B.Negro, Bl = B.Marmol;
        p.Caja(-1, 1, 0, 2, -5, 4, N); p.Caja(0, 0, 3, 3, -3, 2, N); p.Caja(-1, 1, 1, 2, 5, 6, N);
        p.Caja(-4, -2, 1, 1, 1, 2, N); p.Caja(2, 4, 1, 1, 1, 2, N); p.P(-4, 1, 1, Bl); p.P(4, 1, 1, Bl);
        p.Caja(0, 0, 1, 3, -7, -6, N); p.Caja(-3, 3, 4, 4, -8, -8, N); p.P(-3, 4, -8, Bl); p.P(3, 4, -8, Bl);
        p.P(0, 4, 5, Bl); p.P(0, 5, 5, B.Azul); p.P(0, 6, 5, Bl); p.P(-1, 6, 5, Bl); p.P(1, 6, 5, Bl);
    }
    void Pescador(int cx, int y, int cz)   // el pescador en su potrillo
    {
        var p = Pin(cx, y, cz, 0);
        p.Caja(-3, 3, 0, 0, -1, 1, B.Tronco); p.Caja(-2, 2, 0, 0, 0, 0, B.Tablas); p.P(-4, 1, 0, B.Tronco); p.P(4, 1, 0, B.Tronco);
        Figura(cx, y + 1, cz, B.Amarillo, B.Rojo, false);
        p.P(3, 4, 0, B.Tronco); p.P(4, 5, 0, B.Tronco); p.P(5, 6, 0, B.Tronco); for (int q = 0; q <= 6; q++) p.P(6, q, 0, B.Marmol);
    }
    void BiopatrimonioMon()
    {
        int Bz = terreno.Aplanar(-24, 34, 14, 42);
        Pin(6, Bz, 28, 0).Caja(-24, 24, -1, -1, -13, 13, B.Piedra);
        Alabaos(-9, 26, Bz); Parteras(20, 30, Bz);
        Rotulo("Los alabaos", -9, Bz + 12, 26); Rotulo("Las parteras", 20, Bz + 12, 30);
        int Bm = terreno.Aplanar(2, 18, -62, -48); Macetas(10, -55, Bm);
        Rotulo("Macetas de alfeñique", 10, Bm + 16, -55);
        int cf = terreno.Cerro(-80, -96, 18, 15); OsoAnteojos(Pin(-80, cf, -96, 0));
        Rotulo("Oso de anteojos", -80, cf + 11, -96);
        // la laguna del Pacífico: agua real (malla), con la ballena y el pescador
        terreno.Laguna(-42, 60, 16);
        Ballena(Pin(-48, -2, 62, 1)); Pescador(-42, 0, 74);
        Rotulo("Ballena jorobada", -48, 9, 62); Rotulo("El pescador", -42, 10, 74);
    }

    // ================= memoria y resistencia (investigación de Mateo Barney Betancourth) =================
    void Gaitana(int cx, int cz)
    {
        int Bz = terreno.Aplanar(cx - 7, cx + 7, cz - 7, cz + 7);
        var p = Pin(cx, Bz, cz, 0);
        p.Caja(-3, 3, 0, 1, -3, 3, B.Piedra);
        Figura(cx, Bz + 2, cz, B.Crema, B.Rojo, false);
        var q = Pin(cx, Bz + 2, cz, 0);
        q.P(-1, 8, 0, B.Amarillo); q.P(0, 8, 0, B.Rojo); q.P(1, 8, 0, B.Azul); q.P(0, 9, 0, B.Amarillo); q.P(-2, 9, 0, B.Rojo); q.P(2, 9, 0, B.Azul);
        q.Caja(3, 3, 0, 9, 0, 0, B.Tronco); q.P(3, 10, 0, B.Piedra); q.P(3, 11, 0, B.Piedra);
        Rotulo("La Gaitana", cx, Bz + 15, cz);
    }
    void Benkos(int cx, int cz)
    {
        int Bz = terreno.Aplanar(cx - 9, cx + 9, cz - 9, cz + 9);
        var p = Pin(cx, Bz, cz, 0);
        p.Caja(-3, 3, 0, 1, -3, 3, B.Piedra);
        Figura(cx, Bz + 2, cz, B.Crema, B.Amarillo, true);
        var q = Pin(cx, Bz + 2, cz, 0);
        q.P(-1, 8, 0, B.Amarillo); q.P(1, 8, 0, B.Amarillo);
        foreach (var x in new[] { -3, 3 }) { q.P(x, 6, 0, B.Negro); q.P(x, 5, 0, B.Negro); } q.P(-4, 4, 0, B.Negro); q.P(4, 4, 0, B.Negro);
        for (int n = 0; n < 9; n++)   // el arcabuco: el monte espeso donde fundó los palenques
        {
            float a = n / 9f * Mathf.PI * 2; int i = Mathf.RoundToInt(cx + Mathf.Cos(a) * 7), k = Mathf.RoundToInt(cz + Mathf.Sin(a) * 7), h = 4 + (n % 3);
            for (int j = 0; j < h; j++) terreno.Set(i, Bz + j, k, B.Tronco);
            for (int di = -2; di <= 2; di++) for (int dk = -2; dk <= 2; dk++) for (int dj = 0; dj < 2; dj++)
                        if (Mathf.Abs(di) + Mathf.Abs(dk) < 4 && terreno.Get(i + di, Bz + h + dj, k + dk) == B.Aire) terreno.Set(i + di, Bz + h + dj, k + dk, B.Hojas);
        }
        Rotulo("Benkos Biohó", cx, Bz + 15, cz);
    }

    // ---------- rótulo flotante sobre cada monumento ----------
    void Rotulo(string texto, float x, float y, float z)
    {
        var go = new GameObject("Rotulo");
        go.transform.SetParent(raiz, false);
        go.transform.position = new Vector3(x, y, z);
        var tm = go.AddComponent<TextMesh>();
        tm.text = texto; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center;
        tm.fontSize = 48; tm.characterSize = 0.18f; tm.color = Color.white;
        var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (f != null) { tm.font = f; go.GetComponent<MeshRenderer>().sharedMaterial = f.material; }
        go.AddComponent<Cartel>();
    }
}

// el rótulo siempre mira a la cámara
public class Cartel : MonoBehaviour
{
    void LateUpdate()
    {
        var c = Camera.main;
        if (c) transform.rotation = Quaternion.LookRotation(transform.position - c.transform.position);
    }
}

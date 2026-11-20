#!/usr/bin/env python3
"""Pasa las creaciones de los estudiantes de la web Calicraft (colorea-ar/web-rv/creaciones.js) a la versión VIROO.

Genera:
  Assets/Calicraft/Scripts/Mundo.Creaciones.Datos.cs   (los datos, como código C#: sin leer JSON en la sala)
  Assets/Calicraft/Resources/creaciones/<id>.glb        (los modelos 3D; Unity los importa con glTFast al compilar,
                                                         así sus materiales y shaders entran en el paquete)
  Assets/StreamingAssets/bloques/vaca.png               (la textura de la vaca de Mayer)
y agrega a Mundo.Memorias.Datos.cs una memoria (cubo verde) por creación, entre las marcas CREACIONES.
El mundo de Unity va de -96 a 95: las creaciones que en la web están en el «barrio» (x 100-198) se reubican aquí (LUGAR).
"""
import json, math, pathlib, shutil, re
P = pathlib.Path(__file__).resolve().parent.parent
WEB = pathlib.Path.home() / "Proyectos/Juegos/colorea-ar/web-rv"
s = (WEB / "creaciones.js").read_text(encoding="utf-8")
CREA = json.JSONDecoder().raw_decode(s[s.index("= [") + 2:])[0]

# lugar en el mapa de Unity (x, z): las que existen en Cali quedan igual que en la web; las inventadas, en zonas libres
LUGAR = {           # PROVISIONAL: revisar con el mapa aéreo de la prueba (00_mapa_aereo.png)
    "laberinto": (24, 74), "torre": (-12, 84), "arbol_dorado": (82, 80), "muro_berlin": (78, -80),
    "escaleras": (88, -64), "mina_oro": (56, -76), "vaca": (40, -84), "empanadas": (46, 86), "carro": (62, 86),
    "cununo": (-19, -91),   # en la web va en z -95; el mapa de Unity termina en -96
}

def f(v): return f"{float(v):g}f"
def cs(t): return '"' + t.replace("\\", "\\\\").replace('"', '\\"') + '"'
def cajas_cs(cajas):
    return "new PiezaCaja[] { " + ", ".join(
        f"new PiezaCaja({f(x)}, {f(y)}, {f(z)}, {f(sx)}, {f(sy)}, {f(sz)}, {cs(m)}, {'true' if r else 'false'})"
        for x, y, z, sx, sy, sz, m, r in cajas) + " }"
def extension(cajas):
    u = [c for c in cajas if not c[7]]
    return max(abs(c[0]) + c[3] / 2 for c in u), max(c[1] + c[4] / 2 for c in u), max(abs(c[2]) + c[5] / 2 for c in u)

lineas, memorias, piezas = [], [], {}
destino_glb = P / "Assets/Calicraft/Resources/creaciones"; destino_glb.mkdir(parents=True, exist_ok=True)
for c in CREA:
    x, z = LUGAR.get(c["id"], c["lugar"])
    if not (-94 <= x <= 94 and -94 <= z <= 94):      # el mapa de Unity va de -96 a 95
        print("FUERA DEL MAPA (falta LUGAR):", c["id"], (x, z)); continue
    modo = {"bloques": "Bloques", "figura": "Figura", "modelo": "Modelo"}[c["modo"]]
    campos = [f"id = {cs(c['id'])}", f"titulo = {cs(c['titulo'])}", f"autor = {cs(c['autor'])}", f"modo = ModoCreacion.{modo}",
              f"x = {x}", f"z = {z}", f"escala = {f(c.get('escala', 1))}", f"giro = {f(c.get('giro', 0))}"]
    if c["modo"] == "modelo":
        nombre = pathlib.Path(c["archivo"]).stem
        shutil.copyfile(WEB / c["archivo"], destino_glb / (nombre + ".glb"))
        campos += [f"modelo = {cs(nombre)}", f"alto = {f(c['alto'])}", f"plano = {c.get('plano', 0)}"]
        if "pisoEn" in c: campos += [f"hayPiso = true", f"pisoX = {c['pisoEn'][0]}", f"pisoZ = {c['pisoEn'][1]}"]
        fondo = c.get("plano") or 2
    else:
        campos.append("cajas = " + cajas_cs(c["cajas"]))
        if c.get("extras"):
            for k, v in c["piezas"].items(): piezas[k] = v
            campos.append("extras = new Extra[] { " + ", ".join(
                f"new Extra({cs(e['cajas'])}, {f(e['en'][0])}, {f(e['en'][1])}, {f(e.get('escala', 1))})" for e in c["extras"]) + " }")
        fondo = extension(c["cajas"])[2] * c.get("escala", 1)
    if c.get("rotulo"): campos.append(f"rotulo = {cs(c['rotulo'])}")
    lineas.append("        new Creacion { " + ", ".join(campos) + " },")
    texto = (f"{c['titulo']}: creación de {c['autor']}, estudiante de Diseño Visual, para su videojuego en el curso "
             "Modelado Digital de la Institución Universitaria Antonio José Camacho.")
    memorias.append(f'        new Memoria({cs("cr_" + c["id"])}, {cs(c["titulo"])}, Cat.Creacion, {x}, {round(z + fondo + 2)}, {cs(texto)}),')

datos = ["// GENERADO por herramientas/creaciones_unity.py desde colorea-ar/web-rv/creaciones.js: no editar a mano.",
         "using System.Collections.Generic;", "", "public partial class Mundo", "{",
         "    static readonly Creacion[] CREACIONES = {", *lineas, "    };", "",
         "    static readonly Dictionary<string, PiezaCaja[]> PIEZAS_CREACIONES = new Dictionary<string, PiezaCaja[]> {",
         *[f"        {{ {cs(k)}, {cajas_cs(v)} }}," for k, v in piezas.items()], "    };", "}", ""]
(P / "Assets/Calicraft/Scripts/Mundo.Creaciones.Datos.cs").write_text("\n".join(datos), encoding="utf-8")

# memorias de las creaciones dentro de MEMORIAS (para que salgan sus cubos y sus puntos de teletransporte)
md = P / "Assets/Calicraft/Scripts/Mundo.Memorias.Datos.cs"; t = md.read_text(encoding="utf-8")
bloque = "        // ---- CREACIONES de los estudiantes (generado por herramientas/creaciones_unity.py) ----\n" + "\n".join(memorias) + "\n        // ---- FIN CREACIONES ----\n"
if "---- CREACIONES" in t:
    t = re.sub(r"        // ---- CREACIONES.*?// ---- FIN CREACIONES ----\n", lambda _: bloque, t, flags=re.S)
else:
    i = t.rindex("    };"); t = t[:i] + "\n" + bloque + t[i:]
md.write_text(t, encoding="utf-8")
shutil.copyfile(WEB / "texturas/vaca.png", P / "Assets/StreamingAssets/bloques/vaca.png")
print(f"{len(lineas)} creaciones, {len(memorias)} memorias, piezas {list(piezas)}, glb en {destino_glb}")

# Calicraft VIROO

Mundo de bloques en realidad virtual sobre la memoria de Cali (monumentos, memorias, videos y creaciones de
estudiantes), hecho en Unity como aplicación para **VIROO Studio 3.1** y **Room Player 3.1**, para usarse
con gafas VIVE en una sala VIROO universitaria.

Proyecto docente del curso *Modelado Digital*. Se busca ayuda de otros programadores. Ver **Problema abierto**.

## Requisitos

| | |
|---|---|
| Unity | **6000.0.85f2** (Unity 6 LTS) con **Windows Build Support (Mono)** |
| VIROO Studio | **3.1.259** (en `Packages/manifest.json`; Unity lo baja del registro de Virtualware al abrir) |
| Para compilar el `.virooapp` | Una cuenta de la organización en **VIROO Cloud**. El Application Builder cifra la app con la clave de la aplicación creada allí. Sin esa cuenta se puede abrir, editar y probar, pero no generar el archivo final. |

## Estructura

```
Assets/Calicraft/Scripts/     Lógica del juego (clase parcial Mundo.*: terreno, monumentos, memorias,
                              creaciones, videos, integración VIROO en Mundo.Viroo.cs)
Assets/Calicraft/Scenes/      Escena principal Calicraft.unity (armada desde la plantilla de VIROO Studio)
Assets/Calicraft/Resources/   Materiales, creaciones y configuración
Assets/StreamingAssets/       Texturas de bloques y datos que se leen en tiempo de ejecución
Assets/Editor/                Utilidades del editor (menú «Calicraft» en la barra de Unity)
herramientas/                 Scripts Python que generan datos para Unity
LEEME_ADMINISTRADOR_VIROO.md  Guía paso a paso para compilar y subir a VIROO Cloud
```

## Cómo abrirlo

1. Abrir la carpeta con Unity 6000.0.85f2. La primera vez tarda porque descarga los paquetes de VIROO.
2. **File → Build Profiles → Windows → Switch Platform**. VIROO no compila para Linux ni Mac.
3. Panel de VIROO: **Window → Viroo → Dashboard**. También se abre desde **Calicraft → Abrir panel VIROO**.
4. Para probar sin VIROO, darle Play a la escena: hay un modo de prueba con teclado y mouse.

Compilar y publicar: ver [LEEME_ADMINISTRADOR_VIROO.md](LEEME_ADMINISTRADOR_VIROO.md).

## Problema abierto: pantalla negra en la sala

- El `.virooapp` compila sin errores, se sube a VIROO Cloud y **aparece en la lista** de aplicaciones de la sala.
- Al lanzarla desde Room Player **3.1.471**, la imagen queda **en negro**.
- Todavía no hay un log de la sala con los marcadores `[Calicraft] 1/4 … 4/4` que trae `Mundo.cs`. Los logs
  revisados hasta ahora eran de otra aplicación: todas las apps hechas con la plantilla comparten
  `productName = "Viroo Studio"`, y por eso también comparten `LocalLow\Virtualware\Viroo Studio\Player.log`.
- Hipótesis: el bug *«Error when overriding encrypted application»* de VIROO Studio 3.1.258, corregido en
  3.1.259. El proyecto ya está en 3.1.259, pero falta compilar de nuevo y probarlo en la sala.

Se agradecen ideas sobre:
- Requisitos de la escena principal en VIROO 3.1 que puedan dejar la cámara sin imagen. Ver
  `NOTAS_PLANTILLA_VIROO.md` y `Assets/Editor/ConfigurarCalicraft.cs`.
- Dónde deja Room Player 3.1 el log del proceso de cada aplicación.

## Licencia

Código del juego: uso educativo; para otros usos, escribir al autor. Los paquetes de VIROO Studio son
propiedad de Virtualware y **no se incluyen en este repositorio**: Unity los descarga de su registro.

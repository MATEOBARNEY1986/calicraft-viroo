# Calicraft para VIROO: guía para compilar y publicar

Proyecto de Unity listo para compilar como aplicación VIROO, para usarse en la sala con Room Player 3.1.
Lo preparó Mateo Barney (docente, curso Modelado Digital). Para compilarlo hace falta una cuenta de la
organización en VIROO Cloud, porque VIROO cifra la aplicación con las claves de la aplicación creada allí.

## Requisitos

| | |
|---|---|
| Unity | **6000.0 LTS**. Se probó con **6000.0.85f2**; VIROO Studio 3.1 recomienda la última 6000.0.x |
| Módulo de Unity | **Windows Build Support (Mono)** |
| VIROO Studio | **3.1.258** (ya viene en `Packages/manifest.json`; Unity lo descarga al abrir el proyecto) |
| Sala | Room Player 3.1.427 y gafas HTC VIVE Focus Vision |

## Pasos

1. **Crear la aplicación en VIROO Cloud** (viroo.cloud → Applications), con el identificador `calicraft`
   y el nombre `Calicraft`.
2. **Abrir el proyecto** con Unity 6000.0. La primera vez tarda varios minutos, porque descarga los paquetes y
   genera la carpeta `Library`.
3. **Cambiar la plataforma a Windows**: *File → Build Profiles → Windows → Switch Platform*.
4. *Window → Viroo → Dashboard*:
   - **Account**: iniciar sesión con la cuenta de la organización.
   - **Project Validation**: aplicar los *Fix* que proponga.
   - **Application Builder**:
     - en la lista, elegir la aplicación `calicraft` creada en el paso 1;
     - Application Identifier: `calicraft` · Application Name: `Calicraft` · idioma: español;
     - escenas: agregar `Assets/Calicraft/Scenes/Calicraft.unity` y marcarla como **Lobby**;
     - **Build**.
   - **Application Uploader**: elegir el `.virooapp` generado y darle **Upload**.
5. **Crear una sesión** con Calicraft para la sala.

## Qué contiene la experiencia

- Un mundo de bloques (estilo Minecraft) con monumentos y memoria de Cali y el Pacífico: 21 cubos de memoria con
  sus consignas y 5 casas del terremoto para reconstruir.
- **Multiusuario cooperativo.** Los bloques que se ponen o rompen, las memorias encontradas y los videos se
  sincronizan entre todos con una acción propia (`CalicraftRed`, una `BroadcastObjectAction` con persistencia
  *All*), así que quien entra tarde recibe el mundo en el estado en que va.
- **27 puntos de teletransporte** (`TeleportPoint`) en el menú de VIROO: el inicio, cada monumento y cada casa.
  Todo el terreno es zona de teletransporte (`TeleportationArea`, capa de interacción *Teleport*).
- **Sin locomoción con palanca ni gravedad**, pensado para Room Player: el centro del mapa es plano a altura 0.
- **Mandos**: gatillo derecho descubre una memoria, rompe un bloque o activa un botón de video; agarre pone un
  bloque; A/X cambian el tipo de bloque. El gatillo que confirma el teletransporte de VIROO no rompe bloques.

## Necesita internet

Junto a seis monumentos hay pantallas de video. Los 9 videos (MP4) se cargan por internet desde
`https://aula.barneyconstruye.site/materiales/rv/videos/`. Todo lo demás funciona sin conexión.

## Notas técnicas

- **Escena**: `Assets/Calicraft/Scenes/Calicraft.unity`, creada desde la plantilla oficial `VirooStudio.unity`.
  Si hace falta rehacerla: menú *Calicraft → Configurar escena VIROO*.
- **Arena de la sala**: `Data/arenas/arena.json` (14,5 × 8,3 m).
- **Registro NuGet**: en `Packages/manifest.json` el registro `org.nuget` apunta a
  `unitynuget-registry.openupm.com`, porque el de VIROO (`unitynuget-registry.viroo.cloud`) no respondía el
  9-nov-2026. Si ya responde, se puede volver a poner el original.
- **Archivos solo para Linux**: `LinuxCompat.cs` y `viroo.Production.Linux.json` sirven únicamente para probar en
  el editor de Linux. En Windows no se compilan ni se usan.
- **Modo de prueba**: si VIROO no llega a inicializarse, el juego entra en un modo de prueba con cámara propia.
  En la sala no debería activarse; si aparece el aviso `[Calicraft] VIROO no arrancó` en el log, la causa está en
  la configuración de VIROO.

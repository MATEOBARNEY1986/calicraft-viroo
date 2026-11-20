// Prueba automática SIN gafas ni VIROO (en el PC de Linux), para revisar el mundo antes de compilar para la sala.
//   ./Calicraft.x86_64 -calicraftPrueba <carpeta> [-screen-width 1280 -screen-height 720]
// Recorre todos los puntos de teletransporte (inicio, memorias, casas) y saca una foto de cada uno, una vista
// aérea del mapa entero, prueba descubrir una memoria y romper/poner un bloque, y deja <carpeta>/informe.txt
// con los conteos y los errores del registro. Al terminar se cierra sola. Sin el argumento no hace nada.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using Viroo.Teleport;

public partial class Mundo
{
    string carpetaPrueba;
    readonly StringBuilder informePrueba = new StringBuilder();
    int erroresPrueba;

    void IniciarPruebaSiPide()
    {
        var a = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(a, "-calicraftPrueba");
        if (i < 0 || carpetaPrueba != null) return;
        carpetaPrueba = i + 1 < a.Length && !a[i + 1].StartsWith("-") ? a[i + 1] : "prueba_calicraft";
        Directory.CreateDirectory(carpetaPrueba);
        Application.logMessageReceived += (msg, pila, tipo) =>
        {
            if (tipo == LogType.Error || tipo == LogType.Exception) { erroresPrueba++; informePrueba.AppendLine("ERROR: " + msg); }
        };
        StartCoroutine(RutinaPrueba());
    }

    static string Limpio(string s)
    {
        var sb = new StringBuilder();
        foreach (char c in s) sb.Append(char.IsLetterOrDigit(c) ? c : '_');
        return sb.ToString();
    }

    IEnumerator Foto(string nombre)
    {
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(carpetaPrueba, nombre + ".png"));
        yield return null;
    }

    IEnumerator RutinaPrueba()
    {
        var I = informePrueba;
        I.AppendLine("Calicraft VIROO · prueba automática · " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
        yield return new WaitForSeconds(4f);
        I.AppendLine($"modo sin VIROO: {sinViroo} · memorias: {cubos.Count} (datos: {MEMORIAS.Length}) · casas: {casas.Count}");
        I.AppendLine($"creaciones de estudiantes: {creacionesPuestas} puestas, {creacionesModeloOk} modelos .glb cargados, {creacionesModeloMal} fallidos");

        // 1) vista aérea de todo el mapa (para revisar dónde quedó cada cosa)
        var go = new GameObject("CamaraAerea");
        var ca = go.AddComponent<Camera>();
        ca.orthographic = true; ca.orthographicSize = Terreno.MITAD + 4;
        ca.transform.SetPositionAndRotation(new Vector3(0, 140, 0), Quaternion.Euler(90, 0, 0));
        ca.farClipPlane = 400; ca.clearFlags = CameraClearFlags.SolidColor; ca.backgroundColor = new Color(.1f, .1f, .14f);
        var rt = new RenderTexture(2048, 2048, 24); ca.targetTexture = rt; ca.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(2048, 2048, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 2048, 2048), 0, 0); tex.Apply();
        File.WriteAllBytes(Path.Combine(carpetaPrueba, "00_mapa_aereo.png"), tex.EncodeToPNG());
        RenderTexture.active = null; ca.targetTexture = null; Destroy(go); rt.Release();
        I.AppendLine("mapa aéreo: 00_mapa_aereo.png (norte = arriba = +z, este = derecha = +x; ±" + Terreno.MITAD + " m)");

        // 2) foto en cada punto de teletransporte
        var puntos = new List<TeleportPoint>(FindObjectsByType<TeleportPoint>(FindObjectsSortMode.None));
        puntos.Sort((p, q) => p.Index.CompareTo(q.Index));
        I.AppendLine($"puntos de teletransporte: {puntos.Count}");
        int n = 1;
        foreach (var tp in puntos)
        {
            cam.transform.position = tp.transform.position + Vector3.up * 1.65f;
            yawPrueba = tp.transform.eulerAngles.y; pitchPrueba = 8;
            yield return new WaitForSeconds(0.9f);
            yield return Foto($"{n++:00}_{Limpio(tp.Label)}");
            I.AppendLine($"  {tp.Index,2} {tp.Label} @ {tp.transform.position}");
        }

        // 3) fotos de las creaciones de los estudiantes, vistas desde un costado
        foreach (var (nombre, pos) in vistasCreaciones)
        {
            cam.transform.position = pos + new Vector3(0, 4f, -16f);
            var dir = pos + Vector3.up * 2f - cam.transform.position;
            var mira = Quaternion.LookRotation(dir).eulerAngles;
            yawPrueba = mira.y; pitchPrueba = Mathf.Clamp(Mathf.DeltaAngle(0, mira.x), -60, 60);
            yield return new WaitForSeconds(0.9f);
            yield return Foto($"{n++:00}_creacion_{Limpio(nombre)}");
        }

        // 4) jugar: descubrir la primera memoria y romper/poner un bloque como lo haría un jugador
        if (cubos.Count > 0)
        {
            int antes = recogidos.Count;
            RecogerMemoria(0, "prueba", "Jugador de prueba");
            yield return new WaitForSeconds(0.5f);
            I.AppendLine($"descubrir memoria «{cubos[0].m.nombre}»: recogidas {antes} -> {recogidos.Count}");
        }
        var c0 = Vector3Int.FloorToInt(new Vector3(0, terreno.AlturaSuelo(0, 0) - 1, 0));
        var tipo = terreno.Get(c0.x, c0.y, c0.z);
        terreno.Set(c0.x, c0.y, c0.z, B.Aire);
        bool roto = terreno.Get(c0.x, c0.y, c0.z) == B.Aire;
        terreno.Set(c0.x, c0.y, c0.z, tipo);
        I.AppendLine($"romper y volver a poner un bloque en (0,{c0.y},0): {(roto && terreno.Get(c0.x, c0.y, c0.z) == tipo ? "OK" : "FALLA")}");

        I.AppendLine($"errores en el registro: {erroresPrueba}");
        File.WriteAllText(Path.Combine(carpetaPrueba, "informe.txt"), I.ToString());
        yield return new WaitForSeconds(0.5f);
        Application.Quit();
    }
}

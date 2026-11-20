// Compila Calicraft como ejecutable de Linux, SOLO para probarlo en el PC (la sala usa el .virooapp de Windows).
//   Unity -batchmode -quit -projectPath . -executeMethod CompilarLinux.Compilar -logFile Logs/compilar_linux.log
// Resultado: Builds/Linux/Calicraft.x86_64 ; probar con:  ./Calicraft.x86_64 -calicraftPrueba prueba
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class CompilarLinux
{
    public static void Compilar()
    {
        ConfigurarCalicraft.Ejecutar();   // rehace la escena: puntos de teletransporte de TODAS las memorias (también las creaciones)
        var op = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Calicraft/Scenes/Calicraft.unity" },
            locationPathName = "Builds/Linux/Calicraft.x86_64",
            target = BuildTarget.StandaloneLinux64,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.Development,
        };
        var r = BuildPipeline.BuildPlayer(op);
        Debug.Log($"[Calicraft] Build Linux: {r.summary.result} · {r.summary.totalErrors} errores · {r.summary.totalSize / 1048576} MB");
        if (r.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }
}

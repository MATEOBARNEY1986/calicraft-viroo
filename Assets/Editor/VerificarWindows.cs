// Compila los scripts tal como irían en el ejecutable de Windows (sin hacer el build completo, que en
// VIROO exige sesión iniciada en VIROO Cloud). Uso: -executeMethod VerificarWindows.Compilar
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEngine;

public static class VerificarWindows
{
    public static void Compilar()
    {
        var ajustes = new ScriptCompilationSettings
        {
            target = BuildTarget.StandaloneWindows64,
            group = BuildTargetGroup.Standalone,
            options = ScriptCompilationOptions.None,
        };
        var r = PlayerBuildInterface.CompilePlayerScripts(ajustes, "Temp/VerificarWindows");
        int n = r.assemblies?.Count ?? 0;
        Debug.Log($"[Calicraft] Compilación para Windows: {n} ensamblados");
        if (n == 0) EditorApplication.Exit(1);
    }
}

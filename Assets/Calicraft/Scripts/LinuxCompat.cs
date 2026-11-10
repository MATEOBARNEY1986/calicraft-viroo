#if UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
using System;
using UnityEngine;

// Solo para PROBAR en Linux (el editor del PC de Mateo). VIROO está hecho para Windows/macOS/Android:
// sus configuraciones guardan datos en %APPDATA% (Windows), que en Linux no existe, y su arranque
// se cae con NullReferenceException (SetUnityApplicationDataPathEnvironmentVariable).
// Antes de cargar la escena se define APPDATA = ~/.config. En Windows (la sala) esto no se compila.
public static class LinuxCompat
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void DefinirAppData()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPDATA"))) return;
        string home = Environment.GetEnvironmentVariable("HOME") ?? "/tmp";
        Environment.SetEnvironmentVariable("APPDATA", home + "/.config");
        Debug.Log("[Calicraft] Linux: APPDATA = " + home + "/.config (para que VIROO arranque)");
    }
}
#endif

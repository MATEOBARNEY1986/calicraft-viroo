// Atajo para abrir el panel de VIROO Studio (Info / Account / Application Builder).
// Es lo mismo que Window → Viroo → Dashboard.
using UnityEditor;

public static class AbrirPanelViroo
{
    const string RUTA = "Window/Viroo/Dashboard";

    [MenuItem("Calicraft/Abrir panel VIROO (para Build)", priority = 0)]
    public static void Abrir() => EditorApplication.ExecuteMenuItem(RUTA);

    // Se abre solo una vez por sesión de Unity, al terminar de cargar.
    [InitializeOnLoadMethod]
    static void AlCargar()
    {
        if (SessionState.GetBool("calicraft.panelViroo", false)) return;
        SessionState.SetBool("calicraft.panelViroo", true);
        EditorApplication.delayCall += Abrir;
    }
}

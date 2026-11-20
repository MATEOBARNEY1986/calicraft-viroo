using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

// Leer los archivos del juego (texturas, personajes .glb, giros.txt) de StreamingAssets.
// En el navegador no hay disco: todo se DESCARGA de la página. Descargar también funciona en el
// ejecutable (con "file://"), así que es el mismo código para las dos versiones.
public partial class Mundo
{
    static bool EnNavegador => Application.platform == RuntimePlatform.WebGLPlayer;

    static readonly string[] TEXTURAS = {
        "agua.png", "arena.png", "hojas.png", "ladrillo.png", "mineral_carbon.png", "mineral_diamante.png",
        "mineral_hierro.png", "mineral_oro.png", "oro_bloque.png", "pasto_arriba.png", "pasto_lado.png",
        "piedra.png", "roca_base.png", "roca.png", "tablas.png", "tierra.png", "tnt.png",
        "tronco_arriba.png", "tronco_lado.png", "vidrio.png", "vaca.png",
        "bronce.png", "marmol.png", "rosa.png", "amarillo.png", "azul.png", "negro.png", "rojo.png", "ventanal.png", "crema.png" };

    static string Url(string relativa)
    {
        string p = Application.streamingAssetsPath + "/" + relativa;
        return p.Contains("://") ? p : "file://" + p;
    }

    static async Task<byte[]> Bajar(string relativa)
    {
        using var req = UnityWebRequest.Get(Url(relativa));
        var op = req.SendWebRequest();
        while (!op.isDone) await Awaitable.NextFrameAsync();
        return req.result == UnityWebRequest.Result.Success ? req.downloadHandler.data : null;
    }

    async Task PrecargarTexturas()
    {
        foreach (var f in TEXTURAS)
        {
            var datos = await Bajar("bloques/" + f);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
            if (datos != null) tex.LoadImage(datos);
            else diag += f + ": no cargó. ";
            tex.filterMode = FilterMode.Point;   // pixelado, como Minecraft
            texs[f] = tex;
        }
    }

    // ---------- giros de los personajes (tecla G) ----------
    // En el navegador se guardan en el propio navegador (PlayerPrefs); en el ejecutable, en giros.txt.
    async Task CargarGiros()
    {
        var datos = await Bajar("giros.txt");
        if (datos != null) LeerGiros(Encoding.UTF8.GetString(datos));
        if (PlayerPrefs.HasKey("giros")) LeerGiros(PlayerPrefs.GetString("giros"));
    }

    void LeerGiros(string texto)
    {
        foreach (var linea in texto.Split('\n'))
        {
            var p = linea.Trim().Split('=');
            if (p.Length == 2 && float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var g))
                giros[p[0].Trim()] = g;
        }
    }

    void GuardarGiros()
    {
        var sb = new StringBuilder();
        foreach (var kv in giros) sb.Append(kv.Key).Append('=').Append(kv.Value.ToString(CultureInfo.InvariantCulture)).Append('\n');
        try
        {
            if (EnNavegador) { PlayerPrefs.SetString("giros", sb.ToString()); PlayerPrefs.Save(); }
            else File.WriteAllText(Path.Combine(Application.streamingAssetsPath, "giros.txt"), sb.ToString());
        }
        catch (System.Exception e) { diag = "No se pudo guardar el giro: " + e.Message; }
    }
}

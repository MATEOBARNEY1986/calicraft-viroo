using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Cajas, materiales y texturas (pixeladas, como Minecraft). Se guardan en caché para no repetirlas.
public partial class Mundo
{
    readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
    readonly Dictionary<string, Texture2D> texs = new Dictionary<string, Texture2D>();
    readonly Dictionary<string, Color> promedios = new Dictionary<string, Color>();

    GameObject Caja(string nombre, Vector3 centro, Vector3 tam, string tex, bool colision = true,
                    Transform padre = null, bool transparente = false, float rotY = 0)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = nombre;
        g.transform.SetParent(padre != null ? padre : raiz, false);
        g.transform.localPosition = centro;
        g.transform.localRotation = Quaternion.Euler(0, rotY, 0);
        g.transform.localScale = tam;
        if (!colision) Destroy(g.GetComponent<Collider>());
        // repetir la textura según el tamaño (los cubos de Unity estiran la imagen)
        bool plano = tam.y < 1.01f && Mathf.Max(tam.x, tam.z) > tam.y * 2;
        var rep = plano ? new Vector2(tam.x, tam.z) : new Vector2(Mathf.Max(tam.x, tam.z), tam.y);
        if (tex != null) g.GetComponent<Renderer>().sharedMaterial = Mat(tex, rep, transparente);
        return g;
    }

    void Invisible(Vector3 centro, Vector3 tam)
    {
        var b = Caja("Borde", centro, tam, null);
        b.GetComponent<Renderer>().enabled = false;
    }

    Shader ShaderBase() => GraphicsSettings.currentRenderPipeline == null
        ? Shader.Find("Standard") : Shader.Find("Universal Render Pipeline/Lit");

    Texture2D Tex(string archivo)
    {
        // las texturas ya se descargaron al arrancar (PrecargarTexturas)
        if (texs.TryGetValue(archivo, out var tex)) return tex;
        tex = new Texture2D(2, 2) { filterMode = FilterMode.Point };
        texs[archivo] = tex;
        return tex;
    }

    Material Mat(string archivo, Vector2 rep, bool transparente = false)
    {
        string clave = archivo + rep + transparente;
        if (mats.TryGetValue(clave, out var hecho)) return hecho;
        var mat = new Material(ShaderBase()) { mainTexture = Tex(archivo), mainTextureScale = rep };
        if (transparente) Transparente(mat, 0.7f);
        mats[clave] = mat;
        return mat;
    }

    Material MatColor(Color c, float brillo = 0)
    {
        string clave = "color" + c + brillo;
        if (mats.TryGetValue(clave, out var hecho)) return hecho;
        var m = new Material(ShaderBase()) { color = c };
        if (brillo > 0)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * brillo);
        }
        mats[clave] = m;
        return m;
    }

    void Transparente(Material mat, float alfa)
    {
        var c = mat.color; c.a = alfa; mat.color = c;
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1);   // URP
        if (mat.HasProperty("_Mode"))                                    // Standard
        {
            mat.SetFloat("_Mode", 3);
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
        }
    }

    // color promedio de una textura (para los escombros al romper un bloque)
    Color ColorDe(B b)
    {
        string archivo = Terreno.TexturaLado(b);
        if (promedios.TryGetValue(archivo, out var c)) return c;
        var px = Tex(archivo).GetPixels32();
        float r = 0, g = 0, bl = 0;
        foreach (var p in px) { r += p.r; g += p.g; bl += p.b; }
        float n = Mathf.Max(1, px.Length) * 255f;
        c = new Color(r / n, g / n, bl / n);
        promedios[archivo] = c;
        return c;
    }
}

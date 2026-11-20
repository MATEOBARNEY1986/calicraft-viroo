// Arma (o rearma) la escena VIROO de Calicraft. Se puede correr desde el menú
// "Calicraft/Configurar escena VIROO" o sin pantalla:
//   Unity -batchmode -quit -executeMethod ConfigurarCalicraft.Ejecutar
// Es idempotente: si la escena ya existe la reconstruye desde la plantilla oficial de VIROO Studio.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Viroo.Teleport;

public static class ConfigurarCalicraft
{
    const string PLANTILLA = "Packages/com.viroo.studio/Editor/Viroo.Studio.Unity.Editor/VirooStudio.unity";
    const string ESCENA = "Assets/Calicraft/Scenes/Calicraft.unity";
    const string ESCENA_VIEJA = "Assets/Calicraft/Scenes/Juego.unity";
    const string MATERIALES = "Assets/Calicraft/Resources/ShadersCalicraft";

    [MenuItem("Calicraft/Avanzado/Rearmar escena (no hace falta)")]
    public static void Ejecutar()
    {
        CrearMateriales();
        CrearEscena();
        AssetDatabase.SaveAssets();
        Debug.Log("[Calicraft] Escena VIROO lista: " + ESCENA);
    }

    // ---------- materiales que mantienen los shaders (y sus variantes) dentro del build ----------
    static void CrearMateriales()
    {
        Directory.CreateDirectory(MATERIALES);
        Material Nuevo(string nombre, string shader)
        {
            string ruta = $"{MATERIALES}/{nombre}.mat";
            var sh = Shader.Find(shader);
            if (sh == null) { Debug.LogError("[Calicraft] Falta el shader " + shader); return null; }
            var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, ruta); }
            else m.shader = sh;
            return m;
        }
        Nuevo("Lit", "Universal Render Pipeline/Lit");
        var em = Nuevo("LitEmision", "Universal Render Pipeline/Lit");
        if (em != null)
        {
            em.EnableKeyword("_EMISSION");
            em.SetColor("_EmissionColor", Color.white);
            em.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(em);
        }
        var tr = Nuevo("LitTransparente", "Universal Render Pipeline/Lit");
        if (tr != null)
        {
            tr.SetFloat("_Surface", 1);
            tr.SetFloat("_Blend", 0);
            tr.SetOverrideTag("RenderType", "Transparent");
            tr.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            tr.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            tr.SetFloat("_ZWrite", 0);
            tr.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            tr.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(tr);
        }
        Nuevo("Unlit", "Universal Render Pipeline/Unlit");
        Nuevo("Sprites", "Sprites/Default");
        Nuevo("Skybox", "Skybox/Procedural");
    }

    // ---------- escena ----------
    static void CrearEscena()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ESCENA));
        if (File.Exists(ESCENA)) AssetDatabase.DeleteAsset(ESCENA);
        if (!AssetDatabase.CopyAsset(PLANTILLA, ESCENA))
            throw new Exception("[Calicraft] No se pudo copiar la plantilla de VIROO: " + PLANTILLA);
        if (File.Exists(ESCENA_VIEJA)) AssetDatabase.DeleteAsset(ESCENA_VIEJA);   // la de escritorio (vive en mindcraft-unity)

        var escena = EditorSceneManager.OpenScene(ESCENA, OpenSceneMode.Single);
        var todos = escena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        var root = todos.FirstOrDefault(t => t.name == "Root");
        if (root == null) throw new Exception("[Calicraft] La plantilla no trae el objeto Root");

        // el piso de prueba de la plantilla: el terreno de Calicraft se vuelve zona de teletransporte en tiempo de ejecución
        foreach (var t in todos.Where(t => t != null && t.name == "TeleportationArea")) UnityEngine.Object.DestroyImmediate(t.gameObject);

        // Mundo + la acción de red, en el mismo objeto
        var go = new GameObject("Calicraft");
        go.transform.SetParent(root, false);
        go.AddComponent<Mundo>();
        var red = go.AddComponent<CalicraftRed>();
        var so = new SerializedObject(red);
        var pers = so.FindProperty("persistenceType");
        if (pers != null) pers.enumValueIndex = 2;               // ActionPersistenceType.All
        else Debug.LogError("[Calicraft] No encontré 'persistenceType' en CalicraftRed");
        var id = so.FindProperty("id");
        if (id != null && string.IsNullOrEmpty(id.stringValue)) id.stringValue = "calicraft-red-" + Guid.NewGuid().ToString("N");
        so.ApplyModifiedPropertiesWithoutUndo();

        CrearPuntos(root);

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
    }

    // puntos de teletransporte del menú de VIROO: inicio, cada memoria y cada casa
    // (aquí van a la altura 0; Mundo los baja al suelo real cuando genera el mundo)
    static void CrearPuntos(Transform root)
    {
        var padre = new GameObject("PuntosDeTeletransporte").transform;
        padre.SetParent(root, false);
        int indice = 0;

        void Punto(string identificador, string etiqueta, Vector3 pos)
        {
            var g = new GameObject("TP_" + identificador);
            g.transform.SetParent(padre, false);
            g.transform.position = pos;
            var tp = g.AddComponent<TeleportPoint>();
            var so = new SerializedObject(tp);
            so.FindProperty("index").intValue = indice++;
            so.FindProperty("identifier").stringValue = identificador;
            so.FindProperty("label").stringValue = etiqueta;
            so.FindProperty("selectableFromMenu").boolValue = true;
            so.FindProperty("applyRotation").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        Punto("inicio", "Inicio", Vector3.zero);

        var campo = typeof(Mundo).GetField("MEMORIAS", BindingFlags.NonPublic | BindingFlags.Static);
        var memorias = (Mundo.Memoria[])campo.GetValue(null);
        foreach (var m in memorias) Punto("mem_" + m.id, m.nombre, new Vector3(m.x, 0, m.z));

        var casas = new[] { (-34, -26), (26, -30), (-10, -38), (12, 52), (-32, 34) };   // Mundo.Calicraft.cs
        for (int i = 0; i < casas.Length; i++) Punto("casa_" + i, $"Casa del terremoto {i + 1}", new Vector3(casas[i].Item1, 0, casas[i].Item2));
    }
}

using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using GLTFast;

// Los personajes del curso (.glb de Tinkercad): se cargan una vez como plantillas escondidas
// y el jugador y los monstruos son copias. Aquí también se ajusta su tamaño y hacia dónde miran.
public partial class Mundo
{
    readonly string[] personajes = {
        "emily", "kasi", "zombie_walter", "cientifico", "momia_shelly",
        "inari", "leelo", "elzing", "bombul", "snaget", "trug" };
    readonly string[] nombres = {
        "Emily", "Kasi", "Zombie Walter", "Cientifico loco", "Momia Shelly",
        "Inari", "Leelo", "Elzing", "Bombul", "Snaget", "Trug" };
    readonly Dictionary<string, GltfImport> glbs = new Dictionary<string, GltfImport>();
    readonly Dictionary<string, GameObject> plantillas = new Dictionary<string, GameObject>();
    readonly List<(GameObject cuerpo, Transform ancla)> cuerposEnemigos = new List<(GameObject, Transform)>();
    Transform bodega;

    // Cada modelo de Tinkercad viene mirando hacia un lado distinto: este giro lo pone de frente.
    // Los que se ajustan con la tecla G quedan guardados en StreamingAssets/giros.txt.
    // (5-nov: medidos con la geometría de cada modelo; la pantalla de elegir GIRA el personaje,
    //  así que no sirve para probar. Se prueban jugando, con la cámara detrás)
    readonly Dictionary<string, float> giros = new Dictionary<string, float> {
        { "emily", 90f }, { "kasi", 0f }, { "zombie_walter", 90f }, { "cientifico", 0f },
        { "momia_shelly", 0f }, { "inari", 270f }, { "leelo", 0f }, { "elzing", 0f },
        { "bombul", 0f }, { "snaget", 270f }, { "trug", 0f },
    };

    // (glTFast no deja volver a armar el mismo modelo al "jugar otra vez", copiar sí funciona;
    //  y no lo arma dentro de un objeto apagado: se arma encendido y se esconde después)
    async Task CargarPersonajes()
    {
        await CargarGiros();
        bodega = new GameObject("Plantillas").transform;
        bodega.position = new Vector3(0, -500, 0);
        foreach (var n in personajes)
        {
            glbs[n] = await CargarGlb(n);
            if (glbs[n] == null) { diag += n + ": no cargó. "; continue; }
            var t = new GameObject(n);
            t.transform.SetParent(bodega, false);
            bool ok = false;
            try { ok = await glbs[n].InstantiateMainSceneAsync(t.transform); }
            catch (System.Exception e) { diag += n + ": " + e.Message + ". "; }
            t.SetActive(false);
            int mallas = t.GetComponentsInChildren<MeshFilter>(true).Length;
            if (ok && mallas > 0) plantillas[n] = t;
            else diag += $"{n}: armar={ok} mallas={mallas}. ";
        }
        // el aviso solo aparece si algún personaje no cargó
        diag = plantillas.Count < personajes.Length ? $"Modelos {plantillas.Count}/{personajes.Length}. " + diag : "";
    }

    async Task<GltfImport> CargarGlb(string nombre)
    {
        string rel = "personajes/" + nombre + ".glb";
        try
        {
            var datos = await Bajar(rel);
            if (datos == null) return null;
            var g = new GltfImport();
            return await g.LoadGltfBinary(datos, new System.Uri(Url(rel))) ? g : null;
        }
        catch (System.Exception e) { diag += nombre + ": " + e.Message + ". "; return null; }
    }

    // copia la plantilla del personaje dentro de 'padre'; false si no hay modelo
    bool Clonar(string modelo, Transform padre)
    {
        if (!plantillas.TryGetValue(modelo, out var t) || t == null)
        {
            if (!diag.Contains(modelo)) diag += $"{modelo}: sin plantilla (hay {plantillas.Count}). ";
            return false;
        }
        try
        {
            var copia = Instantiate(t, padre, false);
            copia.name = modelo;
            copia.SetActive(true);   // la plantilla está escondida; la copia se enciende
            return true;
        }
        catch (System.Exception e) { diag += "copiar " + modelo + ": " + e.Message + ". "; return false; }
    }

    void CrearJugador()
    {
        jugador = new GameObject("Personaje");
        jugador.transform.SetParent(raiz, false);
        jugador.transform.position = inicio;
        jugador.layer = CAPA_JUGADOR;   // el rayo de poner/romper no choca contigo
        cc = jugador.AddComponent<CharacterController>();
        cc.height = 1.7f; cc.radius = 0.4f; cc.center = new Vector3(0, 0.85f, 0);
        jugador.AddComponent<Empujador>();
        yaw = 0; pitch = 15; velY = 0;

        modelos = new GameObject("Modelos").transform;
        modelos.SetParent(jugador.transform, false);
        for (int i = 0; i < personajes.Length; i++)
        {
            var holder = new GameObject(personajes[i]);
            holder.transform.SetParent(modelos, false);
            if (Clonar(personajes[i], holder.transform)) Orientar(holder, personajes[i]);
            else // si el .glb falla, una cápsula de color
            {
                var cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Destroy(cap.GetComponent<Collider>());
                cap.transform.SetParent(holder.transform, false);
                cap.GetComponent<Renderer>().sharedMaterial = MatColor(Color.HSVToRGB(i / 11f, 0.6f, 0.9f));
            }
            NormalizarTamano(jugador.transform, holder, 1.7f);
            CapaRecursiva(holder, CAPA_JUGADOR);
            listaModelos.Add(holder);
        }
        MostrarPersonaje(elegido);
        cam.transform.SetParent(jugador.transform, false);
    }

    void MostrarPersonaje(int i)
    {
        if (listaModelos.Count == 0) return;
        elegido = (i % listaModelos.Count + listaModelos.Count) % listaModelos.Count;
        for (int j = 0; j < listaModelos.Count; j++) listaModelos[j].SetActive(j == elegido);
    }

    // la cámara de frente mientras eliges personaje; detrás del personaje al jugar
    void VistaDeInicio(bool activa)
    {
        if (activa)
        {
            cam.transform.localPosition = new Vector3(0, 1.1f, 3f);
            cam.transform.localRotation = Quaternion.Euler(5, 180, 0);
        }
        else
        {
            cam.transform.localPosition = CAM_DETRAS;
            cam.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            if (modelos) modelos.localRotation = Quaternion.identity;
        }
    }

    // ---------- hacia dónde mira cada modelo ----------
    // Sin dato en 'giros', se adivina por sus medidas: una figura es más ancha de hombro a hombro
    // que de frente a espalda, así que el frente queda hacia el lado delgado. (La regla vieja
    // estaba invertida porque se "probó" en la pantalla de elegir, donde el personaje gira solo.)
    void Orientar(GameObject holder, string modelo)
    {
        holder.transform.localRotation = Quaternion.identity;
        if (!giros.TryGetValue(modelo, out float giro))
            giro = Limites(holder, out var b) && b.size.x >= b.size.z ? 0f : 90f;
        holder.transform.localRotation = Quaternion.Euler(0, giro, 0);
    }

    // Tecla G: gira 90° el personaje que juegas (y los monstruos iguales) y lo guarda
    void GirarPersonaje()
    {
        string n = personajes[elegido];
        var holder = listaModelos[elegido];
        float actual = giros.TryGetValue(n, out var g) ? g : holder.transform.localEulerAngles.y;
        giros[n] = Mathf.Repeat(Mathf.Round(actual / 90f) * 90f + 90f, 360f);
        GuardarGiros();
        Reorientar(holder, jugador.transform, n);
        foreach (var (cuerpo, ancla) in cuerposEnemigos)
            if (cuerpo && cuerpo.name == n) Reorientar(cuerpo, ancla, n);
        Aviso($"{nombres[elegido]}: giro {giros[n]:0}   (G = girar otra vez)");
    }

    void Reorientar(GameObject holder, Transform ancla, string n)
    {
        var t = holder.transform;
        t.localPosition = Vector3.zero;
        t.localScale = Vector3.one;
        t.localRotation = Quaternion.Euler(0, giros[n], 0);
        NormalizarTamano(ancla, holder, 1.7f);
    }

    // Tinkercad viene gigante: deja el modelo de 'alto' de altura, centrado y con los pies en 'ancla'
    void NormalizarTamano(Transform ancla, GameObject modelo, float alto)
    {
        if (!Limites(modelo, out var b)) return;
        float original = b.size.y;
        if (original > 0.0001f) modelo.transform.localScale *= alto / original;
        Limites(modelo, out b);
        modelo.transform.position += new Vector3(
            ancla.position.x - b.center.x, ancla.position.y - b.min.y, ancla.position.z - b.center.z);
    }

    // La caja que ocupa el modelo, medida directo de las mallas (sirve aunque el modelo
    // se acabe de copiar y Unity aún no haya calculado sus Renderer.bounds)
    static bool Limites(GameObject g, out Bounds b)
    {
        b = default;
        bool hay = false;
        foreach (var mf in g.GetComponentsInChildren<MeshFilter>(true))
        {
            var m = mf.sharedMesh;
            if (m == null) continue;
            var lb = m.bounds;
            var M = mf.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                var esquina = lb.center + Vector3.Scale(lb.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var w = M.MultiplyPoint3x4(esquina);
                if (!hay) { b = new Bounds(w, Vector3.zero); hay = true; }
                else b.Encapsulate(w);
            }
        }
        return hay;
    }

    static void CapaRecursiva(GameObject g, int capa)
    {
        g.layer = capa;
        foreach (Transform t in g.transform) CapaRecursiva(t.gameObject, capa);
    }
}

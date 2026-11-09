using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.Video;

// Videos del canal «Patrimonio, Cultura y Memoria» dentro de la sala VIROO.
// En las gafas no se puede abrir YouTube: los videos se sirven como MP4 desde el servidor del aula
// (BASE_VIDEOS + id de YouTube + ".mp4") y se ven en una pantalla junto a su monumento.
// Apuntar a un botón y apretar el gatillo envía "v|memoria|video" por la red: arranca a la vez para
// todos. "v|memoria|-1" lo detiene. Quien entra tarde no lo ve arrancar (en la reconstrucción se omite).
public partial class Mundo
{
    const string BASE_VIDEOS = "https://aula.barneyconstruye.site/materiales/rv/videos/";

    class PantallaVideo
    {
        public int memoria;
        public Transform raiz;
        public VideoPlayer video;
        public AudioSource audio;
        public TextMeshPro estado;
        public int pedido = -1;              // el último video que se pidió en esta pantalla
        public List<(string texto, string url)> videos = new List<(string, string)>();
    }
    readonly Dictionary<int, PantallaVideo> pantallas = new Dictionary<int, PantallaVideo>();

    // marca los botones de las pantallas (los encuentra el rayo de la mano)
    public class BotonVideo : MonoBehaviour { public int memoria, video; }

    static string IdYouTube(string url)
    {
        var m = Regex.Match(url ?? "", @"(?:v=|youtu\.be/)([\w-]{11})");
        return m.Success ? m.Groups[1].Value : null;
    }

    // una pantalla por cada memoria que tenga videos; se ubica al costado de su punto de teletransporte
    void CrearPantallas()
    {
        foreach (var p in pantallas.Values) if (p.raiz) Destroy(p.raiz.gameObject);
        pantallas.Clear();
        for (int i = 0; i < cubos.Count; i++)
        {
            var m = cubos[i].m;
            if (m.enlaces == null) continue;
            var p = new PantallaVideo { memoria = i };
            foreach (var e in m.enlaces)
            {
                var id = IdYouTube(e.url);
                if (id != null) p.videos.Add((e.texto, BASE_VIDEOS + id + ".mp4"));
            }
            if (p.videos.Count == 0) continue;
            ArmarPantalla(p, cubos[i].lugar);
            pantallas[i] = p;
        }
    }

    void ArmarPantalla(PantallaVideo p, Vector3 cubo)
    {
        // el grupo llega 3 m hacia el centro del mapa desde el cubo (ver ColocarPuntosDeTeletransporte);
        // la pantalla queda 4,5 m a su derecha, mirándolo
        var haciaCentro = new Vector3(-cubo.x, 0, -cubo.z);
        if (haciaCentro.sqrMagnitude < 1) haciaCentro = Vector3.back;
        haciaCentro.Normalize();
        var llegada = cubo + haciaCentro * 3f;
        var derecha = Vector3.Cross(Vector3.up, -haciaCentro);
        var pos = llegada + derecha * 4.5f;
        int x = Mathf.RoundToInt(pos.x), z = Mathf.RoundToInt(pos.z);
        float suelo = terreno.AlturaSuelo(x, z);

        p.raiz = new GameObject("Pantalla_" + cubos[p.memoria].m.id).transform;
        p.raiz.SetParent(raiz, false);
        p.raiz.position = new Vector3(pos.x, suelo, pos.z);
        var mira = p.raiz.position - new Vector3(llegada.x, suelo, llegada.z);   // el frente del Quad mira a -Z
        mira.y = 0;
        p.raiz.rotation = Quaternion.LookRotation(mira.sqrMagnitude > 0.01f ? mira : Vector3.forward);

        // marco y pantalla
        var marcoPant = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(marcoPant.GetComponent<Collider>());
        marcoPant.transform.SetParent(p.raiz, false);
        marcoPant.transform.localPosition = new Vector3(0, 2.3f, 0.06f);
        marcoPant.transform.localScale = new Vector3(3.4f, 2.0f, 0.1f);
        marcoPant.GetComponent<Renderer>().sharedMaterial = MatColor(new Color(0.08f, 0.08f, 0.1f));

        var tela = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(tela.GetComponent<Collider>());
        tela.name = "Tela";
        tela.transform.SetParent(p.raiz, false);
        tela.transform.localPosition = new Vector3(0, 2.3f, 0);
        tela.transform.localScale = new Vector3(3.2f, 1.8f, 1);
        var matTela = new Material(Shaders.Buscar("Universal Render Pipeline/Unlit")) { color = new Color(0.02f, 0.02f, 0.03f) };
        var rend = tela.GetComponent<Renderer>();
        rend.sharedMaterial = matTela;

        // patas
        foreach (float lx in new[] { -1.4f, 1.4f })
        {
            var pata = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(pata.GetComponent<Collider>());
            pata.transform.SetParent(p.raiz, false);
            pata.transform.localPosition = new Vector3(lx, 0.65f, 0.08f);
            pata.transform.localScale = new Vector3(0.12f, 1.3f, 0.12f);
            pata.GetComponent<Renderer>().sharedMaterial = MatColor(new Color(0.15f, 0.15f, 0.17f));
        }

        // reproductor: la imagen va a la tela y el sonido sale de la pantalla (3D: se oye cerca)
        p.audio = p.raiz.gameObject.AddComponent<AudioSource>();
        p.audio.spatialBlend = 1f;
        p.audio.minDistance = 5f;
        p.audio.maxDistance = 35f;
        p.audio.rolloffMode = AudioRolloffMode.Linear;
        p.audio.playOnAwake = false;
        p.video = p.raiz.gameObject.AddComponent<VideoPlayer>();
        p.video.playOnAwake = false;
        p.video.source = VideoSource.Url;
        p.video.renderMode = VideoRenderMode.MaterialOverride;
        p.video.targetMaterialRenderer = rend;
        p.video.targetMaterialProperty = "_BaseMap";
        p.video.audioOutputMode = VideoAudioOutputMode.AudioSource;
        p.video.SetTargetAudioSource(0, p.audio);
        p.video.isLooping = false;
        p.video.errorReceived += (vp, msg) =>
        {
            Debug.LogWarning($"[Calicraft] Video {vp.url}: {msg}");
            if (p.estado) p.estado.text = "No se pudo cargar el video (¿hay internet en la sala?)";
        };
        p.video.loopPointReached += _ => { if (p.estado) p.estado.text = ""; };
        p.video.prepareCompleted += vp =>
        {
            if (p.pedido < 0) return;           // lo detuvieron mientras cargaba
            vp.Play();
            if (p.estado) p.estado.text = p.videos[p.pedido].texto;
        };

        // texto de estado sobre la pantalla
        p.estado = Texto3D("Estado", p.raiz, 0.12f, new Vector2(3.2f, 0.3f), TextAlignmentOptions.Center);
        p.estado.transform.localPosition = new Vector3(0, 3.45f, -0.02f);
        p.estado.transform.localRotation = Quaternion.identity;
        p.estado.text = "";

        // botones: uno por video y uno para detener
        int n = p.videos.Count + 1;
        float ancho = Mathf.Min(1.5f, 3.3f / n - 0.08f);
        for (int k = 0; k < n; k++)
        {
            bool detener = k == p.videos.Count;
            string texto = detener ? "Detener" : p.videos[k].texto;
            float lx = -((n - 1) * (ancho + 0.08f)) / 2 + k * (ancho + 0.08f);
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);   // con colisionador: lo encuentra el rayo
            b.name = "Boton_" + k;
            b.transform.SetParent(p.raiz, false);
            b.transform.localPosition = new Vector3(lx, 1.0f, -0.05f);
            b.transform.localScale = new Vector3(ancho, 0.32f, 0.06f);
            b.GetComponent<Renderer>().sharedMaterial = MatColor(detener ? new Color(0.25f, 0.25f, 0.3f) : new Color(0.78f, 0.06f, 0.18f), 0.3f);
            var bv = b.AddComponent<BotonVideo>();
            bv.memoria = p.memoria;
            bv.video = detener ? -1 : k;
            var t = Texto3D("Texto", p.raiz, 0.075f, new Vector2(ancho - 0.06f, 0.3f), TextAlignmentOptions.Center);
            t.transform.localPosition = new Vector3(lx, 1.0f, -0.09f);
            t.transform.localRotation = Quaternion.identity;
            t.text = detener ? "Detener" : texto;
            t.color = Color.white;
        }
    }

    // el gatillo sobre un botón de pantalla: devuelve true si había uno
    bool TocarBotonVideo(Ray r)
    {
        if (!ApuntarMundo(r, out var hit)) return false;
        var b = hit.collider.GetComponent<BotonVideo>();
        if (b == null) return false;
        Enviar($"v|{b.memoria}|{b.video}");
        return true;
    }

    // llega por la red a todos
    void ReproducirVideo(int memoria, int k)
    {
        if (silencio) return;                              // quien entra tarde no ve arrancar videos viejos
        if (!pantallas.TryGetValue(memoria, out var p)) return;
        p.video.Stop();
        if (k < 0 || k >= p.videos.Count)
        {
            p.pedido = -1;
            p.estado.text = "";
            return;
        }
        p.pedido = k;
        p.video.url = p.videos[k].url;
        p.estado.text = "Cargando: " + p.videos[k].texto + "…";
        p.video.Prepare();                      // al terminar de cargar arranca (prepareCompleted)
        Sonar("clic");
    }
}

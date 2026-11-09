using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Escena de cierre: fondo claro, audio ambiente (viento del altiplano + gallina lejana + olla hirviendo suave)
// y un texto NEGRO que aparece poco a poco, linea por linea, durante `duracionTexto` segundos.
// Despues, fade out total (texto, fondo -> negro y audio).
// Todo se crea por codigo al dar Play: solo hace falta este script en un objeto de la escena.
[DisallowMultipleComponent]
public class EscenaTextoFinal : MonoBehaviour
{
    [Header("Texto (una linea por elemento)")]
    [SerializeField, TextArea(1, 3)] private string[] lineas =
    {
        "Algunas cosas se desordenan.",
        "Otras permanecen en las manos.",
        "Mientras haya manos.",
    };
    [Tooltip("Segundos desde que empieza a aparecer la primera linea hasta que la ultima ya esta completa.")]
    [SerializeField, Min(1f)] private float duracionTexto = 10f;
    [Tooltip("Segundos que tarda cada linea en aparecer.")]
    [SerializeField, Min(0.1f)] private float duracionAparicion = 2f;
    [Tooltip("Segundos que se queda todo quieto antes del fade out.")]
    [SerializeField, Min(0f)] private float pausaFinal = 1.5f;
    [SerializeField, Min(0.1f)] private float duracionFadeOut = 3f;
    [Tooltip("Segundos de silencio visual antes de la primera linea.")]
    [SerializeField, Min(0f)] private float esperaInicial = 1f;

    [Header("Aspecto")]
    [SerializeField] private Color colorTexto = Color.black;
    [Tooltip("Color de fondo (claro para que se lea el texto negro).")]
    [SerializeField] private Color colorFondo = new Color(0.96f, 0.93f, 0.86f, 1f);
    [Tooltip("Opcional. Si esta vacia se usa la fuente por defecto de Unity.")]
    [SerializeField] private Font fuente;
    [SerializeField, Min(10)] private int tamanoLetra = 80;
    [SerializeField] private FontStyle estiloLetra = FontStyle.Bold;
    [SerializeField, Min(0.5f)] private float distanciaTexto = 2f;
    [SerializeField, Min(0f)] private float separacionLineas = 130f;

    [Header("Audio (en loop)")]
    [Tooltip("Viento del altiplano. Se usa SONIDO AMBIENTE.")]
    [SerializeField] private AudioClip viento;
    [SerializeField, Range(0f, 1f)] private float volumenViento = 0.6f;
    [Tooltip("Gallina a lo lejos. Se usa Gallinas.")]
    [SerializeField] private AudioClip gallina;
    [SerializeField, Range(0f, 1f)] private float volumenGallina = 1f;
    [Tooltip("Olla hirviendo suavemente. Se usa COCINANDO (AGUA Y FUEGO).")]
    [SerializeField] private AudioClip olla;
    [SerializeField, Range(0f, 1f)] private float volumenOlla = 0.12f;
    [SerializeField, Min(0f)] private float duracionEntradaAudio = 2f;

    [Header("Al terminar")]
    [Tooltip("Opcional: escena a cargar cuando termina el fade out. Vacio = se queda en negro.")]
    [SerializeField] private string escenaSiguiente = "";

    private Camera cam;
    private Text[] textos;
    private AudioSource[] fuentes;
    private float[] volumenes;

    private void Start()
    {
        cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
        if (cam == null) { Debug.LogError("EscenaTextoFinal: no hay camara en la escena.", this); return; }

        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = colorFondo;

        CrearTextos();
        CrearAudio();
        StartCoroutine(Secuencia());
    }

    private void CrearTextos()
    {
        var canvasGO = new GameObject("Canvas Texto Final");
        canvasGO.transform.SetParent(cam.transform, false);
        canvasGO.transform.localPosition = new Vector3(0f, 0f, distanciaTexto);
        canvasGO.transform.localRotation = Quaternion.identity;
        canvasGO.transform.localScale = Vector3.one * 0.001f;
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = cam;
        canvasGO.GetComponent<RectTransform>().sizeDelta = new Vector2(1920f, 1080f);

        Font f = fuente != null ? fuente : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        textos = new Text[lineas.Length];
        float y0 = (lineas.Length - 1) * 0.5f * separacionLineas;
        for (int i = 0; i < lineas.Length; i++)
        {
            var go = new GameObject("Linea " + (i + 1));
            go.transform.SetParent(canvasGO.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(1700f, separacionLineas);
            rt.anchoredPosition = new Vector2(0f, y0 - i * separacionLineas);
            var t = go.AddComponent<Text>();
            t.font = f;
            t.fontSize = tamanoLetra;
            t.fontStyle = estiloLetra;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = lineas[i];
            Color c = colorTexto; c.a = 0f;
            t.color = c;
            textos[i] = t;
        }
    }

    private void CrearAudio()
    {
        var clips = new[] { viento, gallina, olla };
        var vols = new[] { volumenViento, volumenGallina, volumenOlla };
        var nombres = new[] { "Viento", "Gallina lejana", "Olla hirviendo" };
        fuentes = new AudioSource[3];
        volumenes = vols;
        for (int i = 0; i < 3; i++)
        {
            var go = new GameObject("Audio " + nombres[i]);
            go.transform.SetParent(transform, false);
            var a = go.AddComponent<AudioSource>();
            a.clip = clips[i];
            a.loop = true;
            a.spatialBlend = 0f;
            a.playOnAwake = false;
            a.volume = 0f;
            if (i == 1)
            {
                // "A lo lejos": se apagan los agudos.
                var lp = go.AddComponent<AudioLowPassFilter>();
                lp.cutoffFrequency = 3200f;
            }
            fuentes[i] = a;
            if (clips[i] != null) a.Play();
            else Debug.LogWarning("EscenaTextoFinal: falta el audio de " + nombres[i], this);
        }
    }

    private IEnumerator Secuencia()
    {
        // Entrada suave del audio durante toda la secuencia inicial.
        StartCoroutine(MoverVolumen(0f, 1f, duracionEntradaAudio));

        yield return new WaitForSeconds(esperaInicial);

        int n = textos.Length;
        float intervalo = n > 1 ? Mathf.Max(0f, (duracionTexto - duracionAparicion) / (n - 1)) : 0f;
        for (int i = 0; i < n; i++)
        {
            StartCoroutine(AparecerLinea(textos[i], duracionAparicion));
            if (i < n - 1) yield return new WaitForSeconds(intervalo);
        }
        yield return new WaitForSeconds(duracionAparicion + pausaFinal);

        // Fade out total: texto, fondo a negro y audio.
        StartCoroutine(MoverVolumen(1f, 0f, duracionFadeOut));
        Color fondoIni = cam.backgroundColor;
        for (float t = 0f; t < duracionFadeOut; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / duracionFadeOut);
            cam.backgroundColor = Color.Lerp(fondoIni, Color.black, k);
            foreach (Text tx in textos)
            {
                Color c = tx.color; c.a = 1f - k; tx.color = c;
            }
            yield return null;
        }
        cam.backgroundColor = Color.black;
        foreach (Text tx in textos) { Color c = tx.color; c.a = 0f; tx.color = c; }
        foreach (AudioSource a in fuentes) a.volume = 0f;

        string destino = escenaSiguiente == null ? "" : escenaSiguiente.Trim();
        if (destino.Length > 0 && Application.CanStreamedLevelBeLoaded(destino))
            SceneManager.LoadScene(destino);
    }

    private IEnumerator AparecerLinea(Text t, float segundos)
    {
        for (float s = 0f; s < segundos; s += Time.deltaTime)
        {
            Color c = t.color; c.a = Mathf.SmoothStep(0f, 1f, s / segundos); t.color = c;
            yield return null;
        }
        Color f = t.color; f.a = 1f; t.color = f;
    }

    private IEnumerator MoverVolumen(float desde, float hasta, float segundos)
    {
        for (float s = 0f; s < segundos; s += Time.deltaTime)
        {
            Aplicar(Mathf.Lerp(desde, hasta, s / segundos));
            yield return null;
        }
        Aplicar(hasta);
    }

    private void Aplicar(float k)
    {
        for (int i = 0; i < fuentes.Length; i++)
            if (fuentes[i] != null) fuentes[i].volume = volumenes[i] * k;
    }
}

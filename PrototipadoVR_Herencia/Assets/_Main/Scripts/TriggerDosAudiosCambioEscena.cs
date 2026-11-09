using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Usar en un Empty independiente. Admite un Collider 3D existente.
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public class TriggerDosAudiosCambioEscena : MonoBehaviour
{
    [Header("Deteccion")]
    [SerializeField] private string tagJugador = "Player";

    [Header("Audios en orden")]
    [SerializeField] private AudioClip primerAudio;
    [SerializeField] private AudioClip segundoAudio;

    [Header("Escena de destino")]
    [Tooltip("Nombre exacto sin .unity, o ruta completa de la escena incluida en Build Profiles.")]
    [SerializeField] private string nombreEscenaDestino;

    [Header("Fade out antes del cambio de escena")]
    [Tooltip("Global Volume de la escena. Si se deja vacio se busca el primer Volume. No se modifica el Volume Profile guardado: el fade se hace sobre una copia en memoria.")]
    [SerializeField] private Volume volumeFade;
    [Tooltip("Segundos del fade a negro al terminar el segundo audio. 0 = sin fade.")]
    [SerializeField, Min(0f)] private float duracionFadeOut = 2f;
    [Tooltip("Baja tambien el volumen de todo el audio de la escena durante el fade.")]
    [SerializeField] private bool bajarAudioGlobal = true;

    [Header("Bloqueo de repeticiones")]
    [Tooltip("Las copias de esta misma zona comparten ID. Usa otro ID para una secuencia independiente en la misma escena.")]
    [SerializeField] private string idInteraccion = "DosAudiosCambioEscena";

    private AudioSource fuenteAudio;
    private Collider zona;
    private Coroutine secuencia;
    private bool iniciada;
    private bool avisoConfiguracion;
    private static readonly HashSet<string> interaccionesIniciadas = new HashSet<string>();
    private static float volumenGlobalPrevio = 1f;

    public bool YaIniciada => iniciada;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ReiniciarBloqueosAlComenzarPartida()
    {
        // Solo al comenzar Play/la aplicacion. No al salir ni al recargar escenas.
        interaccionesIniciadas.Clear();
    }

    private void Awake()
    {
        fuenteAudio = GetComponent<AudioSource>();
        fuenteAudio.Stop();
        fuenteAudio.playOnAwake = false;
        fuenteAudio.loop = false;
        fuenteAudio.spatialBlend = 0f;
        fuenteAudio.spatialize = false;
        fuenteAudio.ignoreListenerPause = false;

        zona = GetComponent<Collider>();
        if (zona == null) zona = gameObject.AddComponent<BoxCollider>();
        if (zona is MeshCollider malla && !malla.convex)
        {
            Debug.LogError("Usa un Box Collider o un Mesh Collider convexo para esta zona.", this);
            enabled = false;
            return;
        }
        zona.isTrigger = true;
        if (zona.attachedRigidbody == null)
        {
            Rigidbody cuerpo = gameObject.AddComponent<Rigidbody>();
            cuerpo.useGravity = false;
            cuerpo.isKinematic = true;
        }
    }

    private void OnTriggerEnter(Collider other) { IntentarIniciar(other); }
    private void OnTriggerStay(Collider other) { IntentarIniciar(other); }

    private void IntentarIniciar(Collider other)
    {
        if (!isActiveAndEnabled || iniciada || !EsJugador(other)) return;
        if (!ConfiguracionValida()) return;

        string escenaOrigen = string.IsNullOrEmpty(gameObject.scene.path)
            ? gameObject.scene.name : gameObject.scene.path;
        string id = string.IsNullOrWhiteSpace(idInteraccion)
            ? "DosAudiosCambioEscena" : idInteraccion.Trim();

        // Bloquea ANTES de reproducir: funciona con varios colliders del Player
        // y con otras copias del script que tengan el mismo ID en esta escena.
        iniciada = true;
        if (!interaccionesIniciadas.Add(escenaOrigen + "::" + id)) return;

        // Apaga solo el collider; el Empty sigue activo para completar los audios.
        zona.enabled = false;
        secuencia = StartCoroutine(ReproducirYCambiarEscena(nombreEscenaDestino.Trim()));
    }

    private bool EsJugador(Collider other)
    {
        if (other == null || string.IsNullOrWhiteSpace(tagJugador)) return false;
        for (Transform actual = other.transform; actual != null; actual = actual.parent)
            if (actual.CompareTag(tagJugador)) return true;
        return false;
    }

    private bool ConfiguracionValida()
    {
        string error = null;
        if (primerAudio == null || segundoAudio == null)
            error = "Asigna Primer Audio y Segundo Audio en el Inspector.";
        else if (string.IsNullOrWhiteSpace(nombreEscenaDestino) ||
                 !Application.CanStreamedLevelBeLoaded(nombreEscenaDestino.Trim()))
            error = "Revisa Nombre Escena Destino y agrega esa escena a la Scene List del Build Profile activo.";

        if (error == null) return true;
        if (!avisoConfiguracion) Debug.LogError(error, this);
        avisoConfiguracion = true;
        return false;
    }

    private IEnumerator ReproducirYCambiarEscena(string escenaDestino)
    {
        yield return ReproducirCompleto(primerAudio);
        yield return ReproducirCompleto(segundoAudio);

        // Al terminar el segundo audio: carga la escena por detras, hace el fade a negro
        // y solo entonces la activa.
        AsyncOperation carga = SceneManager.LoadSceneAsync(escenaDestino, LoadSceneMode.Single);
        carga.allowSceneActivation = false;
        yield return FadeOut();
        carga.allowSceneActivation = true;
        secuencia = null;
    }

    private IEnumerator FadeOut()
    {
        if (duracionFadeOut <= 0f) yield break;

        // Fade con el Global Volume: Color Adjustments > Color Filter de blanco a negro.
        ColorAdjustments ajuste = null;
        Volume vol = volumeFade != null ? volumeFade : FindFirstObjectByType<Volume>();
        if (vol != null)
        {
            vol.enabled = true;
            vol.weight = 1f;
            VolumeProfile perfil = vol.profile; // copia en memoria, no toca el asset
            if (!perfil.TryGet(out ajuste)) ajuste = perfil.Add<ColorAdjustments>(true);
            ajuste.active = true;
            ajuste.colorFilter.overrideState = true;
            ajuste.colorFilter.value = Color.white;
        }
        else Debug.LogWarning("TriggerDosAudiosCambioEscena: no hay Volume en la escena, el fade no se vera.", this);

        if (bajarAudioGlobal)
        {
            volumenGlobalPrevio = AudioListener.volume;
            SceneManager.sceneLoaded += RestaurarAudioGlobal;
        }

        float inicio = AudioListener.volume;
        for (float t = 0f; t < duracionFadeOut; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / duracionFadeOut);
            if (ajuste != null) ajuste.colorFilter.value = Color.Lerp(Color.white, Color.black, k);
            if (bajarAudioGlobal) AudioListener.volume = Mathf.Lerp(inicio, 0f, k);
            yield return null;
        }
        if (ajuste != null) ajuste.colorFilter.value = Color.black;
        if (bajarAudioGlobal) AudioListener.volume = 0f;
    }

    private static void RestaurarAudioGlobal(Scene escena, LoadSceneMode modo)
    {
        AudioListener.volume = volumenGlobalPrevio;
        SceneManager.sceneLoaded -= RestaurarAudioGlobal;
    }

    private IEnumerator ReproducirCompleto(AudioClip clip)
    {
        fuenteAudio.enabled = true;
        fuenteAudio.loop = false;
        fuenteAudio.pitch = 1f;
        fuenteAudio.dopplerLevel = 0f;
        fuenteAudio.clip = clip;
        fuenteAudio.Play();

        // El reloj de audio no depende de Time.timeScale y se detiene cuando
        // AudioListener.pause pausa el sistema. El margen evita avanzar si
        // isPlaying tarda un frame en reflejar el inicio de la reproduccion.
        double finMinimo = AudioSettings.dspTime + (double)clip.samples / clip.frequency;
        do { yield return null; }
        while (fuenteAudio.isPlaying || AudioListener.pause || AudioSettings.dspTime < finMinimo);
    }

    private void OnDisable()
    {
        if (secuencia != null) StopCoroutine(secuencia);
        secuencia = null;
        if (fuenteAudio != null) fuenteAudio.Stop();
        // El bloqueo permanece: reactivar el objeto no vuelve a iniciar los audios.
    }
}

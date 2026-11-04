using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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

    [Header("Bloqueo de repeticiones")]
    [Tooltip("Las copias de esta misma zona comparten ID. Usa otro ID para una secuencia independiente en la misma escena.")]
    [SerializeField] private string idInteraccion = "DosAudiosCambioEscena";

    private AudioSource fuenteAudio;
    private Collider zona;
    private Coroutine secuencia;
    private bool iniciada;
    private bool avisoConfiguracion;
    private static readonly HashSet<string> interaccionesIniciadas = new HashSet<string>();

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

        // Sin espera adicional: inicia el cambio al terminar el segundo audio.
        SceneManager.LoadSceneAsync(escenaDestino, LoadSceneMode.Single);
        secuencia = null;
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

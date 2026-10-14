using System.Collections;
using Oculus.Interaction;
using UnityEngine;

// Anadir al punto de encaje o a un Empty activo; no modifica SnapInteractable.
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public class SnapAudioCambioEscena : MonoBehaviour
{
    [Header("Snap de Meta que inicia la secuencia")]
    [SerializeField] private SnapInteractable snapInteractable;

    [Header("Audio que debe terminar antes del fundido")]
    [SerializeField] private AudioClip audioDelSnap;

    [Header("Controlador de la transicion")]
    [SerializeField] private TransicionEscenaConFade transicion;
    [SerializeField] private bool mostrarMensajes = true;

    private AudioSource fuenteAudio;
    private SnapInteractable snapObservado;
    private IInteractableView vistaSnap;
    private Coroutine rutina;
    private bool iniciada;

    private void Reset()
    {
        snapInteractable = GetComponent<SnapInteractable>();
    }

    private void Awake()
    {
        fuenteAudio = GetComponent<AudioSource>();
        fuenteAudio.playOnAwake = false;
        fuenteAudio.loop = false;
        fuenteAudio.spatialBlend = 0f;
        if (snapInteractable == null) snapInteractable = GetComponent<SnapInteractable>();
    }

    private void OnEnable()
    {
        snapObservado = snapInteractable;
        if (snapObservado == null) return;
        vistaSnap = snapObservado;
        vistaSnap.WhenSelectingInteractorViewAdded += AlHacerSnap;
    }

    private void Start()
    {
        if (snapInteractable == null)
        {
            Debug.LogError("Asigna el SnapInteractable del punto de encaje.", this);
            return;
        }
        // Tambien contempla un objeto que ya estaba encajado al iniciar.
        if (vistaSnap != null)
        {
            foreach (IInteractorView interactor in vistaSnap.SelectingInteractorViews)
            {
                IniciarAudioSnap();
                break;
            }
        }
    }

    private void AlHacerSnap(IInteractorView interactor)
    {
        IniciarAudioSnap();
    }

    // Puede llamarse desde el evento On Select de un wrapper de Meta.
    public void IniciarAudioSnap()
    {
        if (!isActiveAndEnabled || iniciada) return;
        if (audioDelSnap == null || audioDelSnap.samples <= 0 || audioDelSnap.frequency <= 0 || transicion == null)
        {
            Debug.LogError("Asigna Audio Del Snap y Transicion en SnapAudioCambioEscena.", this);
            return;
        }
        if (!transicion.ValidarConfiguracion()) return;

        iniciada = true;
        rutina = StartCoroutine(ReproducirYEsperar());
    }

    private IEnumerator ReproducirYEsperar()
    {
        fuenteAudio.Stop();
        fuenteAudio.enabled = true;
        fuenteAudio.loop = false;
        fuenteAudio.pitch = 1f;
        fuenteAudio.dopplerLevel = 0f;
        fuenteAudio.ignoreListenerPause = false;
        fuenteAudio.clip = audioDelSnap;

        double inicio = AudioSettings.dspTime + 0.1;
        double final = inicio + (double)audioDelSnap.samples / audioDelSnap.frequency;
        fuenteAudio.PlayScheduled(inicio);
        if (mostrarMensajes) Debug.Log("[SnapAudioCambioEscena] Snap detectado: reproduciendo audio.", this);

        // Se mantiene en este estado hasta terminar TODO el clip.
        // El reloj de audio se pausa si se pausa el sistema de audio.
        while (AudioSettings.dspTime < final) yield return null;

        if (mostrarMensajes) Debug.Log("[SnapAudioCambioEscena] Audio terminado: iniciar fundido y cambio de escena.", this);
        rutina = null;
        if (transicion != null) transicion.CambiarEscena();
    }

    private void OnDisable()
    {
        if (snapObservado != null && vistaSnap != null)
            vistaSnap.WhenSelectingInteractorViewAdded -= AlHacerSnap;
        snapObservado = null;
        vistaSnap = null;

        if (rutina != null)
        {
            StopCoroutine(rutina);
            rutina = null;
            if (fuenteAudio != null) fuenteAudio.Stop();
            iniciada = false;
        }
    }
}

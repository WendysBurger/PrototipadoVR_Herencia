using UnityEngine;

// Colocar en el objeto con el Collider 3D marcado como Is Trigger.
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public class cuchara : MonoBehaviour
{
    [Header("Jugador")]
    [SerializeField] private string tagJugador = "Player";

    [Header("Audio al entrar")]
    [SerializeField] private AudioClip audio;

    [Header("Animacion opcional, al mismo tiempo que el audio")]
    [SerializeField] private bool activarAnimacion = false;
    [SerializeField] private Animator animador;
    [Tooltip("Parametro de tipo Trigger en el Animator Controller.")]
    [SerializeField] private string nombreTrigger = "Activar";

    private AudioSource fuenteAudio;
    private bool activado;

    private void Awake()
    {
        fuenteAudio = GetComponent<AudioSource>();
        fuenteAudio.playOnAwake = false;
        fuenteAudio.loop = false;
        fuenteAudio.spatialBlend = 0f; // Audio 2D para escucharlo en las gafas.
        fuenteAudio.spatialize = false;
        fuenteAudio.Stop();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isActiveAndEnabled || activado || !EsJugador(other)) return;
        if (audio == null)
        {
            Debug.LogError("TriggerAudioAnimacionOpcional: asigna el Audio.", this);
            return;
        }

        // Marcar antes de reproducir evita duplicados por varios colliders del Player.
        activado = true;
        fuenteAudio.enabled = true;
        fuenteAudio.loop = false;
        fuenteAudio.clip = audio;
        fuenteAudio.Play();

        // El audio ya empezo: ninguna referencia del Animator es un requisito.
        IntentarActivarAnimacion();
    }

    private void IntentarActivarAnimacion()
    {
        if (!activarAnimacion) return;

        if (animador != null && animador.isActiveAndEnabled &&
            animador.runtimeAnimatorController != null &&
            !string.IsNullOrWhiteSpace(nombreTrigger))
        {
            foreach (AnimatorControllerParameter parametro in animador.parameters)
            {
                if (parametro.name != nombreTrigger ||
                    parametro.type != AnimatorControllerParameterType.Trigger) continue;

                animador.ResetTrigger(nombreTrigger);
                animador.SetTrigger(nombreTrigger);
                return;
            }
        }

        Debug.LogWarning("Animacion omitida: revisa el Animator activo, su Controller y el parametro Trigger. El audio se reproduce igualmente.", this);
    }

    private bool EsJugador(Collider other)
    {
        if (other == null || string.IsNullOrWhiteSpace(tagJugador)) return false;
        // Acepta tambien un collider hijo de un objeto con tag Player.
        for (Transform actual = other.transform; actual != null; actual = actual.parent)
            if (actual.CompareTag(tagJugador)) return true;
        return false;
    }
}

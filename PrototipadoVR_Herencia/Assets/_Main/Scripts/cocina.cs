using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Usar en un GameObject con un Collider 3D marcado como Is Trigger.
[RequireComponent(typeof(AudioSource))]
public class cocina: MonoBehaviour
{
    public enum TipoDeteccion { Tag, Layer }

    [Header("Deteccion del jugador")]
    [SerializeField] private TipoDeteccion tipoDeteccion = TipoDeteccion.Tag;
    [SerializeField] private string tagJugador = "Player";
    [SerializeField] private LayerMask capasJugador;

    [Header("Audios en orden de estados")]
    [SerializeField] private AudioClip[] audios;

    [Header("Progreso (0 = primer audio)")]
    [SerializeField, Min(0)] private int estadoActual = 0;

    [Header("Tiempo de cambio (excepto del audio 2 al 3)")]
    [Tooltip("El audio 2 empieza tras este tiempo. El audio 3 espera a que termine el audio 2.")]
    [SerializeField, Min(0.01f)] private float tiempoCambioEstado = 2f;

    private AudioSource fuenteAudio;
    private Coroutine avancePendiente;
    private readonly HashSet<Collider> collidersDentro = new HashSet<Collider>();

    public int EstadoActual => estadoActual;

    private void Awake()
    {
        fuenteAudio = GetComponent<AudioSource>();
        fuenteAudio.playOnAwake = false;
        fuenteAudio.loop = false;
        estadoActual = Mathf.Max(0, estadoActual);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isActiveAndEnabled || !EsJugador(other)) return;

        // Evita avanzar varias veces si el jugador tiene varios colliders.
        if (!collidersDentro.Add(other) || collidersDentro.Count > 1) return;

        // Una sola entrada inicia toda la secuencia.
        if (avancePendiente != null) return;
        if (audios == null || estadoActual >= audios.Length) return;

        if (audios[estadoActual] == null)
        {
            Debug.LogWarning($"Falta asignar el audio del estado {estadoActual}.", this);
            return;
        }

        avancePendiente = StartCoroutine(ReproducirSecuencia());
    }

    private IEnumerator ReproducirSecuencia()
    {
        // Continua automaticamente, sin necesitar salir y volver al trigger.
        while (audios != null && estadoActual < audios.Length)
        {
            AudioClip siguienteAudio = audios[estadoActual];
            if (siguienteAudio == null)
            {
                Debug.LogWarning($"Falta asignar el audio del estado {estadoActual}.", this);
                break;
            }

            // Reemplaza el audio anterior para evitar que se superpongan.
            fuenteAudio.Stop();
            fuenteAudio.clip = siguienteAudio;
            fuenteAudio.Play();

            if (estadoActual == 1)
            {
                // Indice 1 = audio 2. No avanzar al audio 3 hasta que termine.
                // Esperar al menos un frame tras Play antes de consultar isPlaying.
                do
                {
                    yield return null;
                }
                while (fuenteAudio.isPlaying);
            }
            else
            {
                // Los demas cambios conservan la espera en segundos reales.
                yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, tiempoCambioEstado));
            }
            estadoActual++;
        }

        // El ultimo audio sigue sonando hasta terminar. No reinicia la lista.
        avancePendiente = null;
    }

    private void OnTriggerExit(Collider other)
    {
        collidersDentro.Remove(other);
    }

    private bool EsJugador(Collider other)
    {
        // Tambien revisa los padres, por si el collider esta en un hijo.
        for (Transform actual = other.transform; actual != null; actual = actual.parent)
        {
            if (tipoDeteccion == TipoDeteccion.Tag)
            {
                if (actual.CompareTag(tagJugador)) return true;
            }
            else if ((capasJugador.value & (1 << actual.gameObject.layer)) != 0)
            {
                return true;
            }
        }

        return false;
    }

    private void OnDisable()
    {
        if (avancePendiente != null)
        {
            StopCoroutine(avancePendiente);
            avancePendiente = null;
        }
        collidersDentro.Clear();
        if (fuenteAudio != null) fuenteAudio.Stop();
    }
}

using System.Collections.Generic;
using UnityEngine;

// Usar en un GameObject con un Collider 3D marcado como Is Trigger.
[RequireComponent(typeof(AudioSource))]
public class AudioTriggerPorEstados : MonoBehaviour
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

    private AudioSource fuenteAudio;
    private readonly HashSet<Collider> collidersDentro = new HashSet<Collider>();

    public int EstadoActual => estadoActual;

    private void Awake()
    {
        fuenteAudio = GetComponent<AudioSource>();
        fuenteAudio.playOnAwake = false;
        fuenteAudio.loop = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isActiveAndEnabled || !EsJugador(other)) return;

        // Evita avanzar varias veces si el jugador tiene varios colliders.
        if (!collidersDentro.Add(other) || collidersDentro.Count > 1) return;

        // Si entra mientras suena un audio, debe salir y volver a entrar.
        if (fuenteAudio.isPlaying) return;
        if (audios == null || estadoActual >= audios.Length) return;

        AudioClip siguienteAudio = audios[estadoActual];
        if (siguienteAudio == null)
        {
            Debug.LogWarning($"Falta asignar el audio del estado {estadoActual}.", this);
            return;
        }

        fuenteAudio.clip = siguienteAudio;
        fuenteAudio.Play();

        // Prepara el siguiente estado inmediatamente, sin reproducirlo aun.
        estadoActual++;
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
        collidersDentro.Clear();
    }
}
using System.Collections.Generic;
using UnityEngine;

// Unity 6. Colocar en un Empty con Box Collider 3D, separado del Player.
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource), typeof(BoxCollider))]
public class InteraccionUnEstado : MonoBehaviour
{
    [Header("Deteccion")]
    [SerializeField] private string tagJugador = "Player";

    [Header("Un solo audio, una sola vez")]
    [SerializeField] private AudioClip audio;
    [Tooltip("Las copias de una misma interaccion comparten ID. Usa otro ID para cada interaccion independiente en esta escena.")]
    [SerializeField] private string idInteraccion = "InteraccionUnEstado";

    private AudioSource fuenteAudio;
    private bool iniciada;
    private bool avisoAudio;
    private static readonly HashSet<string> interaccionesEjecutadas = new HashSet<string>();

    public bool YaEjecutada => iniciada;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ReiniciarAlComenzarPartida()
    {
        // No se libera al salir, terminar el audio, desactivar ni recargar la escena.
        interaccionesEjecutadas.Clear();
    }

    private void Awake()
    {
        fuenteAudio = GetComponent<AudioSource>();
        fuenteAudio.Stop();
        fuenteAudio.playOnAwake = false;
        fuenteAudio.loop = false;
        fuenteAudio.spatialBlend = 0f;
        fuenteAudio.spatialize = false;
        GetComponent<BoxCollider>().isTrigger = true;
        if (GetComponent<BoxCollider>().attachedRigidbody == null)
        {
            Rigidbody cuerpo = gameObject.AddComponent<Rigidbody>();
            cuerpo.useGravity = false;
            cuerpo.isKinematic = true;
        }
    }

    private void OnTriggerEnter(Collider other) { ProcesarContacto(other); }
    private void OnTriggerStay(Collider other) { ProcesarContacto(other); }
    private void OnCollisionEnter(Collision contacto) { ProcesarContacto(contacto.collider); }

    private void ProcesarContacto(Collider other)
    {
        if (!isActiveAndEnabled || iniciada || !EsJugador(other)) return;
        EjecutarUnaVez();
    }

    private bool EsJugador(Collider other)
    {
        if (other == null || string.IsNullOrWhiteSpace(tagJugador)) return false;
        for (Transform actual = other.transform; actual != null; actual = actual.parent)
            if (actual.CompareTag(tagJugador)) return true;
        return false;
    }

    private void EjecutarUnaVez()
    {
        if (iniciada) return;
        if (audio == null)
        {
            if (!avisoAudio) Debug.LogWarning("Asigna el Audio de esta interaccion en el Inspector.", this);
            avisoAudio = true;
            return;
        }
        string escena = string.IsNullOrEmpty(gameObject.scene.path) ? gameObject.scene.name : gameObject.scene.path;
        string id = string.IsNullOrWhiteSpace(idInteraccion) ? "InteraccionUnEstado" : idInteraccion.Trim();

        // Bloquear ANTES de reproducir: varios colliders o copias
        // con el mismo ID no pueden iniciar este audio en el mismo frame.
        iniciada = true;
        if (!interaccionesEjecutadas.Add(escena + "::" + id)) return;

        fuenteAudio.enabled = true;
        fuenteAudio.clip = audio;
        fuenteAudio.loop = false;
        fuenteAudio.Play();
    }

    private void OnDisable()
    {
        if (fuenteAudio != null) fuenteAudio.Stop();
        // No reiniciar iniciada ni retirar el ID: la interaccion sigue consumida.
    }


}

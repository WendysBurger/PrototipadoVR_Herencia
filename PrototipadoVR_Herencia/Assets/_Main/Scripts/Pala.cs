using System.Collections;
using UnityEngine;

// Usar en un Empty con Collider 3D marcado como Is Trigger y un AudioSource propio.
[RequireComponent(typeof(AudioSource))]
public class pala : MonoBehaviour
{
    public enum Estado
    {
        EsperandoJugador = 0,
        Estado1 = 1,
        Estado2 = 2,
        Estado3 = 3
    }

    [Header("Deteccion")]
    [SerializeField] private string tagJugador = "Player";

    [Header("Audios de cada estado")]
    [SerializeField] private AudioClip audioEstado1;
    [SerializeField] private AudioClip audioEstado2;
    [SerializeField] private AudioClip audioEstado3;

    [Header("Espera desde el inicio del audio 1")]
    [SerializeField, Min(0f)] private float esperaEstado2 = 20f;

    [Header("Efectos ocultos hasta el estado 3")]
    [Tooltip("ParticleSystem de un objeto separado. Se desactiva su GameObject al iniciar.")]
    [SerializeField] private ParticleSystem sistemaParticulas;
    [Tooltip("Renderer del objeto que contiene el material original y el aura adicional.")]
    [SerializeField] private Renderer rendererObjeto;
    [Tooltip("Indice del material adicional del aura. Element 1 corresponde al indice 1.")]
    [SerializeField, Min(1)] private int indiceMaterialAura = 1;

    [Header("Estado durante la ejecucion")]
    [SerializeField] private Estado estadoActual = Estado.EsperandoJugador;
    [SerializeField] private bool mostrarMensajes = true;

    private AudioSource fuenteAudio;
    private Coroutine secuencia;
    private bool iniciada;
    private bool configuracionValida;
    private Material[] materialesOriginales;
    private bool auraOculta;

    public Estado EstadoActual => estadoActual;

    private void Awake()
    {
        fuenteAudio = GetComponent<AudioSource>();
        fuenteAudio.playOnAwake = false;
        fuenteAudio.loop = false;
        fuenteAudio.Stop();
        estadoActual = Estado.EsperandoJugador;

        // Desactivar un ancestro apagaria tambien este script y el trigger.
        if (sistemaParticulas != null && transform.IsChildOf(sistemaParticulas.transform))
        {
            Debug.LogError("Asigna un sistema de particulas separado: su GameObject no puede contener este trigger.", this);
            enabled = false;
            return;
        }

        // El Renderer sigue activo: solo retira el material adicional del aura.
        bool auraValida = PrepararAura();

        if (sistemaParticulas != null)
        {
            var principal = sistemaParticulas.main;
            principal.playOnAwake = false;
            sistemaParticulas.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            sistemaParticulas.gameObject.SetActive(false);
        }

        configuracionValida = audioEstado1 != null && audioEstado2 != null &&
            audioEstado3 != null && sistemaParticulas != null && rendererObjeto != null &&
            !string.IsNullOrEmpty(tagJugador) && auraValida;

        if (!configuracionValida)
            Debug.LogError("AudioTriggerTresEstados: asigna los tres audios, Sistema Particulas, Renderer Objeto y Tag Jugador.", this);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isActiveAndEnabled || !configuracionValida || iniciada || !EsJugador(other)) return;

        // Marcar antes de comenzar evita duplicados por varios colliders del Player.
        iniciada = true;
        secuencia = StartCoroutine(EjecutarSecuencia());
    }

    private IEnumerator EjecutarSecuencia()
    {
        CambiarEstado(Estado.Estado1);
        ReproducirAudio(audioEstado1);

        // No necesita otra entrada al trigger. Son segundos reales.
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, esperaEstado2));

        CambiarEstado(Estado.Estado2);
        PrepararAudio(audioEstado2);

        // Inicio y final ligados al reloj del audio y al clip 2, no a otros sonidos.
        // El pequeno margen da tiempo al sistema de audio para preparar el clip.
        double inicioAudio2 = AudioSettings.dspTime + 0.2;
        double finAudio2 = inicioAudio2 + (double)audioEstado2.samples / audioEstado2.frequency;
        fuenteAudio.PlayScheduled(inicioAudio2);
        if (mostrarMensajes)
            Debug.Log($"[AudioTriggerTresEstados] Audio 2: {audioEstado2.name}. Duracion: {audioEstado2.length:F2} segundos.", this);

        // dspTime se congela cuando se pausa el sistema de audio.
        while (AudioSettings.dspTime < finAudio2) yield return null;

        // El audio 2 ya termino. Activa los efectos y reproduce el audio 3.
        CambiarEstado(Estado.Estado3);
        RestaurarAura();
        sistemaParticulas.gameObject.SetActive(true);
        sistemaParticulas.Play(true);
        ReproducirAudio(audioEstado3);

        // Permanece en estado 3 y no se repite al volver al trigger.
        secuencia = null;
    }

    private void ReproducirAudio(AudioClip clip)
    {
        PrepararAudio(clip);
        fuenteAudio.Play();
    }

    private void PrepararAudio(AudioClip clip)
    {
        fuenteAudio.Stop();
        fuenteAudio.enabled = true;
        fuenteAudio.loop = false;
        // Narraciones a velocidad normal para que su duracion sea determinista.
        fuenteAudio.pitch = 1f;
        fuenteAudio.dopplerLevel = 0f;
        fuenteAudio.ignoreListenerPause = false;
        fuenteAudio.clip = clip;
    }

    private void CambiarEstado(Estado nuevoEstado)
    {
        estadoActual = nuevoEstado;
        if (mostrarMensajes)
            Debug.Log($"[AudioTriggerTresEstados] {nuevoEstado}", this);
    }

    private bool PrepararAura()
    {
        if (rendererObjeto == null) return false;

        Material[] materiales = rendererObjeto.sharedMaterials;
        if (indiceMaterialAura < 1 || indiceMaterialAura >= materiales.Length ||
            materiales[indiceMaterialAura] == null)
        {
            Debug.LogError("Asigna el aura en Materials > Element 1 y deja Indice Material Aura = 1.", this);
            return false;
        }

        Mesh mesh = null;
        if (rendererObjeto is SkinnedMeshRenderer skinned)
            mesh = skinned.sharedMesh;
        else if (rendererObjeto.TryGetComponent<MeshFilter>(out MeshFilter filtro))
            mesh = filtro.sharedMesh;

        // Para un cubo de una submalla con dos materiales, el segundo es una capa extra.
        // No retirar materiales de submallas independientes porque cambiaria su asignacion.
        if (mesh == null || indiceMaterialAura < mesh.subMeshCount)
        {
            Debug.LogError("El aura debe ser un material adicional, como en el cubo de la captura. Este indice corresponde a una submalla propia o falta la malla.", this);
            return false;
        }

        materialesOriginales = materiales;
        Material[] sinAura = new Material[materiales.Length - 1];
        int destino = 0;
        for (int i = 0; i < materiales.Length; i++)
        {
            if (i == indiceMaterialAura) continue;
            sinAura[destino++] = materiales[i];
        }

        rendererObjeto.enabled = true;
        rendererObjeto.sharedMaterials = sinAura;
        auraOculta = true;
        return true;
    }

    private void RestaurarAura()
    {
        if (!auraOculta || rendererObjeto == null || materialesOriginales == null) return;
        rendererObjeto.sharedMaterials = materialesOriginales;
        auraOculta = false;
    }

    private bool EsJugador(Collider other)
    {
        // Permite colliders en hijos de un Player etiquetado en la raiz.
        for (Transform objeto = other.transform; objeto != null; objeto = objeto.parent)
        {
            if (objeto.tag == tagJugador) return true;
        }
        return false;
    }

    private void OnDisable()
    {
        if (secuencia != null)
        {
            if (mostrarMensajes)
                Debug.LogWarning($"[AudioTriggerTresEstados] Secuencia interrumpida en {estadoActual}: se desactivo el componente o su GameObject.", this);
            StopCoroutine(secuencia);
            secuencia = null;
        }
        if (fuenteAudio != null) fuenteAudio.Stop();
    }

    private void OnDestroy()
    {
        // Restaura las referencias si el componente se elimina durante la ejecucion.
        RestaurarAura();
    }
}

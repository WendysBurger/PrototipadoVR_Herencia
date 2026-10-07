using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Usar en un Empty con Collider 3D marcado como Is Trigger y un AudioSource propio.
[RequireComponent(typeof(AudioSource))]
public class Carta : MonoBehaviour
{
    public enum Estado
    {
        EsperandoJugador = 0,
        Estado1 = 1,
        Estado2 = 2,
        Estado3 = 3
    }

    public enum TipoCambioMaterial
    {
        ReemplazarMaterial = 0,
        AgregarMaterial = 1
    }

    [System.Serializable]
    public sealed class CambioMaterialObjeto
    {
        public Renderer rendererObjetivo;
        [Tooltip("Material que se aplicara al tocar la mesa. Asignalo aqui, sin agregarlo al Renderer.")]
        public Material materialNuevo;
        [Tooltip("Reemplazar cambia el material anterior. Agregar conserva el anterior y suma una capa, por ejemplo un aura.")]
        public TipoCambioMaterial tipoCambio = TipoCambioMaterial.ReemplazarMaterial;
        [Tooltip("Solo para Reemplazar. Element 0 corresponde a 0; Element 1 corresponde a 1.")]
        [Min(0)] public int indiceMaterialAReemplazar = 0;
    }

    [Header("Deteccion")]
    [SerializeField] private string tagJugador = "Player";

    [Header("Audios de cada estado")]
    [SerializeField] private AudioClip audioEstado1;
    [SerializeField] private AudioClip audioEstado2;
    [SerializeField] private AudioClip audioEstado3;

    [Header("Espera desde el inicio del audio 1")]
    [SerializeField, Min(0f)] private float esperaEstado2 = 20f;

    [Header("Animacion opcional en el estado 2")]
    [Tooltip("Desactivado por defecto. Los audios y los estados funcionan sin Animator.")]
    [SerializeField] private bool activarAnimacionEstado2 = false;
    [SerializeField] private Animator animadorEstado2;
    [Tooltip("Nombre exacto de un parametro Trigger del Animator Controller.")]
    [SerializeField] private string triggerAnimacionEstado2 = "Activar";

    [Header("Contacto del objeto con la mesa en el estado 3")]
    [Tooltip("Componente DetectorMesaObjeto situado en el objeto que debe tocar la mesa, junto a su Rigidbody.")]
    [SerializeField] private DetectorMesaObjeto detectorMesa;
    [Tooltip("Debe coincidir exactamente con el tag de la mesa: Mesa y mesa son distintos.")]
    [SerializeField] private string tagMesa = "Mesa";

    [Header("Cambio de materiales al tocar la mesa")]
    [Tooltip("Un elemento por objeto. Asigna su Renderer, el material nuevo y la forma de aplicarlo.")]
    [SerializeField]
    private CambioMaterialObjeto[] cambiosMateriales = new CambioMaterialObjeto[3]
    {
        new CambioMaterialObjeto(), new CambioMaterialObjeto(), new CambioMaterialObjeto()
    };

    // Conserva las referencias serializadas para migrar desde las versiones anteriores.
    [SerializeField, HideInInspector] private Renderer[] renderersObjetivo = new Renderer[3];
    [SerializeField, HideInInspector] private int indiceMaterialAura = 1;
    [SerializeField, HideInInspector] private Renderer rendererObjeto;

    [Header("Estado durante la ejecucion")]
    [SerializeField] private Estado estadoActual = Estado.EsperandoJugador;
    [SerializeField] private bool mostrarMensajes = true;
    [SerializeField] private bool contactoMesaCompletado;

    private AudioSource fuenteAudio;
    private Coroutine secuencia;
    private bool iniciada;
    private bool configuracionValida;
    private readonly List<DatosMateriales> materialesPreparados = new List<DatosMateriales>();

    private sealed class DatosMateriales
    {
        public Renderer renderer;
        public Material[] originales;
        public Material[] iniciales;
        public Material[] finales;
    }

    public Estado EstadoActual => estadoActual;

    private void Awake()
    {
        fuenteAudio = GetComponent<AudioSource>();
        fuenteAudio.playOnAwake = false;
        fuenteAudio.loop = false;
        fuenteAudio.Stop();
        estadoActual = Estado.EsperandoJugador;
        contactoMesaCompletado = false;
        MigrarReferenciaAnterior();

        configuracionValida = ValidarConfiguracion() && PrepararMateriales();
        if (!configuracionValida) return;

        // Una sola secuencia controla este detector. No modifica su fisica.
        if (!detectorMesa.Vincular(this))
        {
            configuracionValida = false;
            return;
        }

        foreach (DatosMateriales datos in materialesPreparados)
            datos.renderer.sharedMaterials = datos.iniciales;
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

        // La animacion es opcional y nunca es un requisito para los audios.
        while (AudioSettings.dspTime < inicioAudio2) yield return null;
        IntentarActivarAnimacionEstado2();

        // dspTime se congela cuando se pausa el sistema de audio.
        while (AudioSettings.dspTime < finAudio2) yield return null;

        // El audio 2 ya termino. Ahora espera a que el objeto toque la mesa.
        CambiarEstado(Estado.Estado3);
        if (mostrarMensajes)
            Debug.Log("[AudioTriggerTresEstados] Estado3: esperando contacto del objeto con " + tagMesa + ".", this);

        // Si el objeto ya esta apoyado, no necesita retirarlo y ponerlo otra vez.
        IntentarCompletarEstado3();
        secuencia = null;
    }

    public void IntentarCompletarEstado3()
    {
        if (!isActiveAndEnabled || !configuracionValida ||
            estadoActual != Estado.Estado3 || contactoMesaCompletado ||
            detectorMesa == null || !detectorMesa.TocandoMesa) return;

        // Marcar primero evita duplicados por Enter/Stay o varios colliders.
        contactoMesaCompletado = true;
        AplicarMaterialesFinales();
        ReproducirAudio(audioEstado3);
        if (mostrarMensajes)
            Debug.Log("[AudioTriggerTresEstados] Contacto con mesa: audio 3 y cambios de materiales aplicados.", this);
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

    private void IntentarActivarAnimacionEstado2()
    {
        if (!activarAnimacionEstado2) return;

        if (animadorEstado2 != null && animadorEstado2.gameObject.activeInHierarchy &&
            animadorEstado2.runtimeAnimatorController != null &&
            !string.IsNullOrEmpty(triggerAnimacionEstado2))
        {
            foreach (AnimatorControllerParameter parametro in animadorEstado2.parameters)
            {
                if (parametro.name != triggerAnimacionEstado2 ||
                    parametro.type != AnimatorControllerParameterType.Trigger) continue;

                animadorEstado2.enabled = true;
                animadorEstado2.ResetTrigger(triggerAnimacionEstado2);
                animadorEstado2.SetTrigger(triggerAnimacionEstado2);
                return;
            }
        }

        if (mostrarMensajes)
            Debug.LogWarning("[AudioTriggerTresEstados] Se omite la animacion del estado 2: revisa el Animator, su Controller y el parametro Trigger. Los audios y la secuencia continuan.", this);
    }

    private void MigrarReferenciaAnterior()
    {
        if (rendererObjeto != null && renderersObjetivo != null &&
            renderersObjetivo.Length > 0 && renderersObjetivo[0] == null)
            renderersObjetivo[0] = rendererObjeto;

        if (renderersObjetivo == null || cambiosMateriales == null) return;
        int cantidad = Mathf.Min(renderersObjetivo.Length, cambiosMateriales.Length);
        for (int i = 0; i < cantidad; i++)
        {
            Renderer anterior = renderersObjetivo[i];
            if (anterior == null) continue;
            if (cambiosMateriales[i] == null) cambiosMateriales[i] = new CambioMaterialObjeto();
            CambioMaterialObjeto cambio = cambiosMateriales[i];
            if (cambio.rendererObjetivo != null) continue;

            cambio.rendererObjetivo = anterior;
            // Recupera el material que antes se guardaba en Element 1.
            Material[] materiales = anterior.sharedMaterials;
            if (cambio.materialNuevo == null && indiceMaterialAura >= 0 &&
                indiceMaterialAura < materiales.Length)
                cambio.materialNuevo = materiales[indiceMaterialAura];
        }
    }

    private bool ValidarConfiguracion()
    {
        if (audioEstado1 == null || audioEstado2 == null || audioEstado3 == null ||
            audioEstado2.frequency <= 0 || audioEstado2.samples <= 0 ||
            detectorMesa == null ||
            string.IsNullOrEmpty(tagJugador) || string.IsNullOrEmpty(tagMesa))
        {
            Debug.LogError("AudioTriggerTresEstados: asigna los tres audios, Detector Mesa y los tags.", this);
            return false;
        }

        if (cambiosMateriales == null || cambiosMateriales.Length == 0)
        {
            Debug.LogError("Asigna al menos un objeto en Cambios Materiales.", this);
            return false;
        }

        return true;
    }

    private bool PrepararMateriales()
    {
        materialesPreparados.Clear();
        var usados = new HashSet<Renderer>();
        foreach (CambioMaterialObjeto cambio in cambiosMateriales)
        {
            if (cambio == null || cambio.rendererObjetivo == null ||
                cambio.materialNuevo == null || !usados.Add(cambio.rendererObjetivo))
            {
                Debug.LogError("Asigna un Renderer distinto y un Material Nuevo en cada elemento de Cambios Materiales.", this);
                materialesPreparados.Clear();
                return false;
            }

            Renderer renderer = cambio.rendererObjetivo;
            Material[] originales = renderer.sharedMaterials;
            var iniciales = new List<Material>(originales);

            // Si el material nuevo estaba agregado como capa extra en la version
            // anterior, retirarlo al iniciar. Las submallas reales se conservan.
            int submallas = ObtenerCantidadSubmallas(renderer, originales.Length);
            for (int i = iniciales.Count - 1; i >= submallas; i--)
                if (iniciales[i] == cambio.materialNuevo) iniciales.RemoveAt(i);

            if (iniciales.Count == 0)
            {
                Debug.LogError("El Renderer '" + renderer.name + "' necesita su material inicial antes de Play.", this);
                materialesPreparados.Clear();
                return false;
            }

            var finales = new List<Material>(iniciales);
            if (cambio.tipoCambio == TipoCambioMaterial.ReemplazarMaterial)
            {
                int indice = cambio.indiceMaterialAReemplazar;
                if (indice < 0 || indice >= finales.Count)
                {
                    Debug.LogError("Indice Material A Reemplazar invalido en '" + renderer.name + "'. Usa 0 para reemplazar Element 0.", this);
                    materialesPreparados.Clear();
                    return false;
                }
                // El material nuevo ocupa el lugar del anterior, sin superponerlos.
                finales[indice] = cambio.materialNuevo;
            }
            else if (cambio.tipoCambio == TipoCambioMaterial.AgregarMaterial)
            {
                finales.Add(cambio.materialNuevo);
            }
            else
            {
                Debug.LogError("Tipo Cambio invalido en '" + renderer.name + "'.", this);
                materialesPreparados.Clear();
                return false;
            }

            materialesPreparados.Add(new DatosMateriales
            {
                renderer = renderer,
                originales = originales,
                iniciales = iniciales.ToArray(),
                finales = finales.ToArray()
            });
        }
        return true;
    }

    private static int ObtenerCantidadSubmallas(Renderer renderer, int cantidadMateriales)
    {
        Mesh mesh = null;
        if (renderer is SkinnedMeshRenderer skinned) mesh = skinned.sharedMesh;
        else if (renderer.TryGetComponent<MeshFilter>(out MeshFilter filtro)) mesh = filtro.sharedMesh;
        return mesh != null ? Mathf.Max(1, mesh.subMeshCount) : cantidadMateriales;
    }

    private void AplicarMaterialesFinales()
    {
        foreach (DatosMateriales datos in materialesPreparados)
        {
            if (datos.renderer == null) continue;
            datos.renderer.sharedMaterials = datos.finales;
            if (mostrarMensajes)
                Debug.Log("[AudioTriggerTresEstados] Materiales actualizados en '" + datos.renderer.name + "'.", this);
        }
    }

    private void RestaurarMaterialesOriginales()
    {
        foreach (DatosMateriales datos in materialesPreparados)
            if (datos.renderer != null) datos.renderer.sharedMaterials = datos.originales;
    }

    private bool EsJugador(Collider other)
    {
        // Permite colliders en hijos de un Player etiquetado en la raiz.
        return TieneTagEnJerarquia(other, tagJugador);
    }

    public bool EsMesa(Collider other)
    {
        return TieneTagEnJerarquia(other, tagMesa);
    }

    private static bool TieneTagEnJerarquia(Collider other, string tagBuscado)
    {
        if (other == null) return false;
        for (Transform objeto = other.transform; objeto != null; objeto = objeto.parent)
        {
            if (objeto.tag == tagBuscado) return true;
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
        RestaurarMaterialesOriginales();
        if (detectorMesa != null) detectorMesa.Desvincular(this);
    }

    private void OnValidate()
    {
        MigrarReferenciaAnterior();
    }
}

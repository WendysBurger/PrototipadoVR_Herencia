using System.Collections;
using UnityEngine;

// Colocar en un Empty hijo del Player con su propio AudioSource.
// No depende de OVRInput ni de una version concreta del SDK de Meta.
[RequireComponent(typeof(AudioSource))]
public class PasosVRPorSuperficie : MonoBehaviour
{
    [System.Serializable]
    public class Superficie
    {
        public string tagSuelo = "Grama";
        public AudioClip[] audios;
    }

    [Header("Referencias del jugador")]
    [Tooltip("Objeto raiz que desplaza la locomocion. No asignar la camara ni los controles.")]
    [SerializeField] private Transform jugador;
    [Tooltip("Empty a la altura de los pies, hijo del objeto que se desplaza.")]
    [SerializeField] private Transform puntoPies;

    [Header("Deteccion del suelo")]
    [Tooltip("Incluir los suelos. Excluir la capa del Player.")]
    [SerializeField] private LayerMask capasSuelo;
    [SerializeField, Min(0.01f)] private float alturaOrigen = 0.15f;
    [Tooltip("Distancia adicional debajo de los pies. Mantener corta para no detectar suelo al saltar.")]
    [SerializeField, Min(0.01f)] private float distanciaBajoPies = 0.10f;
    [SerializeField] private Superficie[] superficies;

    [Header("Movimiento y frecuencia")]
    [SerializeField, Min(0.01f)] private float intervaloPasos = 0.5f;
    [Tooltip("Velocidad horizontal minima en metros/segundo para ignorar pequenos movimientos.")]
    [SerializeField, Min(0.01f)] private float velocidadMinima = 0.1f;
    [Tooltip("Ignora saltos de posicion mayores a esta distancia en un frame, como teletransportes.")]
    [SerializeField, Min(0.01f)] private float saltoPosicionMaximo = 0.5f;
    [SerializeField, Range(0f, 1f)] private float volumen = 1f;

    private AudioSource fuenteAudio;
    private Vector3 posicionAnterior;
    private Superficie superficieActual;
    private Coroutine rutinaPasos;

    private void Awake()
    {
        fuenteAudio = GetComponent<AudioSource>();
        fuenteAudio.playOnAwake = false;
        fuenteAudio.loop = false;
        fuenteAudio.spatialBlend = 0f;

        if (jugador == null || puntoPies == null || capasSuelo.value == 0)
        {
            Debug.LogWarning("Pasos VR: asigna Jugador, Punto Pies y Capas Suelo en el Inspector.", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (jugador != null) posicionAnterior = jugador.position;
    }

    private void LateUpdate()
    {
        Vector3 desplazamiento = jugador.position - posicionAnterior;
        posicionAnterior = jugador.position;

        bool saltoPosicion = desplazamiento.sqrMagnitude >
            saltoPosicionMaximo * saltoPosicionMaximo;
        desplazamiento.y = 0f;
        float velocidad = desplazamiento.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);

        superficieActual = BuscarSuelo();
        bool puedeCaminar = Time.timeScale > 0f && !saltoPosicion &&
            velocidad >= velocidadMinima && superficieActual != null &&
            fuenteAudio.isActiveAndEnabled;

        if (puedeCaminar)
        {
            if (rutinaPasos == null)
                rutinaPasos = StartCoroutine(ReproducirPasos());
        }
        else
        {
            DetenerRutina();
        }
    }

    private Superficie BuscarSuelo()
    {
        Vector3 origen = puntoPies.position + Vector3.up * alturaOrigen;
        if (!Physics.Raycast(origen, Vector3.down, out RaycastHit hit,
            alturaOrigen + distanciaBajoPies, capasSuelo, QueryTriggerInteraction.Ignore))
            return null;

        if (superficies == null) return null;

        // Revisa el collider y sus padres para permitir suelos organizados en hijos.
        for (Transform objeto = hit.collider.transform; objeto != null; objeto = objeto.parent)
        {
            foreach (Superficie superficie in superficies)
            {
                if (superficie != null && !string.IsNullOrEmpty(superficie.tagSuelo) &&
                    objeto.tag == superficie.tagSuelo && TieneAudio(superficie))
                    return superficie;
            }
        }
        return null;
    }

    private static bool TieneAudio(Superficie superficie)
    {
        if (superficie.audios == null) return false;
        foreach (AudioClip clip in superficie.audios)
            if (clip != null) return true;
        return false;
    }

    private IEnumerator ReproducirPasos()
    {
        while (true)
        {
            // Busca un clip valido empezando desde un indice aleatorio.
            AudioClip[] clips = superficieActual.audios;
            int inicio = Random.Range(0, clips.Length);
            for (int i = 0; i < clips.Length; i++)
            {
                AudioClip clip = clips[(inicio + i) % clips.Length];
                if (clip == null) continue;

                // Fuente dedicada a pasos. Play reemplaza el paso anterior.
                fuenteAudio.clip = clip;
                fuenteAudio.volume = volumen;
                fuenteAudio.Play();
                break;
            }
            yield return new WaitForSeconds(Mathf.Max(0.01f, intervaloPasos));
        }
    }

    private void DetenerRutina()
    {
        if (rutinaPasos == null) return;
        StopCoroutine(rutinaPasos);
        rutinaPasos = null;
        // Deja terminar el ultimo paso; no produce nuevos pasos al detenerse.
    }

    private void OnDisable()
    {
        DetenerRutina();
        if (fuenteAudio != null) fuenteAudio.Stop();
    }

    private void OnDrawGizmosSelected()
    {
        if (puntoPies == null) return;
        Vector3 origen = puntoPies.position + Vector3.up * alturaOrigen;
        Gizmos.color = Color.green;
        Gizmos.DrawLine(origen, origen + Vector3.down * (alturaOrigen + distanciaBajoPies));
    }
}

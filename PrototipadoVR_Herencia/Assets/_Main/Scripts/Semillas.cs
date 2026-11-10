using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Colocar en un Empty independiente con Box Collider 3D / Is Trigger.
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public class Semillas : MonoBehaviour
{
    public enum Estado { EsperandoJugador, Estado1, Estado2, Estado3 }

    [Header("Deteccion")]
    [SerializeField] private string tagJugador = "Player";
    [Tooltip("Para una zona independiente: configura el Collider como trigger y agrega un Rigidbody cinematico si no existe uno asociado.")]
    [SerializeField] private bool configurarTriggerAutomaticamente = true;

    [Header("Control de los objetos que se mueven")]
    [Tooltip("Evita que la fisica compita con el movimiento. Recupera el estado anterior del Rigidbody al terminar.")]
    [SerializeField] private bool controlarRigidbodyDuranteSecuencia = true;

    [Header("Audios")]
    [SerializeField] private AudioClip audioEstado1;
    [SerializeField] private AudioClip audioEstado2;
    [SerializeField] private AudioClip audioEstado3;

    [Header("Reproducir esta interaccion una sola vez")]
    [Tooltip("Las copias de esta misma interaccion deben compartir este ID. Usa otro ID para una secuencia independiente en la misma escena.")]
    [SerializeField] private string idInteraccion = "SemillasFrasco";

    [Header("Estado 1: cambio inmediato de posicion")]
    [SerializeField] private Transform objetoEstado1;
    [SerializeField] private Transform destinoEstado1;

    [Header("Estado 1: animacion OPCIONAL")]
    [SerializeField] private bool activarAnimacion = false;
    [SerializeField] private Animator animador;
    [Tooltip("Parametro Trigger del Animator, no un Animation Event.")]
    [SerializeField] private string nombreTrigger = "Activar";

    [Header("Estado 3: animacion OPCIONAL")]
    [SerializeField] private bool activarAnimacionEstado3 = false;
    [SerializeField] private Animator animadorEstado3;
    [Tooltip("Parametro Trigger del Animator para el estado 3, no un Animation Event.")]
    [SerializeField] private string nombreTriggerEstado3 = "ActivarEstado3";

    [Header("Estado 3: recorrido por tres puntos")]
    [SerializeField] private Transform objetoEstado3;
    [SerializeField] private Transform punto1;
    [SerializeField] private Transform punto2;
    [SerializeField] private Transform punto3;
    [Tooltip("Unidades de Unity por segundo. Puedes usar 1.5 o 2.")]
    [SerializeField, Min(0.01f)] private float velocidad = 2f;

    [Header("Estado 3: giro durante el recorrido")]
    [SerializeField] private bool girarALaIzquierda = true;
    [Tooltip("Giro total hacia la izquierda alrededor del eje Y mundial, respecto a la orientacion al comenzar el estado 3. No se acumula por punto.")]
    [SerializeField, Range(0f, 180f)] private float gradosGiroIzquierda = 90f;
    [Tooltip("Velocidad del giro en grados por segundo.")]
    [SerializeField, Min(0.01f)] private float velocidadGiro = 90f;

    [Header("Estado 3: giro extra entre el punto 2 y el punto 3")]
    [Tooltip("Al llegar al punto 2 gira estos grados adicionales hacia la izquierda mientras camina hacia el punto 3.")]
    [SerializeField] private bool girarEntrePunto2Y3 = true;
    [SerializeField, Range(0f, 180f)] private float gradosGiroEntrePunto2Y3 = 45f;
    [Tooltip("Activado: gira de golpe al llegar al punto 2. Desactivado: gira poco a poco durante el tramo hacia el punto 3.")]
    [SerializeField] private bool giroEntrePunto2Y3Inmediato = true;

    [Header("Aura: material adicional en Element 1")]
    [Tooltip("Renderer que tiene el material base y el aura adicional. No se apaga la malla.")]
    [SerializeField] private Renderer rendererAura;
    [SerializeField, Min(1)] private int indiceMaterialAura = 1;

    [Header("Alternativa: aura como GameObject separado")]
    [Tooltip("Opcional. Si tu efecto es un objeto separado, asignalo aqui. Se oculta al iniciar y se activa en Estado 3.")]
    [SerializeField] private GameObject objetoAura;

    [Header("Seguimiento durante Play")]
    [SerializeField] private Estado estadoActual = Estado.EsperandoJugador;
    [SerializeField] private bool mostrarMensajes = true;

    private AudioSource fuenteAudio;
    private Coroutine secuencia;
    private bool iniciada;
    private bool propietariaSecuencia;
    private readonly HashSet<Estado> audiosReproducidos = new HashSet<Estado>();
    private static readonly HashSet<string> interaccionesEjecutadas = new HashSet<string>();
    private Material[] materialesOriginales;
    private bool auraMaterialPreparada;
    private bool auraObjetoPreparada;
    private readonly HashSet<int> contactosRegistrados = new HashSet<int>();
    private readonly Dictionary<Rigidbody, bool> cuerposControlados = new Dictionary<Rigidbody, bool>();

    public Estado EstadoActual => estadoActual;

    // Verdadero cuando el objeto del Estado 3 (Yta) ya llego al ultimo punto y termino de girar.
    public bool RutaTerminada { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ReiniciarBloqueosAlIniciarPartida()
    {
        // Se reinicia al comenzar Play/la aplicacion, incluso sin Domain Reload.
        // Nunca se libera al salir del trigger, terminar o desactivar el objeto.
        interaccionesEjecutadas.Clear();
    }

    private void Awake()
    {
        fuenteAudio = GetComponent<AudioSource>();
        fuenteAudio.playOnAwake = false;
        fuenteAudio.loop = false;
        fuenteAudio.spatialBlend = 0f;
        fuenteAudio.spatialize = false;
        fuenteAudio.ignoreListenerPause = false;
        fuenteAudio.Stop();
        estadoActual = Estado.EsperandoJugador;
        PrepararTrigger();
        PrepararAura();
    }

    private void OnTriggerEnter(Collider other)
    {
        ProcesarContacto(other);
    }

    private void OnTriggerStay(Collider other) { ProcesarContacto(other); }
    private void OnCollisionEnter(Collision collision) { ProcesarContacto(collision.collider); }
    private void OnCollisionStay(Collision collision) { ProcesarContacto(collision.collider); }

    private void PrepararTrigger()
    {
        Collider zona = GetComponent<Collider>();
        if (configurarTriggerAutomaticamente)
        {
            if (zona == null) zona = gameObject.AddComponent<BoxCollider>();
            if (zona is MeshCollider malla && !malla.convex)
            {
                Debug.LogError("Usa un Box Collider para esta zona; el Mesh Collider no convexo no sirve como trigger.", this);
                return;
            }
            zona.isTrigger = true;
            zona.enabled = true;
            if (zona.attachedRigidbody == null)
            {
                Rigidbody cuerpo = gameObject.AddComponent<Rigidbody>();
                cuerpo.useGravity = false;
                cuerpo.isKinematic = true;
            }
        }
        if (mostrarMensajes)
            Debug.Log("[Semillas] Zona lista en '" + name + "'. Collider=" +
                (zona != null ? zona.GetType().Name : "FALTA") + ". Layer=" +
                LayerMask.LayerToName(gameObject.layer) + ". Tag esperado=" + tagJugador, this);
    }

    private void ProcesarContacto(Collider other)
    {
        if (!isActiveAndEnabled || iniciada || other == null) return;
        bool primerAviso = contactosRegistrados.Add(other.GetInstanceID());
        if (primerAviso && mostrarMensajes)
            Debug.Log("[Semillas] Contacto recibido: '" + other.name + "', Tag=" + other.tag +
                ", Layer=" + LayerMask.LayerToName(other.gameObject.layer), this);
        if (!EsJugador(other))
        {
            if (primerAviso && mostrarMensajes)
                Debug.LogWarning("[Semillas] Contacto ignorado: ni ese collider ni sus padres tienen el tag '" + tagJugador + "'.", this);
            return;
        }
        if (primerAviso && mostrarMensajes) Debug.Log("[Semillas] Player detectado.", this);
        IniciarSecuencia(primerAviso);
    }

    private void IniciarSecuencia(bool informarErrores)
    {
        if (iniciada || !ValidarSecuencia(informarErrores)) return;

        string escena = string.IsNullOrEmpty(gameObject.scene.path) ? gameObject.scene.name : gameObject.scene.path;
        string identificador = string.IsNullOrWhiteSpace(idInteraccion) ? "SemillasFrasco" : idInteraccion.Trim();
        string clave = escena + "::" + identificador;
        if (!interaccionesEjecutadas.Add(clave))
        {
            // Otra copia ya consumio esta interaccion. Ignorar tambien futuros Stay.
            iniciada = true;
            if (mostrarMensajes)
                Debug.LogWarning("[Semillas] Repeticion bloqueada en '" + name + "': la interaccion '" + identificador + "' ya fue iniciada. Revisa si tienes el script en mas de un objeto.", this);
            return;
        }

        // Tomar el bloqueo antes de iniciar la coroutine evita duplicados en el mismo frame.
        iniciada = true;
        propietariaSecuencia = true;
        secuencia = StartCoroutine(EjecutarSecuencia());
    }

    [ContextMenu("Probar secuencia (solo Play)")]
    private void ProbarSecuencia()
    {
        if (!Application.isPlaying || !isActiveAndEnabled)
        {
            Debug.LogWarning("Primero entra en Play y deja activo este componente.", this);
            return;
        }
        IniciarSecuencia(true);
    }

    private IEnumerator EjecutarSecuencia()
    {
        CambiarEstado(Estado.Estado1);
        Reproducir(audioEstado1);
        if (objetoEstado1 != null && destinoEstado1 != null)
        {
            ControlarCuerpo(objetoEstado1);
            AsignarPosicion(objetoEstado1, destinoEstado1.position);
            if (mostrarMensajes) Debug.Log("[Semillas] Estado 1: '" + objetoEstado1.name + "' colocado en " + destinoEstado1.position, this);
        }
        else Debug.LogWarning("[Semillas] Estado 1: falta Objeto Estado 1 o Destino Estado 1. Se omite ese movimiento y continuan los audios.", this);
        IntentarAnimacion();
        yield return EsperarAudio();

        CambiarEstado(Estado.Estado2);
        Reproducir(audioEstado2);
        yield return EsperarAudio();

        CambiarEstado(Estado.Estado3);
        Reproducir(audioEstado3);
        ActivarAura();
        IntentarAnimacionEstado3();

        // Captura las posiciones del recorrido al comenzar este estado.
        Vector3[] destinos = PrepararRuta();
        int siguientePunto = 0;
        bool giroTerminado = !girarALaIzquierda || destinos.Length == 0 || objetoEstado3 == null;
        Quaternion rotacionFinal = giroTerminado ? Quaternion.identity :
            Quaternion.AngleAxis(-gradosGiroIzquierda, Vector3.up) * objetoEstado3.rotation;
        float velocidadGiroActual = velocidadGiro;
        do
        {
            if ((siguientePunto < destinos.Length || !giroTerminado) && objetoEstado3 == null)
            {
                Debug.LogError("Se perdio Objeto Estado 3 durante el recorrido.", this);
                siguientePunto = destinos.Length;
                giroTerminado = true;
            }
            if (!giroTerminado)
            {
                AsignarRotacion(objetoEstado3, Quaternion.RotateTowards(
                    objetoEstado3.rotation, rotacionFinal, Mathf.Max(0.01f, velocidadGiroActual) * Time.deltaTime));
                if (Quaternion.Angle(objetoEstado3.rotation, rotacionFinal) <= 0.1f)
                {
                    AsignarRotacion(objetoEstado3, rotacionFinal);
                    giroTerminado = true;
                    if (mostrarMensajes) Debug.Log("[Semillas] Giro hacia la izquierda completado.", this);
                }
            }
            if (siguientePunto < destinos.Length)
            {
                Vector3 destino = destinos[siguientePunto];
                AsignarPosicion(objetoEstado3, Vector3.MoveTowards(
                    objetoEstado3.position, destino, Mathf.Max(0.01f, velocidad) * Time.deltaTime));
                if (Vector3.Distance(objetoEstado3.position, destino) <= 0.001f)
                {
                    AsignarPosicion(objetoEstado3, destino);
                    siguientePunto++;
                    if (mostrarMensajes) Debug.Log("[Semillas] Punto " + siguientePunto + " alcanzado.", this);

                    // Giro extra hacia la izquierda al llegar al punto 2, antes de ir al punto 3.
                    // Inmediato (de golpe) o repartido durante el tramo, segun Giro Entre Punto 2 Y 3 Inmediato.
                    if (siguientePunto == 2 && girarEntrePunto2Y3 && gradosGiroEntrePunto2Y3 > 0f &&
                        objetoEstado3 != null && destinos.Length == 3)
                    {
                        Quaternion giroExtra = Quaternion.AngleAxis(-gradosGiroEntrePunto2Y3, Vector3.up);
                        if (giroEntrePunto2Y3Inmediato)
                        {
                            // Gira de golpe. Si el giro inicial sigue en curso, su destino tambien se corre 45 grados.
                            AsignarRotacion(objetoEstado3, giroExtra * objetoEstado3.rotation);
                            if (!giroTerminado) rotacionFinal = giroExtra * rotacionFinal;
                        }
                        else
                        {
                            Quaternion rotacionBase = giroTerminado ? objetoEstado3.rotation : rotacionFinal;
                            rotacionFinal = giroExtra * rotacionBase;
                            float duracionTramo = Vector3.Distance(destinos[1], destinos[2]) / Mathf.Max(0.01f, velocidad);
                            float gradosPendientes = Quaternion.Angle(objetoEstado3.rotation, rotacionFinal);
                            velocidadGiroActual = duracionTramo > 0.01f ? gradosPendientes / duracionTramo : velocidadGiro;
                            giroTerminado = false;
                        }
                        if (mostrarMensajes) Debug.Log("[Semillas] Giro extra de " + gradosGiroEntrePunto2Y3 + " grados hacia la izquierda entre el punto 2 y el punto 3.", this);
                    }
                }
            }
            if (!RutaTerminada && siguientePunto >= destinos.Length && giroTerminado) RutaTerminada = true;
            yield return null;
        }
        while (siguientePunto < destinos.Length || !giroTerminado || fuenteAudio.isPlaying || AudioListener.pause);

        // Mantiene Estado 3 hasta completar tanto el audio como el recorrido.
        if (mostrarMensajes) Debug.Log("[SemillasFrascoTresEstados] Secuencia completada.", this);
        RestaurarCuerpos();
        secuencia = null;
    }

    private Vector3[] PrepararRuta()
    {
        if (objetoEstado3 == null || punto1 == null || punto2 == null || punto3 == null)
        {
            Debug.LogWarning("[Semillas] Estado 3: asigna Objeto Estado 3 y Punto 1, Punto 2, Punto 3. Se omite el recorrido; el audio y el aura continuan.", this);
            return new Vector3[0];
        }
        ControlarCuerpo(objetoEstado3);
        if (mostrarMensajes) Debug.Log("[Semillas] Iniciando recorrido de '" + objetoEstado3.name + "' por los tres puntos.", this);
        return new Vector3[] { punto1.position, punto2.position, punto3.position };
    }

    private void ControlarCuerpo(Transform objeto)
    {
        if (!controlarRigidbodyDuranteSecuencia) return;
        Rigidbody cuerpo = objeto.GetComponent<Rigidbody>();
        if (cuerpo == null || cuerposControlados.ContainsKey(cuerpo)) return;
        cuerposControlados.Add(cuerpo, cuerpo.isKinematic);
        if (!cuerpo.isKinematic)
        {
            cuerpo.linearVelocity = Vector3.zero;
            cuerpo.angularVelocity = Vector3.zero;
        }
        cuerpo.isKinematic = true;
    }

    private void AsignarPosicion(Transform objeto, Vector3 posicion)
    {
        Rigidbody cuerpo = objeto.GetComponent<Rigidbody>();
        if (cuerpo != null && cuerpo.isKinematic) cuerpo.position = posicion;
        objeto.position = posicion;
    }

    private void AsignarRotacion(Transform objeto, Quaternion rotacion)
    {
        Rigidbody cuerpo = objeto.GetComponent<Rigidbody>();
        if (cuerpo != null && cuerpo.isKinematic) cuerpo.rotation = rotacion;
        objeto.rotation = rotacion;
    }

    private void RestaurarCuerpos()
    {
        foreach (KeyValuePair<Rigidbody, bool> dato in cuerposControlados)
            if (dato.Key != null) dato.Key.isKinematic = dato.Value;
        cuerposControlados.Clear();
    }

    private void Reproducir(AudioClip clip)
    {
        // Cada estado admite una sola reproduccion, tambien ante llamadas repetidas
        // desde eventos de animacion. El mismo clip puede usarse en estados distintos.
        if (!propietariaSecuencia || !audiosReproducidos.Add(estadoActual)) return;
        fuenteAudio.Stop();
        fuenteAudio.enabled = true;
        fuenteAudio.loop = false;
        fuenteAudio.clip = clip;
        fuenteAudio.Play();
    }

    private IEnumerator EsperarAudio()
    {
        do { yield return null; }
        while (fuenteAudio.isPlaying || AudioListener.pause);
    }

    private void IntentarAnimacion()
    {
        if (!activarAnimacion) return;
        if (animador != null && animador.isActiveAndEnabled &&
            animador.runtimeAnimatorController != null && !string.IsNullOrWhiteSpace(nombreTrigger))
        {
            foreach (AnimatorControllerParameter parametro in animador.parameters)
            {
                if (parametro.name != nombreTrigger || parametro.type != AnimatorControllerParameterType.Trigger) continue;
                animador.ResetTrigger(nombreTrigger);
                animador.SetTrigger(nombreTrigger);
                return;
            }
        }
        Debug.LogWarning("Se omite la animacion: revisa el Animator y su Trigger. Los audios y los estados continuan.", this);
    }

    private void IntentarAnimacionEstado3()
    {
        if (!activarAnimacionEstado3) return;
        if (animadorEstado3 != null && animadorEstado3.isActiveAndEnabled &&
            animadorEstado3.runtimeAnimatorController != null && !string.IsNullOrWhiteSpace(nombreTriggerEstado3))
        {
            foreach (AnimatorControllerParameter parametro in animadorEstado3.parameters)
            {
                if (parametro.name != nombreTriggerEstado3 || parametro.type != AnimatorControllerParameterType.Trigger) continue;
                animadorEstado3.ResetTrigger(nombreTriggerEstado3);
                animadorEstado3.SetTrigger(nombreTriggerEstado3);
                return;
            }
        }
        Debug.LogWarning("Se omite la animacion del estado 3: revisa el Animator y su Trigger. El audio, el aura y el recorrido continuan.", this);
    }

    private void PrepararAura()
    {
        if (objetoAura != null)
        {
            // El aura debe ser un efecto separado, para no apagar la secuencia ni sus objetos.
            Transform raiz = objetoAura.transform;
            bool contieneReferencias = transform.IsChildOf(raiz) ||
                (objetoEstado1 != null && objetoEstado1.IsChildOf(raiz)) ||
                (objetoEstado3 != null && objetoEstado3.IsChildOf(raiz)) ||
                (animador != null && animador.transform.IsChildOf(raiz));
            if (contieneReferencias)
                Debug.LogWarning("Objeto Aura debe ser solo el efecto visual, separado del trigger y de los objetos que se mueven. Se omite esta referencia.", this);
            else
            {
                auraObjetoPreparada = true;
                objetoAura.SetActive(false);
            }
        }

        if (rendererAura == null) return;
        Material[] materiales = rendererAura.sharedMaterials;
        Mesh mesh = null;
        if (rendererAura is SkinnedMeshRenderer skinned) mesh = skinned.sharedMesh;
        else if (rendererAura.TryGetComponent<MeshFilter>(out MeshFilter filtro)) mesh = filtro.sharedMesh;

        // Solo retira una capa adicional. Conserva los materiales de las submallas reales.
        if (mesh == null || indiceMaterialAura < 1 || indiceMaterialAura >= materiales.Length ||
            indiceMaterialAura < mesh.subMeshCount || materiales[indiceMaterialAura] == null)
        {
            Debug.LogWarning("Renderer Aura necesita un material adicional en Element " + indiceMaterialAura + ". Se omite el aura de material; los audios continuan.", this);
            return;
        }

        materialesOriginales = materiales;
        var sinAura = new Material[materiales.Length - 1];
        int destino = 0;
        for (int i = 0; i < materiales.Length; i++)
            if (i != indiceMaterialAura) sinAura[destino++] = materiales[i];
        rendererAura.sharedMaterials = sinAura;
        auraMaterialPreparada = true;
    }

    private void ActivarAura()
    {
        if (auraMaterialPreparada && rendererAura != null)
            rendererAura.sharedMaterials = materialesOriginales;
        if (auraObjetoPreparada && objetoAura != null)
            objetoAura.SetActive(true);
    }

    private bool ValidarSecuencia(bool informarErrores)
    {
        if (audioEstado1 == null || audioEstado2 == null || audioEstado3 == null)
        {
            if (informarErrores) Debug.LogError("[Semillas] Player detectado, pero falta asignar uno de los tres audios. Revisa Audio Estado 1, 2 y 3.", this);
            return false;
        }
        return true;
    }

    private bool EsJugador(Collider other)
    {
        if (other == null || string.IsNullOrWhiteSpace(tagJugador)) return false;
        for (Transform actual = other.transform; actual != null; actual = actual.parent)
            if (actual.CompareTag(tagJugador)) return true;
        return false;
    }

    private void CambiarEstado(Estado estado)
    {
        estadoActual = estado;
        if (mostrarMensajes) Debug.Log("[SemillasFrascoTresEstados] " + estado, this);
    }

    private void OnDisable()
    {
        if (secuencia != null)
        {
            StopCoroutine(secuencia);
            secuencia = null;
        }
        if (fuenteAudio != null) fuenteAudio.Stop();
        RestaurarCuerpos();
    }

    private void OnDestroy()
    {
        if (auraMaterialPreparada && rendererAura != null)
            rendererAura.sharedMaterials = materialesOriginales;
    }
}

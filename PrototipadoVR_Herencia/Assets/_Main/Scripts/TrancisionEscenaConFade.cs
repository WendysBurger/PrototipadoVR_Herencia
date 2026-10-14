using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Unity URP. Colocar en un Empty RAIZ, separado del Snap y del jugador.
// El Global Volume de transicion debe ser un HIJO de este Empty.
[DisallowMultipleComponent]
public class TransicionEscenaConFade : MonoBehaviour
{
    public enum EstadoTransicion { Esperando, Oscureciendo, Cargando, Aclarando, Completado }

    [Header("Escena de destino")]
    [SerializeField] private string nombreEscena;

    [Header("Global Volume exclusivo para el fundido")]
    [Tooltip("Volume hijo de este Empty. No asignes el volumen que contiene los efectos normales de la escena.")]
    [SerializeField] private Volume volumenFade;

    [Header("Duraciones en segundos reales")]
    [SerializeField, Min(0.01f)] private float duracionOscurecer = 1f;
    [SerializeField, Min(0.01f)] private float duracionAclarar = 1f;

    [Header("Seguimiento")]
    [SerializeField] private bool mostrarMensajes = true;
    [SerializeField] private EstadoTransicion estadoActual = EstadoTransicion.Esperando;

    private static TransicionEscenaConFade transicionEnCurso;
    private Coroutine rutina;
    private VolumeProfile perfilNegro;

    public EstadoTransicion EstadoActual => estadoActual;

    private void Awake()
    {
        estadoActual = EstadoTransicion.Esperando;
        if (JerarquiaValida())
        {
            volumenFade.weight = 0f;
            volumenFade.gameObject.SetActive(false);
        }
    }

    private bool JerarquiaValida()
    {
        return transform.parent == null && volumenFade != null &&
            volumenFade.gameObject != gameObject &&
            volumenFade.transform.IsChildOf(transform);
    }

    public bool ValidarConfiguracion()
    {
        if (!isActiveAndEnabled)
        {
            Debug.LogError("Mantén activo el Empty TransicionEscena y su componente.", this);
            return false;
        }
        if (!JerarquiaValida())
        {
            Debug.LogError("TransicionEscena debe ser un Empty raiz. Asigna en Volumen Fade un Global Volume hijo de ese Empty.", this);
            return false;
        }
        if (string.IsNullOrWhiteSpace(nombreEscena) || !Application.CanStreamedLevelBeLoaded(nombreEscena))
        {
            Debug.LogError("Revisa Nombre Escena y agrega la escena de destino a la Scene List del Build Profile activo.", this);
            return false;
        }
        return true;
    }

    // Tambien puede llamarse desde un UnityEvent al finalizar otro audio.
    public void CambiarEscena()
    {
        if (rutina != null || estadoActual != EstadoTransicion.Esperando) return;
        if (transicionEnCurso != null && transicionEnCurso != this) return;
        if (!ValidarConfiguracion()) return;

        transicionEnCurso = this;
        // Conserva el controlador Y el Volume hijo durante LoadSceneMode.Single.
        DontDestroyOnLoad(gameObject);
        rutina = StartCoroutine(EjecutarTransicion());
    }

    private void PrepararVolumenNegro()
    {
        if (perfilNegro == null)
        {
            // Perfil temporal: no edita los assets del proyecto.
            perfilNegro = ScriptableObject.CreateInstance<VolumeProfile>();
            perfilNegro.name = "Perfil negro de transicion (runtime)";
            ColorAdjustments ajustes = perfilNegro.Add<ColorAdjustments>();
            ajustes.colorFilter.Override(Color.black);
        }
        volumenFade.profile = perfilNegro;
        volumenFade.isGlobal = true;
        volumenFade.priority = 10000f;
        volumenFade.weight = 0f;
        volumenFade.enabled = true;
        volumenFade.gameObject.SetActive(true);
    }

    private IEnumerator EjecutarTransicion()
    {
        PrepararVolumenNegro();
        CambiarEstado(EstadoTransicion.Oscureciendo);
        yield return FundirHasta(1f, duracionOscurecer);

        // Mostrar al menos un frame completamente oscuro antes de cargar.
        yield return null;
        CambiarEstado(EstadoTransicion.Cargando);

        AsyncOperation carga = null;
        try
        {
            carga = SceneManager.LoadSceneAsync(nombreEscena, LoadSceneMode.Single);
        }
        catch (System.Exception error)
        {
            Debug.LogError("No se pudo cargar la escena: " + error.Message, this);
        }

        if (carga == null)
        {
            // Recuperar la imagen de la escena actual si la carga falla.
            yield return FundirHasta(0f, duracionAclarar);
            ApagarVolumen();
            CambiarEstado(EstadoTransicion.Esperando);
            transicionEnCurso = null;
            rutina = null;
            yield break;
        }

        while (!carga.isDone) yield return null;

        // El mismo Volume sigue en Weight = 1 en la nueva escena.
        yield return null;
        CambiarEstado(EstadoTransicion.Aclarando);
        yield return FundirHasta(0f, duracionAclarar);
        ApagarVolumen();
        CambiarEstado(EstadoTransicion.Completado);
        transicionEnCurso = null;
        rutina = null;
    }

    private IEnumerator FundirHasta(float destino, float duracion)
    {
        float inicio = volumenFade.weight;
        float transcurrido = 0f;
        duracion = Mathf.Max(0.01f, duracion);
        while (transcurrido < duracion)
        {
            transcurrido += Time.unscaledDeltaTime;
            float progreso = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(transcurrido / duracion));
            volumenFade.weight = Mathf.Lerp(inicio, destino, progreso);
            yield return null;
        }
        volumenFade.weight = destino;
    }

    private void ApagarVolumen()
    {
        if (volumenFade == null) return;
        volumenFade.weight = 0f;
        volumenFade.gameObject.SetActive(false);
    }

    private void CambiarEstado(EstadoTransicion nuevoEstado)
    {
        estadoActual = nuevoEstado;
        if (mostrarMensajes)
            Debug.Log("[TransicionEscenaConFade] " + nuevoEstado, this);
    }

    private void OnDisable()
    {
        if (rutina != null)
        {
            StopCoroutine(rutina);
            rutina = null;
            estadoActual = EstadoTransicion.Esperando;
        }
        ApagarVolumen();
        if (transicionEnCurso == this) transicionEnCurso = null;
    }

    private void OnDestroy()
    {
        if (volumenFade != null && perfilNegro != null) volumenFade.profile = null;
        if (perfilNegro == null) return;
        foreach (VolumeComponent componente in perfilNegro.components)
            if (componente != null) Destroy(componente);
        Destroy(perfilNegro);
    }
}

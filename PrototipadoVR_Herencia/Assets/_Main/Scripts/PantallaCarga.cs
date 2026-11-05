using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Pantalla de carga dentro de la escena Inicio.
// Conectar el metodo Comenzar() al On Click () del boton "Comenzar".
// Al presionarlo: oculta la pantalla de inicio, funde el fondo a color solido,
// muestra "Espereme mijo." -> ".." -> "..." en loop mientras carga la escena
// destino por detras, y la activa cuando pasa la duracion minima.
[DisallowMultipleComponent]
public class PantallaCarga : MonoBehaviour
{
    [Header("Pantalla de inicio")]
    [Tooltip("Objetos que se ocultan al presionar Comenzar (Logo, Boton_Comenzar, Copyright).")]
    [SerializeField] private GameObject[] ocultarAlComenzar;

    [Tooltip("Image del fondo cafe. Se vuelve opaco al comenzar. Opcional.")]
    [SerializeField] private Image fondo;
    [SerializeField, Min(0f)] private float duracionFundido = 0.5f;

    [Header("Pantalla de carga")]
    [Tooltip("Objeto con el Image de 'Espereme mijo'. Se oculta al iniciar la escena.")]
    [SerializeField] private GameObject panelCarga;
    [SerializeField] private Image imagenCarga;
    [Tooltip("En orden: carga_1, carga_2, carga_3. Se repiten en loop.")]
    [SerializeField] private Sprite[] cuadros;
    [SerializeField, Min(0.05f)] private float segundosPorCuadro = 0.5f;

    [Header("Escena destino")]
    [Tooltip("Nombre exacto de la escena, debe estar en la Scene List del Build Profile.")]
    [SerializeField] private string escenaDestino = "Acto 1";
    [Tooltip("Segundos minimos que se muestra la pantalla de carga.")]
    [SerializeField, Min(0f)] private float duracionMinima = 10f;

    private bool iniciado;
    private int cuadroActual;
    private float tiempoCuadro;

    private void Awake()
    {
        if (panelCarga != null) panelCarga.SetActive(false);
    }

    public void Comenzar()
    {
        if (iniciado) return;

        string destino = escenaDestino == null ? "" : escenaDestino.Trim();
        if (string.IsNullOrEmpty(destino) || !Application.CanStreamedLevelBeLoaded(destino))
        {
            Debug.LogError("PantallaCarga: revisa Escena Destino y agregala a la Scene List del Build Profile.", this);
            return;
        }

        iniciado = true;
        StartCoroutine(Secuencia(destino));
    }

    private IEnumerator Secuencia(string destino)
    {
        foreach (GameObject objeto in ocultarAlComenzar)
            if (objeto != null) objeto.SetActive(false);

        if (fondo != null && duracionFundido > 0f)
        {
            Color inicial = fondo.color;
            Color final = new Color(inicial.r, inicial.g, inicial.b, 1f);
            for (float t = 0f; t < duracionFundido; t += Time.unscaledDeltaTime)
            {
                fondo.color = Color.Lerp(inicial, final, t / duracionFundido);
                yield return null;
            }
            fondo.color = final;
        }
        else if (fondo != null)
        {
            Color c = fondo.color; c.a = 1f; fondo.color = c;
        }

        if (panelCarga != null) panelCarga.SetActive(true);
        cuadroActual = 0;
        tiempoCuadro = 0f;
        MostrarCuadro();

        // Carga por detras sin activar la escena todavia.
        AsyncOperation carga = SceneManager.LoadSceneAsync(destino, LoadSceneMode.Single);
        carga.allowSceneActivation = false;

        float transcurrido = 0f;
        while (transcurrido < duracionMinima || carga.progress < 0.9f)
        {
            transcurrido += Time.unscaledDeltaTime;
            Animar();
            yield return null;
        }

        carga.allowSceneActivation = true;
        while (!carga.isDone)
        {
            Animar();
            yield return null;
        }
    }

    private void Animar()
    {
        if (cuadros == null || cuadros.Length == 0) return;
        tiempoCuadro += Time.unscaledDeltaTime;
        if (tiempoCuadro < segundosPorCuadro) return;
        tiempoCuadro -= segundosPorCuadro;
        cuadroActual = (cuadroActual + 1) % cuadros.Length;
        MostrarCuadro();
    }

    private void MostrarCuadro()
    {
        if (imagenCarga == null || cuadros == null || cuadros.Length == 0) return;
        Sprite s = cuadros[cuadroActual];
        if (s != null) imagenCarga.sprite = s;
    }
}

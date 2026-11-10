using UnityEngine;
using Oculus.Interaction;

// Aura de luz sobre un snap (por ejemplo la maceta) para indicarle al jugador donde colocar un objeto.
// Se enciende cuando la secuencia de las semillas llega al momento elegido y se apaga cuando algo encaja en el snap.
// Colocar en un Empty ubicado sobre la maceta. Las particulas se crean solas.
[DisallowMultipleComponent]
public class LuzGuiaSnap : MonoBehaviour
{
    public enum MomentoEncendido { AlIniciarEstado3, AlTerminarRecorrido }

    [Header("Cuando se enciende")]
    [Tooltip("Script Semillas (objeto Activador semillas). Si queda vacio, la luz se enciende desde el inicio.")]
    [SerializeField] private Semillas semillas;
    [Tooltip("Al terminar recorrido: cuando Yta ya llego a la huerta. Al iniciar Estado 3: apenas Yta empieza a caminar hacia la huerta.")]
    [SerializeField] private MomentoEncendido momento = MomentoEncendido.AlTerminarRecorrido;
    [SerializeField, Min(0f)] private float esperaTrasMomento = 0.5f;

    [Header("Cuando se apaga")]
    [Tooltip("Snap donde se coloca el objeto. La luz se apaga cuando algo encaja.")]
    [SerializeField] private SnapInteractable snap;

    [Header("Luz")]
    [Tooltip("Verde tierra con un toque dorado, como el brillo de la maceta.")]
    [SerializeField] private Color colorLuz = new Color(0.70f, 0.78f, 0.28f, 1f);
    [Tooltip("Opcional. Si esta vacio se crea un material aditivo suave con el shader de particulas de URP.")]
    [SerializeField] private Material materialLuz;
    [SerializeField, Min(0f)] private float particulasPorSegundo = 35f;
    [SerializeField, Min(0.001f)] private float tamanoMin = 0.03f;
    [SerializeField, Min(0.001f)] private float tamanoMax = 0.055f;
    [SerializeField, Min(0.1f)] private float vidaMin = 2f;
    [SerializeField, Min(0.1f)] private float vidaMax = 3.5f;
    [SerializeField, Min(0f)] private float velocidadSubida = 0.12f;
    [SerializeField, Min(0.01f)] private float radioZona = 0.25f;

    [Header("Halo suave")]
    [SerializeField] private bool usarHalo = true;
    [SerializeField, Min(0.05f)] private float tamanoHalo = 0.9f;
    [Range(0f, 1f)] [SerializeField] private float opacidadHalo = 0.35f;

    [Header("Transicion")]
    [SerializeField, Min(0.05f)] private float segundosEncendido = 2f;
    [SerializeField, Min(0.05f)] private float segundosApagado = 1.5f;

    [Header("Seguimiento")]
    [SerializeField] private bool mostrarMensajes = false;

    private ParticleSystem motas;
    private ParticleSystem halo;
    private Material materialCreado;
    private Texture2D texturaSuave;
    private IInteractableView vistaSnap;
    private float intensidad;
    private float instanteEncendido = -1f;
    private bool apagando;

    private void Awake()
    {
        Material mat = materialLuz != null ? materialLuz : CrearMaterial();
        motas = CrearSistema("Luz (generada)", mat, false);
        if (usarHalo) halo = CrearSistema("Halo (generado)", mat, true);
        if (motas != null) motas.Play();
        if (halo != null) halo.Play();
        if (semillas == null) instanteEncendido = Time.time; // sin Semillas: encendida desde el inicio
    }

    private void OnEnable()
    {
        if (snap == null) return;
        vistaSnap = snap;
        vistaSnap.WhenSelectingInteractorViewAdded += AlHacerSnap;
    }

    private void OnDisable()
    {
        if (vistaSnap != null) vistaSnap.WhenSelectingInteractorViewAdded -= AlHacerSnap;
        vistaSnap = null;
    }

    private void AlHacerSnap(IInteractorView interactor)
    {
        Apagar();
    }

    // Tambien pueden llamarse desde eventos.
    public void Encender()
    {
        if (instanteEncendido < 0f) instanteEncendido = Time.time;
    }

    public void Apagar()
    {
        if (apagando) return;
        apagando = true;
        if (mostrarMensajes) Debug.Log("[LuzGuiaSnap] Algo encajo en el snap: la luz se apaga.", this);
    }

    private ParticleSystem CrearSistema(string nombre, Material mat, bool esHalo)
    {
        GameObject go = new GameObject(nombre);
        go.transform.SetParent(transform, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = esHalo ? 6 : 100;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        if (esHalo)
        {
            main.startLifetime = 3f;
            main.startSpeed = 0f;
            main.startSize = tamanoHalo;
            Color c = colorLuz; c.a = opacidadHalo;
            main.startColor = c;
        }
        else
        {
            main.startLifetime = new ParticleSystem.MinMaxCurve(vidaMin, vidaMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(velocidadSubida * 0.5f, velocidadSubida);
            main.startSize = new ParticleSystem.MinMaxCurve(tamanoMin, tamanoMax);
            main.startColor = colorLuz;
            main.gravityModifier = -0.005f;
        }

        var emision = ps.emission;
        emision.enabled = true;
        emision.rateOverTime = 0f;

        var forma = ps.shape;
        forma.enabled = true;
        forma.shapeType = ParticleSystemShapeType.Sphere;
        forma.radius = esHalo ? 0.01f : radioZona;

        var color = ps.colorOverLifetime;
        color.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0f, 1f) });
        color.color = g;

        if (!esHalo)
        {
            var tamano = ps.sizeOverLifetime;
            tamano.enabled = true;
            tamano.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.5f), new Keyframe(0.35f, 1f), new Keyframe(1f, 0.3f)));

            var ruido = ps.noise;
            ruido.enabled = true;
            ruido.strength = 0.05f;
            ruido.frequency = 0.8f;
            ruido.scrollSpeed = 0.3f;
            ruido.damping = true;
        }

        var render = go.GetComponent<ParticleSystemRenderer>();
        render.renderMode = ParticleSystemRenderMode.Billboard;
        render.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        render.receiveShadows = false;
        render.sharedMaterial = mat;
        return ps;
    }

    private Material CrearMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        bool urp = shader != null;
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            Debug.LogError("LuzGuiaSnap: no se encontro un shader para las particulas. Asigna un Material Luz.", this);
            return null;
        }

        texturaSuave = CrearTexturaSuave(64);
        materialCreado = new Material(shader) { name = "Luz guia snap (generado)" };
        if (materialCreado.HasProperty("_BaseMap")) materialCreado.SetTexture("_BaseMap", texturaSuave);
        if (materialCreado.HasProperty("_MainTex")) materialCreado.SetTexture("_MainTex", texturaSuave);
        if (materialCreado.HasProperty("_BaseColor")) materialCreado.SetColor("_BaseColor", Color.white);

        if (urp)
        {
            // Transparente con mezcla aditiva: suma luz en vez de tapar.
            materialCreado.SetFloat("_Surface", 1f);
            materialCreado.SetFloat("_Blend", 2f);
            materialCreado.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            materialCreado.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            materialCreado.SetFloat("_ZWrite", 0f);
            materialCreado.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            materialCreado.SetOverrideTag("RenderType", "Transparent");
            materialCreado.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        return materialCreado;
    }

    private static Texture2D CrearTexturaSuave(int tam)
    {
        Texture2D tex = new Texture2D(tam, tam, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = "Luz suave (generada)"
        };
        float c = (tam - 1) * 0.5f;
        for (int y = 0; y < tam; y++)
        {
            for (int x = 0; x < tam; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float a = Mathf.Clamp01(1f - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        }
        tex.Apply();
        return tex;
    }

    private void Update()
    {
        // Enciende cuando la secuencia de las semillas llega al momento elegido.
        if (instanteEncendido < 0f && semillas != null && MomentoAlcanzado())
        {
            instanteEncendido = Time.time + esperaTrasMomento;
            if (mostrarMensajes) Debug.Log("[LuzGuiaSnap] Momento alcanzado (" + momento + "): la luz se enciende.", this);
        }

        bool encender = !apagando && instanteEncendido >= 0f && Time.time >= instanteEncendido;
        float destino = encender ? 1f : 0f;
        float segundos = encender ? segundosEncendido : segundosApagado;
        intensidad = Mathf.MoveTowards(intensidad, destino, Time.deltaTime / segundos);

        if (motas != null)
        {
            var e = motas.emission;
            e.rateOverTime = particulasPorSegundo * intensidad;
        }
        if (halo != null)
        {
            var e = halo.emission;
            e.rateOverTime = 1.2f * intensidad;
        }

        if (apagando && intensidad <= 0f) enabled = false;
    }

    private bool MomentoAlcanzado()
    {
        if (momento == MomentoEncendido.AlTerminarRecorrido) return semillas.RutaTerminada;
        return semillas.EstadoActual == Semillas.Estado.Estado3;
    }

    private void OnDestroy()
    {
        if (materialCreado != null) Destroy(materialCreado);
        if (texturaSuave != null) Destroy(texturaSuave);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.7f, 0.8f, 0.3f, 0.7f);
        Gizmos.DrawWireSphere(transform.position, radioZona);
    }
}

using UnityEngine;
using Oculus.Interaction;

// Particulas de luz que senalan un objeto (la carta del cajon) para indicarle al jugador que debe tomarlo.
// Colocar en un Empty ubicado sobre la carta. Las particulas se crean solas.
// Se apagan de forma suave cuando el jugador agarra la carta y no vuelven a aparecer.
[DisallowMultipleComponent]
public class LuzGuiaCarta : MonoBehaviour
{
    [Header("Objeto que se debe tomar")]
    [Tooltip("Grabbable de la carta. Al agarrarla, la luz se apaga.")]
    [SerializeField] private Grabbable carta;
    [Tooltip("Respaldo: tambien se apaga si la carta se aleja de su sitio inicial esta distancia (metros).")]
    [SerializeField, Min(0.01f)] private float distanciaApagadoRespaldo = 0.25f;

    [Header("Luz")]
    [Tooltip("Mismo dorado calido del brillo de la carta.")]
    [SerializeField] private Color colorLuz = new Color(1f, 0.78f, 0.35f, 1f);
    [Tooltip("Opcional. Si esta vacio se crea un material aditivo suave con el shader de particulas de URP.")]
    [SerializeField] private Material materialLuz;
    [SerializeField, Min(0f)] private float particulasPorSegundo = 14f;
    [SerializeField, Min(0.001f)] private float tamanoMin = 0.015f;
    [SerializeField, Min(0.001f)] private float tamanoMax = 0.035f;
    [SerializeField, Min(0.1f)] private float vidaMin = 2f;
    [SerializeField, Min(0.1f)] private float vidaMax = 3.5f;
    [SerializeField, Min(0f)] private float velocidadSubida = 0.08f;
    [SerializeField, Min(0.01f)] private float radioZona = 0.12f;

    [Header("Halo suave")]
    [SerializeField] private bool usarHalo = true;
    [SerializeField, Min(0.05f)] private float tamanoHalo = 0.35f;
    [Range(0f, 1f)] [SerializeField] private float opacidadHalo = 0.25f;

    [Header("Transicion")]
    [SerializeField, Min(0.05f)] private float segundosEncendido = 1.5f;
    [SerializeField, Min(0.05f)] private float segundosApagado = 1.5f;

    [Header("Seguimiento")]
    [SerializeField] private bool mostrarMensajes = false;

    private ParticleSystem motas;
    private ParticleSystem halo;
    private Material materialCreado;
    private Texture2D texturaSuave;
    private IPointable puntero;
    private Vector3 posicionInicialCarta;
    private float intensidad;
    private bool apagando;

    private void Awake()
    {
        if (carta != null) posicionInicialCarta = carta.transform.position;
        Material mat = materialLuz != null ? materialLuz : CrearMaterial();
        motas = CrearSistema("Luz (generada)", mat, false);
        if (usarHalo) halo = CrearSistema("Halo (generado)", mat, true);
        Play();
    }

    private void OnEnable()
    {
        if (carta == null) return;
        puntero = carta;
        puntero.WhenPointerEventRaised += AlEventoPuntero;
    }

    private void OnDisable()
    {
        if (puntero != null) puntero.WhenPointerEventRaised -= AlEventoPuntero;
        puntero = null;
    }

    private void AlEventoPuntero(PointerEvent evento)
    {
        if (evento.Type == PointerEventType.Select) Apagar();
    }

    public void Apagar()
    {
        if (apagando) return;
        apagando = true;
        if (mostrarMensajes) Debug.Log("[LuzGuiaCarta] Carta tomada: la luz se apaga.", this);
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
        main.maxParticles = esHalo ? 6 : 80;
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

        // Cada particula aparece, brilla y se desvanece.
        var color = ps.colorOverLifetime;
        color.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0f, 1f) });
        color.color = g;

        if (!esHalo)
        {
            // Titileo: la particula crece un poco y se encoge antes de morir.
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

    private void Play()
    {
        if (motas != null) motas.Play();
        if (halo != null) halo.Play();
    }

    private Material CrearMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        bool urp = shader != null;
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            Debug.LogError("LuzGuiaCarta: no se encontro un shader para las particulas. Asigna un Material Luz.", this);
            return null;
        }

        texturaSuave = CrearTexturaSuave(64);
        materialCreado = new Material(shader) { name = "Luz guia (generado)" };
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

    // Circulo con centro brillante y borde difuso.
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
                a = a * a;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return tex;
    }

    private void Update()
    {
        // Respaldo: si la carta se movio de su sitio, tambien se considera tomada.
        if (!apagando && carta != null &&
            Vector3.Distance(carta.transform.position, posicionInicialCarta) > distanciaApagadoRespaldo)
            Apagar();

        float destino = apagando ? 0f : 1f;
        float segundos = apagando ? segundosApagado : segundosEncendido;
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

        if (apagando && intensidad <= 0f)
        {
            // Ya no se necesita mas: deja terminar las particulas vivas y se desactiva.
            enabled = false;
        }
    }

    private void OnDestroy()
    {
        if (materialCreado != null) Destroy(materialCreado);
        if (texturaSuave != null) Destroy(texturaSuave);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.78f, 0.35f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, radioZona);
    }
}

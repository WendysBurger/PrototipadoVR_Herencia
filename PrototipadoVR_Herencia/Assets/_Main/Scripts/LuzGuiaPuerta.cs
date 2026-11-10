using UnityEngine;

// Particulas de luz sobre una puerta para indicarle al jugador a donde ir.
// Se encienden cuando termina la interaccion de la olla (script "cocina") y se apagan al abrir la puerta.
// Colocar en un Empty ubicado en el centro de la puerta. Las particulas se crean solas.
[DisallowMultipleComponent]
public class LuzGuiaPuerta : MonoBehaviour
{
    [Header("Cuando se enciende")]
    [Tooltip("Script 'cocina' de la olla (objeto Activacion Audio). La luz se enciende cuando termina su ultimo audio. Si queda vacio, se enciende desde el inicio.")]
    [SerializeField] private cocina olla;
    [Tooltip("Segundos de espera despues de que termina la olla.")]
    [SerializeField, Min(0f)] private float esperaTrasOlla = 1f;

    [Header("Cuando se apaga")]
    [Tooltip("Raiz de la puerta (la que rota al abrirse). La luz se apaga cuando se abre este angulo.")]
    [SerializeField] private Transform puerta;
    [SerializeField, Min(1f)] private float gradosApertura = 20f;

    [Header("Ubicacion de la luz")]
    [Tooltip("Modelo de la puerta (el hijo 'Puerta_'). Si se asigna, la luz se coloca sola sobre la puerta y toma su tamano.")]
    [SerializeField] private Transform mallaPuerta;
    [Tooltip("Grosor minimo de la zona de luz, para que las particulas floten un poco delante de la puerta.")]
    [SerializeField, Min(0.01f)] private float grosorMinimo = 0.2f;
    [Tooltip("Solo se usa si no hay Malla Puerta. Medidas en ejes del mundo (metros).")]
    [SerializeField] private Vector3 tamanoZona = new Vector3(0.1f, 1.8f, 0.8f);

    [Header("Luz")]
    [SerializeField] private Color colorLuz = new Color(1f, 0.78f, 0.35f, 1f);
    [Tooltip("Opcional. Si esta vacio se crea un material aditivo suave con el shader de particulas de URP.")]
    [SerializeField] private Material materialLuz;
    [SerializeField, Min(0f)] private float particulasPorSegundo = 40f;
    [SerializeField, Min(0.001f)] private float tamanoMin = 0.035f;
    [SerializeField, Min(0.001f)] private float tamanoMax = 0.06f;
    [SerializeField, Min(0.1f)] private float vidaMin = 2f;
    [SerializeField, Min(0.1f)] private float vidaMax = 3.5f;
    [SerializeField, Min(0f)] private float velocidadSubida = 0.15f;

    [Header("Halo suave")]
    [SerializeField] private bool usarHalo = true;
    [SerializeField, Min(0.05f)] private float tamanoHalo = 1.1f;
    [Range(0f, 1f)] [SerializeField] private float opacidadHalo = 0.3f;

    [Header("Transicion")]
    [SerializeField, Min(0.05f)] private float segundosEncendido = 2f;
    [SerializeField, Min(0.05f)] private float segundosApagado = 1.5f;

    [Header("Seguimiento")]
    [SerializeField] private bool mostrarMensajes = false;

    private ParticleSystem motas;
    private ParticleSystem halo;
    private Material materialCreado;
    private Texture2D texturaSuave;
    private Quaternion rotacionInicialPuerta;
    private float intensidad;
    private float instanteEncendido = -1f;
    private bool apagando;

    // Centro y tamano de la zona: del modelo de la puerta si existe, o los valores manuales.
    private void CalcularZona(out Vector3 centro, out Vector3 tamano)
    {
        centro = transform.position;
        tamano = tamanoZona;
        if (mallaPuerta == null) return;
        Renderer[] renders = mallaPuerta.GetComponentsInChildren<Renderer>();
        if (renders.Length == 0) return;
        Bounds b = renders[0].bounds;
        for (int i = 1; i < renders.Length; i++) b.Encapsulate(renders[i].bounds);
        centro = b.center;
        tamano = new Vector3(Mathf.Max(b.size.x, grosorMinimo), b.size.y * 0.95f, Mathf.Max(b.size.z, grosorMinimo));
    }

    private void Awake()
    {
        if (puerta != null) rotacionInicialPuerta = puerta.rotation;
        CalcularZona(out Vector3 centro, out Vector3 tamano);
        transform.position = centro;
        tamanoZona = tamano;
        Material mat = materialLuz != null ? materialLuz : CrearMaterial();
        motas = CrearSistema("Luz (generada)", mat, false);
        if (usarHalo) halo = CrearSistema("Halo (generado)", mat, true);
        if (motas != null) motas.Play();
        if (halo != null) halo.Play();
        if (olla == null) instanteEncendido = Time.time; // sin olla: encendido desde el inicio
    }

    // Tambien puede llamarse desde un evento si se quiere encender por otra via.
    public void Encender()
    {
        if (instanteEncendido < 0f) instanteEncendido = Time.time;
    }

    public void Apagar()
    {
        if (apagando) return;
        apagando = true;
        if (mostrarMensajes) Debug.Log("[LuzGuiaPuerta] Puerta abierta: la luz se apaga.", this);
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
        main.maxParticles = esHalo ? 6 : 120;
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
        forma.shapeType = ParticleSystemShapeType.Box;
        // Las particulas salen hacia +Z del emisor; se gira para que suban. Con ese giro,
        // el eje Y local del emisor es Z del mundo y el eje Z local es Y del mundo.
        go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        forma.scale = esHalo ? new Vector3(0.05f, 0.05f, 0.05f)
                             : new Vector3(tamanoZona.x, tamanoZona.z, tamanoZona.y);

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
            Debug.LogError("LuzGuiaPuerta: no se encontro un shader para las particulas. Asigna un Material Luz.", this);
            return null;
        }

        texturaSuave = CrearTexturaSuave(64);
        materialCreado = new Material(shader) { name = "Luz guia puerta (generado)" };
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
        // Enciende cuando la olla termino.
        if (instanteEncendido < 0f && olla != null && olla.Terminada)
        {
            instanteEncendido = Time.time + esperaTrasOlla;
            if (mostrarMensajes) Debug.Log("[LuzGuiaPuerta] Olla terminada: la luz se enciende.", this);
        }

        // Se apaga cuando la puerta se abre.
        if (!apagando && puerta != null &&
            Quaternion.Angle(puerta.rotation, rotacionInicialPuerta) > gradosApertura)
            Apagar();

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

    private void OnDestroy()
    {
        if (materialCreado != null) Destroy(materialCreado);
        if (texturaSuave != null) Destroy(texturaSuave);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.78f, 0.35f, 0.7f);
        CalcularZona(out Vector3 centro, out Vector3 tamano);
        Gizmos.DrawWireCube(centro, tamano);
    }
}

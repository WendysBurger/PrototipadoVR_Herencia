using UnityEngine;

// Humo suave que sale de la olla cuando el jugador se acerca.
// Colocar en un Empty ubicado justo sobre la boca de la olla: el humo nace en la posicion de este objeto.
// El sistema de particulas se crea solo; no hace falta configurar colliders ni tags.
[DisallowMultipleComponent]
public class HumoOlla : MonoBehaviour
{
    [Header("Jugador")]
    [Tooltip("Opcional. Si esta vacio se usa la camara principal (la cabeza en VR).")]
    [SerializeField] private Transform jugador;

    [Header("Distancia (metros)")]
    [SerializeField, Min(0.1f)] private float distanciaActivacion = 2.5f;
    [Tooltip("Se apaga un poco mas lejos que la distancia de activacion para que no parpadee.")]
    [SerializeField, Min(0f)] private float margenApagado = 0.6f;

    [Header("Humo")]
    [Tooltip("Opcional. Si esta vacio se crea un material suave con el shader de particulas de URP.")]
    [SerializeField] private Material materialHumo;
    [SerializeField] private Color colorHumo = new Color(0.92f, 0.92f, 0.92f, 0.5f);
    [SerializeField, Min(0f)] private float particulasPorSegundo = 9f;
    [SerializeField, Min(0.01f)] private float tamanoMin = 0.12f;
    [SerializeField, Min(0.01f)] private float tamanoMax = 0.22f;
    [SerializeField, Min(0.1f)] private float vidaMin = 2.5f;
    [SerializeField, Min(0.1f)] private float vidaMax = 3.5f;
    [SerializeField, Min(0f)] private float velocidadSubida = 0.2f;
    [SerializeField, Min(0f)] private float radioBoca = 0.07f;
    [SerializeField, Min(0f)] private float ondulacion = 0.12f;

    [Header("Transicion")]
    [Tooltip("Segundos que tarda el humo en llegar a su intensidad completa (o en apagarse).")]
    [SerializeField, Min(0.05f)] private float segundosTransicion = 1.2f;

    [Header("Seguimiento")]
    [SerializeField] private bool mostrarMensajes = false;

    private ParticleSystem sistema;
    private ParticleSystem.EmissionModule emision;
    private Material materialCreado;
    private Texture2D texturaSuave;
    private float intensidad;
    private bool activo;
    private float proximaBusqueda;

    private void Awake()
    {
        CrearSistema();
    }

    private void CrearSistema()
    {
        GameObject go = new GameObject("Humo (generado)");
        go.transform.SetParent(transform, false);
        // Las particulas de forma "Cone" salen hacia +Z; se gira para que salgan hacia arriba.
        go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

        sistema = go.AddComponent<ParticleSystem>();
        sistema.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = sistema.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(vidaMin, vidaMax);
        main.startSpeed = new ParticleSystem.MinMaxCurve(velocidadSubida * 0.6f, velocidadSubida);
        main.startSize = new ParticleSystem.MinMaxCurve(tamanoMin, tamanoMax);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = colorHumo;
        main.gravityModifier = -0.01f;
        main.maxParticles = 80;

        emision = sistema.emission;
        emision.enabled = true;
        emision.rateOverTime = 0f;

        var forma = sistema.shape;
        forma.enabled = true;
        forma.shapeType = ParticleSystemShapeType.Cone;
        forma.angle = 8f;
        forma.radius = radioBoca;

        // Aparece y se disipa suavemente.
        var color = sistema.colorOverLifetime;
        color.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
        color.color = g;

        // Crece mientras sube.
        var tamano = sistema.sizeOverLifetime;
        tamano.enabled = true;
        tamano.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.6f), new Keyframe(1f, 1.7f)));

        var giro = sistema.rotationOverLifetime;
        giro.enabled = true;
        giro.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);

        var ruido = sistema.noise;
        ruido.enabled = ondulacion > 0f;
        ruido.strength = ondulacion;
        ruido.frequency = 0.4f;
        ruido.scrollSpeed = 0.2f;
        ruido.damping = true;

        var render = go.GetComponent<ParticleSystemRenderer>();
        render.renderMode = ParticleSystemRenderMode.Billboard;
        render.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        render.receiveShadows = false;
        render.sharedMaterial = materialHumo != null ? materialHumo : CrearMaterial();

        sistema.Play();
    }

    private Material CrearMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        bool urp = shader != null;
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            Debug.LogError("HumoOlla: no se encontro un shader para el humo. Asigna un Material Humo.", this);
            return null;
        }

        texturaSuave = CrearTexturaSuave(64);
        materialCreado = new Material(shader) { name = "Humo (generado)" };
        if (materialCreado.HasProperty("_BaseMap")) materialCreado.SetTexture("_BaseMap", texturaSuave);
        if (materialCreado.HasProperty("_MainTex")) materialCreado.SetTexture("_MainTex", texturaSuave);
        if (materialCreado.HasProperty("_BaseColor")) materialCreado.SetColor("_BaseColor", Color.white);

        if (urp)
        {
            // Transparente con mezcla alfa normal.
            materialCreado.SetFloat("_Surface", 1f);
            materialCreado.SetFloat("_Blend", 0f);
            materialCreado.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            materialCreado.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            materialCreado.SetFloat("_ZWrite", 0f);
            materialCreado.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            materialCreado.SetOverrideTag("RenderType", "Transparent");
            materialCreado.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        return materialCreado;
    }

    // Circulo con borde difuso, para que cada particula parezca una bocanada y no un disco.
    private static Texture2D CrearTexturaSuave(int tam)
    {
        Texture2D tex = new Texture2D(tam, tam, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = "Humo suave (generada)"
        };
        float c = (tam - 1) * 0.5f;
        for (int y = 0; y < tam; y++)
        {
            for (int x = 0; x < tam; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a); // suavizado
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return tex;
    }

    private void Update()
    {
        if (sistema == null) return;

        Transform objetivo = BuscarJugador();
        if (objetivo != null)
        {
            float d = Vector3.Distance(objetivo.position, transform.position);
            if (!activo && d <= distanciaActivacion)
            {
                activo = true;
                if (mostrarMensajes) Debug.Log("[HumoOlla] Jugador cerca: sale humo.", this);
            }
            else if (activo && d > distanciaActivacion + margenApagado)
            {
                activo = false;
                if (mostrarMensajes) Debug.Log("[HumoOlla] Jugador lejos: el humo se apaga.", this);
            }
        }

        float destino = activo ? 1f : 0f;
        intensidad = Mathf.MoveTowards(intensidad, destino, Time.deltaTime / segundosTransicion);
        emision.rateOverTime = particulasPorSegundo * intensidad;
    }

    private Transform BuscarJugador()
    {
        if (jugador != null) return jugador;
        if (Time.unscaledTime < proximaBusqueda) return cacheCamara;
        proximaBusqueda = Time.unscaledTime + 0.5f;
        Camera cam = Camera.main;
        if (cam == null)
        {
            foreach (Camera c in Camera.allCameras)
                if (c.enabled && c.cameraType == CameraType.Game) { cam = c; break; }
        }
        cacheCamara = cam != null ? cam.transform : null;
        return cacheCamara;
    }

    private Transform cacheCamara;

    private void OnDestroy()
    {
        if (materialCreado != null) Destroy(materialCreado);
        if (texturaSuave != null) Destroy(texturaSuave);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 1f, 1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, 0.05f);
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, distanciaActivacion);
        Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, distanciaActivacion + margenApagado);
    }
}

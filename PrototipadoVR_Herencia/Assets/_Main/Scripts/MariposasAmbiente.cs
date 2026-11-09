using UnityEngine;

// Mariposas de ambiente. Se crean solas al iniciar (no necesita modelos ni texturas):
// cada una es un cuerpo con dos alas que aletean y vuela entre puntos al azar
// dentro de un circulo alrededor de este objeto, a poca altura sobre el terreno.
// Uso: poner este script en un objeto vacio, ubicarlo en el centro de la zona y ajustar el Radio.
[DisallowMultipleComponent]
public class MariposasAmbiente : MonoBehaviour
{
    [Header("Zona (centro = este objeto)")]
    [SerializeField, Min(1f)] private float radio = 25f;
    [Tooltip("Altura sobre el terreno (m).")]
    [SerializeField] private float alturaMin = 0.6f;
    [SerializeField] private float alturaMax = 2.2f;
    [Tooltip("Si hay Terrain activo, la altura se mide sobre el. Si no, sobre la altura de este objeto.")]
    [SerializeField] private bool seguirTerreno = true;

    [Header("Mariposas")]
    [SerializeField, Range(1, 200)] private int cantidad = 35;
    [SerializeField, Min(0.05f)] private float velocidadMin = 0.5f;
    [SerializeField, Min(0.05f)] private float velocidadMax = 1.2f;
    [Tooltip("Multiplicador de tamano (1 = 22 cm de envergadura).")]
    [SerializeField, Min(0.1f)] private float tamanoMin = 0.8f;
    [SerializeField, Min(0.1f)] private float tamanoMax = 1.6f;
    [SerializeField, Min(1f)] private float aleteoMin = 6f;
    [SerializeField, Min(1f)] private float aleteoMax = 10f;
    [SerializeField, Range(10f, 80f)] private float anguloAleteo = 55f;

    [Header("Aspecto")]
    [Tooltip("Material URP/Unlit con Cull Off (Mariposa.mat). Si esta vacio se crea uno por codigo.")]
    [SerializeField] private Material material;
    [SerializeField] private Color[] colores =
    {
        new Color(1.00f, 0.62f, 0.12f), // naranja
        new Color(1.00f, 0.88f, 0.25f), // amarillo
        new Color(0.97f, 0.97f, 0.92f), // blanca
        new Color(0.35f, 0.62f, 0.95f), // azul
        new Color(0.85f, 0.35f, 0.55f), // rosada
    };

    private class Mariposa
    {
        public Transform raiz, izq, der;
        public Vector3 destino;
        public float velocidad, aleteo, fase, giro, tCambio;
    }

    private Mariposa[] mariposas;
    private Mesh malla;
    private Material[] materiales;

    private void Start()
    {
        if (colores == null || colores.Length == 0) colores = new[] { Color.white };
        malla = CrearAla();
        CrearMateriales();

        mariposas = new Mariposa[cantidad];
        for (int i = 0; i < cantidad; i++)
            mariposas[i] = Crear(i);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        foreach (Mariposa m in mariposas)
        {
            if (m == null) continue;
            Vector3 pos = m.raiz.position;
            Vector3 hacia = m.destino - pos;
            m.tCambio -= dt;
            if (hacia.sqrMagnitude < 0.25f || m.tCambio <= 0f)
            {
                m.destino = PuntoAleatorio();
                m.tCambio = Random.Range(4f, 9f);
                hacia = m.destino - pos;
            }

            // Gira suave hacia el destino y avanza, con ondulacion vertical de vuelo.
            Quaternion objetivo = Quaternion.LookRotation(hacia.normalized, Vector3.up);
            m.raiz.rotation = Quaternion.Slerp(m.raiz.rotation, objetivo, dt * 1.5f);
            float ondula = Mathf.Sin(Time.time * 2.2f + m.fase) * 0.35f;
            Vector3 avance = m.raiz.forward * m.velocidad + Vector3.up * ondula;
            m.raiz.position = pos + avance * dt;

            // Aleteo.
            float a = Mathf.Sin(Time.time * m.aleteo * Mathf.PI * 2f + m.fase) * anguloAleteo;
            m.der.localRotation = Quaternion.Euler(0f, 0f, a);
            m.izq.localRotation = Quaternion.Euler(0f, 0f, -a);
        }
    }

    private Mariposa Crear(int i)
    {
        var m = new Mariposa();
        GameObject go = new GameObject("Mariposa " + i);
        go.transform.SetParent(transform, false);
        m.raiz = go.transform;
        m.raiz.position = PuntoAleatorio();
        m.raiz.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        m.raiz.localScale = Vector3.one * Random.Range(tamanoMin, tamanoMax);
        m.destino = PuntoAleatorio();
        m.velocidad = Random.Range(velocidadMin, velocidadMax);
        m.aleteo = Random.Range(aleteoMin, aleteoMax);
        m.fase = Random.Range(0f, 100f);
        m.tCambio = Random.Range(2f, 8f);

        Material mat = materiales[Random.Range(0, materiales.Length)];
        m.der = CrearAlaObjeto("Ala der", m.raiz, 1f, mat);
        m.izq = CrearAlaObjeto("Ala izq", m.raiz, -1f, mat);
        return m;
    }

    private Transform CrearAlaObjeto(string nombre, Transform padre, float lado, Material mat)
    {
        GameObject go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.transform.localScale = new Vector3(lado, 1f, 1f);
        go.AddComponent<MeshFilter>().sharedMesh = malla;
        MeshRenderer r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return go.transform;
    }

    private Vector3 PuntoAleatorio()
    {
        Vector2 c = Random.insideUnitCircle * radio;
        Vector3 p = transform.position + new Vector3(c.x, 0f, c.y);
        float baseY = transform.position.y;
        Terrain t = seguirTerreno ? Terrain.activeTerrain : null;
        if (t != null) baseY = t.SampleHeight(p) + t.transform.position.y;
        p.y = baseY + Random.Range(alturaMin, alturaMax);
        return p;
    }

    private void CrearMateriales()
    {
        materiales = new Material[colores.Length];
        for (int i = 0; i < colores.Length; i++)
        {
            Material mat;
            if (material != null) mat = new Material(material);
            else
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
                if (sh == null) sh = Shader.Find("Unlit/Color");
                mat = new Material(sh);
                if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", colores[i]);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", colores[i]);
            materiales[i] = mat;
        }
    }

    // Ala derecha en el plano XZ, con la bisagra en el cuerpo (x = 0). Cuerpo mira a +Z.
    private static Mesh CrearAla()
    {
        var m = new Mesh { name = "AlaMariposa" };
        m.vertices = new[]
        {
            new Vector3(0.000f, 0f,  0.020f),
            new Vector3(0.090f, 0f,  0.075f),
            new Vector3(0.110f, 0f, -0.005f),
            new Vector3(0.070f, 0f, -0.070f),
            new Vector3(0.000f, 0f, -0.040f),
        };
        m.triangles = new[] { 0, 1, 2, 0, 2, 3, 0, 3, 4 };
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }
}

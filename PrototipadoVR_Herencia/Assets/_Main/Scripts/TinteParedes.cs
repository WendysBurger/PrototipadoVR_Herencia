using UnityEngine;

// Cambia el color de las paredes de la casa en ESTA escena, sin tocar el material original
// (usa MaterialPropertyBlock), asi cada acto puede verlas distinto:
//   Acto 2 -> blanco "apagado" (gris calido)   Acto 3 -> blanco puro.
// Busca en la escena los objetos cuyo nombre (o el de su material) contenga alguna palabra clave.
// Opcional: arrancar con un color y pasar poco a poco al color final (Segundos Transicion > 0).
[ExecuteAlways]
[DisallowMultipleComponent]
public class TinteParedes : MonoBehaviour
{
    [Header("Que paredes")]
    [Tooltip("Palabras que debe contener el nombre del objeto o de su material (sin importar mayusculas).")]
    [SerializeField] private string[] palabrasClave = { "pared atras", "pared derecha", "pared frente", "pared izquierda", "paredes_cuartos", "pintura_casa" };

    [Header("Color")]
    [Tooltip("Color final de las paredes en este acto. Blanco puro = (255,255,255).")]
    [SerializeField] private Color color = new Color(0.70f, 0.68f, 0.64f, 1f);

    [Header("Transicion (solo al dar Play)")]
    [Tooltip("0 = el color se aplica directo. Mayor que 0 = parte de Color Inicial y llega a Color en esos segundos.")]
    [SerializeField, Min(0f)] private float segundosTransicion = 0f;
    [SerializeField] private Color colorInicial = new Color(0.45f, 0.43f, 0.40f, 1f);

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private MaterialPropertyBlock bloque;
    private Renderer[] objetivos;
    private float t;

    private void OnEnable()
    {
        Buscar();
        t = 0f;
        Aplicar(Application.isPlaying && segundosTransicion > 0f ? colorInicial : color);
    }

    private void OnDisable() { Aplicar(null); }

    private void OnValidate()
    {
        if (!isActiveAndEnabled) return;
        Buscar();
        Aplicar(color);
    }

    private void Update()
    {
        if (!Application.isPlaying || segundosTransicion <= 0f || t >= 1f) return;
        t = Mathf.Min(1f, t + Time.deltaTime / segundosTransicion);
        Aplicar(Color.Lerp(colorInicial, color, Mathf.SmoothStep(0f, 1f, t)));
    }

    private void Buscar()
    {
        var lista = new System.Collections.Generic.List<Renderer>();
        foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            if (Coincide(r)) lista.Add(r);
        }
        objetivos = lista.ToArray();
    }

    private bool Coincide(Renderer r)
    {
        if (palabrasClave == null) return false;
        string nombre = r.gameObject.name.ToLowerInvariant();
        foreach (string p in palabrasClave)
        {
            if (string.IsNullOrWhiteSpace(p)) continue;
            string k = p.Trim().ToLowerInvariant();
            if (nombre.Contains(k)) return true;
            foreach (Material m in r.sharedMaterials)
                if (m != null && m.name.ToLowerInvariant().Contains(k)) return true;
        }
        return false;
    }

    // c == null quita el tinte (deja el material original).
    private void Aplicar(Color? c)
    {
        if (objetivos == null) return;
        if (bloque == null) bloque = new MaterialPropertyBlock();
        foreach (Renderer r in objetivos)
        {
            if (r == null) continue;
            if (c == null) { r.SetPropertyBlock(null); continue; }
            r.GetPropertyBlock(bloque);
            bloque.SetColor(BaseColorId, c.Value);
            bloque.SetColor(ColorId, c.Value);
            r.SetPropertyBlock(bloque);
        }
    }
}

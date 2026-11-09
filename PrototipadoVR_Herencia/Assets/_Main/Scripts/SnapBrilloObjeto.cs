using System.Collections;
using System.Collections.Generic;
using Oculus.Interaction;
using UnityEngine;

// Hace brillar (emision) un objeto, por ejemplo la maceta, cuando algo encaja en un SnapInteractable.
// Anadir a un Empty activo. No modifica el SnapInteractable ni los materiales guardados en el proyecto.
[DisallowMultipleComponent]
public class SnapBrilloObjeto : MonoBehaviour
{
    [Header("Snap de Meta que enciende el brillo")]
    [SerializeField] private SnapInteractable snapInteractable;

    [Header("Objeto que brilla (se usan todos sus Renderers, incluidos los hijos)")]
    [SerializeField] private GameObject objetoQueBrilla;

    [Header("Brillo")]
    [Tooltip("Verde tierra con un toque dorado.")]
    [SerializeField] private Color colorBrillo = new Color(0.60f, 0.66f, 0.24f, 1f);
    [SerializeField, Min(0f)] private float intensidadBrillo = 2.5f;
    [SerializeField, Min(0f)] private float segundosEncendido = 1.5f;
    [Range(0f, 0.6f)] [SerializeField] private float pulso = 0.25f;
    [SerializeField, Min(0f)] private float velocidadPulso = 1.5f;

    [Header("Comportamiento")]
    [Tooltip("Si esta activo, el brillo se apaga al sacar el objeto del snap.")]
    [SerializeField] private bool apagarAlSoltar = false;
    [SerializeField] private bool mostrarMensajes = true;

    private readonly List<Material> materiales = new List<Material>();
    private IInteractableView vistaSnap;
    private Coroutine rutina;
    private bool encendido;

    private void Reset()
    {
        snapInteractable = GetComponent<SnapInteractable>();
    }

    private void OnEnable()
    {
        if (snapInteractable == null) return;
        vistaSnap = snapInteractable;
        vistaSnap.WhenSelectingInteractorViewAdded += AlHacerSnap;
        vistaSnap.WhenSelectingInteractorViewRemoved += AlSoltarSnap;
    }

    private void Start()
    {
        if (snapInteractable == null || objetoQueBrilla == null)
        {
            Debug.LogError("SnapBrilloObjeto: asigna el Snap Interactable y el Objeto Que Brilla.", this);
            return;
        }
        // Contempla un objeto que ya estaba encajado al iniciar.
        if (vistaSnap != null)
            foreach (IInteractorView _ in vistaSnap.SelectingInteractorViews) { Encender(); break; }
    }

    private void AlHacerSnap(IInteractorView interactor)
    {
        Encender();
    }

    private void AlSoltarSnap(IInteractorView interactor)
    {
        if (!apagarAlSoltar) return;
        foreach (IInteractorView _ in vistaSnap.SelectingInteractorViews) return; // aun queda otro encajado
        Apagar();
    }

    // Tambien puede llamarse desde un evento On Select de un wrapper de Meta.
    public void Encender()
    {
        if (encendido || objetoQueBrilla == null) return;
        encendido = true;
        if (rutina != null) StopCoroutine(rutina);
        rutina = StartCoroutine(Brillar());
        if (mostrarMensajes) Debug.Log("[SnapBrilloObjeto] Snap detectado: '" + objetoQueBrilla.name + "' empieza a brillar.", this);
    }

    public void Apagar()
    {
        if (!encendido) return;
        encendido = false;
        if (rutina != null) { StopCoroutine(rutina); rutina = null; }
        foreach (Material m in materiales)
            if (m != null) m.SetColor("_EmissionColor", Color.black);
    }

    private void PrepararMateriales()
    {
        materiales.Clear();
        foreach (Renderer r in objetoQueBrilla.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer) continue;
            // renderer.materials entrega copias solo de este objeto; los materiales del proyecto no cambian.
            foreach (Material m in r.materials)
            {
                if (m == null || !m.HasProperty("_EmissionColor")) continue;
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                // Si el material tiene textura, la emision la conserva para que no se vea plano.
                if (m.HasProperty("_EmissionMap") && m.GetTexture("_EmissionMap") == null &&
                    m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null)
                    m.SetTexture("_EmissionMap", m.GetTexture("_BaseMap"));
                m.SetColor("_EmissionColor", Color.black);
                materiales.Add(m);
            }
        }
    }

    private IEnumerator Brillar()
    {
        PrepararMateriales();
        if (materiales.Count == 0)
        {
            Debug.LogWarning("SnapBrilloObjeto: '" + objetoQueBrilla.name + "' no tiene materiales con Emission.", this);
            yield break;
        }

        float t = 0f;
        while (encendido)
        {
            t += Time.deltaTime;
            float fuerza = segundosEncendido <= 0f ? 1f : Mathf.SmoothStep(0f, 1f, t / segundosEncendido);
            float respiro = 1f + pulso * Mathf.Sin(t * velocidadPulso * Mathf.PI * 2f);
            Color e = colorBrillo.linear * (intensidadBrillo * fuerza * respiro);
            foreach (Material m in materiales)
                if (m != null) m.SetColor("_EmissionColor", e);
            yield return null;
        }
    }

    private void OnDisable()
    {
        if (vistaSnap != null)
        {
            vistaSnap.WhenSelectingInteractorViewAdded -= AlHacerSnap;
            vistaSnap.WhenSelectingInteractorViewRemoved -= AlSoltarSnap;
        }
        vistaSnap = null;
        if (rutina != null) { StopCoroutine(rutina); rutina = null; }
        encendido = false;
    }
}

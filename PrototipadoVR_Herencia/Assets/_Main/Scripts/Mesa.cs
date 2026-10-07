using System.Collections.Generic;
using UnityEngine;

// Agregar al objeto que debe tocar la mesa, junto a su Rigidbody.
// Asignar este componente en Detector Mesa del trigger AudioTriggerTresEstados.
// Admite colision fisica y una zona Is Trigger sobre la mesa.
[DisallowMultipleComponent]
public class DetectorMesaObjeto : MonoBehaviour
{
    private Carta controlador;
    private readonly HashSet<Collider> contactosMesa = new HashSet<Collider>();

    public bool TocandoMesa
    {
        get
        {
            if (!isActiveAndEnabled) return false;
            // Unity no siempre envia Exit cuando se destruye o desactiva un collider.
            contactosMesa.RemoveWhere(c => c == null || !c.enabled ||
                !c.gameObject.activeInHierarchy || controlador == null || !controlador.EsMesa(c));
            return contactosMesa.Count > 0;
        }
    }

    public bool Vincular(Carta nuevoControlador)
    {
        if (controlador != null && controlador != nuevoControlador)
        {
            Debug.LogError("Este DetectorMesaObjeto ya esta asignado a otra secuencia. Usa un detector por objeto y secuencia.", this);
            return false;
        }
        controlador = nuevoControlador;
        contactosMesa.Clear();
        return true;
    }

    public void Desvincular(Carta anteriorControlador)
    {
        if (controlador != anteriorControlador) return;
        controlador = null;
        contactosMesa.Clear();
    }

    private void RegistrarContacto(Collider other)
    {
        if (!isActiveAndEnabled || controlador == null || !controlador.EsMesa(other)) return;
        contactosMesa.Add(other);
        // El controlador solo lo acepta en Estado3 y una sola vez.
        controlador.IntentarCompletarEstado3();
    }

    private void OnCollisionEnter(Collision collision)
    {
        RegistrarContacto(collision.collider);
    }

    private void OnCollisionStay(Collision collision)
    {
        RegistrarContacto(collision.collider);
    }

    private void OnCollisionExit(Collision collision)
    {
        contactosMesa.Remove(collision.collider);
    }

    private void OnTriggerEnter(Collider other)
    {
        RegistrarContacto(other);
    }

    private void OnTriggerStay(Collider other)
    {
        RegistrarContacto(other);
    }

    private void OnTriggerExit(Collider other)
    {
        contactosMesa.Remove(other);
    }

    private void OnDisable()
    {
        contactosMesa.Clear();
    }
}
using System.Collections.Generic;
using UnityEngine;

// Cuelga del objeto "BloqueoMoto". Sus colliders solo estorban cuando
// el personaje va en moto (pasto, jardines, casas); a pie se puede pasar.
public class BloqueoMoto : MonoBehaviour
{
    private static bool motoActiva;
    private static readonly List<BloqueoMoto> todos = new List<BloqueoMoto>();

    private Collider2D[] colisiones;

    private void Awake()
    {
        colisiones = GetComponentsInChildren<Collider2D>(true);
        todos.Add(this);
        Aplicar();
    }

    private void OnDestroy()
    {
        todos.Remove(this);
    }

    public static void Activar(bool valor)
    {
        motoActiva = valor;
        foreach (var b in todos) b.Aplicar();
    }

    private void Aplicar()
    {
        foreach (var c in colisiones) c.enabled = motoActiva;
    }
}
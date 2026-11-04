using System.Collections.Generic;
using UnityEngine;

// Semáforo de un cruce con dos ejes: este-oeste (autos que van de lado) y norte-sur. Ciclo fijo: un eje en
// verde y el otro en rojo, con ámbar entre cambios. Los autos (TrafficCar) solo entran al cruce con su eje
// en verde y los peatones solo pisan las cebras del cruce cuando los autos que las atraviesan están en rojo.
// Al repartidor no lo penaliza: solo choca con el poste como con cualquier farol.
public class Semaforo : MonoBehaviour
{
    public enum Luz { Apagado, Verde, Ambar, Rojo }

    [Header("Cruce (coordenadas del mundo)")]
    [SerializeField] private Rect zona;                  // la misma zona de cruce que usan los autos
    [SerializeField] private Rect[] pasosNorteSur;       // cebras que cruzan una calle por la que van autos norte-sur
    [SerializeField] private Rect[] pasosEsteOeste;      // cebras que cruzan una calle por la que van autos este-oeste

    [Header("Ciclo (segundos)")]
    [SerializeField] private float verde = 8f;
    [SerializeField] private float ambar = 1.2f;
    [SerializeField] private float desfase;              // para que los cruces no cambien todos a la vez
    [SerializeField] private bool encendido = true;

    [Header("Luces")]
    [SerializeField] private SpriteRenderer[] lucesEsteOeste;
    [SerializeField] private SpriteRenderer[] lucesNorteSur;
    [SerializeField] private Sprite spriteVerde, spriteAmbar, spriteRojo, spriteApagado;

    private static readonly List<Semaforo> todos = new List<Semaforo>();

    public Rect Zona => zona;
    public float Ciclo => 2f * (verde + ambar);
    public static IReadOnlyList<Semaforo> Todos => todos;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reiniciar() => todos.Clear();

    private void OnEnable()
    {
        todos.Add(this);
        Pintar();
    }

    private void OnDisable()
    {
        todos.Remove(this);
        Pintar(apagado: true);
    }

    private void Update() => Pintar();

    // Estado de un eje en el instante t: primero este-oeste en verde y ámbar, luego norte-sur
    public Luz Estado(bool esteOeste, float t)
    {
        if (!encendido) return Luz.Apagado;
        float c = Mathf.Repeat(t + desfase, Ciclo);
        bool turnoEsteOeste = c < verde + ambar;
        if (turnoEsteOeste != esteOeste) return Luz.Rojo;
        float enTurno = turnoEsteOeste ? c : c - (verde + ambar);
        return enTurno < verde ? Luz.Verde : Luz.Ambar;
    }

    public Luz Estado(bool esteOeste) => Estado(esteOeste, Time.time);

    // Los autos entran al cruce solo en verde (apagado = como si no hubiera semáforo)
    public bool Verde(bool esteOeste)
    {
        var luz = Estado(esteOeste);
        return luz == Luz.Verde || luz == Luz.Apagado;
    }

    public static Semaforo De(Rect zona)
    {
        foreach (var s in todos)
            if (s.zona == zona) return s;
        return null;
    }

    // Segundos que le quedan en rojo a un eje (0 si no está en rojo)
    public float RojoRestante(bool esteOeste)
    {
        if (Estado(esteOeste) != Luz.Rojo) return 0f;
        float c = Mathf.Repeat(Time.time + desfase, Ciclo), medio = verde + ambar;
        return esteOeste ? Ciclo - c : medio - c;   // este-oeste está en rojo en la segunda mitad del ciclo
    }

    public Rect[] PasosNorteSur => pasosNorteSur;
    public Rect[] PasosEsteOeste => pasosEsteOeste;

    // Un peatón que va de "desde" a "hasta" puede salir si en cada cebra que pisa los autos que la atraviesan
    // están en rojo y les queda rojo para todo el tramo
    public static bool PermitePeaton(Vector2 desde, Vector2 hasta, float velocidad)
    {
        float tiempo = Vector2.Distance(desde, hasta) / Mathf.Max(0.1f, velocidad) + 0.5f;
        foreach (var s in todos)
        {
            if (s.Bloquea(s.pasosNorteSur, false, desde, hasta, tiempo)) return false;
            if (s.Bloquea(s.pasosEsteOeste, true, desde, hasta, tiempo)) return false;
        }
        return true;
    }

    private bool Bloquea(Rect[] pasos, bool esteOeste, Vector2 desde, Vector2 hasta, float tiempo)
    {
        if (pasos == null || !encendido) return false;
        bool pisa = false;
        foreach (var r in pasos)
            for (int i = 0; i <= 20 && !pisa; i++)
                if (r.Contains(Vector2.Lerp(desde, hasta, i / 20f))) pisa = true;
        // si el tramo dura más que un rojo entero, basta con salir al empezar el rojo (los autos frenan ante peatones)
        return pisa && RojoRestante(esteOeste) < Mathf.Min(tiempo, verde + ambar - 0.3f);
    }

    private void Pintar(bool apagado = false)
    {
        float t = Application.isPlaying ? Time.time : 0f;
        Poner(lucesEsteOeste, apagado ? Luz.Apagado : Estado(true, t));
        Poner(lucesNorteSur, apagado ? Luz.Apagado : Estado(false, t));
    }

    private void Poner(SpriteRenderer[] luces, Luz luz)
    {
        if (luces == null) return;
        Sprite s = luz == Luz.Verde ? spriteVerde : luz == Luz.Ambar ? spriteAmbar : luz == Luz.Rojo ? spriteRojo : spriteApagado;
        foreach (var l in luces)
            if (l != null && s != null && l.sprite != s) l.sprite = s;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(zona.center, zona.size);
        Gizmos.color = Color.cyan;
        foreach (var r in pasosNorteSur ?? new Rect[0]) Gizmos.DrawWireCube(r.center, r.size);
        Gizmos.color = Color.magenta;
        foreach (var r in pasosEsteOeste ?? new Rect[0]) Gizmos.DrawWireCube(r.center, r.size);
    }
}

using UnityEditor;
using UnityEngine;

// El progreso (dinero, motos, moto equipada, estrellas y desbloqueos) vive en memoria y se reinicia en cada Play
// (Progreso.AlArrancar). Así que lo que se prepara fuera de Play se aplica al empezar el siguiente Play:
//  - Preparar(...) / Limpiar(): las pruebas automáticas dejan aquí lo que necesitan (p. ej. las 4 motos) antes de
//    entrar a Play y lo quitan al terminar. Ya no hay nada que guardar ni restaurar: al salir de Play no queda nada.
//  - Menús "Delivery Brenn's > Pruebas": "Dar $1000", "Reiniciar progreso" y "Desbloquear todos los niveles". En Play
//    cambian la sesión al momento; fuera de Play quedan pendientes para el próximo Play (una sola vez).
// Queda en SessionState (sobrevive al Domain Reload de entrar a Play) y se aplica en Progreso.Arranque.
[InitializeOnLoad]
public static class ProgresoPrueba
{
    const string P = "ProgresoPrueba.";

    static ProgresoPrueba()
    {
        Progreso.Arranque -= Aplicar;
        Progreso.Arranque += Aplicar;
    }

    // motos: máscara de bits (0b1111 = las 4); dinero: lo que se suma; todosLosNiveles: abre el 2 y el 3
    public static void Preparar(int motos = 0, int dinero = 0, bool todosLosNiveles = false)
    {
        SessionState.SetInt(P + "motos", motos);
        SessionState.SetInt(P + "dinero", dinero);
        SessionState.SetBool(P + "niveles", todosLosNiveles);
        SessionState.SetBool(P + "fijo", true);   // de una prueba: vale para todos los Play hasta Limpiar
    }

    public static void Limpiar()
    {
        foreach (var k in new[] { "motos", "dinero" }) SessionState.EraseInt(P + k);
        SessionState.EraseBool(P + "niveles");
        SessionState.EraseBool(P + "fijo");
    }

    static void Aplicar()
    {
        int motos = SessionState.GetInt(P + "motos", 0), dinero = SessionState.GetInt(P + "dinero", 0);
        bool niveles = SessionState.GetBool(P + "niveles", false);
        if (motos != 0) Progreso.DarMotos(motos);
        if (dinero > 0) Progreso.Agregar(dinero);
        if (niveles) Progreso.DesbloquearTodos();
        if (motos != 0 || dinero > 0 || niveles)
            Debug.Log($"Progreso de prueba aplicado al empezar: motos {motos}, +${dinero}{(niveles ? ", todos los niveles abiertos" : "")}.");
        if (!SessionState.GetBool(P + "fijo", false)) Limpiar();   // lo de los menús, solo una vez
    }

    static void Pendiente(string que, int motos = 0, int dinero = 0, bool niveles = false)
    {
        SessionState.SetInt(P + "motos", SessionState.GetInt(P + "motos", 0) | motos);
        SessionState.SetInt(P + "dinero", SessionState.GetInt(P + "dinero", 0) + dinero);
        if (niveles) SessionState.SetBool(P + "niveles", true);
        Debug.Log($"{que}: se aplica al empezar el próximo Play (el progreso solo vive durante la sesión).");
    }

    [MenuItem("Delivery Brenn's/Pruebas/Dar $1000")]
    public static void DarDinero()
    {
        if (!EditorApplication.isPlaying) { Pendiente("Dar $1000", dinero: 1000); return; }
        Progreso.Agregar(1000);
        Debug.Log("Dinero de la sesión: $" + Progreso.Dinero);
    }

    [MenuItem("Delivery Brenn's/Pruebas/Reiniciar progreso")]
    public static void Reiniciar()
    {
        Limpiar();   // lo pendiente de los menús también
        if (EditorApplication.isPlaying) Progreso.Reiniciar();
        Debug.Log(EditorApplication.isPlaying ? "Progreso de la sesión reiniciado: $0, sin motos y sin estrellas."
                                              : "Fuera de Play no hay progreso guardado: cada Play empieza con $0, sin motos y solo el nivel 1.");
    }

    [MenuItem("Delivery Brenn's/Pruebas/Desbloquear todos los niveles")]
    public static void DesbloquearTodos()
    {
        if (!EditorApplication.isPlaying) { Pendiente("Desbloquear todos los niveles", niveles: true); return; }
        Progreso.DesbloquearTodos();
        Debug.Log("Niveles 1, 2 y 3 abiertos en esta sesión.");
    }
}

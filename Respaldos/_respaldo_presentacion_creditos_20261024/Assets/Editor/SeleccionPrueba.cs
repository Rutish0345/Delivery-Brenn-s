using UnityEngine;
using UnityEngine.SceneManagement;

// Para las pruebas que van del menú a la Intro (PruebaIntro, PruebaNivel2, PruebaAudio...): ahora entre las dos está la
// escena Seleccion. Mientras esperan la Intro, esto confirma ahí lo que ya estaba elegido (Progreso.Genero no cambia).
// Siempre devuelve true para poder ir al principio de la condición: () => SeleccionPrueba.Pasar() && Escena == "Intro".
static class SeleccionPrueba
{
    public static bool Pasar()
    {
        if (SceneManager.GetActiveScene().name != "Seleccion" || Time.timeSinceLevelLoad < 0.3f) return true;
        var s = Object.FindFirstObjectByType<SeleccionPersonaje>();
        if (s != null) s.Confirmar();
        return true;
    }
}

// Lo mismo para la pantalla de niveles (SeleccionNivel), que ahora va después de la Intro: mientras se espera el nivel 1,
// esto pulsa "Jugar" con el nivel ya marcado (al empezar la sesión, el 1). Usada dice si se pasó por ahí (para
// comprobar que la Intro lleva a SeleccionNivel). Siempre devuelve true: () => NivelPrueba.Pasar() && Escena == "SampleScene".
static class NivelPrueba
{
    public static bool Usada;

    public static bool Pasar()
    {
        if (SceneManager.GetActiveScene().name != "SeleccionNivel" || Time.timeSinceLevelLoad < 0.3f) return true;
        var s = Object.FindFirstObjectByType<SeleccionNivel>();
        if (s != null)
        {
            Usada = true;
            s.Jugar();
        }
        return true;
    }
}

using UnityEngine;
using UnityEngine.SceneManagement;

// Para las pruebas que van del menú a la Intro (PruebaIntro, PruebaNivel2, PruebaAudio...): ahora entre las dos está la
// escena Seleccion. Mientras esperan la Intro, esto confirma ahí lo que ya estaba elegido (Progreso.Genero no cambia).
// Siempre devuelve true para poder ir al principio de la condición: () => SeleccionPrueba.Pasar() && Escena == "Intro".
static class SeleccionPrueba
{
    public static bool Usada;   // si se pasó por ahí (para comprobar que "Iniciar juego" siempre pasa por Seleccion)

    public static bool Pasar()
    {
        if (SceneManager.GetActiveScene().name != "Seleccion" || Time.timeSinceLevelLoad < 0.3f) return true;
        var s = Object.FindFirstObjectByType<SeleccionPersonaje>();
        if (s != null)
        {
            Usada = true;
            s.Confirmar();
        }
        return true;
    }
}

// Lo mismo para la Intro (6 viñetas): la omite como el botón Omitir. Usada dice si se pasó por ahí.
// () => SeleccionPrueba.Pasar() && IntroPrueba.Pasar() && Escena == "SeleccionNivel"
static class IntroPrueba
{
    public static bool Usada;

    public static bool Pasar()
    {
        if (SceneManager.GetActiveScene().name != "Intro" || Time.timeSinceLevelLoad < 0.3f) return true;
        var s = Object.FindFirstObjectByType<IntroHistoria>();
        if (s != null)
        {
            Usada = true;
            s.Omitir();
        }
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

// El menú abre con la presentación la primera vez de cada sesión: mientras se espera el menú, esto la quita (como un
// clic) y dice si el PanelMenu ya se ve y recibe clics. () => Escena == "MenuPrincipal" && MenuPrueba.Listo()
static class MenuPrueba
{
    public static bool Listo()
    {
        if (SceneManager.GetActiveScene().name != "MenuPrincipal") return false;
        var m = Object.FindFirstObjectByType<MenuPrincipal>();
        if (m == null) return false;
        if (m.PuedeContinuar) m.Continuar();
        return m.MenuVisible;
    }
}

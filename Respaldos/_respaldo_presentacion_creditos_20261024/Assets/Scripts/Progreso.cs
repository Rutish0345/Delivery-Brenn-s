using System;
using UnityEngine;

// Sprites de una moto (los llena el menú "Configurar motos")
[Serializable]
public class MotoVisual
{
    public string id;
    public Sprite[] frente = new Sprite[3];
    public Sprite[] espalda = new Sprite[3];
    public Sprite[] lado = new Sprite[3];
}

// Progreso de la partida. Solo el nombre y el género del jugador se guardan en PlayerPrefs; el dinero, las motos
// compradas, la moto equipada y el avance de los niveles (estrellas, mejor satisfacción, desbloqueos) viven en memoria
// y se reinician en cada arranque del juego y en cada Play del Editor (SubsystemRegistration: funciona aunque el
// Editor tenga el Domain Reload desactivado). Las claves viejas de PlayerPrefs (dinero_total, motos_compradas,
// moto_equipada) se borran al arrancar.
// Niveles: el 1 siempre abierto; el 2 con 2 estrellas o más en el 1; el 3 con 2 o más en el 2. Un despido no cuenta.
public static class Progreso
{
    public struct DatosMoto
    {
        public string id, nombre, descripcion;
        public int precio;
        public float velocidad;
        public float factorPenalizacion;

        public DatosMoto(string id, string nombre, int precio, float velocidad,
                         float factorPenalizacion, string descripcion)
        {
            this.id = id;
            this.nombre = nombre;
            this.precio = precio;
            this.velocidad = velocidad;
            this.factorPenalizacion = factorPenalizacion;
            this.descripcion = descripcion;
        }
    }
    // Nombre que escribe la jugadora en el menú principal
    public static string NombreJugador
    {
        get => PlayerPrefs.GetString("nombre_jugador", "");
        set { PlayerPrefs.SetString("nombre_jugador", value); PlayerPrefs.Save(); }
    }

    // 0 = repartidor, 1 = repartidora (pantalla Seleccion). Solo cambia el aspecto, no la velocidad ni la economía.
    public static int Genero
    {
        get => PlayerPrefs.GetInt(kGenero, 0) == 1 ? 1 : 0;
        set { PlayerPrefs.SetInt(kGenero, value == 1 ? 1 : 0); PlayerPrefs.Save(); Cambio?.Invoke(); }
    }
    public static bool EsRepartidora => Genero == 1;

    public const float VelocidadAPie = 3.5f;

    // Resultado del nivel 3 (lo deja su LevelManager y lo lee la escena Final). Solo dura la sesión (AlArrancar lo limpia).
    // Sin jugar el nivel 3 (p. ej. al abrir Final directo en el editor) se toma 3 estrellas.
    public static int EstrellasNivel3 { get; private set; } = -1;
    public static bool DespedidoNivel3 { get; private set; }
    public static bool HayResultadoNivel3 => EstrellasNivel3 >= 0;

    public static void GuardarNivel3(int estrellas, bool despedido)
    {
        EstrellasNivel3 = Mathf.Clamp(estrellas, 0, 3);
        DespedidoNivel3 = despedido;
    }


    // El orden de esta lista es el orden de las tarjetas en la tienda (por precio; la velocidad sube con el precio)
    public static readonly DatosMoto[] Motos =
    {
        new DatosMoto("scooter_naranja", "Scooter",   120, 5.0f, 1.0f, "Fácil de manejar.\nAgarre alto."),
        new DatosMoto("clasica_azul",    "Clásica",   180, 5.5f, 1.0f, "Equilibrada\ny rápida."),
        new DatosMoto("chopper_verde",   "Chopper",   260, 6.0f, 0.5f, "Resistente:\nchoques x0.5."),
        new DatosMoto("deportiva_roja",  "Deportiva", 350, 6.5f, 1.5f, "Muy veloz, pero\nchoques x1.5.")
    };

    // ------------------------------------------------------------------ niveles

    public struct DatosNivel
    {
        public string escena, nombre;
        public int pedidos;
        public DatosNivel(string escena, string nombre, int pedidos) { this.escena = escena; this.nombre = nombre; this.pedidos = pedidos; }
    }

    // en orden: el índice es el número de nivel menos 1 (la pantalla SeleccionNivel y el desbloqueo usan este orden)
    public static readonly DatosNivel[] Niveles =
    {
        new DatosNivel("SampleScene", "Residencial", 3),
        new DatosNivel("Nivel2_Comercial", "Comercial", 5),
        new DatosNivel("Nivel3_Lluvia", "Lluvia", 8),
    };
    public const int EstrellasParaDesbloquear = 2;

    public static int IndiceNivel(string escena) => Array.FindIndex(Niveles, n => n.escena == escena);

    // ------------------------------------------------------------------ estado de la sesión (en memoria)

    private const string kGenero = "genero_jugador";
    private static readonly string[] ClavesViejas = { "dinero_total", "motos_compradas", "moto_equipada" };

    private static int dinero;
    private static int compradas;           // bit i = moto i comprada
    private static int equipada = -1;       // -1 = a pie
    private static int[] estrellas = new int[3];
    private static float[] mejorSatisfaccion = { -1f, -1f, -1f };   // -1 = nunca completado
    private static bool todosDesbloqueados; // "Delivery Brenn's > Pruebas > Desbloquear todos los niveles"

    public static event Action Cambio;
    // Al empezar cada sesión, ya reiniciado todo (lo usa el Editor para preparar pruebas y menús de prueba)
    public static event Action Arranque;

    // Personaje elegido en esta sesión (pantalla Seleccion): el menú ya no vuelve a pedirlo
    public static bool PersonajeElegido { get; private set; }
    // Nivel que se marcó por última vez en SeleccionNivel (0..2)
    public static int NivelMarcado { get; set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void AlArrancar()
    {
        foreach (var k in ClavesViejas) PlayerPrefs.DeleteKey(k);
        PlayerPrefs.Save();
        ReiniciarSesion();
        Arranque?.Invoke();
    }

    // Todo de cero: también el personaje elegido y el resultado del nivel 3 (lo usa el arranque)
    private static void ReiniciarSesion()
    {
        ReiniciarAvance();
        PersonajeElegido = false;
        NivelMarcado = 0;
        EstrellasNivel3 = -1;
        DespedidoNivel3 = false;
    }

    private static void ReiniciarAvance()
    {
        dinero = 0;
        compradas = 0;
        equipada = -1;
        estrellas = new int[Niveles.Length];
        mejorSatisfaccion = new float[Niveles.Length];
        for (int i = 0; i < mejorSatisfaccion.Length; i++) mejorSatisfaccion[i] = -1f;
        todosDesbloqueados = false;
    }

    public static int Dinero => dinero;
    public static int MotoEquipada => equipada;
    public static int MotosCompradas { get { int n = 0; for (int i = 0; i < Motos.Length; i++) if (Tiene(i)) n++; return n; } }

    public static float FactorPenalizacion =>
        MotoEquipada >= 0 ? Motos[MotoEquipada].factorPenalizacion : 1f;

    public static bool Tiene(int i) => i >= 0 && (compradas & (1 << i)) != 0;

    public static void Agregar(int cantidad)
    {
        if (cantidad <= 0) return;
        dinero += cantidad;
        Cambio?.Invoke();
    }

    public static bool Comprar(int i)
    {
        if (i < 0 || i >= Motos.Length || Tiene(i) || Dinero < Motos[i].precio) return false;
        dinero -= Motos[i].precio;
        compradas |= 1 << i;
        Cambio?.Invoke();
        return true;
    }

    // i = -1 para bajarse de la moto e ir a pie
    public static void Equipar(int i)
    {
        if (i >= 0 && !Tiene(i)) return;
        equipada = i;
        Cambio?.Invoke();
    }

    // ---- niveles

    public static int Estrellas(int nivel) => nivel >= 0 && nivel < estrellas.Length ? estrellas[nivel] : 0;
    public static float MejorSatisfaccion(int nivel) => nivel >= 0 && nivel < mejorSatisfaccion.Length ? mejorSatisfaccion[nivel] : -1f;
    public static bool Completado(int nivel) => MejorSatisfaccion(nivel) >= 0f;
    public static bool Desbloqueado(int nivel) =>
        nivel == 0 || (nivel > 0 && nivel < Niveles.Length && (todosDesbloqueados || Estrellas(nivel - 1) >= EstrellasParaDesbloquear));
    public static int NivelesCompletados { get { int n = 0; for (int i = 0; i < Niveles.Length; i++) if (Completado(i)) n++; return n; } }

    // Nivel terminado (no despedido): se queda la mejor marca. El GameManager lo llama al terminar.
    public static void CompletarNivel(string escena, int cantidadEstrellas, float satisfaccion)
    {
        int i = IndiceNivel(escena);
        if (i < 0) return;
        estrellas[i] = Mathf.Max(estrellas[i], Mathf.Clamp(cantidadEstrellas, 0, 3));
        mejorSatisfaccion[i] = Mathf.Max(mejorSatisfaccion[i], Mathf.Clamp(satisfaccion, 0f, 100f));
        Cambio?.Invoke();
    }

    public static void MarcarPersonajeElegido() => PersonajeElegido = true;
    // Para las pruebas que pasan por la intro más de una vez en el mismo Play: "Iniciar juego" vuelve a Seleccion e Intro
    public static void OlvidarPersonaje() => PersonajeElegido = false;

    // "Reiniciar progreso": dinero 0, sin motos y sin estrellas (el nombre, el género y el personaje elegido se quedan)
    public static void Reiniciar()
    {
        ReiniciarAvance();
        Cambio?.Invoke();
    }

    // ---- para los menús de prueba y las pruebas del Editor
    public static void DesbloquearTodos() { todosDesbloqueados = true; Cambio?.Invoke(); }
    public static void DarMotos(int mascara) { compradas |= mascara & ((1 << Motos.Length) - 1); Cambio?.Invoke(); }
}

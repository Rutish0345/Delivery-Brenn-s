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

// Progreso que se guarda entre niveles y entre partidas
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
        set { PlayerPrefs.SetInt(kGenero, value == 1 ? 1 : 0); Guardar(); }
    }
    public static bool EsRepartidora => Genero == 1;

    public const float VelocidadAPie = 3.5f;

    // Resultado del nivel 3 (lo deja su LevelManager y lo lee la escena Final). Solo dura la partida: no se guarda.
    // Sin jugar el nivel 3 (p. ej. al abrir Final directo en el editor) se toma 3 estrellas.
    public static int EstrellasNivel3 { get; private set; } = -1;
    public static bool DespedidoNivel3 { get; private set; }
    public static bool HayResultadoNivel3 => EstrellasNivel3 >= 0;

    public static void GuardarNivel3(int estrellas, bool despedido)
    {
        EstrellasNivel3 = Mathf.Clamp(estrellas, 0, 3);
        DespedidoNivel3 = despedido;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void LimpiarNivel3() { EstrellasNivel3 = -1; DespedidoNivel3 = false; }

    // El orden de esta lista es el orden de las tarjetas en la tienda (por precio; la velocidad sube con el precio)
    public static readonly DatosMoto[] Motos =
    {
        new DatosMoto("scooter_naranja", "Scooter",   120, 5.0f, 1.0f, "Fácil de manejar.\nAgarre alto."),
        new DatosMoto("clasica_azul",    "Clásica",   180, 5.5f, 1.0f, "Equilibrada\ny rápida."),
        new DatosMoto("chopper_verde",   "Chopper",   260, 6.0f, 0.5f, "Resistente:\nchoques x0.5."),
        new DatosMoto("deportiva_roja",  "Deportiva", 350, 6.5f, 1.5f, "Muy veloz, pero\nchoques x1.5.")
    };

    private const string kDinero = "dinero_total";
    private const string kCompradas = "motos_compradas";
    private const string kEquipada = "moto_equipada";
    private const string kGenero = "genero_jugador";

    public static event Action Cambio;

    public static int Dinero => PlayerPrefs.GetInt(kDinero, 0);
    public static int MotoEquipada => PlayerPrefs.GetInt(kEquipada, -1); // -1 = a pie

    public static float FactorPenalizacion =>
        MotoEquipada >= 0 ? Motos[MotoEquipada].factorPenalizacion : 1f;

    public static bool Tiene(int i) => (PlayerPrefs.GetInt(kCompradas, 0) & (1 << i)) != 0;

    public static void Agregar(int cantidad)
    {
        if (cantidad <= 0) return;
        PlayerPrefs.SetInt(kDinero, Dinero + cantidad);
        Guardar();
    }

    public static bool Comprar(int i)
    {
        if (i < 0 || i >= Motos.Length || Tiene(i) || Dinero < Motos[i].precio) return false;
        PlayerPrefs.SetInt(kDinero, Dinero - Motos[i].precio);
        PlayerPrefs.SetInt(kCompradas, PlayerPrefs.GetInt(kCompradas, 0) | (1 << i));
        Guardar();
        return true;
    }

    // i = -1 para bajarse de la moto e ir a pie
    public static void Equipar(int i)
    {
        if (i >= 0 && !Tiene(i)) return;
        PlayerPrefs.SetInt(kEquipada, i);
        Guardar();
    }

    public static void Reiniciar()
    {
        PlayerPrefs.DeleteKey(kDinero);
        PlayerPrefs.DeleteKey(kCompradas);
        PlayerPrefs.DeleteKey(kEquipada);
        Guardar();
    }

    private static void Guardar()
    {
        PlayerPrefs.Save();
        Cambio?.Invoke();
    }
}
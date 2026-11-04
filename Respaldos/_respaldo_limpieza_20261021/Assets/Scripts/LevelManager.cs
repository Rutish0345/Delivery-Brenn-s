using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public enum Ambiente { Dia, Atardecer, Noche, NocheConLluvia }

// Uno por escena (por nivel): define cuántos pedidos hay, la luz, la lluvia y si hay motos.
// Corre después del resto de scripts para poder configurar al GameManager ya iniciado.
[DefaultExecutionOrder(100)]
public class LevelManager : MonoBehaviour
{
    [Header("Nivel")]
    [SerializeField] private string nombreNivel = "Residencial";
    [SerializeField] private int pedidos = 3;
    [SerializeField] private float velocidadReferencia = 3.5f;
    [SerializeField] private bool motosHabilitadas = false;
    [Tooltip("Al arrancar abre TiendaMotos en modo AntesDeNivel (\"ELIGE TU MOTO\"): el juego queda en pausa, sin " +
             "pedido ni reloj, hasta \"EMPEZAR NIVEL\". Para los niveles con motos (nivel 2 en adelante).")]
    [SerializeField] private bool elegirMotoAntes = false;

    [Header("Ambiente")]
    [SerializeField] private Ambiente ambiente = Ambiente.Dia;
    [SerializeField] private Light2D luzGlobal;          // si se deja vacío, busca la Global Light 2D
    [SerializeField] private GameObject lluvia;          // capa de lluvia y salpicaduras (nivel 3)
    [Tooltip("Con NocheConLluvia: apágala para ver el nivel de noche sin la capa de lluvia ni las salpicaduras")]
    [SerializeField] private bool lluviaActiva = true;
    [SerializeField] private GameObject[] soloDeNoche;   // luces de faroles, letreros, etc.

    [Header("Escenas (nombres tal como están en Build Profiles)")]
    [SerializeField] private string escenaSiguiente = "";
    [SerializeField] private string escenaMenu = "MenuPrincipal";
    [SerializeField] private GameObject botonSiguiente;  // botón del panel final

    [Header("Cierre de la historia (último nivel, en vez de \"Siguiente nivel\")")]
    [SerializeField] private bool cierreHistoria = false;
    [SerializeField] private int estrellasParaPuesto = 2;   // 1 estrella = sin puesto; 2 o 3 = con puesto
    [SerializeField] private string titularPuesto = "¡TE QUEDASTE CON EL PUESTO!";
    [SerializeField, TextArea] private string textoPuesto =
        "Entregaste todo bajo la lluvia. El Salón Brenn's ya tiene a su mejor repartidor: {nombre}.";
    [SerializeField] private string titularSinPuesto = "¡CASI LO LOGRAS!";
    [SerializeField, TextArea] private string textoSinPuesto = "Inténtalo otra vez y quédate con el puesto.";
    [SerializeField] private TextMeshProUGUI tituloFinal;   // Titulo y TextoCierre del panel final
    [SerializeField] private TextMeshProUGUI textoCierre;

    private static readonly Color ColorAtardecer = new Color32(0xFF, 0xB8, 0x80, 255);
    private static readonly Color ColorNoche = new Color32(0x6F, 0x6A, 0xB8, 255);

    public string NombreNivel => nombreNivel;
    public bool MotosHabilitadas => motosHabilitadas;
    public string EscenaSiguiente => escenaSiguiente;
    public bool CierreHistoria => cierreHistoria;
    public bool Llueve => ambiente == Ambiente.NocheConLluvia && lluviaActiva;

    // Enciende o apaga la lluvia en pleno nivel (las luces de noche no cambian)
    public void SetLluvia(bool activa)
    {
        lluviaActiva = activa;
        if (lluvia != null) lluvia.SetActive(Llueve);
    }

    private void Start()
    {
        Time.timeScale = 1f; // por si venimos de una pausa o de la pantalla final

        AplicarAmbiente();

        GameManager gm = GameManager.Instance;
        if (gm != null)
        {
            gm.Configurar(pedidos, velocidadReferencia);
            gm.NivelCompletado += AlTerminar;
        }

        PlayerController jugador = FindFirstObjectByType<PlayerController>();
        if (jugador != null) jugador.SetMotosHabilitadas(motosHabilitadas);

        if (botonSiguiente != null) botonSiguiente.SetActive(false);

        if (elegirMotoAntes) AbrirEleccionDeMoto(gm, jugador);
    }

    // Pantalla "ELIGE TU MOTO" antes del nivel: todo quieto (timeScale 0, sin pedido ni reloj) hasta que
    // se pulsa "EMPEZAR NIVEL"
    private void AbrirEleccionDeMoto(GameManager gm, PlayerController jugador)
    {
        TiendaMotos tienda = FindFirstObjectByType<TiendaMotos>();
        if (tienda == null)
        {
            Debug.LogWarning("elegirMotoAntes está activo pero no hay TiendaMotos en la escena: el nivel empieza directo.");
            return;
        }
        if (gm != null) gm.Esperar();
        Time.timeScale = 0f;
        tienda.AbrirAntesDeNivel(() =>
        {
            Time.timeScale = 1f;
            if (jugador != null) jugador.SetMotosHabilitadas(true);
            if (gm != null) gm.Empezar();
        });
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.NivelCompletado -= AlTerminar;
    }

    private void AplicarAmbiente()
    {
        if (luzGlobal == null)
        {
            foreach (Light2D l in FindObjectsByType<Light2D>(FindObjectsSortMode.None))
            {
                if (l.lightType == Light2D.LightType.Global) { luzGlobal = l; break; }
            }
        }

        Color color = Color.white;
        float intensidad = 1f;
        bool noche = false;
        bool llueve = false;

        switch (ambiente)
        {
            case Ambiente.Atardecer:
                color = ColorAtardecer; intensidad = 0.95f;
                break;
            case Ambiente.Noche:
                color = ColorNoche; intensidad = 0.6f; noche = true;
                break;
            case Ambiente.NocheConLluvia:
                color = ColorNoche; intensidad = 0.55f; noche = true; llueve = lluviaActiva;
                break;
        }

        if (luzGlobal != null)
        {
            luzGlobal.color = color;
            luzGlobal.intensity = intensidad;
        }

        if (lluvia != null) lluvia.SetActive(llueve);
        foreach (GameObject g in soloDeNoche)
        {
            if (g != null) g.SetActive(noche);
        }
    }

    // Muestra "Siguiente nivel" solo si esa escena ya existe en el proyecto
    private void AlTerminar(int estrellas, float promedio)
    {
        if (botonSiguiente != null)
            botonSiguiente.SetActive(!cierreHistoria && HayEscena(escenaSiguiente));
        if (cierreHistoria) MostrarCierre(estrellas);
    }

    // Último nivel: el titular y el texto dependen de si se quedó con el puesto
    public void MostrarCierre(int estrellas)
    {
        bool puesto = estrellas >= estrellasParaPuesto;
        if (tituloFinal != null) tituloFinal.text = puesto ? titularPuesto : titularSinPuesto;
        if (textoCierre != null)
        {
            string n = Progreso.NombreJugador;
            if (string.IsNullOrWhiteSpace(n)) n = "Repartidor";
            textoCierre.text = (puesto ? textoPuesto : textoSinPuesto).Replace("{nombre}", n.Trim());
        }
    }

    private bool HayEscena(string nombre)
    {
        return !string.IsNullOrEmpty(nombre) && Application.CanStreamedLevelBeLoaded(nombre);
    }

    // ---- Para los botones del panel final (OnClick) ----

    public void Reiniciar() { Cargar(SceneManager.GetActiveScene().name); }

    public void SiguienteNivel()
    {
        if (HayEscena(escenaSiguiente)) Cargar(escenaSiguiente);
        else Debug.LogWarning("Todavía no existe la escena del siguiente nivel.");
    }

    public void IrAlMenu()
    {
        if (HayEscena(escenaMenu)) Cargar(escenaMenu);
        else Debug.LogWarning("Todavía no existe la escena del menú principal.");
    }

    private void Cargar(string escena)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(escena);
    }
}
using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Pantalla "ELIGE A TU REPARTIDOR" (escena Seleccion, entre MenuPrincipal e Intro; la arma ConstruirSeleccion).
// Dos pestañas, REPARTIDOR y REPARTIDORA, sobre una ventana rosa: retrato grande con su idle a la izquierda y la
// ficha a la derecha. Los dos tienen las mismas estadísticas: solo cambia el aspecto (Progreso.Genero).
// Clic en una pestaña o A / D (flechas) para cambiar; Enter o "CONFIRMAR" guardan y cargan la intro.
// La música del menú sigue sin cortarse; las pestañas y el botón suenan con el clic del AudioManager.
public class SeleccionPersonaje : MonoBehaviour
{
    [Serializable]
    public class Ficha
    {
        public Sprite retrato;                  // idle de frente (repartidor_sheet_0 / repartidora_sheet_0)
        public string cita;                     // "{nombre}, LISTO PARA REPARTIR"
        public string puesto;
        [TextArea(1, 3)] public string dato;    // la línea divertida
    }

    [SerializeField] private Ficha[] fichas = new Ficha[2];   // 0 repartidor, 1 repartidora (Progreso.Genero)
    [SerializeField] private Image[] pestanas = new Image[2];
    [SerializeField] private Sprite pestanaActiva, pestanaInactiva;
    [SerializeField] private TextMeshProUGUI[] textosPestana = new TextMeshProUGUI[2];
    [SerializeField] private Color textoPestanaActiva = new Color32(0x2A, 0x1F, 0x40, 255);
    [SerializeField] private Color textoPestanaInactiva = new Color32(0xFF, 0xF1, 0xD0, 255);
    [SerializeField] private Image retrato;
    [SerializeField] private TextMeshProUGUI cita, nombre, puesto, item, dato;
    [SerializeField] private string itemFavorito = "mochila de reparto";
    [SerializeField] private string escenaIntro = "Intro";
    [SerializeField] private string escenaJuego = "SampleScene";

    [Header("Idle del retrato")]
    [SerializeField] private float periodoIdle = 0.6f;   // sube y baja 1 píxel del arte
    [SerializeField] private float saltoIdle = 4f;       // unidades del Canvas (el retrato va a x4)

    private int actual;
    private bool saliendo;
    private Vector2 baseRetrato;

    public int Actual => actual;

    private void Start()
    {
        Time.timeScale = 1f;
        if (AudioManager.Instance != null) AudioManager.Instance.PonerMusica("musica_menu");   // la misma: no se corta
        if (retrato != null) baseRetrato = retrato.rectTransform.anchoredPosition;
        Elegir(Progreso.Genero, false);
    }

    private void Update()
    {
        if (retrato != null)
        {
            bool arriba = Mathf.Repeat(Time.unscaledTime, periodoIdle * 2f) >= periodoIdle;
            retrato.rectTransform.anchoredPosition = baseRetrato + (arriba ? Vector2.up * saltoIdle : Vector2.zero);
        }

        var k = Keyboard.current;
        if (k == null || saliendo) return;
        if (k.aKey.wasPressedThisFrame || k.leftArrowKey.wasPressedThisFrame) Elegir(0, true);
        else if (k.dKey.wasPressedThisFrame || k.rightArrowKey.wasPressedThisFrame) Elegir(1, true);
        else if (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame)
        {
            AudioManager.Sonar("ui_clic");
            Confirmar();
        }
    }

    // Para las pestañas (OnClick): el clic lo pone el AudioManager
    public void ElegirRepartidor() => Elegir(0, false);
    public void ElegirRepartidora() => Elegir(1, false);

    // conTecla: con el teclado suena el clic aquí (con el ratón ya lo pone el AudioManager)
    public void Elegir(int indice, bool conTecla)
    {
        indice = indice == 1 ? 1 : 0;
        if (conTecla && indice != actual) AudioManager.Sonar("ui_clic");
        actual = indice;

        for (int i = 0; i < pestanas.Length; i++)
        {
            bool activa = i == actual;
            if (pestanas[i] != null)
            {
                pestanas[i].sprite = activa ? pestanaActiva : pestanaInactiva;
                pestanas[i].transform.SetAsLastSibling();   // la activa queda encima de la otra
            }
            if (i < textosPestana.Length && textosPestana[i] != null) textosPestana[i].color = activa ? textoPestanaActiva : textoPestanaInactiva;
        }
        if (pestanas.Length > actual && pestanas[actual] != null) pestanas[actual].transform.SetAsLastSibling();

        var f = fichas[actual];
        string n = Progreso.NombreJugador;
        if (string.IsNullOrWhiteSpace(n)) n = actual == 1 ? "Repartidora" : "Repartidor";
        if (retrato != null) retrato.sprite = f.retrato;
        if (cita != null) cita.text = "“" + f.cita.Replace("{nombre}", n.Trim().ToUpperInvariant()) + "”";
        if (nombre != null) nombre.text = "<b>Nombre:</b> " + n.Trim();
        if (puesto != null) puesto.text = "<b>Puesto:</b> " + f.puesto;
        if (item != null) item.text = "<b>Ítem favorito:</b> " + itemFavorito;
        if (dato != null) dato.text = "<b>Dato:</b> " + f.dato;
    }

    // Botón "CONFIRMAR" y Enter: guarda el género y sigue con la intro (o el nivel 1 si no hay intro)
    public void Confirmar()
    {
        if (saliendo) return;
        saliendo = true;
        Progreso.Genero = actual;
        SceneManager.LoadScene(Application.CanStreamedLevelBeLoaded(escenaIntro) ? escenaIntro : escenaJuego);
    }
}

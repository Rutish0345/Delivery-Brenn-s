using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Menú principal (lo arma ConstruirMenuPrincipal). La primera vez de cada sesión se ve solo la presentación (El mirador,
// el título y "HAZ CLIC PARA CONTINUAR" parpadeando); cualquier tecla o clic (pasado el fundido de entrada de 1.2 s: el
// clic con el que se le da el foco a la ventana no cuenta) hace un fundido de 0.4 s al PanelMenu (nombre, Iniciar juego, Instrucciones, Salir y el sonido). Al volver al menú
// desde un nivel o la pantalla de niveles se entra directo al PanelMenu. El PanelMenu no recibe clics hasta que termina
// el fundido, así el clic que quita la presentación no dispara ningún botón. Con el teclado: la primera flecha (o Tab)
// elige "Iniciar juego" y las flechas y Enter recorren y pulsan los botones (al abrir y cerrar las instrucciones, el
// foco pasa a "Cerrar" y vuelve a "Instrucciones").
public class MenuPrincipal : MonoBehaviour
{
    [SerializeField] private CanvasGroup presentacion;
    [SerializeField] private CanvasGroup panelMenu;
    [SerializeField] private float duracionFundido = 0.4f;
    [SerializeField] private float esperaPresentacion = 1.2f;   // las teclas y clics de antes no cuentan (el fundido de entrada)

    // una sola vez por sesión: se reinicia en cada arranque del juego y en cada Play
    private static bool presentacionVista;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void AlArrancar() => presentacionVista = false;

    private bool fundiendo;

    public bool EnPresentacion => presentacion != null && presentacion.gameObject.activeSelf && !fundiendo;
    public bool PuedeContinuar => EnPresentacion && Time.timeSinceLevelLoad >= esperaPresentacion;
    public bool MenuVisible => panelMenu == null || (panelMenu.alpha >= 1f && panelMenu.interactable && !fundiendo);
    public static bool PresentacionVista => presentacionVista;
    // Para volver al menú directo al PanelMenu (los créditos) aunque la presentación no se haya visto
    public static void SaltarPresentacion() => presentacionVista = true;

    [SerializeField] private TMP_InputField campoNombre;
    [SerializeField] private GameObject panelHistoria;       // ya no se usa: la historia es la intro
    [SerializeField] private GameObject panelInstrucciones;
    [SerializeField] private string escenaSeleccion = "Seleccion";   // "Elige a tu repartidor", antes de la intro
    [SerializeField] private string escenaIntro = "Intro";
    [SerializeField] private string escenaJuego = "SeleccionNivel";   // sin Seleccion ni Intro
    [SerializeField] private string escenaNiveles = "SeleccionNivel"; // con el personaje ya elegido en esta sesión
    [SerializeField] private GameObject primerBoton;          // "Iniciar juego": lo elige la primera flecha
    [SerializeField] private GameObject botonInstrucciones;   // recibe el foco al cerrar las instrucciones

    private void Start()
    {
        Time.timeScale = 1f;
        if (campoNombre != null) campoNombre.text = Progreso.NombreJugador;
        CerrarPaneles();
        if (AudioManager.Instance != null) AudioManager.Instance.PonerMusica("musica_menu");
        bool mostrar = !presentacionVista && presentacion != null;
        if (presentacion != null)
        {
            presentacion.gameObject.SetActive(mostrar);
            presentacion.alpha = 1f;
        }
        MostrarMenu(mostrar ? 0f : 1f);
    }

    private void Update()
    {
        if (!PuedeContinuar) return;
        bool tecla = Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;
        var raton = Mouse.current;
        bool clic = raton != null && (raton.leftButton.wasPressedThisFrame || raton.rightButton.wasPressedThisFrame || raton.middleButton.wasPressedThisFrame);
        var mando = Gamepad.current;
        bool boton = mando != null && (mando.buttonSouth.wasPressedThisFrame || mando.startButton.wasPressedThisFrame);
        var toque = Touchscreen.current;
        bool tocado = toque != null && toque.primaryTouch.press.wasPressedThisFrame;
        if (tecla || clic || boton || tocado) Continuar();
    }

    // Después del EventSystem: si no, esa misma flecha movería ya desde "Iniciar juego"
    private void LateUpdate()
    {
        if (MenuVisible) Navegar();
    }

    // Sin nada elegido, la primera flecha o Tab elige "Iniciar juego" (o "Cerrar" con las instrucciones abiertas)
    private void Navegar()
    {
        var es = UnityEngine.EventSystems.EventSystem.current;
        var k = Keyboard.current;
        if (es == null || k == null || es.currentSelectedGameObject != null && es.currentSelectedGameObject.activeInHierarchy) return;
        if (!(k.upArrowKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame || k.leftArrowKey.wasPressedThisFrame
              || k.rightArrowKey.wasPressedThisFrame || k.tabKey.wasPressedThisFrame)) return;
        var cerrar = panelInstrucciones != null && panelInstrucciones.activeSelf ? panelInstrucciones.GetComponentInChildren<UnityEngine.UI.Selectable>() : null;
        es.SetSelectedGameObject(cerrar != null ? cerrar.gameObject : primerBoton);
    }

    // Quita la presentación con un fundido al PanelMenu (también para las pruebas)
    public void Continuar()
    {
        if (!EnPresentacion) return;
        presentacionVista = true;
        AudioManager.Sonar("ui_clic");
        StartCoroutine(Fundido());
    }

    private IEnumerator Fundido()
    {
        fundiendo = true;
        for (float t = 0f; t < duracionFundido; t += Time.unscaledDeltaTime)
        {
            float k = t / duracionFundido;
            presentacion.alpha = 1f - k;
            if (panelMenu != null) panelMenu.alpha = k;
            yield return null;
        }
        presentacion.alpha = 0f;
        presentacion.gameObject.SetActive(false);
        fundiendo = false;
        MostrarMenu(1f);
    }

    // el menú solo recibe clics del todo visible
    private void MostrarMenu(float alfa)
    {
        if (panelMenu == null) return;
        panelMenu.alpha = alfa;
        panelMenu.interactable = panelMenu.blocksRaycasts = alfa >= 1f;
    }

    // Botón "Iniciar juego": guarda el nombre (vacío = "Repartidor" o "Repartidora" según la selección, Texto.Nombre).
    // Si en esta sesión ya se eligió personaje, va directo a la selección de nivel; si no, Seleccion → Intro → niveles
    public void Jugar()
    {
        Progreso.NombreJugador = campoNombre != null ? campoNombre.text.Trim() : "";

        string destino = Progreso.PersonajeElegido && Application.CanStreamedLevelBeLoaded(escenaNiveles) ? escenaNiveles
                       : Application.CanStreamedLevelBeLoaded(escenaSeleccion) ? escenaSeleccion
                       : Application.CanStreamedLevelBeLoaded(escenaIntro) ? escenaIntro : escenaJuego;
        SceneManager.LoadScene(destino);
    }

    public void AbrirHistoria() { Mostrar(panelHistoria); }
    public void AbrirInstrucciones() { Mostrar(panelInstrucciones); }
    public void CerrarPaneles() { Mostrar(null); }

    public void Salir()
    {
#if UNITY_EDITOR
        // En el editor, "salir" detiene el modo Play
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void Mostrar(GameObject panel)
    {
        bool estabaAbierto = panelInstrucciones != null && panelInstrucciones.activeSelf;
        if (panelHistoria != null) panelHistoria.SetActive(panel == panelHistoria);
        if (panelInstrucciones != null) panelInstrucciones.SetActive(panel == panelInstrucciones);
        // el foco del teclado: a "Cerrar" al abrir y de vuelta a "Instrucciones" al cerrar
        var es = UnityEngine.EventSystems.EventSystem.current;
        if (es == null) return;
        if (panel != null && panel == panelInstrucciones)
        {
            var cerrar = panel.GetComponentInChildren<UnityEngine.UI.Selectable>();
            if (cerrar != null) es.SetSelectedGameObject(cerrar.gameObject);
        }
        else if (estabaAbierto && botonInstrucciones != null) es.SetSelectedGameObject(botonInstrucciones);
    }
}
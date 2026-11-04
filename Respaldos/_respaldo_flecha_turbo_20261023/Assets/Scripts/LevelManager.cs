using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
    [Tooltip("Quejas (pedido cancelado o entregado con menos de 30 % de satisfacción) que aguanta el nivel: al llegar, " +
             "GAME OVER (¡TE DESPIDIERON!). Nivel 1 = 2, niveles 2 y 3 = 3.")]
    [SerializeField] private int quejasMaximas = 3;

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

    [Header("Panel final")]
    [Tooltip("Último nivel: el panel queda neutro (¡NIVEL COMPLETADO!) y \"Continuar\" (BotonSiguiente) lleva a la escena " +
             "Final con cualquier cantidad de estrellas; el resultado queda en Progreso para el final de la historia.")]
    [SerializeField] private bool cierreHistoria = false;
    [SerializeField] private int estrellasParaAvanzar = 2;   // con menos: NIVEL NO SUPERADO (niveles 1 y 2)
    [SerializeField] private string titularNoSuperado = "NIVEL NO SUPERADO";
    [SerializeField, TextArea] private string textoNoSuperado = "Necesitas al menos 2 estrellas para avanzar.";
    [SerializeField] private TextMeshProUGUI tituloFinal;   // Titulo y TextoAviso del panel final
    [SerializeField] private TextMeshProUGUI textoAviso;
    [SerializeField] private Button botonJugarDeNuevo;      // se resalta (pastilla dorada que late) si no se supera
    [SerializeField] private Sprite pastillaResaltada;
    [SerializeField] private Sprite pastillaResaltadaPresionada;

    [Header("Despido (GAME OVER por quejas)")]
    [SerializeField] private PantallaDespido pantallaDespido;   // Canvas/PanelDespido

    private static readonly Color ColorAtardecer = new Color32(0xFF, 0xB8, 0x80, 255);
    private static readonly Color ColorNoche = new Color32(0x6F, 0x6A, 0xB8, 255);

    public string NombreNivel => nombreNivel;
    public bool MotosHabilitadas => motosHabilitadas;
    public string EscenaSiguiente => escenaSiguiente;
    public bool CierreHistoria => cierreHistoria;
    public int QuejasMaximas => quejasMaximas;
    public int EstrellasParaAvanzar => estrellasParaAvanzar;
    public bool Llueve => ambiente == Ambiente.NocheConLluvia && lluviaActiva;

    // Enciende o apaga la lluvia en pleno nivel (las luces de noche no cambian)
    public void SetLluvia(bool activa)
    {
        lluviaActiva = activa;
        if (lluvia != null) lluvia.SetActive(Llueve);
        if (AudioManager.Instance != null) AudioManager.Instance.Lluvia(Llueve);
    }

    // La música de cada nivel sale de aquí (y de ningún otro lado), según nombreNivel
    public string MusicaDelNivel()
    {
        switch (nombreNivel)
        {
            case "Comercial": return "musica_nivel2_comercial";
            case "Lluvia": return "musica_nivel3_lluvia";
            default: return "musica_nivel1_residencial";
        }
    }

    private void Start()
    {
        Time.timeScale = 1f; // por si venimos de una pausa o de la pantalla final

        AplicarAmbiente();
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PonerMusica(MusicaDelNivel());
            AudioManager.Instance.AtenuarMusica(1f);   // al reintentar tras un despido la misma pista sigue, pero baja
        }

        GameManager gm = GameManager.Instance;
        if (gm != null)
        {
            gm.Configurar(pedidos, velocidadReferencia, quejasMaximas);
            gm.NivelCompletado += AlTerminar;
            gm.Despedido += AlDespedir;
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
        if (GameManager.Instance == null) return;
        GameManager.Instance.NivelCompletado -= AlTerminar;
        GameManager.Instance.Despedido -= AlDespedir;
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
        if (AudioManager.Instance != null) AudioManager.Instance.Lluvia(llueve);   // y truenos lejanos
        foreach (GameObject g in soloDeNoche)
        {
            if (g != null) g.SetActive(noche);
        }
    }

    // "Siguiente nivel" (o "Continuar" en el último) solo si esa escena ya existe en el proyecto. Niveles 1 y 2 con
    // menos de estrellasParaAvanzar: NIVEL NO SUPERADO, sin siguiente y con "Jugar de nuevo" resaltado.
    // El último nivel deja su resultado en Progreso y "Continuar" va al final de la historia con cualquier resultado.
    private void AlTerminar(int estrellas, float promedio)
    {
        bool supera = cierreHistoria || estrellas >= estrellasParaAvanzar;
        if (cierreHistoria) Progreso.GuardarNivel3(estrellas, false);
        if (botonSiguiente != null) botonSiguiente.SetActive(supera && HayEscena(escenaSiguiente));
        MostrarResultado(supera);
        if (AudioManager.Instance != null) AudioManager.Instance.TerminarNivel(estrellas >= 2, false);
    }

    // El titular y el aviso del panel final según si se superó el nivel (también para las pruebas)
    public void MostrarResultado(bool supera)
    {
        if (tituloFinal != null && !supera) tituloFinal.text = titularNoSuperado;
        if (textoAviso != null)
        {
            textoAviso.text = supera ? "" : textoNoSuperado;
            textoAviso.gameObject.SetActive(!supera);
        }
        if (!supera) Resaltar(botonJugarDeNuevo);
    }

    // Pastilla dorada (el mismo 9-slice que la rosa), seleccionada y latiendo, para que sea lo primero que se vea
    private void Resaltar(Button boton)
    {
        if (boton == null) return;
        if (pastillaResaltada != null && boton.targetGraphic is Image img && img.sprite != pastillaResaltada)
        {
            if (img.sprite != null) img.pixelsPerUnitMultiplier *= img.sprite.pixelsPerUnit / pastillaResaltada.pixelsPerUnit;
            img.sprite = pastillaResaltada;
            var estados = boton.spriteState;
            if (pastillaResaltadaPresionada != null) estados.pressedSprite = pastillaResaltadaPresionada;
            boton.spriteState = estados;
        }
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(boton.gameObject);
        StopAllCoroutines();
        StartCoroutine(Latir(boton.transform));
    }

    private static IEnumerator Latir(Transform t)
    {
        for (float tiempo = 0f; t != null; tiempo += Time.unscaledDeltaTime)
        {
            t.localScale = Vector3.one * (1f + 0.04f * (0.5f + 0.5f * Mathf.Sin(tiempo * 5f)));
            yield return null;
        }
    }

    // GAME OVER por quejas: todo se detiene (juego, reloj, lluvia), la música baja con el jingle de derrota y sale
    // la pantalla de despido. El dinero de este nivel no pasa a Progreso (GameManager no llama a Terminar)
    private void AlDespedir()
    {
        Time.timeScale = 0f;
        if (lluvia != null) lluvia.SetActive(false);
        if (AudioManager.Instance != null) AudioManager.Instance.Despedir();
        if (cierreHistoria) Progreso.GuardarNivel3(0, true);
        if (pantallaDespido != null) pantallaDespido.Mostrar();
        else Debug.LogWarning("Despedido, pero el LevelManager no tiene PantallaDespido (corre Delivery Brenn's > Quejas y despido).");
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
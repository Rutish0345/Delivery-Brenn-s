using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Pantalla "ELIGE EL NIVEL" (escena SeleccionNivel, la arma ConstruirSeleccionNivel): tres tarjetas (Residencial,
// Comercial y Lluvia) con miniatura, pedidos, estrellas, mejor satisfacción y estado (NUEVO / COMPLETADO / BLOQUEADO
// con candado). Arriba el dinero, las motos compradas (x/4) y la barra "Niveles n/3". Todo sale de Progreso (en
// memoria: se reinicia en cada arranque). Clic en una tarjeta o A / D (flechas) para elegir; Enter o "Jugar" cargan
// el nivel por nombre (el 2 y el 3 abren ELIGE TU MOTO como siempre); Esc o "Menú" vuelven al menú.
// "Reiniciar progreso" pide confirmación (Sí / No): dinero 0, sin motos y sin estrellas.
// La música del menú sigue sin cortarse; los botones suenan con el clic del AudioManager.
public class SeleccionNivel : MonoBehaviour
{
    [Serializable]
    public class Tarjeta
    {
        public RectTransform raiz;
        public Image marcoElegida;          // marco dorado detrás de la tarjeta elegida
        public Image miniatura;
        public GameObject velo;             // oscurece la miniatura si está bloqueado
        public GameObject candado;
        public Image[] estrellas = new Image[3];
        public TextMeshProUGUI mejor;       // "Mejor: 85%"
        public TextMeshProUGUI estado;      // NUEVO / COMPLETADO / BLOQUEADO
        public Image placaEstado;
    }

    public const string Nuevo = "NUEVO", Completado = "COMPLETADO", Bloqueado = "BLOQUEADO";

    [SerializeField] private Tarjeta[] tarjetas = new Tarjeta[3];
    [SerializeField] private Sprite estrellaLlena, estrellaVacia;
    [SerializeField] private Color colorNuevo = new Color32(0x7B, 0xD8, 0x8F, 255);
    [SerializeField] private Color colorCompletado = new Color32(0xFF, 0xD1, 0x66, 255);
    [SerializeField] private Color colorBloqueado = new Color32(0xB8, 0xAE, 0xC8, 255);
    [SerializeField] private TextMeshProUGUI textoDinero, textoMotos, textoNiveles;
    [SerializeField] private Image barraNiveles;           // Filled horizontal
    [SerializeField] private Button botonJugar;
    [SerializeField] private GameObject confirmacion;      // recuadro "¿Reiniciar progreso?"
    [SerializeField] private string escenaMenu = "MenuPrincipal";
    [SerializeField] private float alzarElegida = 12f;     // la tarjeta elegida sube un poco

    private int actual;
    private bool saliendo;
    private Vector2[] basePos;

    public int Actual => actual;
    public bool ConfirmacionAbierta => confirmacion != null && confirmacion.activeSelf;
    public static string EstadoDe(int nivel) =>
        !Progreso.Desbloqueado(nivel) ? Bloqueado : Progreso.Completado(nivel) ? Completado : Nuevo;

    private void OnEnable() { Progreso.Cambio += Refrescar; }
    private void OnDisable() { Progreso.Cambio -= Refrescar; }

    private void Start()
    {
        Time.timeScale = 1f;
        if (AudioManager.Instance != null) AudioManager.Instance.PonerMusica("musica_menu");   // la misma: no se corta
        basePos = new Vector2[tarjetas.Length];
        for (int i = 0; i < tarjetas.Length; i++)
            if (tarjetas[i]?.raiz != null) basePos[i] = tarjetas[i].raiz.anchoredPosition;
        if (confirmacion != null) confirmacion.SetActive(false);
        int marcado = Mathf.Clamp(Progreso.NivelMarcado, 0, tarjetas.Length - 1);
        Elegir(Progreso.Desbloqueado(marcado) ? marcado : 0, false);
    }

    private void Update()
    {
        var k = Keyboard.current;
        if (k == null || saliendo) return;
        if (ConfirmacionAbierta)
        {
            if (k.escapeKey.wasPressedThisFrame) { AudioManager.Sonar("ui_clic"); CancelarReinicio(); }
            return;
        }
        if (k.aKey.wasPressedThisFrame || k.leftArrowKey.wasPressedThisFrame) Elegir(actual - 1, true);
        else if (k.dKey.wasPressedThisFrame || k.rightArrowKey.wasPressedThisFrame) Elegir(actual + 1, true);
        else if (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame)
        {
            AudioManager.Sonar("ui_clic");
            Jugar();
        }
        else if (k.escapeKey.wasPressedThisFrame)
        {
            AudioManager.Sonar("ui_clic");
            Menu();
        }
    }

    // Para el clic en cada tarjeta (OnClick con el índice): el clic lo pone el AudioManager
    public void Elegir(int indice) => Elegir(indice, false);

    public void Elegir(int indice, bool conTecla)
    {
        indice = Mathf.Clamp(indice, 0, tarjetas.Length - 1);
        if (conTecla && indice != actual) AudioManager.Sonar("ui_clic");
        actual = indice;
        Progreso.NivelMarcado = actual;
        Refrescar();
    }

    public void Refrescar()
    {
        for (int i = 0; i < tarjetas.Length; i++)
        {
            var t = tarjetas[i];
            if (t == null) continue;
            bool abierto = Progreso.Desbloqueado(i), elegida = i == actual;
            if (t.marcoElegida != null) t.marcoElegida.enabled = elegida;
            if (t.raiz != null && basePos != null && i < basePos.Length)
                t.raiz.anchoredPosition = basePos[i] + (elegida ? Vector2.up * alzarElegida : Vector2.zero);
            if (t.velo != null) t.velo.SetActive(!abierto);
            if (t.candado != null) t.candado.SetActive(!abierto);
            int n = Progreso.Estrellas(i);
            for (int e = 0; e < t.estrellas.Length; e++)
                if (t.estrellas[e] != null) t.estrellas[e].sprite = e < n ? estrellaLlena : estrellaVacia;
            float mejor = Progreso.MejorSatisfaccion(i);
            if (t.mejor != null) t.mejor.text = mejor >= 0f ? $"Mejor: {Mathf.RoundToInt(mejor)}%" : "Mejor: --";
            string estado = EstadoDe(i);
            if (t.estado != null) t.estado.text = estado;
            if (t.placaEstado != null)
                t.placaEstado.color = estado == Bloqueado ? colorBloqueado : estado == Completado ? colorCompletado : colorNuevo;
        }
        if (textoDinero != null) textoDinero.text = "$" + Progreso.Dinero;
        if (textoMotos != null) textoMotos.text = $"Motos {Progreso.MotosCompradas}/{Progreso.Motos.Length}";
        int completados = Progreso.NivelesCompletados;
        if (textoNiveles != null) textoNiveles.text = $"Niveles {completados}/{Progreso.Niveles.Length}";
        if (barraNiveles != null) barraNiveles.fillAmount = (float)completados / Progreso.Niveles.Length;
        if (botonJugar != null) botonJugar.interactable = Progreso.Desbloqueado(actual);
    }

    // "Jugar" y Enter: el nivel elegido, por nombre de escena (si está bloqueado no pasa nada)
    public void Jugar()
    {
        if (saliendo || ConfirmacionAbierta || !Progreso.Desbloqueado(actual)) return;
        string escena = Progreso.Niveles[actual].escena;
        if (!Application.CanStreamedLevelBeLoaded(escena))
        {
            Debug.LogWarning($"La escena {escena} no está en Build Profiles.");
            return;
        }
        saliendo = true;
        Progreso.NivelMarcado = actual;
        SceneManager.LoadScene(escena);
    }

    public void Menu()
    {
        if (saliendo) return;
        saliendo = true;
        SceneManager.LoadScene(escenaMenu);
    }

    // "Reiniciar progreso" → recuadro Sí / No
    public void PedirReinicio()
    {
        if (confirmacion != null) confirmacion.SetActive(true);
    }

    public void ConfirmarReinicio()
    {
        Progreso.Reiniciar();
        if (confirmacion != null) confirmacion.SetActive(false);
        Elegir(0, false);
    }

    public void CancelarReinicio()
    {
        if (confirmacion != null) confirmacion.SetActive(false);
    }
}

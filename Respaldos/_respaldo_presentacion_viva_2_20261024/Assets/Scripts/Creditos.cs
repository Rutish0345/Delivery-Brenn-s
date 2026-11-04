using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Escena de créditos (Creditos, la arma ConstruirCreditos), después de "GRACIAS POR JUGAR" en los finales de 2 y 3
// estrellas. Cielo nocturno con estrellas que titilan y nubes lentas; el título fijo arriba y el texto subiendo a
// 'velocidad' unidades del Canvas por segundo (a 1080p, píxeles). Espacio o clic sostenidos aceleran x3; Esc salta
// al menú. Al terminar el desplazamiento aparece "PRESIONA ESPACIO PARA VOLVER AL MENÚ" parpadeando y Espacio (o
// clic) vuelve a MenuPrincipal directo al PanelMenu (sin presentación). Música: la de historia en bucle; el
// AudioManager hace el fundido y respeta los ajustes de Música y Efectos.
public class Creditos : MonoBehaviour
{
    [SerializeField] private RectTransform contenido;        // el texto que sube (pivote arriba)
    [SerializeField] private RectTransform ventana;          // la zona visible bajo el título
    [SerializeField] private GameObject indicador;           // "PRESIONA ESPACIO PARA VOLVER AL MENÚ"
    [SerializeField] private CanvasGroup fundido;            // negro encima de todo
    [SerializeField] private Image[] estrellas;
    [SerializeField] private RectTransform[] nubes;
    [SerializeField] private float velocidad = 60f;
    [SerializeField] private float aceleracion = 3f;
    [SerializeField] private float velocidadNubes = 10f;
    [SerializeField] private float duracionFundido = 0.6f;
    [SerializeField] private string musica = "musica_historia";
    [SerializeField] private string escenaMenu = "MenuPrincipal";

    private float desde, hasta;          // posición y del contenido al empezar y al terminar
    private float[] fase;
    private float inicio;
    private bool saliendo;

    public bool Terminado { get; private set; }
    public float Avance => Mathf.InverseLerp(desde, hasta, contenido.anchoredPosition.y);
    public float Velocidad => velocidad;
    public float Aceleracion => aceleracion;
    public bool Saliendo => saliendo;

    private void Start()
    {
        Time.timeScale = 1f;
        if (AudioManager.Instance != null) AudioManager.Instance.PonerMusica(musica, true);
        if (indicador != null) indicador.SetActive(false);
        // empieza con la primera línea justo debajo de la ventana y termina con la última en su centro
        Canvas.ForceUpdateCanvases();
        float altoVentana = ventana.rect.height;
        desde = -altoVentana;
        var ultima = UltimoHijo();
        hasta = ultima != null ? -ultima.anchoredPosition.y - altoVentana / 2f : contenido.rect.height - altoVentana / 2f;
        contenido.anchoredPosition = new Vector2(contenido.anchoredPosition.x, desde);
        fase = new float[estrellas.Length];
        var rnd = new System.Random(7);
        for (int i = 0; i < fase.Length; i++) fase[i] = (float)rnd.NextDouble() * 10f;
        inicio = Time.unscaledTime;
        StartCoroutine(Fundir(1f, 0f));
    }

    private RectTransform UltimoHijo()
    {
        RectTransform ultima = null;
        foreach (RectTransform h in contenido)
            if (ultima == null || h.anchoredPosition.y < ultima.anchoredPosition.y) ultima = h;
        return ultima;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        Cielo(dt);
        if (saliendo) return;

        var k = Keyboard.current;
        var m = Mouse.current;
        if (k != null && k.escapeKey.wasPressedThisFrame)
        {
            AudioManager.Sonar("ui_clic");
            Salir();
            return;
        }
        if (!Terminado)
        {
            bool rapido = (k != null && k.spaceKey.isPressed) || (m != null && m.leftButton.isPressed);
            Avanzar(velocidad * (rapido ? aceleracion : 1f) * dt);
        }
        else if (Time.unscaledTime - inicio > 0.3f && ((k != null && (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame))
                                                       || (m != null && m.leftButton.wasPressedThisFrame)))
        {
            AudioManager.Sonar("ui_clic");
            Salir();
        }
    }

    // Sube el texto 'delta' unidades; al llegar al final se detiene y aparece el indicador (también para las pruebas)
    public void Avanzar(float delta)
    {
        if (Terminado) return;
        float y = Mathf.Min(contenido.anchoredPosition.y + delta, hasta);
        contenido.anchoredPosition = new Vector2(contenido.anchoredPosition.x, y);
        if (y >= hasta)
        {
            Terminado = true;
            inicio = Time.unscaledTime;   // el Espacio que aceleraba no cuenta como "volver"
            if (indicador != null) indicador.SetActive(true);
        }
    }

    // estrellas titilando (en pasos, como pixel art) y nubes que cruzan despacio y vuelven a entrar por la izquierda
    private void Cielo(float dt)
    {
        float t = Time.unscaledTime;
        for (int i = 0; i < estrellas.Length; i++)
        {
            if (estrellas[i] == null) continue;
            float onda = 0.5f + 0.5f * Mathf.Sin(t * (1.2f + (i % 5) * 0.35f) + fase[i]);
            var c = estrellas[i].color;
            c.a = Mathf.Round(Mathf.Lerp(0.25f, 1f, onda) * 4f) / 4f;
            estrellas[i].color = c;
        }
        float ancho = ((RectTransform)transform).rect.width;
        foreach (var n in nubes)
        {
            if (n == null) continue;
            var p = n.anchoredPosition;
            p.x += velocidadNubes * dt * (1f + (n.GetSiblingIndex() % 2) * 0.5f);
            if (p.x - n.rect.width / 2f > ancho / 2f) p.x = -ancho / 2f - n.rect.width / 2f;
            n.anchoredPosition = p;
        }
    }

    public void Salir()
    {
        if (saliendo) return;
        saliendo = true;
        MenuPrincipal.SaltarPresentacion();   // de vuelta, directo al PanelMenu
        StartCoroutine(SalirAlMenu());
    }

    private IEnumerator SalirAlMenu()
    {
        yield return Fundir(0f, 1f);
        SceneManager.LoadScene(escenaMenu);
    }

    private IEnumerator Fundir(float a, float b)
    {
        if (fundido == null) yield break;
        fundido.blocksRaycasts = true;
        for (float t = 0f; t < duracionFundido; t += Time.unscaledDeltaTime)
        {
            fundido.alpha = Mathf.Lerp(a, b, t / duracionFundido);
            yield return null;
        }
        fundido.alpha = b;
        fundido.blocksRaycasts = b > 0f;
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Escena de créditos (Creditos, la arma ConstruirCreditos), después de "GRACIAS POR JUGAR" en los finales de 2 y 3
// estrellas. Cielo nocturno con estrellas que titilan y nubes lentas; el título fijo arriba y el texto subiendo a
// 'velocidad' unidades del Canvas por segundo (a 1080p, píxeles) desde abajo, sin pausa al empezar. Espacio o clic
// sostenidos aceleran x4; Esc salta al menú. El texto va en una ventana con RectMask2D que termina justo encima de la
// franja fija de abajo (Canvas/Franja, 80 px, #2A1F40 al 90% con borde rosa de 2 px arriba): nunca se enciman. Todo
// el contenido (los 3 bloques) cabe a la vez: entra desde abajo y se detiene cuando queda centrado en la ventana (el
// área libre entre el título y la franja), con los tres bloques visibles juntos; entonces en la franja aparece
// "PRESIONA ESPACIO PARA VOLVER AL MENÚ" parpadeando (antes, la pista chica de las teclas); Espacio
// (o clic) vuelve a MenuPrincipal directo al PanelMenu (sin presentación). Música: la de historia en bucle; el
// AudioManager hace el fundido y respeta los ajustes de Música y Efectos.
public class Creditos : MonoBehaviour
{
    [SerializeField] private RectTransform contenido;        // el texto que sube (pivote arriba)
    [SerializeField] private RectTransform ventana;          // la zona visible bajo el título
    [SerializeField] private GameObject indicador;           // "PRESIONA ESPACIO PARA VOLVER AL MENÚ", en la franja
    [SerializeField] private GameObject pista;               // la ayuda chica de las teclas, en la franja hasta el final
    [SerializeField] private RectTransform franja;           // la franja fija de abajo
    [SerializeField] private CanvasGroup fundido;            // negro encima de todo
    [SerializeField] private Image[] estrellas;
    [SerializeField] private RectTransform[] nubes;
    [SerializeField] private float velocidad = 170f;
    [SerializeField] private float aceleracion = 4f;
    [SerializeField] private float velocidadNubes = 10f;
    [SerializeField] private float duracionFundido = 0.6f;
    [SerializeField] private string musica = "musica_historia";
    [SerializeField] private string escenaMenu = "MenuPrincipal";

    private float desde, hasta;          // posición y del contenido al empezar y al terminar
    private float[] fase;
    private float inicio;
    private float arranque;
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
        if (pista != null) pista.SetActive(true);
        // empieza con la primera línea justo en la orilla de abajo de la ventana (ya entrando) y termina con todo el
        // contenido centrado en la ventana
        Canvas.ForceUpdateCanvases();
        contenido.anchoredPosition = new Vector2(contenido.anchoredPosition.x, 0f);
        Canvas.ForceUpdateCanvases();
        var todo = Todo();   // dónde quedan las líneas con el contenido en 0 (van centradas en él, no desde su orilla)
        desde = Ventana.yMin - todo.yMax;            // la primera línea justo en la orilla de abajo de la ventana
        hasta = Ventana.center.y - todo.center.y;   // cuánto hay que subir el contenido desde 0
        contenido.anchoredPosition = new Vector2(contenido.anchoredPosition.x, desde);
        fase = new float[estrellas.Length];
        var rnd = new System.Random(7);
        for (int i = 0; i < fase.Length; i++) fase[i] = (float)rnd.NextDouble() * 10f;
        inicio = arranque = Time.unscaledTime;
        StartCoroutine(Fundir(1f, 0f));
    }

    // Los bloques (de cada encabezado, el renglón que termina en "_Encabezado", a su último nombre), en coordenadas del Canvas
    public Rect[] Bloques()
    {
        var bloques = new System.Collections.Generic.List<Rect>();
        foreach (RectTransform h in contenido)
        {
            var x = EnCanvas(h);
            if (h.name.EndsWith("_Encabezado") || bloques.Count == 0) bloques.Add(x);
            else bloques[^1] = Unir(bloques[^1], x);
        }
        return bloques.ToArray();
    }

    public Rect UltimoBloque() { var b = Bloques(); return b.Length > 0 ? b[^1] : default; }

    // todo el contenido junto
    public Rect Todo()
    {
        var b = Bloques();
        Rect r = b.Length > 0 ? b[0] : default;
        foreach (var x in b) r = Unir(r, x);
        return r;
    }

    static Rect Unir(Rect a, Rect b) =>
        Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));

    public Rect Franja => franja != null ? EnCanvas(franja) : default;
    public Rect Ventana => EnCanvas(ventana);
    public float Transcurrido => Time.unscaledTime - arranque;   // desde Start, sin escala

    // el rectángulo en coordenadas locales del Canvas (abajo a la izquierda en (-960, -540) a 1080p)
    public Rect EnCanvas(RectTransform rt)
    {
        var esquinas = new Vector3[4];
        rt.GetWorldCorners(esquinas);
        Vector2 a = transform.InverseTransformPoint(esquinas[0]), b = transform.InverseTransformPoint(esquinas[2]);
        return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
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
            if (pista != null) pista.SetActive(false);
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

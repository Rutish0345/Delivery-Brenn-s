using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Intro tipo videoclip: cada viñeta tiene imagen, un titular grande y un texto de apoyo.
// Espacio / Enter / clic avanzan; Esc la salta.
// Sonido (AudioManager, respeta Música y Efectos): música de la historia con fundido, "pop" con cada titular, tic de
// máquina de escribir cada 2 letras, clic al avanzar, la música baja a 70 % en los fades, remate en la última viñeta y, al
// terminar o saltar, arranque de moto y fundido de salida hacia la música del nivel 1.
// Lo que cambia según quién reparte (imagen y texto de cada viñeta) sale de Progreso.Genero (Texto.PorGenero).
public class IntroHistoria : MonoBehaviour
{
    [Serializable]
    public class Vineta
    {
        public Sprite imagen;
        public Sprite imagenRepartidora;       // si la hay, se usa cuando juega la repartidora (Progreso.Genero)
        public string titular;                 // grande y llamativo; {nombre} se cambia solo
        [TextArea(2, 6)] public string texto;  // línea de apoyo
        [TextArea(2, 6)] public string textoRepartidora;   // si no está vacío, el texto cuando juega la repartidora
        public string indicador = "ESPACIO PARA CONTINUAR";
    }

    [SerializeField] private Vineta[] vinetas;
    [SerializeField] private Image imagen;
    [SerializeField] private TextMeshProUGUI titular;
    [SerializeField] private TextMeshProUGUI texto;
    [SerializeField] private TextMeshProUGUI textoIndicador;
    [SerializeField] private CanvasGroup grupo;             // se desvanece entre viñetas
    [SerializeField] private GameObject indicadorSiguiente; // contenedor del indicador (opcional)
    [SerializeField] private string escenaJuego = "SampleScene";
    [SerializeField] private float caracteresPorSegundo = 40f;
    [SerializeField] private float duracionFade = 0.4f;

    [Header("Sonido")]
    [SerializeField] private string musica = "musica_historia";
    [SerializeField] private int vinetaRemate = 6;            // la del remate (stinger), contando desde 1
    [SerializeField] private float musicaEnFade = 0.7f;
    [SerializeField] private float duracionSalida = 0.35f;    // el arranque de moto y el fundido antes de cargar el nivel

    private static readonly string[] Tics = { "maquina_tic_1", "maquina_tic_2", "maquina_tic_3" };
    private bool saliendo;

    private void Start()
    {
        Time.timeScale = 1f;
        if (grupo != null) grupo.alpha = 0f;
        if (AudioManager.Instance != null) AudioManager.Instance.PonerMusica(musica);   // fundido de 0.8 s desde la del menú
        StartCoroutine(Secuencia());
    }

    // Esc salta en cualquier momento, también durante la espera inicial y los fundidos
    private void Update()
    {
        if (!saliendo && PidenOmitir()) Omitir();
    }

    private IEnumerator Secuencia()
    {
        // ignora el clic del botón "Iniciar juego" que nos trajo hasta aquí
        yield return new WaitForSecondsRealtime(0.3f);

        for (int indice = 0; indice < vinetas.Length; indice++)
        {
            Vineta v = vinetas[indice];
            imagen.sprite = Progreso.EsRepartidora && v.imagenRepartidora != null ? v.imagenRepartidora : v.imagen;
            if (titular != null) titular.text = Texto.ConNombre(v.titular);
            texto.text = Texto.ConNombre(Texto.PorGenero(v.texto, v.textoRepartidora));
            texto.ForceMeshUpdate();
            texto.maxVisibleCharacters = 0;
            if (textoIndicador != null)
                textoIndicador.text = string.IsNullOrEmpty(v.indicador) ? "ESPACIO PARA CONTINUAR" : v.indicador;
            if (indicadorSiguiente != null) indicadorSiguiente.SetActive(false);

            StartCoroutine(PopTitular());
            if (indice + 1 == vinetaRemate) AudioManager.Sonar("historia_remate");
            yield return Fade(0f, 1f);
            Atenuar(1f);   // la música vuelve a su volumen al terminar de aparecer

            // máquina de escribir; si se pulsa avanzar, se muestra todo de golpe
            int total = texto.textInfo.characterCount;
            float visibles = 0f;
            int mostrados = 0;
            while ((int)visibles < total)
            {
                if (PidenOmitir()) { Omitir(); yield break; }
                if (Avanzar()) break;
                visibles += caracteresPorSegundo * Time.unscaledDeltaTime;
                texto.maxVisibleCharacters = (int)visibles;
                Teclear(mostrados, Mathf.Min((int)visibles, total));
                mostrados = Mathf.Max(mostrados, (int)visibles);
                yield return null;
            }
            texto.maxVisibleCharacters = total;
            CallarTics();   // texto completo (o mostrado de golpe): la máquina se calla
            yield return null; // para que la misma pulsación no cuente dos veces

            if (indicadorSiguiente != null) indicadorSiguiente.SetActive(true);
            while (!Avanzar())
            {
                if (PidenOmitir()) { Omitir(); yield break; }
                yield return null;
            }
            AudioManager.Sonar("historia_clic");

            if (indice < vinetas.Length - 1) Atenuar(musicaEnFade);   // baja durante el fade y vuelve con la siguiente
            yield return Fade(1f, 0f);
        }

        Omitir();
    }

    // Un tic cada 2 caracteres visibles (no en espacios), con uno de tres tonos al azar; uno por cuadro como mucho
    private void Teclear(int antes, int ahora)
    {
        var info = texto.textInfo;
        for (int i = ahora - 1; i >= antes; i--)
        {
            if ((i + 1) % 2 != 0 || i >= info.characterCount || char.IsWhiteSpace(info.characterInfo[i].character)) continue;
            AudioManager.Sonar(Tics[UnityEngine.Random.Range(0, Tics.Length)]);
            return;
        }
    }

    private static void CallarTics()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.CallarEfectos("maquina_tic");
    }

    private static void Atenuar(float factor)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.AtenuarMusica(factor);
    }

    // El titular aparece grande y se asienta, como un golpe de efecto
    private IEnumerator PopTitular()
    {
        if (titular == null) yield break;

        const float duracion = 0.3f;
        AudioManager.Sonar("historia_pop");
        float t = 0f;
        while (t < duracion)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duracion);
            float suave = 1f - (1f - k) * (1f - k);
            titular.transform.localScale = Vector3.one * Mathf.Lerp(1.35f, 1f, suave);
            yield return null;
        }
        titular.transform.localScale = Vector3.one;
    }

    private IEnumerator Fade(float desde, float hasta)
    {
        float t = 0f;
        while (t < duracionFade)
        {
            t += Time.unscaledDeltaTime;
            if (grupo != null) grupo.alpha = Mathf.Lerp(desde, hasta, t / duracionFade);
            yield return null;
        }
        if (grupo != null) grupo.alpha = hasta;
    }

    private bool Avanzar()
    {
        var k = Keyboard.current;
        var m = Mouse.current;
        return (k != null && (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame))
            || (m != null && m.leftButton.wasPressedThisFrame);
    }

    private bool PidenOmitir()
    {
        var k = Keyboard.current;
        return k != null && k.escapeKey.wasPressedThisFrame;
    }

    // También sirve para un botón "Saltar" (OnClick). Arranque de moto, la imagen y la música se desvanecen y se carga
    // el nivel 1, cuyo LevelManager cambia a su música con fundido (sin cortes)
    public void Omitir()
    {
        if (saliendo) return;
        saliendo = true;
        StopAllCoroutines();
        Time.timeScale = 1f;
        CallarTics();
        AudioManager.Sonar("moto_arranque");
        Atenuar(0.5f);
        StartCoroutine(Salir());
    }

    private IEnumerator Salir()
    {
        float desde = grupo != null ? grupo.alpha : 0f;
        for (float t = 0f; t < duracionSalida; t += Time.unscaledDeltaTime)
        {
            if (grupo != null) grupo.alpha = Mathf.Lerp(desde, 0f, t / duracionSalida);
            yield return null;
        }
        SceneManager.LoadScene(escenaJuego);
    }
}
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Pantalla "ELIGE A TU REPARTIDOR" (escena Seleccion, entre MenuPrincipal e Intro; la arma ConstruirSeleccion).
// Dos tarjetas lado a lado (oscuras con borde rosa), cada una con su personaje grande en idle y su nombre. La elegida
// sube 12 px, crece a 1.05 y lleva borde dorado con halo; la otra se atenúa al 70%. Al elegir, el personaje da un
// pasito (los cuadros de caminar de frente) con un saltito. Los dos tienen las mismas estadísticas: solo cambia el
// aspecto (Progreso.Genero). A / D (flechas) o clic en una tarjeta para cambiar; Enter o "CONFIRMAR" guardan y cargan
// la intro. La música del menú sigue sin cortarse.
public class SeleccionPersonaje : MonoBehaviour
{
    [Serializable]
    public class Tarjeta
    {
        public RectTransform raiz;
        public Image marco;          // seleccion_tarjeta / seleccion_tarjeta_elegida
        public Image halo;           // detrás de la tarjeta, solo en la elegida
        public Image velo;           // negro al 30% encima: la no elegida queda al 70%
        public Image personaje;
        public Sprite[] cuadros;     // [0] idle de frente (repartidor_sheet_0 / repartidora_sheet_0), luego el pasito
    }

    [SerializeField] private Tarjeta[] tarjetas = new Tarjeta[2];   // 0 repartidor, 1 repartidora (Progreso.Genero)
    [SerializeField] private Sprite marcoNormal, marcoElegido;
    [SerializeField] private string escenaIntro = "Intro";
    [SerializeField] private string escenaJuego = "SeleccionNivel";   // si no hubiera intro

    [Header("Elegida")]
    [SerializeField] private float subida = 12f;
    [SerializeField] private float escalaElegida = 1.05f;
    [SerializeField] private float alfaVelo = 0.3f;
    [SerializeField] private float suavidad = 14f;   // qué tan rápido llega a su lugar (1/s)

    [Header("Idle y pasito")]
    [SerializeField] private float periodoIdle = 0.6f;   // sube y baja 1 píxel del arte
    [SerializeField] private float saltoIdle = 6f;       // unidades del Canvas (el personaje va a x6)
    [SerializeField] private float duracionCuadro = 0.1f;
    [SerializeField] private float alturaSalto = 30f;

    public const int Escala = 6;

    private int actual;
    private bool saliendo;
    private Vector2[] baseTarjeta, basePersonaje;
    private float inicioPaso = -10f;

    public int Actual => actual;
    public Tarjeta[] Tarjetas => tarjetas;
    public bool DandoPaso => Time.unscaledTime - inicioPaso < DuracionPaso;
    float DuracionPaso => Mathf.Max(tarjetas[0].cuadros.Length, 1) * duracionCuadro;

    private void Start()
    {
        Time.timeScale = 1f;
        if (AudioManager.Instance != null) AudioManager.Instance.PonerMusica("musica_menu");   // la misma: no se corta
        baseTarjeta = new Vector2[tarjetas.Length];
        basePersonaje = new Vector2[tarjetas.Length];
        for (int i = 0; i < tarjetas.Length; i++)
        {
            baseTarjeta[i] = tarjetas[i].raiz.anchoredPosition;
            basePersonaje[i] = tarjetas[i].personaje.rectTransform.anchoredPosition;
        }
        Elegir(Progreso.Genero, false, false);
        Acomodar(1f);   // ya en su lugar, sin animar la entrada
    }

    private void Update()
    {
        Acomodar(1f - Mathf.Exp(-suavidad * Time.unscaledDeltaTime));
        Animar();

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

    // Para el clic en las tarjetas (TarjetaPersonaje)
    public void ElegirRepartidor() => Elegir(0, false);
    public void ElegirRepartidora() => Elegir(1, false);

    // conTecla: con el teclado suena el clic aquí (con el ratón lo pone TarjetaPersonaje)
    public void Elegir(int indice, bool conTecla) => Elegir(indice, conTecla, true);

    private void Elegir(int indice, bool conTecla, bool conPaso)
    {
        indice = indice == 1 ? 1 : 0;
        if (conTecla && indice != actual) AudioManager.Sonar("ui_clic");
        if (conPaso && (indice != actual || !DandoPaso)) inicioPaso = Time.unscaledTime;
        actual = indice;
        for (int i = 0; i < tarjetas.Length; i++)
        {
            bool elegida = i == actual;
            var t = tarjetas[i];
            t.marco.sprite = elegida ? marcoElegido : marcoNormal;
            t.halo.enabled = elegida;
            if (elegida) t.raiz.SetAsLastSibling();   // la que crece queda encima de la otra
        }
    }

    // Lleva cada tarjeta hacia su estado (k = 1: de golpe)
    private void Acomodar(float k)
    {
        for (int i = 0; i < tarjetas.Length; i++)
        {
            bool elegida = i == actual;
            var t = tarjetas[i];
            Vector2 meta = baseTarjeta[i] + (elegida ? Vector2.up * subida : Vector2.zero);
            t.raiz.anchoredPosition = Vector2.Lerp(t.raiz.anchoredPosition, meta, k);
            float escala = Mathf.Lerp(t.raiz.localScale.x, elegida ? escalaElegida : 1f, k);
            t.raiz.localScale = new Vector3(escala, escala, 1f);
            var c = t.velo.color;
            c.a = Mathf.Lerp(c.a, elegida ? 0f : alfaVelo, k);
            t.velo.color = c;
        }
    }

    // El idle (1 píxel arriba y abajo) en los dos; el pasito con saltito solo en la elegida
    private void Animar()
    {
        bool arriba = Mathf.Repeat(Time.unscaledTime, periodoIdle * 2f) >= periodoIdle;
        float paso = Time.unscaledTime - inicioPaso;
        for (int i = 0; i < tarjetas.Length; i++)
        {
            var t = tarjetas[i];
            Vector2 pos = basePersonaje[i];
            Sprite cuadro = t.cuadros[0];
            if (i == actual && paso < DuracionPaso)
            {
                int n = Mathf.Clamp((int)(paso / duracionCuadro), 0, t.cuadros.Length - 1);
                cuadro = t.cuadros[(n + 1) % t.cuadros.Length];   // del primer cuadro del pasito de vuelta al idle
                float salto = Mathf.Sin(Mathf.Clamp01(paso / DuracionPaso) * Mathf.PI) * alturaSalto;
                pos.y += Mathf.Round(salto / Escala) * Escala;   // en la rejilla del arte
            }
            else if (arriba) pos.y += saltoIdle;
            t.personaje.sprite = cuadro;
            t.personaje.rectTransform.anchoredPosition = pos;
        }
    }

    // Botón "CONFIRMAR" y Enter: guarda el género y sigue con la intro (o la selección de nivel si no hay intro)
    public void Confirmar()
    {
        if (saliendo) return;
        saliendo = true;
        Progreso.Genero = actual;
        Progreso.MarcarPersonajeElegido();   // en esta sesión el menú ya no vuelve a pedirlo
        SceneManager.LoadScene(Application.CanStreamedLevelBeLoaded(escenaIntro) ? escenaIntro : escenaJuego);
    }
}

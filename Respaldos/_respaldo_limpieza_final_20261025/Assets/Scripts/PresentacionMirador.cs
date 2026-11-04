using UnityEngine;
using UnityEngine.UI;

// Da vida a "El mirador", el fondo del menú principal (lo arma ConstruirMenuPrincipal con el arte de
// pixel_town/generate_presentacion_mirador.py). Todo con tiempo sin escalar y en pasos de píxel del arte (x4):
//  - fundido desde negro de 1.2 s al abrir;
//  - el cielo: estrellas que titilan, el sol que baja un poco a lo largo de 40 s y nubes violetas en parallax lento;
//  - la ciudad: sus dos capas se mecen muy despacio (la de cerca más) y algunas ventanas se encienden y apagan;
//  - el neón "SALÓN BRENN'S" se enciende con 2 parpadeos a los 1.5 s (con su halo);
//  - el repartidor, con la mano de visera, y la repartidora, que espera y al encenderse el letrero levanta el brazo y
//    lo señala; los dos con el viento (3 cuadros: 1 px en la gorra, el cabello y la falda);
//  - luciérnagas que flotan y titilan sobre la colina, y un destello que recorre el título cada ~6 s.
// Sin sonidos. Sigue viva detrás del PanelMenu.
public class PresentacionMirador : MonoBehaviour
{
    const float Pixel = 4f;   // un píxel del arte en el Canvas

    [Header("Fundido de entrada")]
    [SerializeField] private Image negro;
    [SerializeField] private float duracionNegro = 1.2f;

    [Header("Cielo")]
    [SerializeField] private Image[] estrellas;
    [SerializeField] private Sprite[] cuadrosEstrella;      // grande, chica
    [SerializeField] private RectTransform sol;
    [SerializeField] private float bajadaSol = 52f;
    [SerializeField] private float duracionSol = 40f;
    [SerializeField] private RectTransform[] nubes;
    [SerializeField] private float[] velocidadNubes;

    [Header("Ciudad")]
    [SerializeField] private RectTransform[] capas;         // lejos, cerca
    [SerializeField] private float[] amplitudCapas;          // unidades del Canvas
    [SerializeField] private float periodoCapas = 60f;
    [SerializeField] private Image[] ventanas;
    [SerializeField] private Sprite ventanaEncendida;
    [SerializeField] private Sprite ventanaApagada;

    [Header("Neón")]
    [SerializeField] private Image letrero;
    [SerializeField] private Sprite letreroApagado;
    [SerializeField] private Sprite letreroEncendido;
    [SerializeField] private Image halo;
    [SerializeField] private float encenderA = 1.5f;

    [Header("Personajes")]
    [SerializeField] private Image repartidor;
    [SerializeField] private Sprite[] cuadrosVisera;         // 3 cuadros de viento
    [SerializeField] private Image repartidora;
    [SerializeField] private Sprite[] cuadrosEspera;
    [SerializeField] private Sprite cuadroMedio;
    [SerializeField] private Sprite[] cuadrosSenala;

    [Header("Luciérnagas y título")]
    [SerializeField] private Image[] luciernagas;
    [SerializeField] private Sprite[] cuadrosLuciernaga;     // oro grande, oro chica, verde grande, verde chica
    [SerializeField] private Rect zonaLuciernagas;           // x, y abajo a la izquierda
    [SerializeField] private Image destello;
    [SerializeField] private float destelloDesde = -720f, destelloHasta = 720f;
    [SerializeField] private float cadaDestello = 6f;

    // el letrero: encendido / apagado desde encenderA (2 parpadeos y se queda encendido)
    static readonly float[] Parpadeos = { 0f, 0.08f, 0.18f, 0.26f, 0.36f };
    static readonly int[] Viento = { 0, 1, 2, 1 };

    public bool LetreroEncendido { get; private set; }
    public int Encendidos { get; private set; }               // veces que se prendió (2 parpadeos + el final = 3)
    public bool Senalando => repartidora != null && cuadrosSenala.Length > 0 && System.Array.IndexOf(cuadrosSenala, repartidora.sprite) >= 0;
    public float AlfaNegro => negro != null && negro.enabled ? negro.color.a : 0f;
    public int CambiosDeVentana { get; private set; }
    public int Destellos { get; private set; }
    public float Tiempo => t0;
    public float EncendidoFinal => encenderA + Parpadeos[Parpadeos.Length - 1];
    public int ObjetosAnimados =>
        (negro ? 1 : 0) + Len(estrellas) + (sol ? 1 : 0) + Len(nubes) + Len(capas) + Len(ventanas) + (letrero ? 1 : 0)
        + (halo ? 1 : 0) + (repartidor ? 1 : 0) + (repartidora ? 1 : 0) + Len(luciernagas) + (destello ? 1 : 0);

    static int Len(System.Array a) => a == null ? 0 : a.Length;

    private float t0, proximaVentana;   // t0: el tiempo de la presentación
    private bool listo;
    private Vector2 origenSol;
    private Vector2[] origenCapas, posNubes, origenLuciernagas;
    private float[] faseLuciernaga, proximaEstrella;

    private void Iniciar()
    {
        listo = true;
        if (sol != null) origenSol = sol.anchoredPosition;
        origenCapas = new Vector2[Len(capas)];
        for (int i = 0; i < origenCapas.Length; i++) origenCapas[i] = capas[i].anchoredPosition;
        posNubes = new Vector2[Len(nubes)];
        for (int i = 0; i < posNubes.Length; i++) posNubes[i] = nubes[i].anchoredPosition;
        origenLuciernagas = new Vector2[Len(luciernagas)];
        faseLuciernaga = new float[Len(luciernagas)];
        for (int i = 0; i < origenLuciernagas.Length; i++)
        {
            origenLuciernagas[i] = luciernagas[i].rectTransform.anchoredPosition;
            faseLuciernaga[i] = i * 2.399f;   // ángulo de oro: fases bien repartidas
        }
    }

    private void OnEnable()
    {
        if (!listo) Iniciar();
        t0 = -1f;   // el reloj arranca en el primer cuadro y avanza a lo más 0.1 s por cuadro: un tirón no se come el fundido
        proximaVentana = 0.6f;
        proximaEstrella = new float[Len(estrellas)];
        for (int i = 0; i < proximaEstrella.Length; i++) proximaEstrella[i] = 0.3f + i * 0.7f;
        LetreroEncendido = false;
        Encendidos = 0;
        if (letrero != null) letrero.sprite = letreroApagado;
        if (halo != null) halo.enabled = false;
        if (destello != null) destello.enabled = false;
        if (negro != null) { negro.enabled = true; negro.color = Color.black; }
        foreach (var v in ventanas) if (v != null) v.sprite = Random.value < 0.5f ? ventanaEncendida : ventanaApagada;
    }

    private void Update()
    {
        t0 = t0 < 0f ? 0f : t0 + Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        float t = t0;
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        Negro(t);
        Cielo(t, dt);
        Ciudad(t);
        Neon(t);
        Personajes(t);
        Luciernagas(t);
        Destello(t);
    }

    static float Paso(float v) => Mathf.Round(v / Pixel) * Pixel;

    private void Negro(float t)
    {
        if (negro == null || !negro.enabled) return;
        float a = 1f - Mathf.Clamp01(t / duracionNegro);
        negro.color = new Color(0f, 0f, 0f, a);
        if (a <= 0f) negro.enabled = false;
    }

    private void Cielo(float t, float dt)
    {
        if (sol != null) sol.anchoredPosition = origenSol - new Vector2(0f, Paso(bajadaSol * Mathf.Clamp01(t / duracionSol)));
        for (int i = 0; i < Len(nubes); i++)
        {
            posNubes[i].x += velocidadNubes[i] * dt;
            if (posNubes[i].x > 1000f) posNubes[i].x = -1000f - nubes[i].sizeDelta.x;
            nubes[i].anchoredPosition = new Vector2(Paso(posNubes[i].x), posNubes[i].y);
        }
        // cada estrella se agranda un momento y vuelve a ser un puntito
        for (int i = 0; i < Len(estrellas); i++)
        {
            if (t >= proximaEstrella[i] + 0.5f) proximaEstrella[i] = t + Random.Range(1.2f, 3.5f);
            estrellas[i].sprite = cuadrosEstrella[t >= proximaEstrella[i] && t < proximaEstrella[i] + 0.5f ? 0 : 1];
        }
    }

    private void Ciudad(float t)
    {
        float s = Mathf.Sin(t * Mathf.PI * 2f / periodoCapas);
        for (int i = 0; i < Len(capas); i++) capas[i].anchoredPosition = origenCapas[i] + new Vector2(Paso(s * amplitudCapas[i]), 0f);
        if (Len(ventanas) == 0 || t < proximaVentana) return;
        proximaVentana = t + Random.Range(0.4f, 1.1f);
        var v = ventanas[Random.Range(0, ventanas.Length)];
        v.sprite = v.sprite == ventanaEncendida ? ventanaApagada : ventanaEncendida;
        CambiosDeVentana++;
    }

    private void Neon(float t)
    {
        if (letrero == null) return;
        float s = t - encenderA;
        int k = -1;
        for (int i = 0; i < Parpadeos.Length; i++) if (s >= Parpadeos[i]) k = i;
        bool encendido = k >= 0 && k % 2 == 0;   // 0 prende, 1 apaga, 2 prende, 3 apaga, 4 prende y se queda
        if (encendido != LetreroEncendido)
        {
            LetreroEncendido = encendido;
            if (encendido) Encendidos++;
            letrero.sprite = encendido ? letreroEncendido : letreroApagado;
            if (halo != null) halo.enabled = encendido;
        }
    }

    private void Personajes(float t)
    {
        int v = Viento[(int)(t / 0.35f) % Viento.Length];
        if (repartidor != null && Len(cuadrosVisera) == 3) repartidor.sprite = cuadrosVisera[Viento[(int)(t / 0.35f + 1) % Viento.Length]];
        if (repartidora == null) return;
        float s = t - EncendidoFinal;
        repartidora.sprite = s < 0.25f ? cuadrosEspera[v] : s < 0.45f ? cuadroMedio : cuadrosSenala[v];
    }

    private void Luciernagas(float t)
    {
        for (int i = 0; i < Len(luciernagas); i++)
        {
            float f = faseLuciernaga[i];
            var p = origenLuciernagas[i] + new Vector2(Mathf.Sin(t * 0.37f + f) * 56f + Mathf.Sin(t * 1.1f + f * 2f) * 10f,
                                                       Mathf.Sin(t * 0.53f + f * 1.3f) * 28f + Mathf.Sin(t * 1.7f + f) * 6f);
            p.x = Mathf.Clamp(p.x, zonaLuciernagas.xMin, zonaLuciernagas.xMax);
            p.y = Mathf.Clamp(p.y, zonaLuciernagas.yMin, zonaLuciernagas.yMax);
            luciernagas[i].rectTransform.anchoredPosition = new Vector2(Paso(p.x), Paso(p.y));
            // titila: brilla, se achica y se apaga un rato
            float b = Mathf.Repeat(t * 0.6f + f * 0.31f, 1f);
            int color = i % 3 == 0 ? 2 : 0;
            luciernagas[i].enabled = b < 0.75f;
            luciernagas[i].sprite = cuadrosLuciernaga[color + (b < 0.45f ? 0 : 1)];
        }
    }

    private void Destello(float t)
    {
        if (destello == null) return;
        float s = Mathf.Repeat(t - 2.5f, cadaDestello);
        const float recorre = 0.9f;
        bool visible = t >= 2.5f && s < recorre;
        if (visible && !destello.enabled) Destellos++;
        destello.enabled = visible;
        if (!visible) return;
        var p = destello.rectTransform.anchoredPosition;
        destello.rectTransform.anchoredPosition = new Vector2(Paso(Mathf.Lerp(destelloDesde, destelloHasta, s / recorre)), p.y);
    }
}

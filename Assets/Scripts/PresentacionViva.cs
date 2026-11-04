using UnityEngine;
using UnityEngine.UI;

// Da vida a la presentación del menú principal (lo arma ConstruirMenuPrincipal con el arte de
// pixel_town/intro/generar_presentacion.py). Todo con tiempo sin escalar y en pasos de píxel del arte:
//  - el letrero se balancea 1 px (3 cuadros: el cable sigue amarrado al poste); el título (Flotar) y el subtítulo van
//    pegados a él mientras la presentación esté a la vista;
//  - el repartidor y la repartidora respiran, parpadean y saludan con la mano por turnos cada ~4 s;
//  - ventanas que se encienden y apagan, un globo de pedido sobre una casa distinta cada ~5 s, pétalos que caen
//    girando, brillitos en los cerezos, nubes en parallax, una mariposa cada ~15 s y, cada ~6 s, el faro de la
//    camioneta parpadea o una moto da un rebotito.
// Sin sonidos. Al apagarse (fin del fundido) el repartidor, que sigue en el PanelMenu, queda en su idle.
public class PresentacionViva : MonoBehaviour
{
    const float Pixel = 4f;   // un píxel del fondo en el Canvas

    [Header("Letrero")]
    [SerializeField] private Image letrero;
    [SerializeField] private Sprite[] cuadrosLetrero;   // desplazado -1, 0 y +1 px
    [SerializeField] private RectTransform subtitulo;
    [SerializeField] private float periodoLetrero = 4f;

    [Header("Personajes (0 el repartidor, 1 la repartidora)")]
    [SerializeField] private Image[] personajes;
    [SerializeField] private Sprite[] cuadrosRepartidor;    // idle, respira, parpadeo, saludo_0..2
    [SerializeField] private Sprite[] cuadrosRepartidora;
    [SerializeField] private float cadaSaludo = 4f;

    [Header("Barrio")]
    [SerializeField] private Image[] ventanas;
    [SerializeField] private Sprite ventanaEncendida;
    [SerializeField] private Sprite ventanaApagada;
    [SerializeField] private Image globo;
    [SerializeField] private Sprite[] iconosGlobo;
    [SerializeField] private Vector2[] techos;              // punta de cada techo (donde nace el globo)
    [SerializeField] private float cadaGlobo = 5f;

    [Header("Cerezos, cielo y mariposa")]
    [SerializeField] private RectTransform[] petalos;
    [SerializeField] private Sprite[] cuadrosPetalo;
    [SerializeField] private Image[] brillos;
    [SerializeField] private Sprite[] cuadrosBrillo;
    [SerializeField] private Rect[] copas;                   // dónde salen los brillos (x, y abajo a la izquierda)
    [SerializeField] private RectTransform[] nubes;
    [SerializeField] private float[] velocidadNubes;
    [SerializeField] private Image mariposa;
    [SerializeField] private Sprite[] cuadrosMariposa;
    [SerializeField] private float cadaMariposa = 15f;

    [Header("Vehículos")]
    [SerializeField] private Image faro;
    [SerializeField] private RectTransform[] motos;
    [SerializeField] private float cadaVehiculo = 6f;

    // para el título (Flotar) y las pruebas
    public Vector2 Desplazamiento { get; private set; }
    public int UltimoEnSaludar { get; private set; } = -1;
    public int SaludosDe(int i) => saludos[i];
    public int GlobosVistos { get; private set; }
    public int CambiosDeVentana { get; private set; }
    public int ObjetosAnimados =>
        1 + Len(personajes) + Len(ventanas) + (globo ? 1 : 0) + Len(petalos) + Len(brillos) + Len(nubes)
        + (mariposa ? 1 : 0) + (faro ? 1 : 0) + Len(motos) + (subtitulo ? 1 : 0);

    static int Len(System.Array a) => a == null ? 0 : a.Length;

    private float t0;
    private readonly int[] saludos = new int[2];
    private float[] proximoParpadeo, finParpadeo;
    private float inicioSaludo = -10f, proximoSaludo;
    private float proximaVentana, inicioGlobo = -10f, proximoGlobo, inicioMariposa = -100f, proximaMariposa;
    private float proximoVehiculo, inicioVehiculo = -10f;
    private int techoGlobo = -1, motoRebote = -1, turnoVehiculo;
    private Vector2 origenGlobo, origenMariposa, origenSubtitulo;
    private Vector2[] origenMotos;
    private Vector2[] posPetalo, velPetalo;
    private float[] giroPetalo, cambioPetalo;
    private int[] cuadroPetalo;
    private float[] proximoBrillo;
    private Vector2[] posNubes;
    private bool listo;

    private void Iniciar()
    {
        listo = true;
        if (subtitulo != null) origenSubtitulo = subtitulo.anchoredPosition;
        if (globo != null) origenGlobo = globo.rectTransform.anchoredPosition;
        if (mariposa != null) origenMariposa = mariposa.rectTransform.anchoredPosition;
        origenMotos = new Vector2[Len(motos)];
        for (int i = 0; i < origenMotos.Length; i++) origenMotos[i] = motos[i].anchoredPosition;
        posNubes = new Vector2[Len(nubes)];
        for (int i = 0; i < posNubes.Length; i++) posNubes[i] = nubes[i].anchoredPosition;
    }

    private void OnEnable()
    {
        if (!listo) Iniciar();
        t0 = Time.unscaledTime;
        proximoParpadeo = new[] { 1.5f, 2.6f };
        finParpadeo = new float[2];
        proximoSaludo = 1f;
        proximaVentana = 0.5f;
        proximoGlobo = 1.2f;
        proximaMariposa = 4f;
        proximoVehiculo = 3f;
        foreach (var v in ventanas) if (v != null) v.sprite = Random.value < 0.6f ? ventanaEncendida : ventanaApagada;
        if (globo != null) globo.enabled = false;
        if (mariposa != null) mariposa.enabled = false;
        if (faro != null) faro.enabled = false;
        foreach (var b in brillos) b.enabled = false;
        proximoBrillo = new float[Len(brillos)];
        for (int i = 0; i < proximoBrillo.Length; i++) proximoBrillo[i] = Random.Range(0.2f, 2f);
        int n = Len(petalos);
        posPetalo = new Vector2[n];
        velPetalo = new Vector2[n];
        giroPetalo = new float[n];
        cambioPetalo = new float[n];
        cuadroPetalo = new int[n];
        for (int i = 0; i < n; i++) NuevoPetalo(i, true);
    }

    private void OnDisable()
    {
        Desplazamiento = Vector2.zero;
        // el repartidor sigue a la vista en el PanelMenu: quieto en su idle
        if (personajes != null && personajes.Length > 0 && personajes[0] != null && cuadrosRepartidor.Length > 0)
            personajes[0].sprite = cuadrosRepartidor[0];
        if (motos != null && origenMotos != null)
            for (int i = 0; i < motos.Length; i++) motos[i].anchoredPosition = origenMotos[i];
    }

    private void Update()
    {
        float t = Time.unscaledTime - t0;
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        Letrero(t);
        Personajes(t);
        Ventanas(t);
        Globo(t);
        Petalos(t, dt);
        Brillos(t);
        Nubes(dt);
        Mariposa(t);
        Vehiculos(t);
    }

    private void Letrero(float t)
    {
        if (letrero == null || cuadrosLetrero.Length < 3) return;
        float s = Mathf.Sin(t * Mathf.PI * 2f / periodoLetrero);
        int k = s > 0.5f ? 2 : s < -0.5f ? 0 : 1;
        letrero.sprite = cuadrosLetrero[k];
        Desplazamiento = new Vector2((k - 1) * Pixel, 0f);
        if (subtitulo != null) subtitulo.anchoredPosition = origenSubtitulo + Desplazamiento;
    }

    private void Personajes(float t)
    {
        if (t >= proximoSaludo)
        {
            UltimoEnSaludar = UltimoEnSaludar == 0 ? 1 : 0;   // por turnos: primero el repartidor
            saludos[UltimoEnSaludar]++;
            inicioSaludo = t;
            proximoSaludo = t + cadaSaludo;
        }
        for (int i = 0; i < Len(personajes); i++)
        {
            var cuadros = i == 0 ? cuadrosRepartidor : cuadrosRepartidora;
            if (personajes[i] == null || cuadros == null || cuadros.Length < 6) continue;
            int c;
            float s = t - inicioSaludo;
            if (i == UltimoEnSaludar && s < 1.4f)
            {
                // levanta la mano y la agita: 0 1 2 1 2 1 0
                int[] secuencia = { 3, 4, 5, 4, 5, 4, 3 };
                c = secuencia[Mathf.Min(secuencia.Length - 1, (int)(s / 0.2f))];
            }
            else
            {
                if (t >= proximoParpadeo[i])
                {
                    finParpadeo[i] = t + 0.12f;
                    proximoParpadeo[i] = t + Random.Range(2.5f, 5f);
                }
                c = t < finParpadeo[i] ? 2 : ((int)((t + i * 0.6f) / 1.2f) % 2 == 1 ? 1 : 0);
            }
            personajes[i].sprite = cuadros[c];
        }
    }

    private void Ventanas(float t)
    {
        if (Len(ventanas) == 0 || t < proximaVentana) return;
        proximaVentana = t + Random.Range(0.5f, 1.3f);
        var v = ventanas[Random.Range(0, ventanas.Length)];
        v.sprite = v.sprite == ventanaEncendida ? ventanaApagada : ventanaEncendida;
        CambiosDeVentana++;
    }

    private void Globo(float t)
    {
        if (globo == null || Len(techos) == 0) return;
        if (t >= proximoGlobo)
        {
            int k = Random.Range(0, techos.Length - (techoGlobo >= 0 && techos.Length > 1 ? 1 : 0));
            if (techoGlobo >= 0 && techos.Length > 1 && k >= techoGlobo) k++;   // una casa distinta cada vez
            techoGlobo = k;
            globo.sprite = iconosGlobo[Random.Range(0, iconosGlobo.Length)];
            inicioGlobo = t;
            proximoGlobo = t + cadaGlobo;
            GlobosVistos++;
        }
        float s = t - inicioGlobo;
        const float dura = 2.6f;
        globo.enabled = s < dura;
        if (!globo.enabled) return;
        float sube = Mathf.Round(s / dura * 18f) * Pixel;
        globo.rectTransform.anchoredPosition = techos[techoGlobo] + new Vector2(0f, 2 * Pixel + sube);
        float a = Mathf.Min(1f, s / 0.3f) * Mathf.Clamp01((dura - s) / 0.7f);
        globo.color = new Color(1f, 1f, 1f, a);
    }

    private void NuevoPetalo(int i, bool enCualquierLado)
    {
        posPetalo[i] = enCualquierLado ? new Vector2(Random.Range(-980f, 940f), Random.Range(-520f, 560f))
                                       : new Vector2(Random.Range(-1100f, 700f), Random.Range(560f, 640f));
        velPetalo[i] = new Vector2(Random.Range(25f, 55f), -Random.Range(45f, 80f));
        giroPetalo[i] = Random.Range(0.22f, 0.45f);
        cambioPetalo[i] = 0f;
        cuadroPetalo[i] = Random.Range(0, 4);
    }

    private void Petalos(float t, float dt)
    {
        for (int i = 0; i < Len(petalos); i++)
        {
            posPetalo[i] += velPetalo[i] * dt;
            // vaivén lateral suave mientras cae
            var p = posPetalo[i] + new Vector2(Mathf.Sin(t * 1.3f + i) * 10f, 0f);
            if (p.y < -560f || p.x > 980f) NuevoPetalo(i, false);
            petalos[i].anchoredPosition = new Vector2(Mathf.Round(p.x / Pixel) * Pixel, Mathf.Round(p.y / Pixel) * Pixel);
            cambioPetalo[i] += dt;
            if (cambioPetalo[i] >= giroPetalo[i])
            {
                cambioPetalo[i] = 0f;
                cuadroPetalo[i] = (cuadroPetalo[i] + 1) % 4;   // el giro: 4 cuadros del pétalo
                petalos[i].GetComponent<Image>().sprite = cuadrosPetalo[cuadroPetalo[i]];
            }
        }
    }

    private void Brillos(float t)
    {
        for (int i = 0; i < Len(brillos); i++)
        {
            var b = brillos[i];
            float s = t - proximoBrillo[i];
            if (s < 0f) { b.enabled = false; continue; }
            if (!b.enabled && s < 0.05f)
            {
                var c = copas[Random.Range(0, copas.Length)];
                b.rectTransform.anchoredPosition = new Vector2(Mathf.Round(Random.Range(c.xMin, c.xMax) / Pixel) * Pixel,
                                                               Mathf.Round(Random.Range(c.yMin, c.yMax) / Pixel) * Pixel);
            }
            // 2 cuadros: chico, grande, chico; luego se esconde un rato
            int k = (int)(s / 0.15f);
            if (k >= 3) { b.enabled = false; proximoBrillo[i] = t + Random.Range(0.6f, 2.2f); continue; }
            b.enabled = true;
            b.sprite = cuadrosBrillo[k == 1 ? 1 : 0];
        }
    }

    private void Nubes(float dt)
    {
        for (int i = 0; i < Len(nubes); i++)
        {
            posNubes[i].x += velocidadNubes[i] * dt;
            if (posNubes[i].x > 1000f) posNubes[i].x = -1000f - nubes[i].sizeDelta.x;
            nubes[i].anchoredPosition = new Vector2(Mathf.Round(posNubes[i].x / Pixel) * Pixel, posNubes[i].y);
        }
    }

    private void Mariposa(float t)
    {
        if (mariposa == null) return;
        if (t >= proximaMariposa)
        {
            inicioMariposa = t;
            proximaMariposa = t + cadaMariposa;
        }
        float s = t - inicioMariposa;
        const float cruza = 8f;
        mariposa.enabled = s < cruza;
        if (!mariposa.enabled) return;
        float x = Mathf.Lerp(-1000f, 1000f, s / cruza);
        float y = origenMariposa.y + Mathf.Sin(s * 2.2f) * 48f + Mathf.Sin(s * 5.1f) * 12f;
        mariposa.rectTransform.anchoredPosition = new Vector2(Mathf.Round(x / Pixel) * Pixel, Mathf.Round(y / Pixel) * Pixel);
        mariposa.sprite = cuadrosMariposa[(int)(s / 0.12f) % cuadrosMariposa.Length];
    }

    private void Vehiculos(float t)
    {
        if (t >= proximoVehiculo)
        {
            inicioVehiculo = t;
            proximoVehiculo = t + cadaVehiculo;
            turnoVehiculo++;
            motoRebote = Len(motos) > 0 && (turnoVehiculo % 2 == 0 || faro == null) ? Random.Range(0, motos.Length) : -1;
        }
        float s = t - inicioVehiculo;
        if (faro != null) faro.enabled = motoRebote < 0 && s < 0.6f && (int)(s / 0.15f) % 2 == 0;   // dos destellos
        for (int i = 0; i < Len(motos); i++)
        {
            // dos rebotitos de 1 px (la moto va a x3)
            bool arriba = i == motoRebote && s < 0.5f && (int)(s / 0.12f) % 2 == 0;
            motos[i].anchoredPosition = origenMotos[i] + (arriba ? new Vector2(0f, 3f) : Vector2.zero);
        }
    }
}

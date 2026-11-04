using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Choque de la moto (niveles 2 y 3), en el repartidor. Solo con moto; a pie no hace nada (los autos y peatones
// cobran su choque de siempre). Detecta el golpe en OnCollisionEnter2D: autos (fuerte), peatones (media) y todo
// lo demás sólido, como paredes, setos, casas o puestos (leve). Un roce lento (menos de velocidadMinima hacia el
// obstáculo) no cuenta. Bache.cs llama a Golpe con la intensidad Bache. Una sola penalización por golpe, con
// GameManager.Penalizar (que ya multiplica por el factor de la moto).
//  - Fuerte/Media: pierde el control (velocidad 0) y rebota hacia atrás con la física (no atraviesa paredes),
//    tambaleo ±12° y squash, destello blanco 2 cuadros y tinte rojo, polvo (y chispas si es fuerte), texto
//    "¡CHOQUE!"/"¡AY!" con la penalización real, temblor de cámara y 1 s de parpadeo sin recibir otro golpe.
//    El auto frena 1 s con un globo "¡PIII!"; el peatón se detiene 1 s.
//  - Bache: tambaleo corto, tinte y texto (el bache hace su frenado, temblor y salpicón); no inicia el cooldown.
//  - Leve: solo un rebote corto y una nube de polvo chica, sin penalización ni texto.
// Las chispas, el polvo y el globo se dibujan en código (sin assets externos).
[DefaultExecutionOrder(50)]   // después de PlayerController: guarda la velocidad que pidió el jugador antes de la física
[RequireComponent(typeof(PlayerController))]
public class ImpactoMoto : MonoBehaviour
{
    public enum Intensidad { Leve, Bache, Media, Fuerte }

    [Header("Detección")]
    [SerializeField] private float velocidadMinima = 1.5f;   // tiles/s hacia el obstáculo
    [SerializeField] private float cooldown = 1.2f;
    [SerializeField] private float penalizacionAuto = 8f;
    [SerializeField] private float penalizacionPeaton = 5f;
    [SerializeField] private float duracionParpadeo = 1f;
    [SerializeField] private float detenerOtro = 1f;          // lo que frena el auto o se para el peatón

    [Header("Texto y globo (los asigna ConstruirImpactoMoto)")]
    [SerializeField] private TMP_FontAsset fuente;           // Brenn Start (Press Start 2P)
    [SerializeField] private Material materialContorno;      // contorno #2A1F40
    [SerializeField] private Material materialPlano;         // sin contorno, para el globo del auto
    [SerializeField] private Material materialDestello;      // silueta blanca del destello

    private struct Efecto
    {
        public float control, rebote, grados;
        public Vector2 squash;
        public bool destello, tinte, parpadeo, temblor;
        public int chispas, nubes;
        public float tamNube;
        public string texto;
    }

    private static readonly Efecto Fuerte = new Efecto
    {
        control = 0.35f, rebote = 0.3f, grados = 12f, squash = new Vector2(1.15f, 0.85f), destello = true, tinte = true,
        parpadeo = true, temblor = true, chispas = 6, nubes = 3, tamNube = 1f, texto = "¡CHOQUE!"
    };
    private static readonly Efecto Media = new Efecto
    {
        control = 0.2f, rebote = 0.15f, grados = 12f, squash = new Vector2(1.15f, 0.85f), destello = true, tinte = true,
        parpadeo = true, temblor = true, chispas = 0, nubes = 2, tamNube = 0.85f, texto = "¡AY!"
    };
    private static readonly Efecto DeBache = new Efecto
    {
        grados = 6f, squash = new Vector2(1.08f, 0.92f), tinte = true, texto = "¡BACHE!"
    };
    private static readonly Efecto Leve = new Efecto
    {
        control = 0.1f, rebote = 0.1f, squash = Vector2.one, nubes = 1, tamNube = 0.6f
    };

    private const float TiempoRebote = 0.1f;
    private const float DuracionTambaleo = 0.4f;
    private const float DuracionSquash = 0.25f;
    private const float DuracionTinte = 0.5f;
    private const int OrdenTexto = 100;
    private const int OrdenParticulas = 99;
    private const float PPU = 32f;

    private static readonly Color Amarillo = new Color32(0xFF, 0xD1, 0x66, 0xFF);
    private static readonly Color Oscuro = new Color32(0x2A, 0x1F, 0x40, 0xFF);
    private static readonly Color Crema = new Color32(0xFF, 0xF1, 0xD0, 0xFF);
    private static readonly Color Rojo = new Color32(0xFF, 0x4D, 0x4D, 0xFF);
    private static readonly Color Tinte = new Color(1f, 0.55f, 0.55f, 1f);

    private PlayerController jugador;
    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private BoxCollider2D caja;
    private CameraFollow camara;

    private Vector2 velocidadPedida;
    private float bloqueadoHasta;   // cooldown y parpadeo de un golpe fuerte o medio
    private float listoLeve;

    // tambaleo, destello, tinte y parpadeo: una copia del sprite en un hijo, así el collider no gira
    private SpriteRenderer visual, destello;
    private Efecto actual;
    private bool efectoActivo;
    private float inicioEfecto, finEfecto;
    private int cuadrosDestello;
    private float sentidoGiro = 1f;

    // para las pruebas
    public int Golpes { get; private set; }
    public Intensidad UltimoGolpe { get; private set; }
    public float UltimaPenalizacion { get; private set; }
    public bool Invulnerable => Time.time < bloqueadoHasta;

    private static Sprite spriteChispa, spriteGlobo, spriteColita;
    private static Sprite[] spritesNube;

    private void Awake()
    {
        jugador = GetComponent<PlayerController>();
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        caja = GetComponent<BoxCollider2D>();
        camara = FindFirstObjectByType<CameraFollow>();
        CrearSprites();

        var go = new GameObject("ImpactoVisual");
        go.transform.SetParent(transform, false);
        visual = go.AddComponent<SpriteRenderer>();
        var goDestello = new GameObject("Destello");
        goDestello.transform.SetParent(go.transform, false);
        destello = goDestello.AddComponent<SpriteRenderer>();
        if (materialDestello != null) destello.sharedMaterial = materialDestello;
        go.SetActive(false);
    }

    private void OnDisable() { Restaurar(); }

    private void FixedUpdate()
    {
        if (rb != null) velocidadPedida = rb.linearVelocity;
    }

    // Lo usan TrafficCar y Peaton: si el repartidor va en moto, el choque lo cobra ImpactoMoto y no ellos
    public static bool SeEncarga(Component quien)
    {
        var im = quien != null ? quien.GetComponentInParent<ImpactoMoto>() : null;
        return im != null && im.isActiveAndEnabled && im.jugador != null && im.jugador.EnMoto;
    }

    private Vector2 CentroCaja => caja != null ? (Vector2)caja.bounds.center : rb.position;

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (jugador == null || !jugador.EnMoto || col.contactCount == 0) return;
        var otro = col.collider;
        if (otro.isTrigger) return;

        var contacto = col.GetContact(0);
        // la normal apuntando del repartidor hacia el obstáculo (todos los colliders del mapa son cajas)
        Vector2 hacia = contacto.normal.normalized;
        if (Vector2.Dot(hacia, (Vector2)otro.bounds.center - CentroCaja) < 0f) hacia = -hacia;

        var auto = otro.GetComponentInParent<TrafficCar>();
        var peaton = auto == null ? otro.GetComponentInParent<Peaton>() : null;

        float rapidez = Vector2.Dot(velocidadPedida, hacia);
        if (auto != null) rapidez += Mathf.Max(0f, Vector2.Dot(auto.Movimiento, -hacia));
        if (rapidez < velocidadMinima) return;   // un roce

        if (auto != null) Golpear(Intensidad.Fuerte, penalizacionAuto, contacto.point, -hacia, auto, null);
        else if (peaton != null) Golpear(Intensidad.Media, penalizacionPeaton, contacto.point, -hacia, null, peaton);
        else Golpear(Intensidad.Leve, 0f, contacto.point, -hacia, null, null);
    }

    // Golpe desde fuera (Bache.cs). Devuelve false si no hizo nada (a pie, o en cooldown / parpadeando).
    public bool Golpe(Intensidad intensidad, float penalizacion, Vector2 punto)
    {
        Vector2 alejarse = CentroCaja - punto;
        alejarse = alejarse.sqrMagnitude > 1e-6f ? alejarse.normalized : Vector2.up;
        return Golpear(intensidad, penalizacion, punto, alejarse, null, null);
    }

    private bool Golpear(Intensidad intensidad, float penalizacion, Vector2 punto, Vector2 alejarse, TrafficCar auto, Peaton peaton)
    {
        if (!isActiveAndEnabled || jugador == null || !jugador.EnMoto) return false;
        float t = Time.time;
        if (t < bloqueadoHasta) return false;   // parpadeando: no recibe otro golpe

        Efecto e = intensidad == Intensidad.Fuerte ? Fuerte : intensidad == Intensidad.Media ? Media
                 : intensidad == Intensidad.Bache ? DeBache : Leve;
        if (intensidad == Intensidad.Leve)
        {
            if (t < listoLeve || jugador.SinControl) return false;
            listoLeve = t + cooldown;
        }
        else if (intensidad != Intensidad.Bache)
            bloqueadoHasta = t + Mathf.Max(cooldown, e.control + duracionParpadeo);

        // una sola penalización: Penalizar ya aplica el factor de la moto; aquí solo se calcula para mostrarla
        float real = 0f;
        var gm = GameManager.Instance;
        if (penalizacion > 0f && gm != null && !gm.NivelTerminado && !gm.EnEspera)
        {
            gm.Penalizar(penalizacion);
            real = penalizacion * Progreso.FactorPenalizacion;
        }
        Golpes++;
        UltimoGolpe = intensidad;
        if (intensidad == Intensidad.Fuerte || intensidad == Intensidad.Media) jugador.CancelarTurbo();   // auto o peatón
        UltimaPenalizacion = real;

        if (e.control > 0f)
            jugador.PerderControl(e.control, alejarse * (e.rebote / TiempoRebote), Mathf.Min(TiempoRebote, e.control));
        if (e.temblor && camara != null) camara.Temblar(0.15f, 0.25f);

        if (e.grados > 0f || e.tinte) Empezar(e, alejarse);
        for (int i = 0; i < e.nubes; i++)
        {
            Vector2 lado = Vector2.Perpendicular(alejarse) * ((i - (e.nubes - 1) / 2f) * 0.35f);
            StartCoroutine(Nube(punto + lado, e.tamNube, i * 0.04f));
        }
        if (e.chispas > 0) Chispas(punto, alejarse, e.chispas);
        if (!string.IsNullOrEmpty(e.texto)) StartCoroutine(Texto(e.texto, real, intensidad == Intensidad.Bache ? 0.8f : 1f));

        AudioManager.Sonar(intensidad == Intensidad.Fuerte ? "choque_fuerte" : intensidad == Intensidad.Media ? "choque_medio"
                           : intensidad == Intensidad.Bache ? "bache" : "choque_leve");
        if (auto != null) { auto.Frenar(detenerOtro); StartCoroutine(Globo(auto)); }
        if (peaton != null) peaton.Detener(detenerOtro);
        return true;
    }

    // ------------------------------------------------------------------ el sprite: tambaleo, destello, tinte, parpadeo

    private void Empezar(Efecto e, Vector2 alejarse)
    {
        if (sr == null || !sr.enabled && !efectoActivo) return;
        actual = e;
        efectoActivo = true;
        inicioEfecto = Time.time;
        finEfecto = Mathf.Max(DuracionTambaleo, e.tinte ? DuracionTinte : 0f, e.parpadeo ? e.control + duracionParpadeo : 0f);
        cuadrosDestello = e.destello ? 2 : 0;
        sentidoGiro = alejarse.x >= 0f ? -1f : 1f;   // se ladea alejándose del golpe
        visual.gameObject.SetActive(true);
        sr.enabled = false;
    }

    private void Restaurar()
    {
        if (!efectoActivo) return;
        efectoActivo = false;
        if (sr != null) sr.enabled = true;
        if (visual != null) visual.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (!efectoActivo) return;
        float e = Time.time - inicioEfecto;
        if (e >= finEfecto || !jugador.EnMoto) { Restaurar(); return; }

        // el mismo cuadro que puso PlayerController
        visual.sprite = sr.sprite;
        visual.flipX = sr.flipX;
        visual.sharedMaterial = sr.sharedMaterial;
        visual.sortingLayerID = sr.sortingLayerID;
        visual.sortingOrder = sr.sortingOrder;
        visual.spriteSortPoint = sr.spriteSortPoint;

        // tambaleo amortiguado (gira sobre los pies) y squash que vuelve a 1
        float u = Mathf.Clamp01(e / DuracionTambaleo);
        float grados = actual.grados * Mathf.Pow(1f - u, 1.5f) * Mathf.Sin(u * Mathf.PI * 5f) * sentidoGiro;
        float s = Mathf.Clamp01(e / DuracionSquash);
        Vector2 escala = Vector2.Lerp(actual.squash, Vector2.one, 1f - Mathf.Pow(1f - s, 3f));
        visual.transform.localRotation = Quaternion.Euler(0f, 0f, grados);
        visual.transform.localScale = new Vector3(escala.x, escala.y, 1f);

        // destello blanco 2 cuadros, luego tinte rojo que se apaga; después del control, parpadeo
        Color color = Color.white;
        if (actual.tinte && e < DuracionTinte) color = Color.Lerp(Tinte, Color.white, e / DuracionTinte);
        if (actual.parpadeo && e >= actual.control)
            color.a = Mathf.FloorToInt((e - actual.control) / 0.1f) % 2 == 0 ? 0.5f : 1f;
        visual.color = color;

        bool blanco = cuadrosDestello > 0 && materialDestello != null;
        destello.enabled = blanco;
        if (blanco)
        {
            cuadrosDestello--;
            destello.sprite = sr.sprite;
            destello.flipX = sr.flipX;
            destello.sortingLayerID = sr.sortingLayerID;
            destello.sortingOrder = sr.sortingOrder + 1;
            destello.spriteSortPoint = sr.spriteSortPoint;
            destello.color = Color.white;
        }
    }

    // ------------------------------------------------------------------ partículas

    private SpriteRenderer NuevaParticula(string nombre, Sprite sprite, Vector2 donde, int orden)
    {
        var go = new GameObject(nombre);
        go.transform.position = new Vector3(donde.x, donde.y, 0f);
        var p = go.AddComponent<SpriteRenderer>();
        p.sprite = sprite;
        p.sortingLayerID = sr != null ? sr.sortingLayerID : 0;
        p.sortingOrder = orden;
        return p;
    }

    private IEnumerator Nube(Vector2 donde, float tam, float retraso)
    {
        if (retraso > 0f) yield return new WaitForSeconds(retraso);
        var p = NuevaParticula("PolvoChoque", spritesNube[0], donde + Vector2.up * 0.05f, OrdenParticulas - 1);
        p.transform.localScale = Vector3.one * tam;
        const float porCuadro = 0.09f;
        for (int i = 0; i < spritesNube.Length; i++)
        {
            p.sprite = spritesNube[i];
            float fin = Time.time + porCuadro;
            while (Time.time < fin)
            {
                if (p == null) yield break;
                p.transform.position += Vector3.up * (0.6f * Time.deltaTime);
                yield return null;
            }
        }
        Destroy(p.gameObject);
    }

    private void Chispas(Vector2 donde, Vector2 alejarse, int cantidad)
    {
        Vector2 base_ = (alejarse + Vector2.up * 0.6f).normalized;
        for (int i = 0; i < cantidad; i++)
        {
            float angulo = Random.Range(-75f, 75f);
            Vector2 v = (Vector2)(Quaternion.Euler(0f, 0f, angulo) * base_) * Random.Range(3f, 5f);
            StartCoroutine(Chispa(donde + Vector2.up * 0.2f, v, i % 2 == 0 ? Amarillo : Color.white, Random.Range(0.28f, 0.4f)));
        }
    }

    private IEnumerator Chispa(Vector2 donde, Vector2 v, Color color, float vida)
    {
        var p = NuevaParticula("ChispaChoque", spriteChispa, donde, OrdenParticulas);
        p.color = color;
        float t = 0f;
        while (t < vida && p != null)
        {
            t += Time.deltaTime;
            v *= Mathf.Exp(-4f * Time.deltaTime);   // se frena
            v += Vector2.down * (6f * Time.deltaTime);
            p.transform.position += (Vector3)(v * Time.deltaTime);
            // alargada en la dirección en que vuela
            p.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg);
            p.transform.localScale = new Vector3(Mathf.Lerp(2.5f, 1f, t / vida), 1f, 1f);
            var c = color; c.a = 1f - Mathf.Clamp01((t / vida - 0.5f) * 2f);
            p.color = c;
            yield return null;
        }
        if (p != null) Destroy(p.gameObject);
    }

    // ------------------------------------------------------------------ texto flotante y globo del auto

    private TextMeshPro NuevoTexto(string nombre, string texto, float tam, Color color, Material material, int orden)
    {
        var go = new GameObject(nombre);
        var t = go.AddComponent<TextMeshPro>();
        if (fuente != null) t.font = fuente;
        if (material != null) t.fontSharedMaterial = material;
        t.text = texto;
        t.fontSize = tam;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.rectTransform.sizeDelta = new Vector2(8f, 1f);
        var rend = t.GetComponent<MeshRenderer>();
        rend.sortingLayerID = sr != null ? sr.sortingLayerID : 0;
        rend.sortingOrder = orden;
        return t;
    }

    private IEnumerator Texto(string titulo, float penalizacion, float escala)
    {
        // sobre la cabeza (con el sprite, porque durante el efecto el SpriteRenderer del repartidor está apagado)
        float arriba = transform.position.y + (sr != null && sr.sprite != null ? sr.sprite.bounds.max.y : 1.6f) + 0.35f;
        var raiz = new GameObject("TextoChoque").transform;
        raiz.position = new Vector3(transform.position.x, arriba, 0f);
        var t1 = NuevoTexto("Titulo", titulo, 4.5f * escala, Crema, materialContorno, OrdenTexto);
        t1.transform.SetParent(raiz, false);
        TextMeshPro t2 = null;
        if (penalizacion > 0f)
        {
            t2 = NuevoTexto("Penalizacion", "-" + penalizacion.ToString("0.#"), 3.8f * escala, Rojo, materialContorno, OrdenTexto);
            t2.transform.SetParent(raiz, false);
            t2.transform.localPosition = new Vector3(0f, -0.5f * escala, 0f);
        }

        const float vida = 1.1f;
        float t = 0f;
        Vector3 inicio = raiz.position;
        while (t < vida && raiz != null)
        {
            t += Time.deltaTime;
            // aparece con un salto (0 → 1.25 → 1), sube y se desvanece al final
            float pop = t < 0.1f ? Mathf.Lerp(0.2f, 1.25f, t / 0.1f) : t < 0.18f ? Mathf.Lerp(1.25f, 1f, (t - 0.1f) / 0.08f) : 1f;
            raiz.localScale = Vector3.one * pop;
            raiz.position = inicio + Vector3.up * (0.6f * (1f - Mathf.Pow(1f - t / vida, 2f)));
            float a = 1f - Mathf.Clamp01((t - (vida - 0.35f)) / 0.35f);
            t1.alpha = a;
            if (t2 != null) t2.alpha = a;
            yield return null;
        }
        if (raiz != null) Destroy(raiz.gameObject);
    }

    private IEnumerator Globo(TrafficCar auto)
    {
        var col = auto.GetComponent<Collider2D>();
        Vector2 arriba = col != null ? new Vector2(col.bounds.center.x, col.bounds.max.y + 0.55f) : (Vector2)auto.transform.position + Vector2.up * 2f;
        var raiz = new GameObject("GloboPiii").transform;
        raiz.position = arriba;

        var texto = NuevoTexto("Texto", "¡PIII!", 3.2f, Oscuro, materialPlano, OrdenTexto + 2);
        texto.transform.SetParent(raiz, false);
        texto.ForceMeshUpdate();
        Vector2 tam = texto.GetPreferredValues("¡PIII!");

        var fondo = NuevaParticula("Fondo", spriteGlobo, arriba, OrdenTexto);
        fondo.drawMode = SpriteDrawMode.Sliced;
        fondo.size = new Vector2(tam.x + 0.4f, tam.y + 0.3f);
        fondo.transform.SetParent(raiz, true);
        // la fila de arriba de la colita tapa el borde de abajo del globo
        var colita = NuevaParticula("Colita", spriteColita, arriba + Vector2.down * (fondo.size.y / 2f + 1f / PPU), OrdenTexto + 1);
        colita.transform.SetParent(raiz, true);
        raiz.SetParent(auto.transform, true);   // sigue al auto

        float t = 0f, vida = detenerOtro;
        while (t < vida && raiz != null)
        {
            t += Time.deltaTime;
            float pop = t < 0.08f ? Mathf.Lerp(0.3f, 1.15f, t / 0.08f) : t < 0.15f ? Mathf.Lerp(1.15f, 1f, (t - 0.08f) / 0.07f) : 1f;
            raiz.localScale = Vector3.one * pop;
            yield return null;
        }
        if (raiz != null) Destroy(raiz.gameObject);
    }

    // ------------------------------------------------------------------ sprites hechos en código

    private static Texture2D Textura(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var vacio = new Color32[w * h];
        tex.SetPixels32(vacio);
        return tex;
    }

    private static void CrearSprites()
    {
        if (spriteChispa != null) return;

        // chispa: 3x2 px blanca (se tiñe)
        var ch = Textura(3, 2);
        for (int x = 0; x < 3; x++) for (int y = 0; y < 2; y++) ch.SetPixel(x, y, Color.white);
        ch.Apply();
        spriteChispa = Sprite.Create(ch, new Rect(0, 0, 3, 2), new Vector2(0.5f, 0.5f), PPU);

        // nube de polvo de 3 cuadros (16x16): bola, bola grande con huecos, anillo que se deshace
        Color claro = new Color32(0xE8, 0xDC, 0xC2, 0xFF), sombra = new Color32(0xB8, 0xA6, 0x8A, 0xFF);
        spritesNube = new Sprite[3];
        for (int f = 0; f < 3; f++)
        {
            var tex = Textura(16, 16);
            float r = 4f + f * 1.7f;
            for (int x = 0; x < 16; x++)
                for (int y = 0; y < 16; y++)
                {
                    float dx = x - 7.5f, dy = y - 7.5f, d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > r) continue;
                    if (f == 1 && ((x * 7 + y * 3) % 11 == 0)) continue;                  // huecos
                    if (f == 2 && (d < r - 2f || (x + y) % 2 == 0)) continue;             // anillo ralo
                    tex.SetPixel(x, y, dy < -1.5f && d > r - 2f ? sombra : claro);
                }
            tex.Apply();
            spritesNube[f] = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.3f), PPU);
        }

        // globo: 12x12 crema con borde #2A1F40 y esquinas redondeadas, en 9 partes
        var gl = Textura(12, 12);
        for (int x = 0; x < 12; x++)
            for (int y = 0; y < 12; y++)
            {
                int ex = Mathf.Min(x, 11 - x), ey = Mathf.Min(y, 11 - y);
                if (ex + ey < 2) continue;                                   // esquina recortada
                bool borde = ex == 0 || ey == 0 || ex + ey == 2;
                gl.SetPixel(x, y, borde ? Oscuro : Crema);
            }
        gl.Apply();
        spriteGlobo = Sprite.Create(gl, new Rect(0, 0, 12, 12), new Vector2(0.5f, 0.5f), PPU, 0, SpriteMeshType.FullRect, new Vector4(4, 4, 4, 4));

        // colita del globo (triángulo hacia abajo)
        var co = Textura(7, 4);
        for (int y = 0; y < 4; y++)
            for (int x = 3 - y; x <= 3 + y; x++)
                co.SetPixel(x, y, x == 3 - y || x == 3 + y ? Oscuro : Crema);
        co.Apply();
        spriteColita = Sprite.Create(co, new Rect(0, 0, 7, 4), new Vector2(0.5f, 0.5f), PPU);
    }
}

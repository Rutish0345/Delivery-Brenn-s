using UnityEngine;

// Flecha del mundo: chevron dorado opaco (flecha_chevron.png de pixel_town/generate_flecha.py: relleno #FFD166, borde
// #2A1F40 de 2 px y borde blanco de 1 px) que orbita al repartidor (a pie o en moto) y apunta al centro del pedido
// activo. Siempre a alfa 1 y Unlit (no la oscurecen la noche, la lluvia ni el atardecer); el pulso solo cambia la
// escala de 1.0 a 1.1. Lejos (más de distanciaVisible) se ve completa; entre distanciaVisible y distanciaOculta se
// encoge hasta desaparecer; más cerca no se ve (ya manda la burbuja del marcador). Si el destino queda fuera de la
// cámara, el mismo chevron más chico, pegado a la orilla de la pantalla, señala hacia él (con el mismo pulso).
// La crea ConstruirMarcadorPedido.CrearFlecha: objeto FlechaPedido con los hijos flecha_guia (la que orbita) y
// flecha_borde (la de la orilla), Order in Layer 90: sobre el suelo y los props, debajo de la burbuja, el globo de la
// clienta y el texto de choque.
[DefaultExecutionOrder(100)]   // después de CameraFollow: flecha_borde usa la cámara ya movida en este cuadro
public class FlechaGuia : MonoBehaviour
{
    [SerializeField] private float radio = 1.2f;             // distancia fija al pecho del repartidor
    [SerializeField] private float alturaPecho = 0.9f;       // el pivote del repartidor está en los pies
    [SerializeField] private float distanciaVisible = 6f;    // tiles: más lejos se ve completa
    [SerializeField] private float distanciaOculta = 4f;     // tiles: más cerca no se ve
    [SerializeField] private float escalaPulso = 1.1f;       // el pulso va de 1.0 a esto
    [SerializeField] private float periodoPulso = 1.2f;      // segundos de un pulso completo
    [SerializeField] private float margenBorde = 0.55f;      // de la orilla de la cámara al centro de flecha_borde
    [SerializeField] private SpriteRenderer orbita;
    [SerializeField] private SpriteRenderer borde;

    private Transform jugador;
    private Camera cam;
    private Vector3 escalaOrbita, escalaBorde;

    public bool Visible => orbita != null && orbita.enabled;
    public bool VisibleBorde => borde != null && borde.enabled;
    public float AlfaOrbita => orbita != null && orbita.enabled ? orbita.color.a : 0f;
    // escala de la que orbita respecto a la construida (pulso x lejanía): 1.0–1.1 lejos, menos al acercarse
    public float EscalaRelativa => orbita != null && orbita.enabled && escalaOrbita.x > 0f ? orbita.transform.localScale.x / escalaOrbita.x : 0f;

    private void Awake()
    {
        if (orbita == null) orbita = transform.Find("flecha_guia")?.GetComponent<SpriteRenderer>() ?? GetComponentInChildren<SpriteRenderer>(true);
        if (borde == null) borde = transform.Find("flecha_borde")?.GetComponent<SpriteRenderer>();
        if (orbita != null) escalaOrbita = orbita.transform.localScale;
        if (borde != null) escalaBorde = borde.transform.localScale;
        Opaca(orbita);
        Opaca(borde);
    }

    private void Start()
    {
        var pc = FindFirstObjectByType<PlayerController>();
        if (pc != null) jugador = pc.transform;
        cam = Camera.main;
    }

    private void LateUpdate()
    {
        var gm = GameManager.Instance;
        var punto = gm != null ? gm.PuntoActual : null;
        if (jugador == null || punto == null || gm.NivelTerminado || gm.EnEspera)
        {
            Mostrar(orbita, escalaOrbita, 0f);
            Mostrar(borde, escalaBorde, 0f);
            return;
        }

        Vector2 destino = punto.Centro();
        float pulso = Mathf.Lerp(1f, escalaPulso, 0.5f - 0.5f * Mathf.Cos(Time.time * 2f * Mathf.PI / periodoPulso));

        // la distancia de los pies al centro del punto, como DeliveryPoint
        float distancia = Vector2.Distance(jugador.position, destino);
        float lejania = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(distanciaOculta, distanciaVisible, distancia));
        Vector2 origen = (Vector2)jugador.position + Vector2.up * alturaPecho;
        Vector2 hacia = destino - origen;
        if (lejania > 0f && hacia.sqrMagnitude > 0.0001f)
        {
            Vector2 dir = hacia.normalized;
            orbita.transform.position = origen + dir * radio;
            orbita.transform.rotation = Angulo(dir);
            Mostrar(orbita, escalaOrbita, pulso * lejania);
        }
        else Mostrar(orbita, escalaOrbita, 0f);

        Borde(destino, pulso);
    }

    // chevron en la orilla de la cámara, en la línea del centro de la pantalla al destino, solo si el destino no se ve
    private void Borde(Vector2 destino, float pulso)
    {
        if (borde == null) return;
        if (cam == null) cam = Camera.main;
        if (cam == null || !cam.orthographic)
        {
            Mostrar(borde, escalaBorde, 0f);
            return;
        }
        Vector2 centro = cam.transform.position;
        Vector2 medio = new Vector2(cam.orthographicSize * cam.aspect, cam.orthographicSize);
        Vector2 rel = destino - centro;
        if (Mathf.Abs(rel.x) <= medio.x && Mathf.Abs(rel.y) <= medio.y)
        {
            Mostrar(borde, escalaBorde, 0f);
            return;
        }
        Vector2 caja = medio - Vector2.one * margenBorde;
        float t = Mathf.Min(caja.x / Mathf.Max(Mathf.Abs(rel.x), 1e-4f), caja.y / Mathf.Max(Mathf.Abs(rel.y), 1e-4f));
        borde.transform.position = centro + rel * t;
        borde.transform.rotation = Angulo(rel.normalized);
        Mostrar(borde, escalaBorde, pulso);
    }

    private static Quaternion Angulo(Vector2 dir) => Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

    private static void Opaca(SpriteRenderer sr)
    {
        if (sr != null) sr.color = Color.white;   // el dorado del sprite tal cual, alfa 1
    }

    // escala 0 = oculta; si no, a esa fracción de la escala construida, siempre opaca
    private static void Mostrar(SpriteRenderer sr, Vector3 escala, float factor)
    {
        if (sr == null) return;
        bool ver = factor > 0.05f;
        if (sr.enabled != ver) sr.enabled = ver;
        if (ver) sr.transform.localScale = escala * factor;
    }
}

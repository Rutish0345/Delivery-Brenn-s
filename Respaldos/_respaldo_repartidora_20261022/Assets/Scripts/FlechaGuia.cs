using UnityEngine;

// Flecha del mundo, discreta: un chevron fino que orbita al repartidor (a pie o en moto) y apunta al centro del
// pedido activo, medio transparente y con un pulso suave. Lejos (más de distanciaVisible) se ve; entre distanciaVisible
// y distanciaOculta se desvanece; más cerca desaparece (ya manda la burbuja del marcador). Si el destino queda fuera de
// la cámara, un chevron más chico pegado a la orilla de la pantalla señala hacia él.
// La crea ConstruirMarcadorPedido.CrearFlecha: objeto FlechaPedido con los hijos flecha_guia (la que orbita) y
// flecha_borde (la de la orilla), Order in Layer 90, debajo de la burbuja, el globo de la clienta y el texto de choque.
[DefaultExecutionOrder(100)]   // después de CameraFollow: flecha_borde usa la cámara ya movida en este cuadro
public class FlechaGuia : MonoBehaviour
{
    [SerializeField] private float radio = 1.2f;             // distancia fija al pecho del repartidor
    [SerializeField] private float alturaPecho = 0.9f;       // el pivote del repartidor está en los pies
    [SerializeField] private float distanciaVisible = 6f;    // tiles: más lejos se ve completa
    [SerializeField] private float distanciaOculta = 4f;     // tiles: más cerca no se ve
    [SerializeField] private float alfaMin = 0.45f, alfaMax = 0.7f;
    [SerializeField] private float periodoPulso = 1.2f;      // segundos de un pulso completo
    [SerializeField] private float margenBorde = 0.45f;      // de la orilla de la cámara al centro de flecha_borde
    [SerializeField] private SpriteRenderer orbita;
    [SerializeField] private SpriteRenderer borde;

    private Transform jugador;
    private Camera cam;

    public bool Visible => orbita != null && orbita.enabled;
    public bool VisibleBorde => borde != null && borde.enabled;
    public float AlfaOrbita => orbita != null && orbita.enabled ? orbita.color.a : 0f;

    private void Awake()
    {
        if (orbita == null) orbita = transform.Find("flecha_guia")?.GetComponent<SpriteRenderer>() ?? GetComponentInChildren<SpriteRenderer>(true);
        if (borde == null) borde = transform.Find("flecha_borde")?.GetComponent<SpriteRenderer>();
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
            Mostrar(orbita, 0f);
            Mostrar(borde, 0f);
            return;
        }

        Vector2 destino = punto.Centro();
        float pulso = Mathf.Lerp(alfaMin, alfaMax, 0.5f - 0.5f * Mathf.Cos(Time.time * 2f * Mathf.PI / periodoPulso));

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
            Mostrar(orbita, pulso * lejania);
        }
        else Mostrar(orbita, 0f);

        Borde(destino, pulso);
    }

    // chevron en la orilla de la cámara, en la línea del centro de la pantalla al destino, solo si el destino no se ve
    private void Borde(Vector2 destino, float alfa)
    {
        if (borde == null) return;
        if (cam == null) cam = Camera.main;
        if (cam == null || !cam.orthographic)
        {
            Mostrar(borde, 0f);
            return;
        }
        Vector2 centro = cam.transform.position;
        Vector2 medio = new Vector2(cam.orthographicSize * cam.aspect, cam.orthographicSize);
        Vector2 rel = destino - centro;
        if (Mathf.Abs(rel.x) <= medio.x && Mathf.Abs(rel.y) <= medio.y)
        {
            Mostrar(borde, 0f);
            return;
        }
        Vector2 caja = medio - Vector2.one * margenBorde;
        float t = Mathf.Min(caja.x / Mathf.Max(Mathf.Abs(rel.x), 1e-4f), caja.y / Mathf.Max(Mathf.Abs(rel.y), 1e-4f));
        borde.transform.position = centro + rel * t;
        borde.transform.rotation = Angulo(rel.normalized);
        Mostrar(borde, alfa);
    }

    private static Quaternion Angulo(Vector2 dir) => Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

    private static void Mostrar(SpriteRenderer sr, float alfa)
    {
        if (sr == null) return;
        bool ver = alfa > 0.005f;
        if (sr.enabled != ver) sr.enabled = ver;
        if (ver)
        {
            var c = sr.color;
            c.a = alfa;
            sr.color = c;
        }
    }
}

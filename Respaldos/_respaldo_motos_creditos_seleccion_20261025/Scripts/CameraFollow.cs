using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform objetivo;
    [SerializeField] private float suavizado = 8f;
    // sube el punto de enfoque del pivote (pies) al centro del cuerpo
    [SerializeField] private Vector3 desfase = new Vector3(0f, 1.25f, 0f);
    // con el turbo (PlayerController.TurboActivo) la cámara se aleja un poco: +zoomTurbo de orthographicSize en
    // transicionZoom segundos (curva suave), y regresa igual de suave al acabarse
    [SerializeField] private float zoomTurbo = 0.4f;
    [SerializeField] private float transicionZoom = 0.3f;

    // Límites del mapa (lado interior de BordeMapa); la vista nunca muestra lo de afuera
    private Camera cam;
    private Rect mapa;
    private bool hayMapa;

    // Temblor corto (Bache.cs): se suma a la posición que sigue al repartidor, sin mover esa posición
    private Vector3 seguida;
    private Vector3 puesta;
    private float amplitudTemblor, duracionTemblor, relojTemblor;

    public bool Temblando => relojTemblor > 0f;

    private PlayerController jugador;
    private float tamBase, avanceZoom;   // avanceZoom: 0 sin turbo, 1 con el zoom completo
    public float TamanoBase => tamBase;
    public float ZoomActual => cam != null ? cam.orthographicSize - tamBase : 0f;

    public void Temblar(float amplitud, float duracion)
    {
        amplitudTemblor = amplitud;
        duracionTemblor = Mathf.Max(0.01f, duracion);
        relojTemblor = duracionTemblor;
    }

    public Rect Mapa => mapa;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
        hayMapa = LimitesDelMapa(out mapa);
        if (cam != null) tamBase = cam.orthographicSize;
        if (!hayMapa) Debug.LogWarning("CameraFollow: no encontré BordeMapa; la cámara no se limita.");
    }

    private void Start()
    {
        if (objetivo == null)
        {
            PlayerController jugador = FindFirstObjectByType<PlayerController>();
            if (jugador != null) objetivo = jugador.transform;
        }
        if (objetivo != null) jugador = objetivo.GetComponent<PlayerController>();
        if (objetivo != null) transform.position = Limitar(Destino());
        seguida = puesta = transform.position;
    }

    private void LateUpdate()
    {
        if (objetivo == null) return;

        if (cam != null && cam.orthographic && transicionZoom > 0f)
        {
            bool turbo = jugador != null && jugador.TurboActivo;
            avanceZoom = Mathf.MoveTowards(avanceZoom, turbo ? 1f : 0f, Time.deltaTime / transicionZoom);
            cam.orthographicSize = tamBase + zoomTurbo * Mathf.SmoothStep(0f, 1f, avanceZoom);
        }

        if (transform.position != puesta) seguida = transform.position;   // la movió alguien más
        Vector3 suave = Vector3.Lerp(seguida, Limitar(Destino()), suavizado * Time.deltaTime);
        seguida = Limitar(suave);
        Vector3 p = seguida;
        if (relojTemblor > 0f)
        {
            relojTemblor -= Time.deltaTime;
            float fuerza = amplitudTemblor * Mathf.Clamp01(relojTemblor / duracionTemblor);
            p += (Vector3)(UnityEngine.Random.insideUnitCircle * fuerza);
            p = Limitar(p);
        }
        transform.position = puesta = p;
    }

    private Vector3 Destino()
    {
        return new Vector3(
            objetivo.position.x + desfase.x,
            objetivo.position.y + desfase.y,
            transform.position.z);
    }

    // El medio ancho sale de orthographicSize * aspect, así funciona con cualquier relación de aspecto.
    // Si el mapa es más chico que la vista en un eje, la cámara se centra en ese eje.
    public Vector3 Limitar(Vector3 p)
    {
        if (!hayMapa || cam == null || !cam.orthographic) return p;
        float medioAlto = cam.orthographicSize;
        float medioAncho = medioAlto * cam.aspect;
        p.x = Eje(p.x, mapa.xMin, mapa.xMax, medioAncho);
        p.y = Eje(p.y, mapa.yMin, mapa.yMax, medioAlto);
        return p;
    }

    private static float Eje(float v, float min, float max, float medio)
    {
        if (max - min <= 2f * medio) return (min + max) * 0.5f;
        return Mathf.Clamp(v, min + medio, max - medio);
    }

    // Los cuatro colliders de BordeMapa rodean el mapa: el rectángulo de adentro es el mapa
    public static bool LimitesDelMapa(out Rect r)
    {
        r = default;
        var borde = GameObject.Find("BordeMapa");
        if (borde == null) return false;
        var cajas = borde.GetComponents<BoxCollider2D>();
        if (cajas.Length == 0) return false;

        Vector2 escala = borde.transform.lossyScale;
        Vector2 origen = borde.transform.position;
        float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
        float grosor = float.MaxValue;
        foreach (var c in cajas)
        {
            Vector2 centro = origen + Vector2.Scale(c.offset, escala);
            Vector2 tam = Vector2.Scale(c.size, escala);
            xMin = Mathf.Min(xMin, centro.x - tam.x / 2f);
            xMax = Mathf.Max(xMax, centro.x + tam.x / 2f);
            yMin = Mathf.Min(yMin, centro.y - tam.y / 2f);
            yMax = Mathf.Max(yMax, centro.y + tam.y / 2f);
            grosor = Mathf.Min(grosor, Mathf.Min(tam.x, tam.y));
        }
        r = Rect.MinMaxRect(xMin + grosor, yMin + grosor, xMax - grosor, yMax - grosor);
        return true;
    }
}

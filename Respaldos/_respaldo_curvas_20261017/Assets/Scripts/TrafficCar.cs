using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class TrafficCar : MonoBehaviour
{
    [Header("Recorrido")]
    [SerializeField] private Vector2 direccion = Vector2.right;
    [SerializeField] private float velocidad = 2.5f;
    [SerializeField] private float limiteMin;   // inicio del carril (x si es horizontal, y si es vertical)
    [SerializeField] private float limiteMax;   // final del carril
    // Puntos por los que pasa el CENTRO del cuerpo, en coordenadas del mundo. Si está vacío, el carril es
    // recto de limiteMin a limiteMax según "direccion". Empieza y termina fuera del mapa.
    [SerializeField] private Vector2[] ruta;
    [SerializeField] private string carril = "";
    [SerializeField] private string grupoSalida = "";        // autos que salen del mismo punto (vacío = el carril)
    [SerializeField] private float separacionMinima = 10f;   // al reaparecer, nadie de su salida tan cerca
    // Cruces con otra calle: solo entran a la vez autos que llegan por el mismo eje
    [SerializeField] private Rect[] cruces;

    [Header("Frenado")]
    [SerializeField] private float distanciaFrenado = 2.5f;   // medida desde la defensa

    [Header("Sprites y cuerpo según hacia dónde va")]
    [SerializeField] private Sprite spriteLado;      // mira a la derecha; a la izquierda se voltea
    [SerializeField] private Sprite spriteFrente;    // bajando
    [SerializeField] private Sprite spriteEspalda;   // subiendo
    [SerializeField] private Vector2 cuerpoLado = new Vector2(2.875f, 1.5f);
    [SerializeField] private Vector2 centroLado = new Vector2(0f, 0.75f);        // centro del cuerpo desde el pivote
    [SerializeField] private Vector2 cuerpoVertical = new Vector2(1.375f, 2.9375f);
    [SerializeField] private Vector2 centroVertical = new Vector2(0f, 1.5312f);

    [Header("Choque con el repartidor")]
    [SerializeField] private float penalizacion = 15f;
    [SerializeField] private float tiempoEntreGolpes = 2f;

    private static readonly List<TrafficCar> todos = new List<TrafficCar>();
    private static readonly Dictionary<Rect, List<(TrafficCar auto, bool horizontal)>> ocupados =
        new Dictionary<Rect, List<(TrafficCar, bool)>>();

    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private BoxCollider2D caja;
    private Vector2[] puntos;
    private float[] acumulado;
    private float largo;
    private float recorrido;
    private Vector2 dir;
    private bool horizontal;
    private bool esperandoSalida;
    private float proximoGolpe;

    public string Carril => carril;
    public Vector2 Centro => rb.position + OffsetCentro(horizontal);
    public Vector2 Dir => dir;
    public bool Esperando => esperandoSalida;
    public float Velocidad => velocidad;
    public float SeparacionMinima => separacionMinima;
    public static IReadOnlyList<TrafficCar> Todos => todos;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reiniciar()
    {
        todos.Clear();
        ocupados.Clear();
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        sr = GetComponent<SpriteRenderer>();
        caja = GetComponent<BoxCollider2D>();

        dir = direccion.normalized;
        horizontal = Mathf.Abs(dir.x) >= Mathf.Abs(dir.y);
        ArmarRuta();
        recorrido = Proyectar(rb.position + OffsetCentro(horizontal));
        Colocar(recorrido, true);
    }

    private void OnEnable() { todos.Add(this); }

    private void OnDisable()
    {
        todos.Remove(this);
        SoltarCruces();
    }

    private void ArmarRuta()
    {
        if (ruta != null && ruta.Length >= 2)
            puntos = (Vector2[])ruta.Clone();
        else
        {
            // carril recto de siempre: de limiteMin a limiteMax (o al revés), a la altura del auto
            Vector2 c = (Vector2)transform.position + OffsetCentro(horizontal);
            float desde = (horizontal ? dir.x : dir.y) > 0 ? limiteMin : limiteMax;
            float hasta = (horizontal ? dir.x : dir.y) > 0 ? limiteMax : limiteMin;
            puntos = horizontal
                ? new[] { new Vector2(desde, c.y), new Vector2(hasta, c.y) }
                : new[] { new Vector2(c.x, desde), new Vector2(c.x, hasta) };
        }
        acumulado = new float[puntos.Length];
        for (int i = 1; i < puntos.Length; i++)
            acumulado[i] = acumulado[i - 1] + Vector2.Distance(puntos[i - 1], puntos[i]);
        largo = acumulado[acumulado.Length - 1];
    }

    // Distancia recorrida del punto de la ruta más cercano a p
    private float Proyectar(Vector2 p)
    {
        float mejor = float.MaxValue, s = 0f;
        for (int i = 1; i < puntos.Length; i++)
        {
            Vector2 a = puntos[i - 1], b = puntos[i];
            float t = Mathf.Clamp01(Vector2.Dot(p - a, b - a) / Mathf.Max(1e-6f, (b - a).sqrMagnitude));
            float d = (a + (b - a) * t - p).sqrMagnitude;
            if (d < mejor) { mejor = d; s = acumulado[i - 1] + t * (acumulado[i] - acumulado[i - 1]); }
        }
        return s;
    }

    private void Muestra(float s, out Vector2 centro, out Vector2 d)
    {
        int i = 1;
        while (i < puntos.Length - 1 && acumulado[i] < s) i++;
        float tramo = Mathf.Max(1e-6f, acumulado[i] - acumulado[i - 1]);
        float t = Mathf.Clamp01((s - acumulado[i - 1]) / tramo);
        centro = Vector2.Lerp(puntos[i - 1], puntos[i], t);
        d = (puntos[i] - puntos[i - 1]).normalized;
    }

    private Vector2 OffsetCentro(bool deLado) => deLado ? centroLado : centroVertical;
    private Vector2 Cuerpo(bool deLado) => deLado ? cuerpoLado : cuerpoVertical;

    // Pone el auto en la distancia s de la ruta, con el sprite y el cuerpo de su orientación
    private void Colocar(float s, bool teletransporte)
    {
        Muestra(s, out Vector2 centro, out Vector2 d);
        dir = d;
        bool deLado = Mathf.Abs(d.x) >= Mathf.Abs(d.y);
        if (deLado != horizontal || teletransporte) Orientar(deLado);
        if (sr != null && deLado) sr.flipX = d.x < 0f;

        Vector2 pivote = centro - OffsetCentro(deLado);
        if (teletransporte)
        {
            rb.position = pivote;
            transform.position = new Vector3(pivote.x, pivote.y, transform.position.z);
        }
        else rb.MovePosition(pivote);
    }

    private void Orientar(bool deLado)
    {
        horizontal = deLado;
        if (sr != null)
        {
            Sprite s = deLado ? spriteLado : (dir.y < 0f ? spriteFrente : spriteEspalda);
            if (s != null) sr.sprite = s;
            if (!deLado) sr.flipX = false;
        }
        if (caja != null)
        {
            caja.size = Cuerpo(deLado);
            caja.offset = OffsetCentro(deLado);
        }
    }

    private void FixedUpdate()
    {
        if (esperandoSalida)
        {
            if (SalidaLibre()) Reaparecer();
            return;
        }
        if (HayAlgoAdelante()) return;

        float siguiente = recorrido + velocidad * Time.fixedDeltaTime;
        if (siguiente >= largo)
        {
            // final del carril (fuera del mapa): reaparece al inicio solo si hay hueco
            if (SalidaLibre()) Reaparecer();
            else esperandoSalida = true;
            return;
        }

        Muestra(siguiente, out Vector2 centro, out Vector2 d);
        if (!PermisoCruces(centro, d)) return;

        recorrido = siguiente;
        Colocar(recorrido, false);
    }

    private void Reaparecer()
    {
        esperandoSalida = false;
        SoltarCruces();
        recorrido = 0f;
        Colocar(0f, true);
    }

    private string Salida => string.IsNullOrEmpty(grupoSalida) ? carril : grupoSalida;

    // Nadie que salga del mismo punto a menos de separacionMinima de él (los que esperan al final no cuentan)
    private bool SalidaLibre()
    {
        Vector2 salida = puntos[0];
        foreach (var otro in todos)
        {
            if (otro == this || otro.Salida != Salida || otro.esperandoSalida) continue;
            if (Vector2.Distance(otro.Centro, salida) < separacionMinima) return false;
        }
        return true;
    }

    // ------------------------------------------------------------------ cruces

    private Rect Caja(Vector2 centro, Vector2 d, float margen)
    {
        Vector2 tam = Cuerpo(Mathf.Abs(d.x) >= Mathf.Abs(d.y)) + Vector2.one * (2f * margen);
        return new Rect(centro - tam / 2f, tam);
    }

    private bool PermisoCruces(Vector2 centro, Vector2 d)
    {
        if (cruces == null || cruces.Length == 0) return true;
        Rect yo = Caja(centro, d, 0.2f);
        bool eje = Mathf.Abs(d.x) >= Mathf.Abs(d.y);

        // primero se revisan todos; solo si todos dejan pasar se reserva
        foreach (var zona in cruces)
        {
            if (!yo.Overlaps(zona) || Dentro(zona)) continue;
            if (ocupados.TryGetValue(zona, out var lista))
                foreach (var o in lista)
                    if (o.auto != this && o.horizontal != eje) return false;
        }
        foreach (var zona in cruces)
        {
            bool toca = yo.Overlaps(zona);
            if (!ocupados.TryGetValue(zona, out var lista)) ocupados[zona] = lista = new List<(TrafficCar, bool)>();
            int i = lista.FindIndex(o => o.auto == this);
            if (toca && i < 0) lista.Add((this, eje));
            else if (!toca && i >= 0) lista.RemoveAt(i);
        }
        return true;
    }

    private bool Dentro(Rect zona) =>
        ocupados.TryGetValue(zona, out var lista) && lista.Exists(o => o.auto == this);

    private void SoltarCruces()
    {
        foreach (var lista in ocupados.Values) lista.RemoveAll(o => o.auto == this);
    }

    // ------------------------------------------------------------------ frenado

    // Frena si hay otro auto, un peatón o el repartidor (a pie o en moto) justo delante de la defensa,
    // o si el auto de adelante en su mismo carril está a menos de separacionMinima
    private bool HayAlgoAdelante()
    {
        Vector2 yo = Centro;
        foreach (var otro in todos)
        {
            if (otro == this || otro.esperandoSalida || Vector2.Dot(otro.dir, dir) < 0.9f) continue;
            Vector2 delta = otro.Centro - yo;
            if (Vector2.Dot(delta, dir) <= 0f) continue;                        // va detrás
            if (Mathf.Abs(dir.x * delta.y - dir.y * delta.x) > 0.6f) continue;   // otro carril
            if (delta.magnitude < separacionMinima) return true;
        }

        Vector2 cuerpo = Cuerpo(horizontal);
        float medioLargo = (horizontal ? cuerpo.x : cuerpo.y) / 2f;
        float ancho = (horizontal ? cuerpo.y : cuerpo.x) - 0.1f;
        Vector2 centro = Centro + dir * (medioLargo + distanciaFrenado / 2f);
        float angulo = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        Collider2D[] golpes = Physics2D.OverlapBoxAll(centro, new Vector2(distanciaFrenado, ancho), angulo);
        foreach (var c in golpes)
        {
            if (c.attachedRigidbody == rb) continue;
            if (c.GetComponentInParent<PlayerController>() != null) return true;
            if (c.GetComponentInParent<Peaton>() != null) return true;

            var otro = c.GetComponentInParent<TrafficCar>();
            if (otro != null && otro != this) return true;
        }
        return false;
    }

    private void OnCollisionStay2D(Collision2D col)
    {
        if (col.collider.GetComponentInParent<PlayerController>() == null) return;
        if (Time.time < proximoGolpe) return;

        proximoGolpe = Time.time + tiempoEntreGolpes;
        if (GameManager.Instance != null) GameManager.Instance.Penalizar(penalizacion);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        if (ruta != null)
            for (int i = 1; i < ruta.Length; i++) Gizmos.DrawLine(ruta[i - 1], ruta[i]);
        Gizmos.color = Color.red;
        if (cruces != null)
            foreach (var z in cruces) Gizmos.DrawWireCube(z.center, z.size);
    }
}

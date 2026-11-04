using System;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Nivel")]
    [SerializeField] private int pedidosMeta = 3;

    [Header("Tiempo por distancia (lo ajusta el LevelManager)")]
    [SerializeField] private bool tiempoPorDistancia = true;
    [SerializeField] private float velocidadReferencia = 3.5f;  // tiles por segundo esperados
    [SerializeField] private float factorRuta = 1.5f;           // las calles no son línea recta
    [SerializeField] private float colchon = 4f;                // segundos extra de margen
    [SerializeField] private float tiempoIdealMinimo = 10f;
    [SerializeField] private float multiplicadorMaximo = 2.5f;  // tiempoMaximo = ideal x esto

    [Header("Satisfacción (segundos por pedido)")]
    [SerializeField] private float tiempoIdeal = 20f;
    [SerializeField] private float tiempoMaximo = 50f;

    [Header("Pago")]
    [SerializeField] private int pagoBase = 50;
    [SerializeField] private int propinaMaxima = 30;

    public int PedidosMeta => pedidosMeta;
    public int Entregados => entregados;
    public int Dinero => dinero;
    public bool NivelTerminado => nivelTerminado;
    public bool EnEspera => enEspera;   // antes de empezar (p. ej. eligiendo moto): sin pedido ni reloj
    public float SatisfaccionActual => CalcularSatisfaccion();
    public DeliveryPoint PuntoActual => puntoActual;
    public float TiempoIdeal => tiempoIdeal;
    public float TiempoMaximo => tiempoMaximo;
    public IReadOnlyList<float> SatisfaccionesPorPedido => satisfacciones;

    public event Action<int, int> PedidosCambiaron;
    public event Action<int> DineroCambio;
    public event Action<int, float> NivelCompletado; // estrellas, promedio

    public event Action<DeliveryPoint, int> PedidoEntregado;

    private DeliveryPoint[] puntos;
    private DeliveryPoint puntoActual;
    private Transform jugador;
    private int entregados;
    private int dinero;

    private float tiempoPedido;
    private float penalizacion;
    private float sumaSatisfaccion;
    private bool nivelTerminado;
    private bool enEspera;
    private readonly List<float> satisfacciones = new List<float>();

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        puntos = FindObjectsByType<DeliveryPoint>(FindObjectsSortMode.None);

        PlayerController pc = FindFirstObjectByType<PlayerController>();
        if (pc != null) jugador = pc.transform;

        ElegirNuevoPunto();
    }

    private void Update()
    {
        if (!nivelTerminado && !enEspera) tiempoPedido += Time.deltaTime;
    }

    // El LevelManager detiene el nivel antes de empezar (la tienda en modo "antes del nivel"): se apaga el
    // pedido que hubiera y no corre el tiempo hasta Empezar
    public void Esperar()
    {
        enEspera = true;
        puntoActual = null;
        tiempoPedido = 0f;
        if (puntos != null) foreach (DeliveryPoint p in puntos) p.SetActivo(false);
    }

    public void Empezar()
    {
        if (!enEspera) return;
        enEspera = false;
        ElegirNuevoPunto();
        PedidosCambiaron?.Invoke(entregados, pedidosMeta);
    }

    // Lo llama el LevelManager al empezar cada nivel
    public void Configurar(int meta, float velocidadRef)
    {
        pedidosMeta = Mathf.Max(1, meta);
        velocidadReferencia = Mathf.Max(0.5f, velocidadRef);

        CalcularTiempos();
        tiempoPedido = 0f;
        PedidosCambiaron?.Invoke(entregados, pedidosMeta); // refresca "Pedidos 0/N"
    }

    // para que los baches y autos le quiten puntos al cliente actual
    public void Penalizar(float cantidad)
    {
        cantidad *= Progreso.FactorPenalizacion; // el chopper recibe la mitad, la deportiva uno y medio
        penalizacion += cantidad;
    }

    private float CalcularSatisfaccion()
    {
        float retraso = Mathf.InverseLerp(tiempoIdeal, tiempoMaximo, tiempoPedido);
        return Mathf.Clamp(100f * (1f - retraso) - penalizacion, 0f, 100f);
    }

    // El tiempo ideal depende de qué tan lejos queda la casa desde donde estás
    private void CalcularTiempos()
    {
        if (!tiempoPorDistancia || jugador == null || puntoActual == null) return;

        float distancia = Vector2.Distance(jugador.position, puntoActual.Centro());
        tiempoIdeal = Mathf.Max(tiempoIdealMinimo, distancia * factorRuta / velocidadReferencia + colchon);
        tiempoMaximo = tiempoIdeal * multiplicadorMaximo;
    }

    private void ElegirNuevoPunto()
    {
        if (puntos.Length == 0) return;

        DeliveryPoint nuevo;
        do
        {
            nuevo = puntos[UnityEngine.Random.Range(0, puntos.Length)];
        } while (nuevo == puntoActual && puntos.Length > 1);

        puntoActual = nuevo;
        tiempoPedido = 0f;
        penalizacion = 0f;
        CalcularTiempos();
        foreach (DeliveryPoint p in puntos) p.SetActivo(p == puntoActual);
    }

    public void Entregar(DeliveryPoint punto)
    {
        if (punto != puntoActual || nivelTerminado || enEspera) return;

        float satisfaccion = CalcularSatisfaccion();
        sumaSatisfaccion += satisfaccion;
        satisfacciones.Add(satisfaccion);

        int propina = Mathf.RoundToInt(propinaMaxima * satisfaccion / 100f);
        dinero += pagoBase + propina;
        entregados++;

        bool terminado = entregados >= pedidosMeta;
        if (terminado)
        {
            nivelTerminado = true;
            punto.SetActivo(false);
        }
        else
        {
            ElegirNuevoPunto();
        }
        PedidoEntregado?.Invoke(punto, propina);

        // la interfaz se avisa al final, así un error de UI no frena el juego
        PedidosCambiaron?.Invoke(entregados, pedidosMeta);
        DineroCambio?.Invoke(dinero);

        if (terminado)
        {
            float promedio = sumaSatisfaccion / entregados;
            int estrellas = promedio >= 80f ? 3 : promedio >= 50f ? 2 : 1;
            Progreso.Agregar(dinero); // el dinero del nivel pasa a la cartera
            NivelCompletado?.Invoke(estrellas, promedio);
        }
    }
}
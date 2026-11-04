using System;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Nivel")]
    [SerializeField] private int pedidosMeta = 3;

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
    public float SatisfaccionActual => CalcularSatisfaccion();
    public DeliveryPoint PuntoActual => puntoActual;

    public event Action<int, int> PedidosCambiaron;
    public event Action<int> DineroCambio;
    public event Action<int, float> NivelCompletado; // estrellas, promedio

    public event Action<DeliveryPoint, int> PedidoEntregado;

    private DeliveryPoint[] puntos;
    private DeliveryPoint puntoActual;
    private int entregados;
    private int dinero;

    private float tiempoPedido;
    private float penalizacion;
    private float sumaSatisfaccion;
    private bool nivelTerminado;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        puntos = FindObjectsByType<DeliveryPoint>(FindObjectsSortMode.None);
        ElegirNuevoPunto();
    }

    private void Update()
    {
        if (!nivelTerminado) tiempoPedido += Time.deltaTime;
    }

    // para que los baches y autos le quiten puntos al cliente actual
    public void Penalizar(float puntos)
    {
         puntos *= Progreso.FactorPenalizacion;
        penalizacion += puntos;
    }

    private float CalcularSatisfaccion()
    {
        float retraso = Mathf.InverseLerp(tiempoIdeal, tiempoMaximo, tiempoPedido);
        return Mathf.Clamp(100f * (1f - retraso) - penalizacion, 0f, 100f);
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
        foreach (DeliveryPoint p in puntos) p.SetActivo(p == puntoActual);
    }

    public void Entregar(DeliveryPoint punto)
    {
        if (punto != puntoActual || nivelTerminado) return;

        float satisfaccion = CalcularSatisfaccion();
        sumaSatisfaccion += satisfaccion;
        dinero += pagoBase + Mathf.RoundToInt(propinaMaxima * satisfaccion / 100f);
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
        PedidoEntregado?.Invoke(punto, Mathf.RoundToInt(propinaMaxima * satisfaccion / 100f));

        // la interfaz se avisa al final, así un error de UI no frena el juego
        PedidosCambiaron?.Invoke(entregados, pedidosMeta);
        DineroCambio?.Invoke(dinero);

        if (terminado)
        {
            float promedio = sumaSatisfaccion / entregados;
            int estrellas = promedio >= 80f ? 3 : promedio >= 50f ? 2 : 1;
            Progreso.Agregar(Mathf.RoundToInt(Dinero));
            NivelCompletado?.Invoke(estrellas, promedio);
        }
    }
}
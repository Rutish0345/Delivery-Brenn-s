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

    [Header("Quejas (el máximo lo ajusta el LevelManager)")]
    [SerializeField] private int quejasMaximas = 3;
    [SerializeField] private float satisfaccionQueja = 30f;     // entregar por debajo de esto también es queja

    [Header("Pago")]
    [SerializeField] private int pagoBase = 50;
    [SerializeField] private int propinaMaxima = 30;

    public int PedidosMeta => pedidosMeta;
    public int Entregados => entregados;
    public int Cancelados => cancelados;
    public int Resueltos => entregados + cancelados;   // lo que cuenta "Pedidos x/N" y el fin del nivel
    public float TiempoPedido => tiempoPedido;          // segundos del pedido actual (se detiene en pausa)
    public int Dinero => dinero;
    public bool NivelTerminado => nivelTerminado;
    public bool EnEspera => enEspera;   // antes de empezar (p. ej. eligiendo moto): sin pedido ni reloj
    public float SatisfaccionActual => CalcularSatisfaccion();
    public DeliveryPoint PuntoActual => puntoActual;
    public Producto ProductoActual => productoActual;   // lo que lleva el pedido actual (marcador, HUD, mochila)
    public float TiempoIdeal => tiempoIdeal;
    public float TiempoMaximo => tiempoMaximo;
    public IReadOnlyList<float> SatisfaccionesPorPedido => satisfacciones;
    public int Quejas => quejas;
    public int QuejasMaximas => quejasMaximas;
    public bool FueDespedido => despedido;
    public float SatisfaccionQueja => satisfaccionQueja;

    public event Action<int, int> PedidosCambiaron;
    public event Action<int> DineroCambio;
    public event Action<int, float> NivelCompletado; // estrellas, promedio

    public event Action<DeliveryPoint, int> PedidoEntregado;
    public event Action<DeliveryPoint, Producto> PedidoNuevo;   // al asignar cada pedido
    public event Action<DeliveryPoint> PedidoCancelado;         // se llegó a TiempoMaximo sin entregar
    public event Action<int, int> QuejasCambiaron;              // quejas, máximo del nivel
    public event Action Despedido;                              // se llegó al máximo de quejas: GAME OVER

    private DeliveryPoint[] puntos;
    private SeleccionPedidos seleccion;   // si la escena lo trae (nivel 2), decide el orden de los pedidos
    private DeliveryPoint puntoActual;
    private Producto productoActual;
    private bool hayProducto;
    private Transform jugador;
    private int entregados;
    private int cancelados;
    private int dinero;
    private int quejas;
    private bool despedido;

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
        seleccion = FindFirstObjectByType<SeleccionPedidos>();

        PlayerController pc = FindFirstObjectByType<PlayerController>();
        if (pc != null) jugador = pc.transform;

        ElegirNuevoPunto();
    }

    private void Update()
    {
        if (nivelTerminado || enEspera) return;
        tiempoPedido += Time.deltaTime;   // con timeScale 0 (pausa) no avanza
        if (puntoActual != null && tiempoPedido >= tiempoMaximo) Cancelar();
    }

    // La clienta cancela: sin pago ni propina, cuenta como resuelto con satisfacción 0, suma una queja y sigue el
    // siguiente pedido (o termina el nivel si era el último). El punto deja de ser el actual, así ya no se puede
    // entregar. Si con esta queja se llega al máximo, despiden al jugador aunque fuera el último pedido.
    private void Cancelar()
    {
        DeliveryPoint punto = puntoActual;
        satisfacciones.Add(0f);
        cancelados++;

        bool despide = SumarQueja();
        bool terminado = despide || Resueltos >= pedidosMeta;
        if (terminado)
        {
            nivelTerminado = true;
            punto.SetActivo(false);
        }
        else
        {
            ElegirNuevoPunto();
        }
        PedidoCancelado?.Invoke(punto);
        PedidosCambiaron?.Invoke(Resueltos, pedidosMeta);
        QuejasCambiaron?.Invoke(quejas, quejasMaximas);
        if (despide) Despedir();
        else if (terminado) Terminar();
    }

    // true si con esta queja se llega al máximo del nivel
    private bool SumarQueja()
    {
        quejas++;
        return quejas >= quejasMaximas;
    }

    // GAME OVER: se apagan los pedidos y el reloj (nivelTerminado) y no se paga nada a Progreso; el LevelManager
    // detiene el juego y muestra la pantalla de despido
    private void Despedir()
    {
        despedido = true;
        nivelTerminado = true;
        if (puntos != null) foreach (DeliveryPoint p in puntos) p.SetActivo(false);
        Despedido?.Invoke();
    }

    private void Terminar()
    {
        float promedio = sumaSatisfaccion / satisfacciones.Count;
        int estrellas = promedio >= 80f ? 3 : promedio >= 50f ? 2 : 1;
        Progreso.Agregar(dinero); // el dinero del nivel pasa a la cartera
        NivelCompletado?.Invoke(estrellas, promedio);
    }

    // El LevelManager detiene el nivel antes de empezar (la tienda en modo "antes del nivel"): se apaga el
    // pedido que hubiera y no corre el tiempo hasta Empezar
    // Salir al menú desde la pausa: el reloj se detiene y ya no se puede entregar ni cancelar. No se paga nada ni
    // se toca Progreso (el dinero del nivel solo pasa a la cartera en Terminar)
    public void Abandonar()
    {
        enEspera = true;
    }

    public void Esperar()
    {
        enEspera = true;
        puntoActual = null;
        tiempoPedido = 0f;
        if (seleccion != null) seleccion.Reiniciar();
        if (puntos != null) foreach (DeliveryPoint p in puntos) p.SetActivo(false);
    }

    public void Empezar()
    {
        if (!enEspera) return;
        enEspera = false;
        ElegirNuevoPunto();
        PedidosCambiaron?.Invoke(Resueltos, pedidosMeta);
    }

    // Lo llama el LevelManager al empezar cada nivel
    public void Configurar(int meta, float velocidadRef, int maxQuejas)
    {
        pedidosMeta = Mathf.Max(1, meta);
        velocidadReferencia = Mathf.Max(0.5f, velocidadRef);
        quejasMaximas = Mathf.Max(1, maxQuejas);
        quejas = 0;

        CalcularTiempos();
        tiempoPedido = 0f;
        PedidosCambiaron?.Invoke(Resueltos, pedidosMeta); // refresca "Pedidos 0/N"
        QuejasCambiaron?.Invoke(quejas, quejasMaximas);   // y los iconos de queja vacíos
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
        if (seleccion != null)
        {
            Vector2 desde = puntoActual != null ? puntoActual.Centro() : jugador != null ? (Vector2)jugador.position : Vector2.zero;
            nuevo = seleccion.Siguiente(puntos, puntoActual, desde);
        }
        else
            do
            {
                nuevo = puntos[UnityEngine.Random.Range(0, puntos.Length)];
            } while (nuevo == puntoActual && puntos.Length > 1);

        puntoActual = nuevo;
        // un producto al azar, distinto al del pedido anterior; se queda igual mientras el pedido siga activo
        int cantidad = CatalogoProductos.Cantidad;
        int elegido = UnityEngine.Random.Range(0, hayProducto ? cantidad - 1 : cantidad);
        if (hayProducto && elegido >= (int)productoActual) elegido++;
        productoActual = (Producto)elegido;
        hayProducto = true;
        tiempoPedido = 0f;
        penalizacion = 0f;
        CalcularTiempos();
        foreach (DeliveryPoint p in puntos) p.SetActivo(p == puntoActual);
        PedidoNuevo?.Invoke(puntoActual, productoActual);
    }

    public void Entregar(DeliveryPoint punto)
    {
        if (punto != puntoActual || nivelTerminado || enEspera) return;
        if (tiempoPedido >= tiempoMaximo) { Cancelar(); return; }   // ya se pasó: se cancela, no se entrega

        float satisfaccion = CalcularSatisfaccion();
        sumaSatisfaccion += satisfaccion;
        satisfacciones.Add(satisfaccion);

        int propina = Mathf.RoundToInt(propinaMaxima * satisfaccion / 100f);
        dinero += pagoBase + propina;
        entregados++;

        // llegó, pero tan tarde o tan golpeado que la clienta se queja (la entrega cuenta igual y se paga)
        bool queja = satisfaccion < satisfaccionQueja;
        bool despide = queja && SumarQueja();
        bool terminado = despide || Resueltos >= pedidosMeta;
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
        PedidosCambiaron?.Invoke(Resueltos, pedidosMeta);
        DineroCambio?.Invoke(dinero);
        if (queja) QuejasCambiaron?.Invoke(quejas, quejasMaximas);

        if (despide) Despedir();
        else if (terminado) Terminar();
    }
}
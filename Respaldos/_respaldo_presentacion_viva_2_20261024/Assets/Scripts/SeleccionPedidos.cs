using System.Collections.Generic;
using UnityEngine;

// Orden de los pedidos de una partida (nivel 2, Comercial): en el objeto PuntosEntrega. Al primer pedido arma un
// plan de "cantidad" puntos distintos al azar, cada uno a "distanciaMinima" tiles o más del anterior (el primero,
// del repartidor), con al menos uno al norte y uno al sur de la avenida. Si la partida pide más pedidos que el
// plan, siguen al azar respetando la distancia. Sin este componente, GameManager elige al azar como siempre.
public class SeleccionPedidos : MonoBehaviour
{
    [SerializeField] private int cantidad = 5;
    [SerializeField] private float distanciaMinima = 20f;
    [SerializeField] private float yAvenida = 32f;      // en el mundo: arriba es el norte, abajo el sur
    [SerializeField] private int intentos = 500;
    [Tooltip("\"de>a\": pares que no se piden seguidos porque ni la moto más lenta llega en el tiempo ideal " +
             "(\"spawn\" es el repartidor al empezar). Los calcula el generador del mapa (nivel 3).")]
    [SerializeField] private string[] paresInjustos = new string[0];

    private HashSet<string> injustos;

    private readonly List<DeliveryPoint> plan = new List<DeliveryPoint>();
    private int siguiente;

    public IReadOnlyList<DeliveryPoint> Plan => plan;
    public float DistanciaMinima => distanciaMinima;

    // Vuelve a empezar (GameManager.Esperar: el nivel arranca de nuevo después de elegir moto)
    public void Reiniciar()
    {
        plan.Clear();
        siguiente = 0;
    }

    public DeliveryPoint Siguiente(DeliveryPoint[] puntos, DeliveryPoint actual, Vector2 desde)
    {
        if (puntos == null || puntos.Length == 0) return null;
        if (plan.Count == 0) ArmarPlan(puntos, desde);
        if (siguiente < plan.Count) return plan[siguiente++];

        // más pedidos que el plan: al azar, lejos del anterior
        var lejos = new List<DeliveryPoint>();
        string de = actual != null ? actual.name : "spawn";
        foreach (var p in puntos)
            if (p != actual && Vector2.Distance(p.Centro(), desde) >= distanciaMinima && Justo(de, p)) lejos.Add(p);
        if (lejos.Count == 0)
            foreach (var p in puntos) if (p != actual) lejos.Add(p);
        return lejos.Count > 0 ? lejos[Random.Range(0, lejos.Count)] : puntos[0];
    }

    void ArmarPlan(DeliveryPoint[] puntos, Vector2 desde)
    {
        int n = Mathf.Min(cantidad, puntos.Length);
        var mejor = new List<DeliveryPoint>();
        for (int k = 0; k < intentos; k++)
        {
            var intento = new List<DeliveryPoint>();
            Vector2 previo = desde;
            string de = "spawn";
            for (int i = 0; i < n; i++)
            {
                var opciones = new List<DeliveryPoint>();
                foreach (var p in puntos)
                    if (!intento.Contains(p) && Vector2.Distance(p.Centro(), previo) >= distanciaMinima && Justo(de, p)) opciones.Add(p);
                if (opciones.Count == 0) break;
                var elegido = opciones[Random.Range(0, opciones.Count)];
                intento.Add(elegido);
                previo = elegido.Centro();
                de = elegido.name;
            }
            if (intento.Count == n && HayNorteYSur(intento)) { mejor = intento; break; }
            if (intento.Count > mejor.Count) mejor = intento;
        }
        if (mejor.Count < n || !HayNorteYSur(mejor))
            Debug.LogWarning($"SeleccionPedidos: no salió un plan de {n} con {distanciaMinima} tiles entre pedidos y norte/sur; quedan {mejor.Count}.", this);
        plan.Clear();
        plan.AddRange(mejor);
        siguiente = 0;
    }

    public bool Justo(string de, DeliveryPoint a)
    {
        if (paresInjustos == null || paresInjustos.Length == 0) return true;
        if (injustos == null) injustos = new HashSet<string>(paresInjustos);
        return !injustos.Contains(de + ">" + a.name);
    }

    bool HayNorteYSur(List<DeliveryPoint> lista)
    {
        bool norte = false, sur = false;
        foreach (var p in lista)
        {
            if (p.Centro().y > yAvenida) norte = true;
            else sur = true;
        }
        return norte && sur;
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Reloj del pedido en el HUD (placa "PlacaReloj" debajo de "Pedidos", la arma ConstruirReloj): cuenta regresiva
// hasta GameManager.TiempoMaximo en m:ss. La placa cambia de color según el tiempo transcurrido del pedido:
// verde antes de TiempoIdeal, amarilla desde el ideal, roja en el último 25 % del máximo, y en los últimos 5 s
// parpadea con un pulso por segundo. Usa el tiempo del GameManager, así se detiene en pausa (timeScale 0) y no
// corre mientras se elige moto. Se esconde cuando no hay pedido activo.
public class RelojPedido : MonoBehaviour
{
    public enum Estado { Oculto, Verde, Amarillo, Rojo }

    [SerializeField] private CanvasGroup grupo;
    [SerializeField] private Image placa;
    [SerializeField] private TextMeshProUGUI texto;
    [SerializeField] private Sprite placaVerde;
    [SerializeField] private Sprite placaAmarilla;
    [SerializeField] private Sprite placaRoja;
    [SerializeField, Range(0f, 1f)] private float fraccionRoja = 0.25f;   // último 25 % del máximo
    [SerializeField] private float segundosParpadeo = 5f;
    [SerializeField] private float escalaPulso = 1.12f;

    private int ultimoSegundo = -1;
    private float inicioPulso = -1f;

    public Estado EstadoActual { get; private set; } = Estado.Oculto;
    public string Texto => texto != null ? texto.text : "";
    public bool Parpadeando { get; private set; }

    // LateUpdate: después del Update del GameManager, así el pedido que se cancela en este cuadro ya no se ve
    private void LateUpdate()
    {
        var gm = GameManager.Instance;
        bool hayPedido = gm != null && !gm.NivelTerminado && !gm.EnEspera && gm.PuntoActual != null;
        if (!hayPedido)
        {
            Mostrar(Estado.Oculto, 0f);
            ultimoSegundo = -1;
            return;
        }

        float transcurrido = gm.TiempoPedido;
        float restante = Mathf.Max(0f, gm.TiempoMaximo - transcurrido);
        int segundos = Mathf.CeilToInt(restante);
        if (texto != null) texto.text = $"{segundos / 60}:{segundos % 60:00}";

        Estado estado = transcurrido < gm.TiempoIdeal ? Estado.Verde
                      : restante <= gm.TiempoMaximo * fraccionRoja ? Estado.Rojo
                      : Estado.Amarillo;

        // últimos segundos: un pulso (tick) cada vez que cambia el número y la placa parpadea con él
        Parpadeando = restante <= segundosParpadeo;
        if (Parpadeando && segundos != ultimoSegundo)
        {
            inicioPulso = transcurrido;
            if (ultimoSegundo >= 0 && segundos > 0) AudioManager.Sonar("tick");
        }
        ultimoSegundo = segundos;

        float alfa = 1f, escala = 1f;
        if (Parpadeando && inicioPulso >= 0f)
        {
            float t = transcurrido - inicioPulso;            // tiempo del juego: en pausa se queda quieto
            alfa = t % 1f < 0.5f ? 1f : 0.35f;
            if (t < 0.15f) escala = Mathf.Lerp(escalaPulso, 1f, t / 0.15f);
        }
        transform.localScale = Vector3.one * escala;
        Mostrar(estado, alfa);
    }

    private void Mostrar(Estado estado, float alfa)
    {
        EstadoActual = estado;
        if (grupo != null) grupo.alpha = estado == Estado.Oculto ? 0f : alfa;
        if (estado == Estado.Oculto)
        {
            Parpadeando = false;
            transform.localScale = Vector3.one;
            return;
        }
        if (placa == null) return;
        Sprite s = estado == Estado.Verde ? placaVerde : estado == Estado.Amarillo ? placaAmarilla : placaRoja;
        if (s != null && placa.sprite != s) placa.sprite = s;
    }
}

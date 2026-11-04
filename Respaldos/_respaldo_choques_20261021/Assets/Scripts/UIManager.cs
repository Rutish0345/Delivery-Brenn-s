using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("HUD")]
    [SerializeField] private TextMeshProUGUI textoPedidos;
    [SerializeField] private TextMeshProUGUI textoDinero;

    [Header("Medidor de satisfacción")]
    [SerializeField] private Image barraRelleno;        // Image tipo Filled
    [SerializeField] private Image[] caritasMedidor;    // enojada, neutral, feliz

    [Header("Pantalla final")]
    [SerializeField] private GameObject panelFinal;
    [SerializeField] private TextMeshProUGUI textoResumen;
    [SerializeField] private Image[] estrellas;
    [SerializeField] private Sprite estrellaLlena;
    [SerializeField] private Sprite estrellaVacia;

    private static readonly Color Verde = new Color32(0x7B, 0xD8, 0x8F, 255);
    private static readonly Color Dorado = new Color32(0xFF, 0xD1, 0x66, 255);
    private static readonly Color Rojo = new Color32(0xFF, 0x5D, 0x73, 255);
    private static readonly Color Apagada = new Color32(0x6B, 0x5B, 0x8C, 255);

    private void Start()
    {
        GameManager gm = GameManager.Instance;
        gm.PedidosCambiaron += ActualizarPedidos;
        gm.DineroCambio += ActualizarDinero;
        gm.NivelCompletado += MostrarFinal;

        ActualizarPedidos(gm.Entregados, gm.PedidosMeta);
        ActualizarDinero(gm.Dinero);
        if (panelFinal != null) panelFinal.SetActive(false);
    }

    private void OnDestroy()
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.PedidosCambiaron -= ActualizarPedidos;
        GameManager.Instance.DineroCambio -= ActualizarDinero;
        GameManager.Instance.NivelCompletado -= MostrarFinal;
    }

    private void Update()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || gm.NivelTerminado) return;

        float nivel = gm.SatisfaccionActual / 100f;

        if (barraRelleno != null)
        {
            barraRelleno.fillAmount = nivel;
            barraRelleno.color = ColorSegunNivel(nivel);
        }

        if (caritasMedidor == null) return;

        // mismos cortes que las estrellas finales: 80 y 50
        int activa = nivel >= 0.8f ? 2 : nivel >= 0.5f ? 1 : 0;
        for (int i = 0; i < caritasMedidor.Length; i++)
        {
            if (caritasMedidor[i] == null) continue; // por si queda un espacio vacío en la lista
            caritasMedidor[i].color = i == activa ? Color.white : Apagada;
        }
    }

    // rojo abajo, dorado en medio, verde arriba
    private Color ColorSegunNivel(float t)
    {
        return t > 0.5f
            ? Color.Lerp(Dorado, Verde, (t - 0.5f) * 2f)
            : Color.Lerp(Rojo, Dorado, t * 2f);
    }

    private void ActualizarPedidos(int entregados, int meta)
    {
        if (textoPedidos != null) textoPedidos.text = "Pedidos " + entregados + "/" + meta;
    }

    private void ActualizarDinero(int dinero)
    {
        if (textoDinero != null) textoDinero.text = "$" + dinero;
    }

    private void MostrarFinal(int cantidad, float promedio)
    {
        if (panelFinal != null) panelFinal.SetActive(true);

              if (textoResumen != null)
        {
            string detalle = "";
            var lista = GameManager.Instance.SatisfaccionesPorPedido;
            for (int i = 0; i < lista.Count; i++)
                detalle += (i > 0 ? " · " : "") + Mathf.RoundToInt(lista[i]);

            textoResumen.text = "Satisfacción " + Mathf.RoundToInt(promedio)
                + "%  |  $" + GameManager.Instance.Dinero
                + "\n<size=70%>Pedidos: " + detalle + "%</size>";
        }
        StartCoroutine(AnimarEstrellas(cantidad));
    }

    private IEnumerator AnimarEstrellas(int cantidad)
    {
        if (estrellas == null) yield break;

        foreach (Image e in estrellas)
        {
            if (e == null) continue;
            e.sprite = estrellaVacia;
            e.transform.localScale = Vector3.one;
        }

        yield return new WaitForSecondsRealtime(0.5f);

        for (int i = 0; i < estrellas.Length && i < cantidad; i++)
        {
            if (estrellas[i] == null) continue;
            estrellas[i].sprite = estrellaLlena;
            yield return StartCoroutine(Rebote(estrellas[i].transform));
            yield return new WaitForSecondsRealtime(0.15f);
        }
    }

    // la estrella crece un poco y vuelve a su tamaño
    private IEnumerator Rebote(Transform t)
    {
        const float duracion = 0.25f;
        float tiempo = 0f;
        while (tiempo < duracion)
        {
            tiempo += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(tiempo / duracion);
            t.localScale = Vector3.one * (1f + 0.5f * Mathf.Sin(k * Mathf.PI));
            yield return null;
        }
        t.localScale = Vector3.one;
    }
}
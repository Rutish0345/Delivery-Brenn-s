using UnityEngine;
using UnityEngine.UI;

// Placa del turbo en el HUD (Canvas/PlacaTurbo, la arma ConstruirTurbos debajo de la fila de quejas): píldora lavanda
// con el rayo y una barra delgada que se vacía con el tiempo que le queda al turbo. Solo aparece mientras hay turbo
// (fundido corto); el rayo late. Lee el estado de PlayerController.
public class TurboHUD : MonoBehaviour
{
    [SerializeField] private CanvasGroup grupo;
    [SerializeField] private Image relleno;         // Image tipo Filled (horizontal, desde la izquierda)
    [SerializeField] private RectTransform icono;
    [SerializeField] private float fundido = 0.15f;

    private PlayerController jugador;

    public bool Visible => grupo != null && grupo.alpha >= 0.99f;
    public float Fraccion => relleno != null ? relleno.fillAmount : 0f;

    private void Start()
    {
        jugador = FindFirstObjectByType<PlayerController>();
        if (grupo != null) grupo.alpha = 0f;
    }

    private void Update()
    {
        if (grupo == null) return;
        bool hay = jugador != null && jugador.TurboActivo;
        float paso = fundido > 0f ? Time.unscaledDeltaTime / fundido : 1f;
        grupo.alpha = Mathf.MoveTowards(grupo.alpha, hay ? 1f : 0f, paso);
        if (!hay) return;
        if (relleno != null) relleno.fillAmount = jugador.TurboDuracion > 0f ? jugador.TurboRestante / jugador.TurboDuracion : 0f;
        if (icono != null) icono.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(Time.unscaledTime * 10f));
    }
}

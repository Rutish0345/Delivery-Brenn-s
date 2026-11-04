using UnityEngine;
using UnityEngine.UI;

// Placa del turbo en el HUD (Canvas/PlacaTurbo, la arma ConstruirTurbos debajo de la fila de quejas): píldora lavanda
// con el rayo y una barra dorada que se vacía con el tiempo que le queda al turbo. Solo aparece mientras hay turbo
// (fundido corto); el rayo late. Con el turbo también se ven las líneas de velocidad suaves en la orilla de la pantalla
// (Canvas/LineasVelocidad, debajo de todo el HUD): alterna sus 3 cuadros y se desvanece al acabarse. Lee el estado de
// PlayerController.
public class TurboHUD : MonoBehaviour
{
    [SerializeField] private CanvasGroup grupo;
    [SerializeField] private Image relleno;         // Image tipo Filled (horizontal, desde la izquierda)
    [SerializeField] private RectTransform icono;
    [SerializeField] private float fundido = 0.15f;
    [SerializeField] private Image lineas;          // pantalla completa, sin raycast
    [SerializeField] private Sprite[] cuadrosLineas; // lineas_velocidad_0..2
    [SerializeField] private float alfaLineas = 0.75f;
    [SerializeField] private float tiempoPorCuadroLineas = 0.07f;

    private PlayerController jugador;
    private float relojLineas;
    private int cuadroLineas;

    public bool Visible => grupo != null && grupo.alpha >= 0.99f;
    public float Fraccion => relleno != null ? relleno.fillAmount : 0f;
    public float AlfaLineas => lineas != null && lineas.enabled ? lineas.color.a : 0f;

    private void Start()
    {
        jugador = FindFirstObjectByType<PlayerController>();
        if (grupo != null) grupo.alpha = 0f;
        if (lineas != null) { Alfa(lineas, 0f); lineas.enabled = false; }
    }

    private void Update()
    {
        if (grupo == null) return;
        bool hay = jugador != null && jugador.TurboActivo;
        float paso = fundido > 0f ? Time.unscaledDeltaTime / fundido : 1f;
        grupo.alpha = Mathf.MoveTowards(grupo.alpha, hay ? 1f : 0f, paso);

        if (lineas != null)
        {
            float a = Mathf.MoveTowards(lineas.color.a, hay ? alfaLineas : 0f, paso * alfaLineas);
            Alfa(lineas, a);
            lineas.enabled = a > 0.001f;
            if (lineas.enabled && cuadrosLineas != null && cuadrosLineas.Length > 0 && Time.deltaTime > 0f)   // en pausa no se mueven
            {
                relojLineas += Time.deltaTime;
                if (relojLineas >= tiempoPorCuadroLineas)
                {
                    relojLineas = 0f;
                    cuadroLineas = (cuadroLineas + 1) % cuadrosLineas.Length;
                }
                lineas.sprite = cuadrosLineas[cuadroLineas];
            }
        }

        if (!hay) return;
        if (relleno != null) relleno.fillAmount = jugador.TurboDuracion > 0f ? jugador.TurboRestante / jugador.TurboDuracion : 0f;
        if (icono != null) icono.localScale = Vector3.one * (1f + 0.1f * Mathf.Sin(Time.unscaledTime * 10f));
    }

    private static void Alfa(Image img, float a)
    {
        var c = img.color;
        c.a = a;
        img.color = c;
    }
}

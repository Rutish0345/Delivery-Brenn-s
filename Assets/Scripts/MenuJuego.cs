using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class MenuJuego : MonoBehaviour
{
    [SerializeField] private GameObject panelPausa;
    [Tooltip("Recuadro \"¿Salir al menú?\" dentro de PanelPausa (PausaConfirmar), lo arma ConstruirPausa")]
    [SerializeField] private GameObject confirmacion;

    private bool enPausa;

    public bool Confirmando => confirmacion != null && confirmacion.activeSelf;

    private void Start()
    {
        // por si se reinicia desde una pausa
        Time.timeScale = 1f;
        if (panelPausa != null) panelPausa.SetActive(false);
        if (confirmacion != null) confirmacion.SetActive(false);
    }

    private void Update()
    {
        var k = Keyboard.current;
        if (k == null) return;
        // P también pausa y sigue (como Esc), pero no contesta la pregunta de salir
        if (k.pKey.wasPressedThisFrame && !Confirmando) { AlternarPausa(); return; }
        if (!k.escapeKey.wasPressedThisFrame) return;
        // Esc con "¿Salir al menú?" abierto equivale a "No"
        if (Confirmando) { CancelarMenu(); AudioManager.Sonar("ui_clic"); }
        else AlternarPausa();
    }

    public void AlternarPausa()
    {
        // con el nivel terminado ya no tiene sentido pausar, ni mientras se elige moto (ya está en pausa)
        if (GameManager.Instance != null && (GameManager.Instance.NivelTerminado || GameManager.Instance.EnEspera)) return;

        enPausa = !enPausa;
        Time.timeScale = enPausa ? 0f : 1f;
        if (panelPausa != null) panelPausa.SetActive(enPausa);
        if (confirmacion != null) confirmacion.SetActive(false);   // la pausa siempre abre sin la pregunta
        AudioManager.Sonar(enPausa ? "pausa_abrir" : "pausa_cerrar");
    }

    // Botón "Menú" de la pausa: primero pregunta
    public void PedirMenu()
    {
        if (enPausa && confirmacion != null) confirmacion.SetActive(true);
    }

    // "No": vuelve a la pausa
    public void CancelarMenu()
    {
        if (confirmacion != null) confirmacion.SetActive(false);
    }

    // "Sí": se abandona el nivel (sin pagar nada ni tocar Progreso) y se va al menú como desde el panel final
    public void ConfirmarMenu()
    {
        if (GameManager.Instance != null) GameManager.Instance.Abandonar();      // el reloj se detiene
        if (AudioManager.Instance != null) AudioManager.Instance.SalirDelNivel(); // lluvia, motor y efectos; la música la pone el menú
        Time.timeScale = 1f;
        enPausa = false;
        var nivel = FindFirstObjectByType<LevelManager>();
        if (nivel != null) nivel.IrAlMenu();
        else SceneManager.LoadScene("MenuPrincipal");
    }

    public void Reiniciar()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}

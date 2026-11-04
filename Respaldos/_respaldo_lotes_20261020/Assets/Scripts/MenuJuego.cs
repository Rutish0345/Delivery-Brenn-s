using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class MenuJuego : MonoBehaviour
{
    [SerializeField] private GameObject panelPausa;

    private bool enPausa;

    private void Start()
    {
        // por si se reinicia desde una pausa
        Time.timeScale = 1f;
        if (panelPausa != null) panelPausa.SetActive(false);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            AlternarPausa();
    }

    public void AlternarPausa()
    {
        // con el nivel terminado ya no tiene sentido pausar
        if (GameManager.Instance != null && GameManager.Instance.NivelTerminado) return;

        enPausa = !enPausa;
        Time.timeScale = enPausa ? 0f : 1f;
        if (panelPausa != null) panelPausa.SetActive(enPausa);
    }

    public void Reiniciar()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
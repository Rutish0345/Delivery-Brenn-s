using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuPrincipal : MonoBehaviour
{
    [SerializeField] private TMP_InputField campoNombre;
    [SerializeField] private GameObject panelHistoria;       // ya no se usa: la historia es la intro
    [SerializeField] private GameObject panelInstrucciones;
    [SerializeField] private string escenaIntro = "Intro";
    [SerializeField] private string escenaJuego = "SampleScene";

    private void Start()
    {
        Time.timeScale = 1f;
        if (campoNombre != null) campoNombre.text = Progreso.NombreJugador;
        CerrarPaneles();
    }

    // Botón "Iniciar juego": guarda el nombre y muestra la intro
    public void Jugar()
    {
        string nombre = campoNombre != null ? campoNombre.text.Trim() : "";
        if (nombre.Length == 0) nombre = "Repartidor";
        Progreso.NombreJugador = nombre;

        string destino = Application.CanStreamedLevelBeLoaded(escenaIntro) ? escenaIntro : escenaJuego;
        SceneManager.LoadScene(destino);
    }

    public void AbrirHistoria() { Mostrar(panelHistoria); }
    public void AbrirInstrucciones() { Mostrar(panelInstrucciones); }
    public void CerrarPaneles() { Mostrar(null); }

    public void Salir()
    {
        Application.Quit(); // en el editor no hace nada; funciona en el juego compilado
    }

    private void Mostrar(GameObject panel)
    {
        if (panelHistoria != null) panelHistoria.SetActive(panel == panelHistoria);
        if (panelInstrucciones != null) panelInstrucciones.SetActive(panel == panelInstrucciones);
    }
}
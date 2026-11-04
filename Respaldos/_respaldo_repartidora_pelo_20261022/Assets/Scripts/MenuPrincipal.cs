using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuPrincipal : MonoBehaviour
{
    [SerializeField] private TMP_InputField campoNombre;
    [SerializeField] private GameObject panelHistoria;       // ya no se usa: la historia es la intro
    [SerializeField] private GameObject panelInstrucciones;
    [SerializeField] private string escenaSeleccion = "Seleccion";   // "Elige a tu repartidor", antes de la intro
    [SerializeField] private string escenaIntro = "Intro";
    [SerializeField] private string escenaJuego = "SampleScene";

    private void Start()
    {
        Time.timeScale = 1f;
        if (campoNombre != null) campoNombre.text = Progreso.NombreJugador;
        CerrarPaneles();
        if (AudioManager.Instance != null) AudioManager.Instance.PonerMusica("musica_menu");
    }

    // Botón "Iniciar juego": guarda el nombre (vacío = "Repartidor" o "Repartidora" según la selección, Texto.Nombre)
    // y pasa a la selección de personaje; sin ella, a la intro o al nivel 1
    public void Jugar()
    {
        Progreso.NombreJugador = campoNombre != null ? campoNombre.text.Trim() : "";

        string destino = Application.CanStreamedLevelBeLoaded(escenaSeleccion) ? escenaSeleccion
                       : Application.CanStreamedLevelBeLoaded(escenaIntro) ? escenaIntro : escenaJuego;
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
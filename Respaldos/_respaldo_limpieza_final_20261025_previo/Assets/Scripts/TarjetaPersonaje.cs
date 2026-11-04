using UnityEngine;
using UnityEngine.EventSystems;

// Una de las dos tarjetas de "ELIGE A TU REPARTIDOR": el clic la elige en SeleccionPersonaje. No es un Button (las
// flechas las maneja SeleccionPersonaje y ConstruirBotones no la viste), así que el clic suena aquí.
public class TarjetaPersonaje : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private SeleccionPersonaje seleccion;
    [SerializeField] private int indice;   // 0 repartidor, 1 repartidora (Progreso.Genero)

    public int Indice => indice;

    public void OnPointerClick(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left || seleccion == null) return;
        AudioManager.Sonar("ui_clic");
        seleccion.Elegir(indice, false);
    }
}

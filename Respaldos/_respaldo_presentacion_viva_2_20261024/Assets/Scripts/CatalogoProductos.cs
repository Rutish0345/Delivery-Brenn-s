using UnityEngine;

// Lo que lleva cada pedido. El GameManager elige uno al asignar el pedido y lo usan el marcador del punto
// de entrega, la guía del HUD y la animación de la entrega.
public enum Producto { Esmalte, Labial, Tijeras, Crema, Tinte, Accesorio }

// Los íconos de 16x16 de cada producto, en el orden del enum (pixel_town/generate_marcador_pedido.py).
// Un solo asset (Assets/Sprits/Comun/Marcador/Productos.asset) para todos los niveles.
[CreateAssetMenu(menuName = "Delivery Brenn's/Catálogo de productos")]
public class CatalogoProductos : ScriptableObject
{
    [SerializeField] private Sprite[] iconos = new Sprite[6];

    public static int Cantidad => System.Enum.GetValues(typeof(Producto)).Length;

    public Sprite Icono(Producto p)
    {
        int i = (int)p;
        return iconos != null && i < iconos.Length ? iconos[i] : null;
    }

}

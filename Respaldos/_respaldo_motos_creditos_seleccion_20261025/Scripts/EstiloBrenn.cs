using TMPro;
using UnityEngine;

// El arte y los colores del estilo único de botones y paneles (BotonBrenn). Un solo asset en
// Assets/Resources/EstiloBrenn.asset, lo llena ConstruirBotones con el arte de pixel_town/generate_menu_letreros.py; en el
// juego se carga con EstiloBrenn.Cargar() (TiendaMotos arma su ventana al jugar).
[CreateAssetMenu(menuName = "Delivery Brenn's/Estilo de botones")]
public class EstiloBrenn : ScriptableObject
{
    public const string RutaResources = "EstiloBrenn";

    public Sprite normal, normalElegido;
    public Sprite principal, principalElegido;
    public Sprite peligro, peligroElegido;
    public Sprite icono, iconoElegido;
    public Sprite tarjeta, tarjetaElegida;
    public Sprite tijera;
    public Sprite panel;          // #2A1F40 con borde rosa de 3 px (pausa, instrucciones, nivel completado, game over...)
    public Sprite raya;           // la raya diagonal roja de apagado de los iconos de sonido
    public TMP_FontAsset fuenteTitulos;   // Press Start 2P
    public Color crema = new Color32(0xFF, 0xF1, 0xD0, 0xFF);
    public Color dorado = new Color32(0xFF, 0xD1, 0x66, 0xFF);
    public Color rosa = new Color32(0xFF, 0x6F, 0xA5, 0xFF);
    public Color oscuro = new Color32(0x2A, 0x1F, 0x40, 0xFF);

    static EstiloBrenn cargado;
    public static EstiloBrenn Cargar() => cargado != null ? cargado : cargado = Resources.Load<EstiloBrenn>(RutaResources);

    public (Sprite normal, Sprite elegido) Sprites(BotonBrenn.Variante v) => v switch
    {
        BotonBrenn.Variante.Principal => (principal, principalElegido),
        BotonBrenn.Variante.Peligro => (peligro, peligroElegido),
        BotonBrenn.Variante.Icono => (icono, iconoElegido),
        BotonBrenn.Variante.Tarjeta => (tarjeta, tarjetaElegida),
        _ => (normal, normalElegido),
    };
}

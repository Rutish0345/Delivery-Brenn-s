// Textos que cambian según quién reparte (Progreso.Genero) y el {nombre} escrito en el menú
public static class Texto
{
    // masc para el repartidor, fem para la repartidora (si fem viene vacío se usa masc)
    public static string PorGenero(string masc, string fem) =>
        Progreso.EsRepartidora && !string.IsNullOrEmpty(fem) ? fem : masc;

    // El nombre del menú; sin nombre, "Repartidor" o "Repartidora"
    public static string Nombre()
    {
        string n = Progreso.NombreJugador;
        return string.IsNullOrWhiteSpace(n) ? PorGenero("Repartidor", "Repartidora") : n.Trim();
    }

    // Cambia {nombre} por el nombre
    public static string ConNombre(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace("{nombre}", Nombre());
}

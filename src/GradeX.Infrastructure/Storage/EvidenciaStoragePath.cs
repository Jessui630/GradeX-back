namespace GradeX.Infrastructure.Storage;

/// <summary>
/// Resuelve la ubicación física de las evidencias. En Azure App Service el paquete
/// se monta como solo lectura (WEBSITE_RUN_FROM_PACKAGE=1), por lo que la ruta
/// predeterminada no puede vivir dentro de wwwroot.
/// </summary>
public static class EvidenciaStoragePath
{
    private const string RutaPredeterminada = "App_Data/evidencias";
    private const string RutaPersistenteAppService = "data/evidencias";

    public static string Resolver(
        string? rutaConfigurada,
        string contentRoot,
        bool esDesarrollo,
        string? home)
    {
        if (!string.IsNullOrWhiteSpace(rutaConfigurada))
        {
            return Path.IsPathRooted(rutaConfigurada)
                ? Path.GetFullPath(rutaConfigurada)
                : Path.GetFullPath(Path.Combine(contentRoot, rutaConfigurada));
        }

        if (!esDesarrollo &&
            !string.IsNullOrWhiteSpace(home) &&
            EstaDentroDe(contentRoot, home))
        {
            return Path.GetFullPath(Path.Combine(home, RutaPersistenteAppService));
        }

        return Path.GetFullPath(Path.Combine(contentRoot, RutaPredeterminada));
    }

    private static bool EstaDentroDe(string ruta, string baseRuta) =>
        RutaAlmacenamiento.EstaDentro(baseRuta, ruta);
}

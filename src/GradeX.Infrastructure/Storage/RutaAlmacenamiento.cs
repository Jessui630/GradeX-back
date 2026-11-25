namespace GradeX.Infrastructure.Storage;

/// <summary>
/// Compara rutas con la sensibilidad del sistema operativo. Windows usa un
/// sistema de archivos habitualmente insensible a mayúsculas; Linux, el entorno
/// de App Service, es sensible. Usar OrdinalIgnoreCase en Linux permitiría que
/// una ruta fuera del directorio raíz, pero con distinto uso de mayúsculas,
/// pasara la comprobación de prefijo.
/// </summary>
internal static class RutaAlmacenamiento
{
    internal static StringComparison Comparacion =>
        OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    internal static bool EstaDentro(string root, string destino) =>
        EstaDentro(root, destino, Comparacion);

    internal static bool EstaDentro(
        string root, string destino, StringComparison comparacion)
    {
        var baseCompleta = Path.GetFullPath(root);
        var destinoCompleto = Path.GetFullPath(destino);
        var prefijo = baseCompleta.EndsWith(Path.DirectorySeparatorChar)
            ? baseCompleta
            : baseCompleta + Path.DirectorySeparatorChar;
        return destinoCompleto.Equals(baseCompleta, comparacion) ||
            destinoCompleto.StartsWith(prefijo, comparacion);
    }
}

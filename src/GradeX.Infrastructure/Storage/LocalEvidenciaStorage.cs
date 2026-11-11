using GradeX.Application.Solicitudes;

namespace GradeX.Infrastructure.Storage;

public sealed class LocalEvidenciaStorage : IEvidenciaStorage
{
    private readonly string root;

    public LocalEvidenciaStorage(string root)
    {
        if (string.IsNullOrWhiteSpace(root))
            throw new ArgumentException("La ruta de evidencias es obligatoria.", nameof(root));
        this.root = Path.GetFullPath(root);
        Directory.CreateDirectory(this.root);
    }

    public async Task<string> GuardarAsync(
        Stream contenido, string extension, CancellationToken cancellationToken)
    {
        if (!EsExtensionPermitida(extension))
            throw new ArgumentException("Extensión de evidencia no permitida.", nameof(extension));

        var ahora = DateTime.UtcNow;
        var relativa = Path.Combine(
            ahora.ToString("yyyy", System.Globalization.CultureInfo.InvariantCulture),
            ahora.ToString("MM", System.Globalization.CultureInfo.InvariantCulture),
            $"{Guid.NewGuid():N}{extension}");
        var destino = Resolver(relativa);
        Directory.CreateDirectory(Path.GetDirectoryName(destino)!);

        await using var archivo = new FileStream(
            destino, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            bufferSize: 81920, FileOptions.Asynchronous);
        await contenido.CopyToAsync(archivo, cancellationToken);
        return relativa.Replace('\\', '/');
    }

    public Task<Stream?> AbrirAsync(string clave, CancellationToken cancellationToken)
    {
        var destino = Resolver(clave);
        if (!File.Exists(destino))
            return Task.FromResult<Stream?>(null);
        Stream stream = new FileStream(
            destino, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult<Stream?>(stream);
    }

    public Task EliminarAsync(string clave, CancellationToken cancellationToken)
    {
        var destino = Resolver(clave);
        if (File.Exists(destino))
            File.Delete(destino);
        return Task.CompletedTask;
    }

    private string Resolver(string clave)
    {
        var normalizada = clave.Replace('/', Path.DirectorySeparatorChar);
        var destino = Path.GetFullPath(Path.Combine(root, normalizada));
        var prefijo = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        if (!destino.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Ruta de evidencia fuera del almacenamiento permitido.");
        return destino;
    }

    private static bool EsExtensionPermitida(string extension) =>
        extension is ".jpg" or ".jpeg" or ".png" or ".webp";
}

using GradeX.Infrastructure.Storage;

namespace GradeX.Tests;

public sealed class RutaAlmacenamientoTests
{
    [Fact]
    public void LaRaizEsUnaRutaPermitida()
    {
        var root = Path.Combine(Path.GetTempPath(), "gradex-root-seguro");

        Assert.True(RutaAlmacenamiento.EstaDentro(root, root));
    }

    [Fact]
    public void UnArchivoDentroDeLaRaizEsPermitido()
    {
        var root = Path.Combine(Path.GetTempPath(), "gradex-root-seguro");
        var archivo = Path.Combine(root, "2026", "11", "foto.png");

        Assert.True(RutaAlmacenamiento.EstaDentro(
            root, archivo, StringComparison.Ordinal));
    }

    [Fact]
    public void PuntoPuntoFueraDeLaRaizNoEsPermitido()
    {
        var root = Path.Combine(Path.GetTempPath(), "gradex-root-seguro");
        var fuera = Path.Combine(root, "..", "otra-carpeta", "foto.png");

        Assert.False(RutaAlmacenamiento.EstaDentro(
            root, fuera, StringComparison.Ordinal));
    }

    [Fact]
    public void LaComparacionSensibleDistingueMayusculas()
    {
        var root = Path.Combine(Path.GetTempPath(), "gradex-root-seguro");
        var fuera = Path.Combine(
            Path.GetTempPath(), "GRADEX-ROOT-SEGURO", "foto.png");

        Assert.False(RutaAlmacenamiento.EstaDentro(
            root, fuera, StringComparison.Ordinal));
        Assert.True(RutaAlmacenamiento.EstaDentro(
            root, fuera, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EnLinuxElAlmacenamientoRechazaUnaRaizConMayusculasDistintas()
    {
        if (OperatingSystem.IsWindows())
            return;

        var root = Path.Combine(
            Path.GetTempPath(), $"gradex-case-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var storage = new LocalEvidenciaStorage(root);
            var clave = $"../{Path.GetFileName(root).ToUpperInvariant()}/foto.png";

            Assert.Throws<InvalidOperationException>(() =>
            {
                _ = storage.AbrirAsync(clave, CancellationToken.None);
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

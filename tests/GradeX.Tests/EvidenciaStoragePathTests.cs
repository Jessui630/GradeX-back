using GradeX.Infrastructure.Storage;

namespace GradeX.Tests;

public sealed class EvidenciaStoragePathTests
{
    [Fact]
    public void RutaConfiguradaRelativaSeResuelveContraContentRoot()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), "gradex-content");

        var ruta = EvidenciaStoragePath.Resolver(
            "evidencias", contentRoot, esDesarrollo: true, home: "/home");

        Assert.Equal(Path.GetFullPath(Path.Combine(contentRoot, "evidencias")), ruta);
    }

    [Fact]
    public void RutaConfiguradaAbsolutaSeRespeta()
    {
        var absoluta = Path.Combine(Path.GetTempPath(), "gradex-absoluta");

        var ruta = EvidenciaStoragePath.Resolver(
            absoluta, Path.GetTempPath(), esDesarrollo: false, home: "/home");

        Assert.Equal(Path.GetFullPath(absoluta), ruta);
    }

    [Fact]
    public void EnAppServiceUsaHomePersistenteFueraDeWwwroot()
    {
        var home = Path.Combine(Path.GetTempPath(), "gradex-home");
        var contentRoot = Path.Combine(home, "site", "wwwroot");

        var ruta = EvidenciaStoragePath.Resolver(
            null, contentRoot, esDesarrollo: false, home: home);

        Assert.Equal(Path.GetFullPath(Path.Combine(home, "data", "evidencias")), ruta);
        Assert.DoesNotContain("wwwroot", ruta, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnDesarrolloUsaAppDataJuntoAlContenido()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), "gradex-dev");
        var home = Path.Combine(Path.GetTempPath(), "gradex-home-dev");

        var ruta = EvidenciaStoragePath.Resolver(
            null, contentRoot, esDesarrollo: true, home: home);

        Assert.Equal(
            Path.GetFullPath(Path.Combine(contentRoot, "App_Data", "evidencias")), ruta);
    }

    [Fact]
    public void SinHomeNiConfiguracionUsaAppData()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), "gradex-sin-home");

        var ruta = EvidenciaStoragePath.Resolver(
            null, contentRoot, esDesarrollo: false, home: null);

        Assert.Equal(
            Path.GetFullPath(Path.Combine(contentRoot, "App_Data", "evidencias")), ruta);
    }
}

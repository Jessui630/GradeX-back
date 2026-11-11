using GradeX.Application.Cuentas;

namespace GradeX.Tests;

public sealed class IniciarSesionTests
{
    [Fact]
    public async Task CredencialesValidasDevuelvenUsuarioSinHash()
    {
        var store = new Store(new CredencialesUsuario(
            7, "Nombre", "correo@example.com", "hash", true, ["Solicitante"]));
        var hasher = new Hasher { Valida = true };
        var resultado = await new IniciarSesion(store, hasher)
            .EjecutarAsync(new(" CORREO@EXAMPLE.COM ", "Una frase de prueba larga"));

        Assert.Equal(EstadoInicioSesion.Exito, resultado.Estado);
        Assert.NotNull(resultado.Usuario);
        Assert.Equal(7, resultado.Usuario.IdUsuario);
        Assert.Equal("correo@example.com", store.UltimoCorreo);
        Assert.Equal("Una frase de prueba larga", hasher.UltimaContrasena);
        Assert.DoesNotContain("hash", resultado.Usuario.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ContrasenaIncorrectaEsGenerica()
    {
        var store = new Store(new CredencialesUsuario(
            7, "Nombre", "correo@example.com", "hash", true, ["Solicitante"]));
        var resultado = await new IniciarSesion(store, new Hasher { Valida = false })
            .EjecutarAsync(new("correo@example.com", "otra frase suficientemente larga"));

        Assert.Equal(EstadoInicioSesion.CredencialesInvalidas, resultado.Estado);
        Assert.Null(resultado.Usuario);
    }

    [Fact]
    public async Task CuentaInactivaEsRechazadaSinDistincion()
    {
        var store = new Store(new CredencialesUsuario(
            7, "Nombre", "correo@example.com", "hash", false, ["Solicitante"]));
        var resultado = await new IniciarSesion(store, new Hasher { Valida = true })
            .EjecutarAsync(new("correo@example.com", "Una frase de prueba larga"));

        Assert.Equal(EstadoInicioSesion.CredencialesInvalidas, resultado.Estado);
    }

    [Fact]
    public async Task CuentaSinRolesActivosEsRechazada()
    {
        var store = new Store(new CredencialesUsuario(
            7, "Nombre", "correo@example.com", "hash", true, []));
        var resultado = await new IniciarSesion(store, new Hasher { Valida = true })
            .EjecutarAsync(new("correo@example.com", "Una frase de prueba larga"));

        Assert.Equal(EstadoInicioSesion.CredencialesInvalidas, resultado.Estado);
    }

    [Fact]
    public async Task UsuarioInexistenteVerificaHashFicticioYNoEnumera()
    {
        var store = new Store(null);
        var hasher = new Hasher { Valida = false };
        var resultado = await new IniciarSesion(store, hasher)
            .EjecutarAsync(new("no-existe@example.com", "Una frase de prueba larga"));

        Assert.Equal(EstadoInicioSesion.CredencialesInvalidas, resultado.Estado);
        Assert.Null(hasher.UltimoHash);
        Assert.Equal("Una frase de prueba larga", hasher.UltimaContrasena);
    }

    [Fact]
    public async Task NormalizaNfcEnLaContrasena()
    {
        var store = new Store(new CredencialesUsuario(
            7, "Nombre", "correo@example.com", "hash", true, ["Solicitante"]));
        var hasher = new Hasher { Valida = true };
        await new IniciarSesion(store, hasher)
            .EjecutarAsync(new("correo@example.com", "  frase e\u0301 de prueba larga  "));

        Assert.Equal("  frase é de prueba larga  ", hasher.UltimaContrasena);
    }

    [Fact]
    public async Task CancelacionSePropaga()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var store = new Store(null);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new IniciarSesion(store, new Hasher()).EjecutarAsync(
                new("correo@example.com", "Una frase de prueba larga"), cts.Token));
    }

    private sealed class Store(CredencialesUsuario? usuario) : ICredencialesStore
    {
        public string? UltimoCorreo { get; private set; }

        public Task<CredencialesUsuario?> BuscarPorCorreoAsync(string correo, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            UltimoCorreo = correo;
            return Task.FromResult(usuario);
        }
    }

    private sealed class Hasher : IContrasenaHasher
    {
        public bool Valida { get; init; }
        public string? UltimoHash { get; private set; }
        public string? UltimaContrasena { get; private set; }

        public string Hash(string contrasena) => "hash";

        public bool Verificar(string? passwordHash, string contrasena)
        {
            UltimoHash = passwordHash;
            UltimaContrasena = contrasena;
            return Valida;
        }
    }
}

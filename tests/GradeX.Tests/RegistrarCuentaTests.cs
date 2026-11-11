using GradeX.Application.Cuentas;

namespace GradeX.Tests;

public class RegistrarCuentaTests
{
    [Fact]
    public async Task RegistroValidoNormalizaYEntregaSoloHash()
    {
        var store = new Store();
        var caso = new RegistrarCuenta(store, new Hasher(), new Lista());
        var resultado = await caso.EjecutarAsync(new(" Nombre ", " USUARIO@EXAMPLE.COM ", "Una frase de prueba larga"));
        Assert.Equal(EstadoRegistroCuenta.Exito, resultado.Estado);
        Assert.Equal("Nombre", store.Nombre);
        Assert.Equal("usuario@example.com", store.Correo);
        Assert.Equal("hash-de-prueba", store.Hash);
        Assert.Equal(17, resultado.IdUsuario);
    }

    [Theory]
    [InlineData(null, "usuario@example.com", "Una frase de prueba larga", EstadoRegistroCuenta.NombreInvalido)]
    [InlineData("Nombre", null, "Una frase de prueba larga", EstadoRegistroCuenta.CorreoInvalido)]
    [InlineData("Nombre", "invalido", "Una frase de prueba larga", EstadoRegistroCuenta.CorreoInvalido)]
    [InlineData("Nombre", "usuario@example.com", "corta", EstadoRegistroCuenta.ContrasenaInvalida)]
    public async Task RechazaEntradasInvalidas(string? nombre, string? correo, string? contrasena, EstadoRegistroCuenta esperado)
    {
        var store = new Store();
        var resultado = await new RegistrarCuenta(store, new Hasher(), new Lista()).EjecutarAsync(new(nombre, correo, contrasena));
        Assert.Equal(esperado, resultado.Estado);
        Assert.Null(store.Hash);
    }

    [Fact]
    public async Task RechazaContrasenaComprometida()
    {
        var store = new Store();
        var resultado = await new RegistrarCuenta(store, new Hasher(), new Lista(true))
            .EjecutarAsync(new("Nombre", "usuario@example.com", "Una frase de prueba larga"));
        Assert.Equal(EstadoRegistroCuenta.ContrasenaInvalida, resultado.Estado);
        Assert.Null(store.Hash);
        Assert.Equal(0, store.Consultas);
    }

    [Fact]
    public async Task IndisponibilidadNoConsultaNiPersiste()
    {
        var store = new Store { Fallar = true };
        var resultado = await new RegistrarCuenta(store, new Hasher(), new NoDisponible())
            .EjecutarAsync(new("Nombre", "usuario@example.com", "Una frase de prueba larga"));
        Assert.Equal(EstadoRegistroCuenta.ComprobacionContrasenaNoDisponible, resultado.Estado);
        Assert.Null(store.Hash);
        Assert.Equal(0, store.Consultas);
    }

    [Fact]
    public async Task NormalizaNfcSinEliminarEspaciosDeContrasena()
    {
        var lista = new ListaCaptura();
        var hasher = new HasherCaptura();
        var caso = new RegistrarCuenta(new Store(), hasher, lista);
        await caso.EjecutarAsync(new("Nombre", "usuario@example.com", "  frase larga e\u0301 de prueba  "));
        Assert.Equal("  frase larga é de prueba  ", lista.Valor);
        Assert.Equal(lista.Valor, hasher.Valor);
    }

    [Fact]
    public async Task DetectaCorreoExistenteSinGuardar()
    {
        var store = new Store { Existe = true };
        var resultado = await new RegistrarCuenta(store, new Hasher(), new Lista())
            .EjecutarAsync(new("Nombre", "usuario@example.com", "Una frase de prueba larga"));
        Assert.Equal(EstadoRegistroCuenta.CorreoDuplicado, resultado.Estado);
        Assert.Null(store.Hash);
    }

    [Theory]
    [InlineData(EstadoRegistroCuenta.RolNoDisponible)]
    [InlineData(EstadoRegistroCuenta.CorreoDuplicado)]
    public async Task PropagaResultadosControlados(EstadoRegistroCuenta estado)
    {
        var store = new Store { Estado = estado };
        var resultado = await new RegistrarCuenta(store, new Hasher(), new Lista())
            .EjecutarAsync(new("Nombre", "usuario@example.com", "Una frase de prueba larga"));
        Assert.Equal(estado, resultado.Estado);
    }

    [Fact]
    public async Task PropagaFalloDePersistencia()
    {
        var store = new Store { Fallar = true };
        var caso = new RegistrarCuenta(store, new Hasher(), new Lista());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            caso.EjecutarAsync(new("Nombre", "usuario@example.com", "Una frase de prueba larga")));
    }

    private sealed class Hasher : IContrasenaHasher
    {
        public string Hash(string contrasena) => "hash-de-prueba";
        public bool Verificar(string? passwordHash, string contrasena) => false;
    }

    private sealed class HasherCaptura : IContrasenaHasher
    {
        public string? Valor { get; private set; }
        public string Hash(string contrasena)
        {
            Valor = contrasena;
            return "hash-de-prueba";
        }

        public bool Verificar(string? passwordHash, string contrasena) => false;
    }

    private sealed class ListaCaptura : IContrasenaComprometida
    {
        public string? Valor { get; private set; }
        public ValueTask<EstadoContrasenaComprometida> EstaComprometidaAsync(string contrasenaNormalizada, CancellationToken cancellationToken)
        {
            Valor = contrasenaNormalizada;
            return ValueTask.FromResult(EstadoContrasenaComprometida.NoEncontrada);
        }
    }

    private sealed class Lista(bool comprometida = false) : IContrasenaComprometida
    {
        public ValueTask<EstadoContrasenaComprometida> EstaComprometidaAsync(string contrasenaNormalizada, CancellationToken cancellationToken) =>
            ValueTask.FromResult(comprometida ? EstadoContrasenaComprometida.Comprometida : EstadoContrasenaComprometida.NoEncontrada);
    }

    private sealed class NoDisponible : IContrasenaComprometida
    {
        public ValueTask<EstadoContrasenaComprometida> EstaComprometidaAsync(string contrasenaNormalizada, CancellationToken cancellationToken) =>
            ValueTask.FromResult(EstadoContrasenaComprometida.NoDisponible);
    }

    private sealed class Store : IRegistroCuentaStore
    {
        public bool Existe { get; init; }
        public bool Fallar { get; init; }
        public EstadoRegistroCuenta Estado { get; init; } = EstadoRegistroCuenta.Exito;
        public string? Nombre { get; private set; }
        public string? Correo { get; private set; }
        public string? Hash { get; private set; }
        public int Consultas { get; private set; }

        public Task<bool> ExisteCorreoAsync(string correo, CancellationToken cancellationToken)
        {
            Consultas++;
            return Task.FromResult(Existe);
        }

        public Task<ResultadoRegistroCuenta> RegistrarAsync(string nombreCompleto, string correo, string passwordHash, CancellationToken cancellationToken)
        {
            if (Fallar) throw new InvalidOperationException();
            Nombre = nombreCompleto;
            Correo = correo;
            Hash = passwordHash;
            return Task.FromResult(new ResultadoRegistroCuenta(Estado, Estado == EstadoRegistroCuenta.Exito ? 17 : null));
        }
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GradeX.Application.Cuentas;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GradeX.Tests;

public sealed class CuentasApiTests
{
    private const string ContrasenaValida = "Una frase de prueba larga";

    [Fact]
    public async Task RegistroValidoDevuelve201SinDatosSensibles()
    {
        var store = new RegistroStore();
        using var factory = CrearFactory(store);
        using var client = factory.CreateClient(Cliente());

        var response = await PostCuentaAsync(client, new
        {
            nombreCompleto = "Nombre de prueba",
            correo = "Usuario@Example.com ",
            contrasena = ContrasenaValida
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(42, json.RootElement.GetProperty("idUsuario").GetInt32());
        Assert.Equal("usuario@example.com", store.Correo);
        Assert.Equal("hash-de-prueba", store.Hash);
        Assert.DoesNotContain(ContrasenaValida, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task NombreInvalidoDevuelve400ConErrorDeCampo()
    {
        using var factory = CrearFactory(new RegistroStore());
        using var client = factory.CreateClient(Cliente());

        var response = await PostCuentaAsync(client, new
        {
            nombreCompleto = " ",
            correo = "usuario@example.com",
            contrasena = ContrasenaValida
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("errors").TryGetProperty("nombreCompleto", out _));
    }

    [Fact]
    public async Task CorreoDuplicadoDevuelve409()
    {
        using var factory = CrearFactory(new RegistroStore { Existe = true });
        using var client = factory.CreateClient(Cliente());

        var response = await PostCuentaAsync(client, new
        {
            nombreCompleto = "Nombre de prueba",
            correo = "usuario@example.com",
            contrasena = ContrasenaValida
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("errors").TryGetProperty("correo", out _));
    }

    [Fact]
    public async Task IndisponibilidadDelProveedorDevuelve503()
    {
        using var factory = CrearFactory(new RegistroStore(), new Comprometida(EstadoContrasenaComprometida.NoDisponible));
        using var client = factory.CreateClient(Cliente());

        var response = await PostCuentaAsync(client, new
        {
            nombreCompleto = "Nombre de prueba",
            correo = "usuario@example.com",
            contrasena = ContrasenaValida
        });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task RolNoDisponibleDevuelve503()
    {
        using var factory = CrearFactory(new RegistroStore { Estado = EstadoRegistroCuenta.RolNoDisponible });
        using var client = factory.CreateClient(Cliente());

        var response = await PostCuentaAsync(client, new
        {
            nombreCompleto = "Nombre de prueba",
            correo = "usuario@example.com",
            contrasena = ContrasenaValida
        });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task PropiedadDeRolAdicionalEsRechazada()
    {
        var store = new RegistroStore();
        using var factory = CrearFactory(store);
        using var client = factory.CreateClient(Cliente());

        using var content = new StringContent(
            $$"""{"nombreCompleto":"Nombre","correo":"usuario@example.com","contrasena":"{{ContrasenaValida}}","rol":"Administrador"}""",
            Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/cuentas", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(store.Correo);
    }

    [Fact]
    public async Task ContenidoNoJsonDevuelve415()
    {
        using var factory = CrearFactory(new RegistroStore());
        using var client = factory.CreateClient(Cliente());
        using var content = new StringContent("texto", Encoding.UTF8, "text/plain");

        var response = await client.PostAsync("/api/cuentas", content);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task LimiteDeSolicitudesDevuelve429()
    {
        using var factory = CrearFactory(new RegistroStore(), limiteRegistro: 1);
        using var client = factory.CreateClient(Cliente());
        var datos = new
        {
            nombreCompleto = "Nombre de prueba",
            correo = "usuario@example.com",
            contrasena = ContrasenaValida
        };

        Assert.Equal(HttpStatusCode.Created, (await PostCuentaAsync(client, datos)).StatusCode);
        var segunda = await PostCuentaAsync(client, datos);

        Assert.Equal(HttpStatusCode.TooManyRequests, segunda.StatusCode);
    }

    private static Task<HttpResponseMessage> PostCuentaAsync(HttpClient client, object datos) =>
        client.PostAsJsonAsync("/api/cuentas", datos);

    private static WebApplicationFactory<Program> CrearFactory(
        RegistroStore store,
        IContrasenaComprometida? comprometida = null,
        int limiteRegistro = 100)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:GradeX",
                "Server=gradex-test.invalid;Database=GradeX_Test;Integrated Security=true;Connect Timeout=1");
            builder.UseSetting("RateLimiting:Registro:PermitLimit", limiteRegistro.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.UseSetting("RateLimiting:Global:PermitLimit", "1000");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IRegistroCuentaStore>();
                services.AddSingleton<IRegistroCuentaStore>(store);
                services.RemoveAll<IContrasenaHasher>();
                services.AddSingleton<IContrasenaHasher, HasherPrueba>();
                services.RemoveAll<IContrasenaComprometida>();
                services.AddSingleton<IContrasenaComprometida>(
                    comprometida ?? new Comprometida(EstadoContrasenaComprometida.NoEncontrada));
            });
        });
    }

    private static WebApplicationFactoryClientOptions Cliente() => new()
    {
        BaseAddress = new Uri("https://localhost"),
        HandleCookies = true
    };

    private sealed class HasherPrueba : IContrasenaHasher
    {
        public string Hash(string contrasena) => "hash-de-prueba";
        public bool Verificar(string? passwordHash, string contrasena) => false;
    }

    private sealed class Comprometida(EstadoContrasenaComprometida estado) : IContrasenaComprometida
    {
        public ValueTask<EstadoContrasenaComprometida> EstaComprometidaAsync(
            string contrasenaNormalizada, CancellationToken cancellationToken) =>
            ValueTask.FromResult(estado);
    }

    private sealed class RegistroStore : IRegistroCuentaStore
    {
        public bool Existe { get; init; }
        public EstadoRegistroCuenta Estado { get; init; } = EstadoRegistroCuenta.Exito;
        public string? Correo { get; private set; }
        public string? Hash { get; private set; }

        public Task<bool> ExisteCorreoAsync(string correo, CancellationToken cancellationToken) =>
            Task.FromResult(Existe);

        public Task<ResultadoRegistroCuenta> RegistrarAsync(
            string nombreCompleto, string correo, string passwordHash, CancellationToken cancellationToken)
        {
            Correo = correo;
            Hash = passwordHash;
            return Task.FromResult(new ResultadoRegistroCuenta(
                Estado, Estado == EstadoRegistroCuenta.Exito ? 42 : null));
        }
    }
}

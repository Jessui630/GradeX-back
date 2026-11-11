using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GradeX.Application.Cuentas;
using GradeX.Application.Sesiones;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GradeX.Tests;

public sealed class SesionesApiTests
{
    private const string Correo = "correo@example.com";
    private const string Contrasena = "Una frase de prueba larga";

    [Fact]
    public async Task CatalogosYSolicitudesSinSesionDevuelven401()
    {
        using var factory = CrearFactory(Usuario());
        using var client = factory.CreateClient(Cliente(handleCookies: true));

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/catalogo/categorias")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/solicitudes")).StatusCode);
    }

    [Fact]
    public async Task LoginActualYLogoutFuncionanConCookieYCsrf()
    {
        using var factory = CrearFactory(Usuario());
        using var client = factory.CreateClient(Cliente(handleCookies: true));

        var (csrf, _) = await ObtenerCsrfAsync(client);
        var login = await LoginAsync(client, csrf, Correo, Contrasena);

        Assert.True(login.StatusCode == HttpStatusCode.OK, await login.Content.ReadAsStringAsync());
        Assert.Contains("no-store", login.Headers.CacheControl?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        var cookies = login.Headers.GetValues("Set-Cookie").ToArray();
        Assert.Contains(cookies, cookie => cookie.Contains("GradeX.Session=", StringComparison.Ordinal) &&
            cookie.Contains("httponly", StringComparison.OrdinalIgnoreCase) &&
            cookie.Contains("samesite=strict", StringComparison.OrdinalIgnoreCase));

        var actual = await client.GetAsync("/api/sesiones/actual");
        Assert.Equal(HttpStatusCode.OK, actual.StatusCode);
        using var json = JsonDocument.Parse(await actual.Content.ReadAsStringAsync());
        Assert.Equal(7, json.RootElement.GetProperty("idUsuario").GetInt32());
        Assert.Equal("Solicitante", json.RootElement.GetProperty("roles")[0].GetString());

        var (csrfLogout, _) = await ObtenerCsrfAsync(client);
        using var logout = new HttpRequestMessage(HttpMethod.Delete, "/api/sesiones/actual");
        logout.Headers.Add("X-CSRF-Token", csrfLogout);
        var cierre = await client.SendAsync(logout);
        Assert.Equal(HttpStatusCode.NoContent, cierre.StatusCode);

        var despues = await client.GetAsync("/api/sesiones/actual");
        Assert.Equal(HttpStatusCode.Unauthorized, despues.StatusCode);
    }

    [Fact]
    public async Task SesionEliminadaNoPuedeReutilizarCookieAnterior()
    {
        using var factory = CrearFactory(Usuario());
        using var client = factory.CreateClient(Cliente(handleCookies: true));
        var (csrf, _) = await ObtenerCsrfAsync(client);

        var login = await LoginAsync(client, csrf, Correo, Contrasena);
        Assert.True(login.StatusCode == HttpStatusCode.OK, await login.Content.ReadAsStringAsync());
        var sessionCookie = ExtraerCookie(login, "GradeX.Session");

        var (csrfLogout, _) = await ObtenerCsrfAsync(client);
        using var logout = new HttpRequestMessage(HttpMethod.Delete, "/api/sesiones/actual");
        logout.Headers.Add("X-CSRF-Token", csrfLogout);
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(logout)).StatusCode);

        using var reutilizado = factory.CreateClient(Cliente(handleCookies: false));
        using var despues = new HttpRequestMessage(HttpMethod.Get, "/api/sesiones/actual");
        despues.Headers.Add("Cookie", $"GradeX.Session={sessionCookie}");
        Assert.Equal(HttpStatusCode.Unauthorized, (await reutilizado.SendAsync(despues)).StatusCode);
    }

    [Fact]
    public async Task CredencialesInvalidasYCuentaInactivaRespondenIgual()
    {
        using var factory = CrearFactory(Usuario());
        using var client = factory.CreateClient(Cliente(handleCookies: true));
        var (csrf, _) = await ObtenerCsrfAsync(client);

        var incorrecta = await LoginAsync(client, csrf, Correo, "contrasena incorrecta");
        using var json = JsonDocument.Parse(await incorrecta.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, incorrecta.StatusCode);
        Assert.DoesNotContain("inactiva", json.RootElement.GetProperty("detail").GetString()!, StringComparison.OrdinalIgnoreCase);

        using var inactivaFactory = CrearFactory(Usuario() with { Activo = false });
        using var inactivaClient = inactivaFactory.CreateClient(Cliente(handleCookies: true));
        var (csrfInactiva, _) = await ObtenerCsrfAsync(inactivaClient);
        var inactiva = await LoginAsync(inactivaClient, csrfInactiva, Correo, Contrasena);

        Assert.Equal(HttpStatusCode.Unauthorized, inactiva.StatusCode);
        Assert.Equal(incorrecta.StatusCode, inactiva.StatusCode);
    }

    [Fact]
    public async Task LoginSinCsrfEsRechazado()
    {
        using var factory = CrearFactory(Usuario());
        using var client = factory.CreateClient(Cliente(handleCookies: true));

        var response = await client.PostAsJsonAsync("/api/sesiones", new { correo = Correo, contrasena = Contrasena });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AccesoAnonimoDevuelveProblemDetails()
    {
        using var factory = CrearFactory(Usuario());
        using var client = factory.CreateClient(Cliente(handleCookies: true));

        var response = await client.GetAsync("/api/sesiones/actual");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task PoliticaDeRolProtegeEndpointAdministrativo()
    {
        using var solicitanteFactory = CrearFactory(Usuario(), addTestController: true);
        using var solicitanteClient = solicitanteFactory.CreateClient(Cliente(handleCookies: true));
        var (csrfSolicitante, _) = await ObtenerCsrfAsync(solicitanteClient);
        Assert.Equal(HttpStatusCode.OK,
            (await LoginAsync(solicitanteClient, csrfSolicitante, Correo, Contrasena)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await solicitanteClient.GetAsync("/api/test/admin")).StatusCode);

        using var adminFactory = CrearFactory(Usuario() with { Roles = ["Administrador"] }, addTestController: true);
        using var adminClient = adminFactory.CreateClient(Cliente(handleCookies: true));
        var (csrfAdmin, _) = await ObtenerCsrfAsync(adminClient);
        Assert.Equal(HttpStatusCode.OK,
            (await LoginAsync(adminClient, csrfAdmin, Correo, Contrasena)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await adminClient.GetAsync("/api/test/admin")).StatusCode);
    }

    [Fact]
    public async Task LimiteDeLoginDevuelve429()
    {
        using var factory = CrearFactory(Usuario(), limiteLogin: 1);
        using var client = factory.CreateClient(Cliente(handleCookies: true));
        var (csrf, _) = await ObtenerCsrfAsync(client);

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await LoginAsync(client, csrf, Correo, "contrasena incorrecta")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await LoginAsync(client, csrf, Correo, "contrasena incorrecta")).StatusCode);
    }

    [Fact]
    public async Task CorsPermiteOrigenConfiguradoConCredenciales()
    {
        using var factory = CrearFactory(Usuario());
        using var client = factory.CreateClient(Cliente(handleCookies: false));
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/sesiones");
        request.Headers.Add("Origin", "http://localhost:5173");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type,x-csrf-token");
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("http://localhost:5173",
            Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Equal("true",
            Assert.Single(response.Headers.GetValues("Access-Control-Allow-Credentials")));
    }

    [Fact]
    public async Task CorsRechazaOrigenNoConfigurado()
    {
        using var factory = CrearFactory(Usuario());
        using var client = factory.CreateClient(Cliente(handleCookies: false));
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/sesiones");
        request.Headers.Add("Origin", "https://origen-no-permitido.example");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        using var response = await client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private static CredencialesUsuario Usuario() =>
        new(7, "Nombre de prueba", Correo, "hash", true, ["Solicitante"]);

    private static WebApplicationFactory<Program> CrearFactory(
        CredencialesUsuario usuario,
        int limiteLogin = 100,
        bool addTestController = false)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:GradeX",
                "Server=gradex-test.invalid;Database=GradeX_Test;Integrated Security=true;Connect Timeout=1");
            builder.UseSetting("RateLimiting:Login:PermitLimit", limiteLogin.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.UseSetting("RateLimiting:Global:PermitLimit", "1000");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ICredencialesStore>();
                services.AddSingleton<ICredencialesStore>(new CredencialesStore(usuario));
                services.RemoveAll<IContrasenaHasher>();
                services.AddSingleton<IContrasenaHasher, HasherPrueba>();
                services.RemoveAll<ISesionStore>();
                services.AddSingleton<ISesionStore, InMemorySesionStore>();
                if (addTestController)
                    services.AddControllers().AddApplicationPart(typeof(TestAdminController).Assembly);
            });
        });
    }

    private static WebApplicationFactoryClientOptions Cliente(bool handleCookies) => new()
    {
        BaseAddress = new Uri("https://localhost"),
        HandleCookies = handleCookies
    };

    private static async Task<(string Token, string? Cookie)> ObtenerCsrfAsync(
        HttpClient client)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/sesiones/csrf");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (json.RootElement.GetProperty("token").GetString()!,
            ExtraerCookieOpcional(response, "GradeX.Csrf"));
    }

    private static async Task<HttpResponseMessage> LoginAsync(
        HttpClient client, string csrf, string correo, string contrasena)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/sesiones")
        {
            Content = JsonContent.Create(new { correo, contrasena })
        };
        request.Headers.Add("X-CSRF-Token", csrf);
        return await client.SendAsync(request);
    }

    private static string ExtraerCookie(HttpResponseMessage response, string nombre)
    {
        var cookie = ExtraerCookieOpcional(response, nombre);
        Assert.False(string.IsNullOrEmpty(cookie), $"No se recibió la cookie {nombre}.");
        return cookie;
    }

    private static string? ExtraerCookieOpcional(HttpResponseMessage response, string nombre)
    {
        var valores = response.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies : [];
        var prefijo = nombre + "=";
        var cookie = valores
            .SelectMany(valor => valor.Split('\n'))
            .FirstOrDefault(valor => valor.TrimStart().StartsWith(prefijo, StringComparison.OrdinalIgnoreCase));
        return cookie?[..cookie.IndexOf(';', StringComparison.Ordinal)];
    }

    private sealed class HasherPrueba : IContrasenaHasher
    {
        public string Hash(string contrasena) => "hash";
        public bool Verificar(string? passwordHash, string contrasena) =>
            passwordHash == "hash" && contrasena == Contrasena;
    }

    private sealed class CredencialesStore(CredencialesUsuario usuario) : ICredencialesStore
    {
        public Task<CredencialesUsuario?> BuscarPorCorreoAsync(string correo, CancellationToken cancellationToken) =>
            Task.FromResult<CredencialesUsuario?>(usuario);
    }
}

[ApiController]
[Route("api/test/admin")]
public sealed class TestAdminController : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "Administrador")]
    public IActionResult Get() => Ok(new { ok = true });
}

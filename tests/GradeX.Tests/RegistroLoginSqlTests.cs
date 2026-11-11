using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GradeX.Application.Cuentas;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GradeX.Tests;

public sealed class RegistroLoginSqlTests
{
    [LocalDbFact]
    public async Task RegistroLoginYRecuperacionDeSesionFuncionanConSqlReal()
    {
        var correo = $"registro-prod-{Guid.NewGuid():N}@example.test";
        const string contrasena = "Frase segura de prueba 2026";
        using var factory = CrearFactory();
        using var client = factory.CreateClient(Cliente());

        try
        {
            var registro = await client.PostAsJsonAsync("/api/cuentas", new
            {
                nombreCompleto = "Cuenta de prueba HU-001",
                correo,
                contrasena
            });
            Assert.True(registro.StatusCode == HttpStatusCode.Created,
                await registro.Content.ReadAsStringAsync());
            using var registroJson = JsonDocument.Parse(await registro.Content.ReadAsStringAsync());
            Assert.True(registroJson.RootElement.GetProperty("idUsuario").GetInt32() > 0);

            var duplicado = await client.PostAsJsonAsync("/api/cuentas", new
            {
                nombreCompleto = "Cuenta de prueba HU-001",
                correo,
                contrasena
            });
            Assert.Equal(HttpStatusCode.Conflict, duplicado.StatusCode);

            var csrfResponse = await client.GetAsync("/api/sesiones/csrf");
            Assert.Equal(HttpStatusCode.OK, csrfResponse.StatusCode);
            using var csrfJson = JsonDocument.Parse(await csrfResponse.Content.ReadAsStringAsync());
            var csrf = csrfJson.RootElement.GetProperty("token").GetString();
            Assert.False(string.IsNullOrWhiteSpace(csrf));

            using var login = new HttpRequestMessage(HttpMethod.Post, "/api/sesiones")
            {
                Content = JsonContent.Create(new { correo, contrasena })
            };
            login.Headers.Add("X-CSRF-Token", csrf);
            var loginResponse = await client.SendAsync(login);
            Assert.True(loginResponse.StatusCode == HttpStatusCode.OK,
                await loginResponse.Content.ReadAsStringAsync());
            var cookies = loginResponse.Headers.TryGetValues("Set-Cookie", out var valores)
                ? valores.ToArray()
                : [];
            Assert.Contains(cookies, cookie =>
                cookie.Contains("GradeX.Session=", StringComparison.Ordinal) &&
                cookie.Contains("httponly", StringComparison.OrdinalIgnoreCase));

            var actual = await client.GetAsync("/api/sesiones/actual");
            Assert.True(actual.StatusCode == HttpStatusCode.OK,
                await actual.Content.ReadAsStringAsync());
            using var actualJson = JsonDocument.Parse(await actual.Content.ReadAsStringAsync());
            Assert.Equal(correo, actualJson.RootElement.GetProperty("correo").GetString());
            Assert.Contains("Solicitante", actualJson.RootElement.GetProperty("roles")
                .EnumerateArray().Select(rol => rol.GetString()));

            await using var db = await SqlIntegration.AbrirContextoAsync();
            var usuario = await db.Usuarios.AsNoTracking()
                .Where(u => u.Correo == correo)
                .Select(u => new
                {
                    u.IdUsuario,
                    u.Activo,
                    u.PasswordHash,
                    Roles = u.UsuarioRols.Select(ur => ur.IdRolNavigation.Nombre).ToList()
                })
                .SingleAsync();
            Assert.True(usuario.Activo);
            Assert.NotEqual(contrasena, usuario.PasswordHash);
            Assert.Contains("Solicitante", usuario.Roles);
        }
        finally
        {
            await LimpiarAsync(correo);
        }
    }

    private static WebApplicationFactory<Program> CrearFactory()
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting(
                "ConnectionStrings:GradeX",
                Environment.GetEnvironmentVariable("GRADEX_INTEGRATION_TEST_CONNECTION")!);
            builder.UseSetting("RateLimiting:Registro:PermitLimit", "1000");
            builder.UseSetting("RateLimiting:Login:PermitLimit", "1000");
            builder.UseSetting("RateLimiting:Global:PermitLimit", "1000");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IContrasenaComprometida>();
                services.AddSingleton<IContrasenaComprometida, ContrasenaNoComprometida>();
            });
        });
    }

    private static WebApplicationFactoryClientOptions Cliente() => new()
    {
        BaseAddress = new Uri("https://localhost"),
        HandleCookies = true
    };

    private static async Task LimpiarAsync(string correo)
    {
        await using var db = await SqlIntegration.AbrirContextoAsync();
        await db.SesionUsuarios
            .Where(s => s.IdUsuarioNavigation.Correo == correo)
            .ExecuteDeleteAsync();
        await db.UsuarioRols
            .Where(ur => ur.IdUsuarioNavigation.Correo == correo)
            .ExecuteDeleteAsync();
        await db.Usuarios.Where(u => u.Correo == correo).ExecuteDeleteAsync();
    }

    private sealed class ContrasenaNoComprometida : IContrasenaComprometida
    {
        public ValueTask<EstadoContrasenaComprometida> EstaComprometidaAsync(
            string contrasenaNormalizada, CancellationToken cancellationToken) =>
            ValueTask.FromResult(EstadoContrasenaComprometida.NoEncontrada);
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GradeX.Application.Cuentas;
using GradeX.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GradeX.Tests;

public sealed class Sprint1FlowSqlTests
{
    private const string Contrasena = "Frase segura de prueba 2026";

    [LocalDbFact]
    public async Task FlujoCompletoSolicitanteFuncionaPorHttpYSql()
    {
        var correo = $"flujo-{Guid.NewGuid():N}@example.test";
        var otroCorreo = $"flujo-otro-{Guid.NewGuid():N}@example.test";
        var almacenamiento = Path.Combine(
            Path.GetTempPath(), "gradex-flujo-" + Guid.NewGuid().ToString("N"));
        using var factory = CrearFactory(almacenamiento);
        using var cliente = factory.CreateClient(Cliente());
        using var otroCliente = factory.CreateClient(Cliente());

        try
        {
            Assert.Equal(HttpStatusCode.Created, (await RegistrarAsync(cliente, correo)).StatusCode);
            Assert.Equal(HttpStatusCode.Created, (await RegistrarAsync(otroCliente, otroCorreo)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await LoginAsync(cliente, correo)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await LoginAsync(otroCliente, otroCorreo)).StatusCode);

            var sesion = await cliente.GetAsync("/api/sesiones/actual");
            Assert.Equal(HttpStatusCode.OK, sesion.StatusCode);

            var vacio = await cliente.GetAsync("/api/solicitudes?pagina=1&tamanoPagina=3");
            Assert.True(vacio.StatusCode == HttpStatusCode.OK,
                await vacio.Content.ReadAsStringAsync());
            using var vacioJson = JsonDocument.Parse(await vacio.Content.ReadAsStringAsync());
            Assert.Equal(0, vacioJson.RootElement.GetProperty("total").GetInt32());
            Assert.Equal(0, vacioJson.RootElement.GetProperty("elementos").GetArrayLength());

            var categorias = await cliente.GetAsync("/api/catalogo/categorias");
            Assert.Equal(HttpStatusCode.OK, categorias.StatusCode);
            using var categoriasJson = JsonDocument.Parse(await categorias.Content.ReadAsStringAsync());
            var categoria = categoriasJson.RootElement.EnumerateArray()
                .First(c => c.GetProperty("nombre").GetString() == "Sneakers");
            var idCategoria = categoria.GetProperty("idCategoria").GetInt32();

            var requisitos = await cliente.GetAsync(
                $"/api/catalogo/categorias/{idCategoria}/requisitos");
            Assert.Equal(HttpStatusCode.OK, requisitos.StatusCode);
            using var requisitosJson = JsonDocument.Parse(await requisitos.Content.ReadAsStringAsync());
            var tipoEvidencia = requisitosJson.RootElement.GetProperty("evidencias")
                .EnumerateArray()
                .First(e => e.GetProperty("obligatorio").GetBoolean())
                .GetProperty("idTipoEvidencia").GetInt32();

            var csrf = await ObtenerCsrfAsync(cliente);
            using var crear = new HttpRequestMessage(HttpMethod.Post, "/api/solicitudes")
            {
                Content = JsonContent.Create(new
                {
                    idCategoria,
                    idMarca = (int?)null,
                    nombre = "Producto de flujo completo",
                    modelo = "Modelo E2E",
                    sku = "SKU-E2E",
                    numeroSerie = "SERIE-E2E",
                    imei = (string?)null,
                    accesorios = "Caja",
                    descripcion = "Producto para probar el recorrido del solicitante.",
                    observaciones = "Prueba de integración."
                })
            };
            crear.Headers.Add("X-CSRF-Token", csrf);
            crear.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            var creada = await cliente.SendAsync(crear);
            Assert.True(creada.StatusCode == HttpStatusCode.Created,
                await creada.Content.ReadAsStringAsync());
            using var creadaJson = JsonDocument.Parse(await creada.Content.ReadAsStringAsync());
            var idSolicitud = creadaJson.RootElement.GetProperty("idSolicitud").GetInt32();

            var evidencia = await SubirEvidenciaAsync(
                cliente, csrf, idSolicitud, tipoEvidencia);
            Assert.True(evidencia.StatusCode == HttpStatusCode.Created,
                await evidencia.Content.ReadAsStringAsync());

            using var enviar = new HttpRequestMessage(
                HttpMethod.Post, $"/api/solicitudes/{idSolicitud}/enviar");
            enviar.Headers.Add("X-CSRF-Token", csrf);
            var enviada = await cliente.SendAsync(enviar);
            Assert.True(enviada.StatusCode == HttpStatusCode.OK,
                await enviada.Content.ReadAsStringAsync());

            var listado = await cliente.GetAsync("/api/solicitudes?pagina=1&tamanoPagina=3");
            Assert.True(listado.StatusCode == HttpStatusCode.OK,
                await listado.Content.ReadAsStringAsync());
            using var listadoJson = JsonDocument.Parse(await listado.Content.ReadAsStringAsync());
            Assert.Equal(1, listadoJson.RootElement.GetProperty("total").GetInt32());
            var elemento = Assert.Single(listadoJson.RootElement.GetProperty("elementos")
                .EnumerateArray());
            Assert.Equal(idSolicitud, elemento.GetProperty("idSolicitud").GetInt32());
            Assert.Equal("PENDIENTE_ASIGNACION", elemento.GetProperty("estadoCodigo").GetString());
            Assert.Equal(1, elemento.GetProperty("evidenciasActivas").GetInt32());

            var detalle = await cliente.GetAsync($"/api/solicitudes/{idSolicitud}");
            Assert.Equal(HttpStatusCode.OK, detalle.StatusCode);

            var ajeno = await otroCliente.GetAsync($"/api/solicitudes/{idSolicitud}");
            Assert.Equal(HttpStatusCode.NotFound, ajeno.StatusCode);
            var listadoAjeno = await otroCliente.GetAsync("/api/solicitudes?pagina=1&tamanoPagina=3");
            Assert.Equal(HttpStatusCode.OK, listadoAjeno.StatusCode);
            using var listadoAjenoJson = JsonDocument.Parse(await listadoAjeno.Content.ReadAsStringAsync());
            Assert.Equal(0, listadoAjenoJson.RootElement.GetProperty("total").GetInt32());

            var csrfLogout = await ObtenerCsrfAsync(cliente);
            using var logout = new HttpRequestMessage(HttpMethod.Delete, "/api/sesiones/actual");
            logout.Headers.Add("X-CSRF-Token", csrfLogout);
            Assert.Equal(HttpStatusCode.NoContent, (await cliente.SendAsync(logout)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await cliente.GetAsync("/api/solicitudes")).StatusCode);
        }
        finally
        {
            await using var dbLimpieza = await SqlIntegration.AbrirContextoAsync();
            await SqlIntegration.LimpiarUsuarioAsync(dbLimpieza, correo);
            await SqlIntegration.LimpiarUsuarioAsync(dbLimpieza, otroCorreo);
            if (Directory.Exists(almacenamiento))
                Directory.Delete(almacenamiento, recursive: true);
        }
    }

    private static async Task<HttpResponseMessage> RegistrarAsync(
        HttpClient cliente, string correo) =>
        await cliente.PostAsJsonAsync("/api/cuentas", new
        {
            nombreCompleto = "Cuenta de flujo",
            correo,
            contrasena = Contrasena
        });

    private static async Task<HttpResponseMessage> LoginAsync(
        HttpClient cliente, string correo)
    {
        var csrf = await ObtenerCsrfAsync(cliente);
        using var login = new HttpRequestMessage(HttpMethod.Post, "/api/sesiones")
        {
            Content = JsonContent.Create(new { correo, contrasena = Contrasena })
        };
        login.Headers.Add("X-CSRF-Token", csrf);
        return await cliente.SendAsync(login);
    }

    private static async Task<string> ObtenerCsrfAsync(HttpClient cliente)
    {
        var response = await cliente.GetAsync("/api/sesiones/csrf");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("token").GetString()!;
    }

    private static async Task<HttpResponseMessage> SubirEvidenciaAsync(
        HttpClient cliente, string csrf, int idSolicitud, int idTipoEvidencia)
    {
        var png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Y9Z4R8AAAAASUVORK5CYII=");
        using var formulario = new MultipartFormDataContent();
        formulario.Add(
            new StringContent(idTipoEvidencia.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            "IdTipoEvidencia");
        formulario.Add(new StringContent("Vista general"), "Descripcion");
        var archivo = new ByteArrayContent(png);
        archivo.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        formulario.Add(archivo, "Archivo", "evidencia.png");

        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/solicitudes/{idSolicitud}/evidencias")
        {
            Content = formulario
        };
        request.Headers.Add("X-CSRF-Token", csrf);
        return await cliente.SendAsync(request);
    }

    private static WebApplicationFactory<Program> CrearFactory(string almacenamiento)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting(
                "ConnectionStrings:GradeX",
                Environment.GetEnvironmentVariable("GRADEX_INTEGRATION_TEST_CONNECTION")!);
            builder.UseSetting("Almacenamiento:Evidencias:Ruta", almacenamiento);
            builder.UseSetting("RateLimiting:Registro:PermitLimit", "1000");
            builder.UseSetting("RateLimiting:Login:PermitLimit", "1000");
            builder.UseSetting("RateLimiting:Solicitudes:PermitLimit", "1000");
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

    private sealed class ContrasenaNoComprometida : IContrasenaComprometida
    {
        public ValueTask<EstadoContrasenaComprometida> EstaComprometidaAsync(
            string contrasenaNormalizada, CancellationToken cancellationToken) =>
            ValueTask.FromResult(EstadoContrasenaComprometida.NoEncontrada);
    }
}

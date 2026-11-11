using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GradeX.Application.Asignaciones;
using GradeX.Application.Cuentas;
using GradeX.Application.Sesiones;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GradeX.Tests;

public sealed class AsignacionesApiTests
{
    [Fact]
    public async Task SinSesionDevuelve401()
    {
        using var factory = CrearFactory(new AsignacionStoreFalso());
        using var client = factory.CreateClient(Cliente(handleCookies: true));

        var response = await client.GetAsync("/api/administracion/solicitudes");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task RolSolicitanteDevuelve403YAdministradorPuedeConsultar()
    {
        var store = new AsignacionStoreFalso();
        using var factory = CrearFactory(store);
        using var solicitante = await ClienteAutenticadoAsync(factory, ["Solicitante"]);
        using var administrador = await ClienteAutenticadoAsync(factory, ["Administrador"]);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await solicitante.GetAsync("/api/administracion/solicitudes")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await administrador.GetAsync("/api/administracion/solicitudes")).StatusCode);
    }

    [Fact]
    public async Task ListadoYDetalleExponenAsignacionYTrazabilidad()
    {
        var asignacion = new AsignacionActual(
            8, 30, "Especialista Uno", DateTime.UtcNow, true);
        var store = new AsignacionStoreFalso
        {
            Listado = new ListadoSolicitudesAsignacion(
                [
                    new SolicitudAsignacionResumen(
                        20, "SOL-1", "Producto", "Sneakers", null,
                        "ASIGNADA", "Asignada", DateTime.UtcNow, 2, asignacion)
                ],
                1, 20, 1),
            Detalle = new SolicitudAsignacionDetalle(
                20, "SOL-1", "Producto", "Modelo", 1, "Sneakers", null, null,
                "ASIGNADA", "Asignada", DateTime.UtcNow, DateTime.UtcNow,
                false, asignacion, [])
        };
        using var factory = CrearFactory(store);
        using var cliente = await ClienteAutenticadoAsync(factory, ["Administrador"]);

        var listado = await cliente.GetAsync("/api/administracion/solicitudes");
        var detalle = await cliente.GetAsync("/api/administracion/solicitudes/20");

        Assert.Equal(HttpStatusCode.OK, listado.StatusCode);
        Assert.Equal(HttpStatusCode.OK, detalle.StatusCode);
        using var listadoJson = JsonDocument.Parse(await listado.Content.ReadAsStringAsync());
        Assert.Equal(1, listadoJson.RootElement.GetProperty("total").GetInt32());
        Assert.Equal("Especialista Uno",
            listadoJson.RootElement.GetProperty("elementos")[0]
                .GetProperty("asignacion").GetProperty("especialista").GetString());
        using var detalleJson = JsonDocument.Parse(await detalle.Content.ReadAsStringAsync());
        Assert.False(detalleJson.RootElement.GetProperty("puedeAsignarse").GetBoolean());
        Assert.Equal(8, detalleJson.RootElement.GetProperty("asignacion")
            .GetProperty("idAsignacion").GetInt32());
    }

    [Fact]
    public async Task SinCompatiblesDevuelve200ConListaVacia()
    {
        var store = new AsignacionStoreFalso
        {
            Compatibles = new CompatiblesSolicitud(
                20, "SOL-1", "Sneakers", null, "PENDIENTE_ASIGNACION", true, null, [])
        };
        using var factory = CrearFactory(store);
        using var cliente = await ClienteAutenticadoAsync(factory, ["Administrador"]);

        var response = await cliente.GetAsync(
            "/api/administracion/solicitudes/20/especialistas-compatibles");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("puedeAsignarse").GetBoolean());
        Assert.Equal(0, json.RootElement.GetProperty("especialistas").GetArrayLength());
    }

    [Fact]
    public async Task AsignacionExitosaDevuelve201ConLocation()
    {
        var store = new AsignacionStoreFalso();
        using var factory = CrearFactory(store);
        using var cliente = await ClienteAutenticadoAsync(factory, ["Administrador"]);

        var response = await PostAsignacionAsync(cliente, 20, 30);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/api/administracion/solicitudes/20", response.Headers.Location?.ToString());
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(30, json.RootElement.GetProperty("idEspecialista").GetInt32());
        Assert.Equal("ASIGNADA", json.RootElement.GetProperty("estadoCodigo").GetString());
        Assert.Equal(new DatosAsignacion(20, 30), store.UltimaAsignacion);
    }

    [Fact]
    public async Task PostSinCsrfDevuelve400YNoLlamaAlStore()
    {
        var store = new AsignacionStoreFalso();
        using var factory = CrearFactory(store);
        using var cliente = await ClienteAutenticadoAsync(factory, ["Administrador"]);

        var response = await PostSinCsrfAsync(cliente, 20, 30);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(store.UltimaAsignacion);
    }

    [Fact]
    public async Task IdentificadorDeEspecialistaInvalidoDevuelve400()
    {
        var store = new AsignacionStoreFalso();
        using var factory = CrearFactory(store);
        using var cliente = await ClienteAutenticadoAsync(factory, ["Administrador"]);

        var response = await PostAsignacionAsync(cliente, 20, 0);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(store.UltimaAsignacion);
    }

    [Theory]
    [InlineData(EstadoAsignarEspecialista.SolicitudNoEncontrada, HttpStatusCode.NotFound)]
    [InlineData(EstadoAsignarEspecialista.EspecialistaNoEncontrado, HttpStatusCode.NotFound)]
    [InlineData(EstadoAsignarEspecialista.SolicitudNoAsignable, HttpStatusCode.Conflict)]
    [InlineData(EstadoAsignarEspecialista.SinCompatibilidad, HttpStatusCode.UnprocessableEntity)]
    [InlineData(EstadoAsignarEspecialista.EstadoNoConfigurado, HttpStatusCode.ServiceUnavailable)]
    [InlineData(EstadoAsignarEspecialista.ConflictoConcurrente, HttpStatusCode.Conflict)]
    public async Task ErroresDeNegocioSeTraducenAlCodigoHttp(
        EstadoAsignarEspecialista estado, HttpStatusCode esperado)
    {
        var store = new AsignacionStoreFalso
        {
            Resultado = new ResultadoAsignarEspecialista(
                estado,
                EstadoSolicitudCodigo: "PENDIENTE_ASIGNACION",
                EstadoSolicitudNombre: "Pendiente de asignación")
        };
        using var factory = CrearFactory(store);
        using var cliente = await ClienteAutenticadoAsync(factory, ["Administrador"]);

        var response = await PostAsignacionAsync(cliente, 20, 30);

        Assert.Equal(esperado, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task AsignacionDuplicadaDevuelve409ConResponsableActual()
    {
        var actual = new AsignacionActual(
            8, 30, "Especialista Uno", DateTime.UtcNow, true);
        var store = new AsignacionStoreFalso
        {
            Resultado = new ResultadoAsignarEspecialista(
                EstadoAsignarEspecialista.YaAsignada, AsignacionExistente: actual)
        };
        using var factory = CrearFactory(store);
        using var cliente = await ClienteAutenticadoAsync(factory, ["Administrador"]);

        var response = await PostAsignacionAsync(cliente, 20, 30);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(30, json.RootElement.GetProperty("asignacionActual")
            .GetProperty("idEspecialista").GetInt32());
        Assert.Equal("Especialista Uno", json.RootElement.GetProperty("asignacionActual")
            .GetProperty("especialista").GetString());
    }

    [Fact]
    public async Task SolicitudInexistenteEnConsultaDevuelve404()
    {
        using var factory = CrearFactory(new AsignacionStoreFalso());
        using var cliente = await ClienteAutenticadoAsync(factory, ["Administrador"]);

        var response = await cliente.GetAsync("/api/administracion/solicitudes/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> PostSinCsrfAsync(
        ClienteAutenticado cliente, int idSolicitud, int idEspecialista)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/administracion/solicitudes/{idSolicitud}/asignacion")
        {
            Content = JsonContent.Create(new { idEspecialista })
        };
        request.Headers.Add("Cookie", $"GradeX.Session={cliente.Token}");
        return await cliente.Client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> PostAsignacionAsync(
        ClienteAutenticado cliente, int idSolicitud, int idEspecialista)
    {
        const string csrf = "ABCDEF0123456789";
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/administracion/solicitudes/{idSolicitud}/asignacion")
        {
            Content = JsonContent.Create(new { idEspecialista })
        };
        request.Headers.Add(
            "Cookie", $"GradeX.Session={cliente.Token}; GradeX.Csrf={csrf}");
        request.Headers.Add("X-CSRF-Token", csrf);
        return await cliente.Client.SendAsync(request);
    }

    private static async Task<ClienteAutenticado> ClienteAutenticadoAsync(
        WebApplicationFactory<Program> factory, string[] roles)
    {
        var sesiones = factory.Services.GetRequiredService<ISesionStore>();
        var sesion = await sesiones.CrearAsync(
            new UsuarioAutenticado(
                1, "Administrador de prueba", "admin@example.test", roles),
            TimeSpan.FromMinutes(30),
            CancellationToken.None);
        var client = factory.CreateClient(Cliente(handleCookies: false));
        return new ClienteAutenticado(client, sesion.Token);
    }

    private static WebApplicationFactory<Program> CrearFactory(AsignacionStoreFalso store)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:GradeX",
                "Server=gradex-test.invalid;Database=GradeX_Test;Integrated Security=true;Connect Timeout=1");
            builder.UseSetting("RateLimiting:Asignaciones:PermitLimit", "1000");
            builder.UseSetting("RateLimiting:Global:PermitLimit", "1000");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAsignacionStore>();
                services.AddSingleton<IAsignacionStore>(store);
                services.RemoveAll<ISesionStore>();
                services.AddSingleton<ISesionStore, InMemorySesionStore>();
            });
        });
    }

    private static WebApplicationFactoryClientOptions Cliente(bool handleCookies) => new()
    {
        BaseAddress = new Uri("https://localhost"),
        HandleCookies = handleCookies
    };

    private sealed record ClienteAutenticado(HttpClient Client, string Token) : IDisposable
    {
        public async Task<HttpResponseMessage> GetAsync(string path)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add("Cookie", $"GradeX.Session={Token}");
            return await Client.SendAsync(request);
        }

        public void Dispose() => Client.Dispose();
    }

    private sealed class AsignacionStoreFalso : IAsignacionStore
    {
        public ListadoSolicitudesAsignacion Listado { get; init; } =
            new([], 1, 20, 0);

        public SolicitudAsignacionDetalle? Detalle { get; init; }

        public CompatiblesSolicitud? Compatibles { get; init; }

        public ResultadoAsignarEspecialista Resultado { get; init; } = new(
            EstadoAsignarEspecialista.Exito,
            new AsignacionCreada(
                8, 20, "SOL-1", 30, "Especialista Uno",
                DateTime.UtcNow, "ASIGNADA", "Asignada"));

        public DatosAsignacion? UltimaAsignacion { get; private set; }

        public Task<ListadoSolicitudesAsignacion> ListarSolicitudesAsync(
            string? estado, int pagina, int tamanoPagina, CancellationToken cancellationToken) =>
            Task.FromResult(Listado);

        public Task<SolicitudAsignacionDetalle?> ObtenerSolicitudAsync(
            int idSolicitud, CancellationToken cancellationToken) =>
            Task.FromResult(Detalle);

        public Task<CompatiblesSolicitud?> ObtenerCompatiblesAsync(
            int idSolicitud, CancellationToken cancellationToken) =>
            Task.FromResult(Compatibles);

        public Task<ResultadoAsignarEspecialista> AsignarAsync(
            DatosAsignacion datos, CancellationToken cancellationToken)
        {
            UltimaAsignacion = datos;
            return Task.FromResult(Resultado);
        }
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GradeX.Application.Cuentas;
using GradeX.Application.Workflow;
using GradeX.Infrastructure.Persistence;
using GradeX.Infrastructure.Persistence.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GradeX.Tests;

public sealed class Sprint2HttpSqlTests
{
    private const string Contrasena = "GradeX-Sprint2-http-2026!";

    [LocalDbFact]
    public async Task SeguridadYFlujoHttpDeAsignacionEvaluacionCertificadoYNotificaciones()
    {
        var sufijo = Guid.NewGuid().ToString("N")[..10];
        var correoSolicitante = $"http-solicitante-{sufijo}@example.test";
        var correoEspecialista = $"http-especialista-{sufijo}@example.test";
        var correoOtroEspecialista = $"http-otro-{sufijo}@example.test";
        var correoAdmin = $"http-admin-{sufijo}@example.test";
        var almacenamiento = Path.Combine(
            Path.GetTempPath(), "gradex-http-" + Guid.NewGuid().ToString("N"));
        using var factory = CrearFactory(almacenamiento);
        using var anonimo = factory.CreateClient(Cliente());
        using var solicitante = factory.CreateClient(Cliente());
        using var especialista = factory.CreateClient(Cliente());
        using var otroEspecialista = factory.CreateClient(Cliente());
        using var admin = factory.CreateClient(Cliente());
        var idsEspecialistas = new List<int>();
        var idCategoria = 0;
        var idTipoEvidencia = 0;

        try
        {
            await RegistrarAsync(admin, "Admin HTTP", correoAdmin);
            await RegistrarAsync(especialista, "Especialista HTTP", correoEspecialista);
            await RegistrarAsync(otroEspecialista, "Otro Especialista HTTP", correoOtroEspecialista);
            await RegistrarAsync(solicitante, "Solicitante HTTP", correoSolicitante);

            int idSolicitante;
            int idSolicitud;
            int idBorrador;
            int idEspecialista;
            await using (var db = await SqlIntegration.AbrirContextoAsync())
            {
                var usuarios = await db.Usuarios.AsNoTracking()
                    .Where(u => u.Correo == correoSolicitante || u.Correo == correoEspecialista ||
                        u.Correo == correoOtroEspecialista || u.Correo == correoAdmin)
                    .ToDictionaryAsync(u => u.Correo, u => u.IdUsuario);
                idSolicitante = usuarios[correoSolicitante];
                await ConcederRolAsync(db, usuarios[correoAdmin], "Administrador");
                await ConcederRolAsync(db, usuarios[correoEspecialista], "Especialista");
                await ConcederRolAsync(db, usuarios[correoOtroEspecialista], "Especialista");

                var categoria = new Categorium
                {
                    Nombre = $"Categoria HTTP {sufijo}",
                    Descripcion = "Categoría aislada para la prueba HTTP de Sprint 2",
                    Activa = true
                };
                db.Categoria.Add(categoria);
                await db.SaveChangesAsync();
                idCategoria = categoria.IdCategoria;
                idTipoEvidencia = await db.TipoEvidencia.AsNoTracking()
                    .Where(t => t.Codigo == "VISTA_GENERAL")
                    .Select(t => t.IdTipoEvidencia)
                    .SingleAsync();

                idEspecialista = await CrearEspecialistaAsync(
                    db, usuarios[correoEspecialista], idCategoria);
                idsEspecialistas.Add(idEspecialista);
                idsEspecialistas.Add(await CrearEspecialistaAsync(
                    db, usuarios[correoOtroEspecialista], idCategoria));

                idSolicitud = await CrearSolicitudAsync(
                    db, idSolicitante, idCategoria, idTipoEvidencia,
                    FlujoGradeX.EstadosSolicitud.PendienteAsignacion,
                    $"SOL-HTTP-{sufijo}");
                idBorrador = await CrearSolicitudAsync(
                    db, idSolicitante, idCategoria, idTipoEvidencia,
                    FlujoGradeX.EstadosSolicitud.Borrador,
                    $"SOL-HTTP-BORRADOR-{sufijo}");
            }

            await IniciarSesionAsync(solicitante, correoSolicitante);
            await IniciarSesionAsync(especialista, correoEspecialista);
            await IniciarSesionAsync(otroEspecialista, correoOtroEspecialista);
            await IniciarSesionAsync(admin, correoAdmin);

            Assert.Equal(HttpStatusCode.Unauthorized,
                (await anonimo.GetAsync("/api/administracion/solicitudes")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await solicitante.GetAsync("/api/administracion/solicitudes")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await solicitante.GetAsync("/api/especialista/asignaciones")).StatusCode);

            var listado = await admin.GetAsync(
                "/api/administracion/solicitudes?estado=PENDIENTE_ASIGNACION&pagina=1&tamanoPagina=50");
            Assert.Equal(HttpStatusCode.OK, listado.StatusCode);
            using (var json = JsonDocument.Parse(await listado.Content.ReadAsStringAsync()))
            {
                var folios = json.RootElement.GetProperty("elementos").EnumerateArray()
                    .Select(e => e.GetProperty("folio").GetString())
                    .ToList();
                Assert.Contains($"SOL-HTTP-{sufijo}", folios);
                Assert.DoesNotContain($"SOL-HTTP-BORRADOR-{sufijo}", folios);
            }

            Assert.Equal(HttpStatusCode.NotFound,
                (await admin.GetAsync($"/api/administracion/solicitudes/{idBorrador}")).StatusCode);

            var sinCsrf = await admin.PostAsJsonAsync(
                $"/api/administracion/solicitudes/{idSolicitud}/asignacion",
                new { idEspecialista });
            Assert.Equal(HttpStatusCode.BadRequest, sinCsrf.StatusCode);

            using (var asignacion = new HttpRequestMessage(
                HttpMethod.Post, $"/api/administracion/solicitudes/{idSolicitud}/asignacion")
            {
                Content = JsonContent.Create(new { idEspecialista })
            })
            {
                asignacion.Headers.Add("X-CSRF-Token", await ObtenerCsrfAsync(admin));
                var respuesta = await admin.SendAsync(asignacion);
                Assert.True(respuesta.StatusCode == HttpStatusCode.Created,
                    await respuesta.Content.ReadAsStringAsync());
            }

            var asignaciones = await especialista.GetAsync(
                "/api/especialista/asignaciones?pagina=1&tamanoPagina=50");
            Assert.Equal(HttpStatusCode.OK, asignaciones.StatusCode);
            using (var json = JsonDocument.Parse(await asignaciones.Content.ReadAsStringAsync()))
            {
                Assert.Contains(json.RootElement.GetProperty("elementos").EnumerateArray(),
                    e => e.GetProperty("idSolicitud").GetInt32() == idSolicitud);
            }

            Assert.Equal(HttpStatusCode.OK,
                (await especialista.GetAsync(
                    $"/api/especialista/asignaciones/{idSolicitud}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,
                (await otroEspecialista.GetAsync(
                    $"/api/especialista/asignaciones/{idSolicitud}")).StatusCode);

            using (var guardar = new HttpRequestMessage(
                HttpMethod.Put, $"/api/especialista/asignaciones/{idSolicitud}/evaluacion")
            {
                Content = JsonContent.Create(new
                {
                    conditionGrade = 8.5m,
                    technicalHealth = 91m,
                    observacionesGenerales = "Revisión HTTP",
                    resultados = Array.Empty<object>()
                })
            })
            {
                guardar.Headers.Add("X-CSRF-Token", await ObtenerCsrfAsync(especialista));
                var respuesta = await especialista.SendAsync(guardar);
                Assert.True(respuesta.StatusCode == HttpStatusCode.OK,
                    await respuesta.Content.ReadAsStringAsync());
            }

            using (var sinCalificacion = new HttpRequestMessage(
                HttpMethod.Post, $"/api/especialista/asignaciones/{idSolicitud}/dictamen")
            {
                Content = JsonContent.Create(new
                {
                    dictamen = FlujoGradeX.Dictamenes.NoAutentico,
                    conditionGrade = (decimal?)null,
                    technicalHealth = 91m,
                    observacionesGenerales = "Sin calificación",
                    resultados = Array.Empty<object>()
                })
            })
            {
                sinCalificacion.Headers.Add("X-CSRF-Token", await ObtenerCsrfAsync(especialista));
                Assert.Equal(HttpStatusCode.BadRequest,
                    (await especialista.SendAsync(sinCalificacion)).StatusCode);
            }

            using (var dictamen = new HttpRequestMessage(
                HttpMethod.Post, $"/api/especialista/asignaciones/{idSolicitud}/dictamen")
            {
                Content = JsonContent.Create(new
                {
                    dictamen = FlujoGradeX.Dictamenes.NoAutentico,
                    conditionGrade = 8.0m,
                    technicalHealth = 91m,
                    observacionesGenerales = "Producto no auténtico",
                    resultados = Array.Empty<object>()
                })
            })
            {
                dictamen.Headers.Add("X-CSRF-Token", await ObtenerCsrfAsync(especialista));
                var respuesta = await especialista.SendAsync(dictamen);
                Assert.True(respuesta.StatusCode == HttpStatusCode.OK,
                    await respuesta.Content.ReadAsStringAsync());
            }

            await using (var db = await SqlIntegration.AbrirContextoAsync())
            {
                var idOtroEspecialista = idsEspecialistas.Single(id => id != idEspecialista);
                await db.Evaluacions
                    .Where(e => e.IdSolicitud == idSolicitud)
                    .ExecuteUpdateAsync(update => update
                        .SetProperty(e => e.IdEspecialista, idOtroEspecialista));
            }
            using (var intentoAjeno = new HttpRequestMessage(
                HttpMethod.Post, $"/api/especialista/asignaciones/{idSolicitud}/certificado"))
            {
                intentoAjeno.Headers.Add("X-CSRF-Token", await ObtenerCsrfAsync(especialista));
                Assert.Equal(HttpStatusCode.Forbidden,
                    (await especialista.SendAsync(intentoAjeno)).StatusCode);
            }
            await using (var db = await SqlIntegration.AbrirContextoAsync())
            {
                await db.Evaluacions
                    .Where(e => e.IdSolicitud == idSolicitud)
                    .ExecuteUpdateAsync(update => update
                        .SetProperty(e => e.IdEspecialista, idEspecialista));
            }

            using var emitir = new HttpRequestMessage(
                HttpMethod.Post, $"/api/especialista/asignaciones/{idSolicitud}/certificado");
            emitir.Headers.Add("X-CSRF-Token", await ObtenerCsrfAsync(especialista));
            var emitido = await especialista.SendAsync(emitir);
            Assert.True(emitido.StatusCode == HttpStatusCode.Created,
                await emitido.Content.ReadAsStringAsync());
            string numero;
            Guid token;
            using (var json = JsonDocument.Parse(await emitido.Content.ReadAsStringAsync()))
            {
                var root = json.RootElement;
                numero = root.GetProperty("numeroCertificado").GetString()!;
                token = root.GetProperty("qrToken").GetGuid();
                Assert.Equal(FlujoGradeX.Dictamenes.NoAutentico,
                    root.GetProperty("dictamen").GetString());
                Assert.Equal(8.0m, root.GetProperty("conditionGrade").GetDecimal());
                Assert.False(string.IsNullOrWhiteSpace(
                    root.GetProperty("especialidad").GetString()));
            }

            var publico = await anonimo.GetAsync($"/api/certificados/verificar/{token}");
            Assert.Equal(HttpStatusCode.OK, publico.StatusCode);
            using (var json = JsonDocument.Parse(await publico.Content.ReadAsStringAsync()))
            {
                var root = json.RootElement;
                Assert.Equal(FlujoGradeX.Dictamenes.NoAutentico,
                    root.GetProperty("dictamen").GetString());
                Assert.Equal(8.0m, root.GetProperty("conditionGrade").GetDecimal());
            }
            Assert.Equal(HttpStatusCode.OK,
                (await anonimo.GetAsync(
                    $"/api/certificados/verificar/numero/{Uri.EscapeDataString(numero)}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,
                (await anonimo.GetAsync(
                    $"/api/certificados/verificar/{Guid.NewGuid()}")).StatusCode);

            var listadoCertificados = await solicitante.GetAsync("/api/certificados");
            Assert.Equal(HttpStatusCode.OK, listadoCertificados.StatusCode);
            using (var json = JsonDocument.Parse(
                await listadoCertificados.Content.ReadAsStringAsync()))
            {
                Assert.Contains(json.RootElement.EnumerateArray(),
                    c => c.GetProperty("numeroCertificado").GetString() == numero);
            }
            using (var json = JsonDocument.Parse(await (await otroEspecialista.GetAsync(
                "/api/certificados")).Content.ReadAsStringAsync()))
            {
                Assert.Empty(json.RootElement.EnumerateArray());
            }

            var notificacionesSolicitante = await solicitante.GetAsync(
                "/api/notificaciones?pagina=1&tamanoPagina=50");
            Assert.Equal(HttpStatusCode.OK, notificacionesSolicitante.StatusCode);
            using (var json = JsonDocument.Parse(
                await notificacionesSolicitante.Content.ReadAsStringAsync()))
            {
                Assert.True(json.RootElement.GetProperty("total").GetInt32() >= 4);
            }

            var notificacionesEspecialista = await especialista.GetAsync(
                "/api/notificaciones?pagina=1&tamanoPagina=50");
            Assert.Equal(HttpStatusCode.OK, notificacionesEspecialista.StatusCode);
            long idNotificacionEspecialista;
            using (var json = JsonDocument.Parse(
                await notificacionesEspecialista.Content.ReadAsStringAsync()))
            {
                idNotificacionEspecialista = json.RootElement.GetProperty("elementos")[0]
                    .GetProperty("idNotificacion").GetInt64();
            }

            using var marcarAjena = new HttpRequestMessage(
                HttpMethod.Patch, $"/api/notificaciones/{idNotificacionEspecialista}/leida");
            marcarAjena.Headers.Add("X-CSRF-Token", await ObtenerCsrfAsync(otroEspecialista));
            Assert.Equal(HttpStatusCode.NotFound,
                (await otroEspecialista.SendAsync(marcarAjena)).StatusCode);

            await using (var db = await SqlIntegration.AbrirContextoAsync())
            {
                await db.Usuarios.Where(u => u.IdUsuario == idSolicitante)
                    .ExecuteUpdateAsync(update => update
                        .SetProperty(u => u.RecibirNotificaciones, false));
            }
            using (var json = JsonDocument.Parse(await (await solicitante.GetAsync(
                "/api/notificaciones?pagina=1&tamanoPagina=50")).Content.ReadAsStringAsync()))
            {
                Assert.Equal(0, json.RootElement.GetProperty("total").GetInt32());
                Assert.Equal(0, json.RootElement.GetProperty("noLeidas").GetInt32());
            }
            await using (var db = await SqlIntegration.AbrirContextoAsync())
            {
                await db.Usuarios.Where(u => u.IdUsuario == idSolicitante)
                    .ExecuteUpdateAsync(update => update
                        .SetProperty(u => u.RecibirNotificaciones, true));
            }
        }
        finally
        {
            await using var dbLimpieza = await SqlIntegration.AbrirContextoAsync();
            // Primero se eliminan solicitudes/asignaciones del solicitante; después
            // los perfiles de especialista y, por último, las cuentas.
            await SqlIntegration.LimpiarUsuarioAsync(dbLimpieza, correoSolicitante);
            foreach (var idEspecialista in idsEspecialistas)
            {
                await dbLimpieza.EspecialidadEspecialista
                    .Where(e => e.IdEspecialista == idEspecialista).ExecuteDeleteAsync();
                await dbLimpieza.Especialista
                    .Where(e => e.IdEspecialista == idEspecialista).ExecuteDeleteAsync();
            }
            foreach (var correo in new[]
            {
                correoEspecialista, correoOtroEspecialista, correoAdmin
            })
                await SqlIntegration.LimpiarUsuarioAsync(dbLimpieza, correo);
            if (idCategoria > 0)
                await dbLimpieza.Categoria
                    .Where(c => c.IdCategoria == idCategoria).ExecuteDeleteAsync();
            if (Directory.Exists(almacenamiento))
                Directory.Delete(almacenamiento, recursive: true);
        }
    }

    private static async Task<int> CrearEspecialistaAsync(
        GradeXDbContext db, int idUsuario, int idCategoria)
    {
        var especialista = new Especialistum
        {
            IdUsuario = idUsuario,
            Estado = FlujoGradeX.EstadosEspecialista.Activo,
            AniosExperiencia = 6,
            FechaAlta = DateTime.UtcNow
        };
        db.Especialista.Add(especialista);
        await db.SaveChangesAsync();
        db.EspecialidadEspecialista.Add(new EspecialidadEspecialistum
        {
            IdEspecialista = especialista.IdEspecialista,
            IdCategoria = idCategoria,
            Nivel = FlujoGradeX.NivelesEspecialista.Experto,
            Activa = true,
            FechaRegistro = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return especialista.IdEspecialista;
    }

    private static async Task<int> CrearSolicitudAsync(
        GradeXDbContext db, int idSolicitante, int idCategoria, int idTipoEvidencia,
        string estadoCodigo, string folio)
    {
        var estado = await db.EstadoSolicituds.AsNoTracking()
            .SingleAsync(e => e.Codigo == estadoCodigo);
        var solicitud = new SolicitudEvaluacion
        {
            Folio = folio,
            IdSolicitante = idSolicitante,
            IdEstadoSolicitud = estado.IdEstadoSolicitud,
            FechaSolicitud = DateTime.UtcNow,
            FechaUltimoCambio = DateTime.UtcNow,
            IdProductoNavigation = new Producto
            {
                IdPropietario = idSolicitante,
                IdCategoria = idCategoria,
                Nombre = $"Producto HTTP {folio}",
                Modelo = "Modelo HTTP",
                FechaRegistro = DateTime.UtcNow,
                Activo = true
            },
            Evidencia =
            [
                new Evidencium
                {
                    IdTipoEvidencia = idTipoEvidencia,
                    UrlArchivo = $"2026/11/{folio}.png",
                    NombreOriginal = $"{folio}.png",
                    TipoContenido = "image/png",
                    TamanoBytes = 100,
                    FechaCarga = DateTime.UtcNow,
                    Activa = true
                }
            ]
        };
        db.SolicitudEvaluacions.Add(solicitud);
        await db.SaveChangesAsync();
        return solicitud.IdSolicitud;
    }

    private static async Task ConcederRolAsync(
        GradeXDbContext db, int idUsuario, string nombreRol)
    {
        var idRol = await db.Rols.AsNoTracking()
            .Where(r => r.Nombre == nombreRol && r.Activo)
            .Select(r => r.IdRol)
            .SingleAsync();
        if (!await db.UsuarioRols.AsNoTracking()
            .AnyAsync(ur => ur.IdUsuario == idUsuario && ur.IdRol == idRol))
        {
            db.UsuarioRols.Add(new UsuarioRol
            {
                IdUsuario = idUsuario,
                IdRol = idRol,
                FechaAsignacion = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
    }

    private static async Task RegistrarAsync(
        HttpClient cliente, string nombre, string correo)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/cuentas", new
        {
            nombreCompleto = nombre,
            correo,
            contrasena = Contrasena
        });
        Assert.True(respuesta.StatusCode == HttpStatusCode.Created,
            await respuesta.Content.ReadAsStringAsync());
    }

    private static async Task IniciarSesionAsync(HttpClient cliente, string correo)
    {
        using var login = new HttpRequestMessage(HttpMethod.Post, "/api/sesiones")
        {
            Content = JsonContent.Create(new { correo, contrasena = Contrasena })
        };
        login.Headers.Add("X-CSRF-Token", await ObtenerCsrfAsync(cliente));
        var respuesta = await cliente.SendAsync(login);
        Assert.True(respuesta.StatusCode == HttpStatusCode.OK,
            await respuesta.Content.ReadAsStringAsync());
    }

    private static async Task<string> ObtenerCsrfAsync(HttpClient cliente)
    {
        var respuesta = await cliente.GetAsync("/api/sesiones/csrf");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("token").GetString()!;
    }

    private static WebApplicationFactory<Program> CrearFactory(string almacenamiento) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting(
                "ConnectionStrings:GradeX",
                Environment.GetEnvironmentVariable("GRADEX_INTEGRATION_TEST_CONNECTION")!);
            builder.UseSetting("Almacenamiento:Evidencias:Ruta", almacenamiento);
            builder.UseSetting("RateLimiting:Registro:PermitLimit", "1000");
            builder.UseSetting("RateLimiting:Login:PermitLimit", "1000");
            builder.UseSetting("RateLimiting:Solicitudes:PermitLimit", "1000");
            builder.UseSetting("RateLimiting:Asignaciones:PermitLimit", "1000");
            builder.UseSetting("RateLimiting:Certificados:PermitLimit", "1000");
            builder.UseSetting("RateLimiting:Global:PermitLimit", "1000");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IContrasenaComprometida>();
                services.AddSingleton<IContrasenaComprometida, ContrasenaNoComprometida>();
            });
        });

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

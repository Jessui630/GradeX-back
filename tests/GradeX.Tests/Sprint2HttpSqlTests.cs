using System.Net;
using System.Net.Http.Headers;
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

    [LocalDbFact]
    public async Task FlujoAdministrativoPerfilNotificacionesYEvidenciaHttp()
    {
        var sufijo = Guid.NewGuid().ToString("N")[..10];
        var correoAdmin = $"cov-admin-{sufijo}@example.test";
        var correoEspecialista = $"cov-especialista-{sufijo}@example.test";
        var correoSolicitante = $"cov-solicitante-{sufijo}@example.test";
        var contrasenaNueva = "GradeX-Coverage-Nueva-2026!";
        var almacenamiento = Path.Combine(
            Path.GetTempPath(), "gradex-coverage-" + Guid.NewGuid().ToString("N"));
        using var factory = CrearFactory(almacenamiento);
        using var anonimo = factory.CreateClient(Cliente());
        using var admin = factory.CreateClient(Cliente());
        using var especialista = factory.CreateClient(Cliente());
        using var solicitante = factory.CreateClient(Cliente());
        var idsEspecialistas = new List<int>();
        var idCategoria = 0;
        var idMarca = 0;
        var idCriterio = 0;
        var idSolicitud = 0;
        var idEvidencia = 0;
        var idUsuarioEspecialista = 0;
        var folio = $"COV-{sufijo}";

        try
        {
            await RegistrarAsync(admin, "Admin Cobertura", correoAdmin);
            await RegistrarAsync(especialista, "Especialista Cobertura", correoEspecialista);
            await RegistrarAsync(solicitante, "Solicitante Cobertura", correoSolicitante);

            int idAdmin;
            int idSolicitante;
            await using (var db = await SqlIntegration.AbrirContextoAsync())
            {
                var usuarios = await db.Usuarios.AsNoTracking()
                    .Where(u => u.Correo == correoAdmin || u.Correo == correoEspecialista ||
                        u.Correo == correoSolicitante)
                    .ToDictionaryAsync(u => u.Correo, u => u.IdUsuario);
                idAdmin = usuarios[correoAdmin];
                idUsuarioEspecialista = usuarios[correoEspecialista];
                idSolicitante = usuarios[correoSolicitante];
                await ConcederRolAsync(db, idAdmin, "Administrador");
                await ConcederRolAsync(db, idUsuarioEspecialista, "Especialista");
            }

            await IniciarSesionAsync(admin, correoAdmin);
            await IniciarSesionAsync(especialista, correoEspecialista);
            await IniciarSesionAsync(solicitante, correoSolicitante);

            Assert.Equal(HttpStatusCode.Unauthorized,
                (await anonimo.GetAsync("/api/administracion/catalogo/categorias")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await solicitante.GetAsync("/api/administracion/catalogo/categorias")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await solicitante.GetAsync("/api/administracion/especialistas")).StatusCode);

            var categorias = await admin.GetAsync("/api/administracion/catalogo/categorias");
            Assert.Equal(HttpStatusCode.OK, categorias.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest,
                (await EnviarJsonAsync(admin, HttpMethod.Post,
                    "/api/administracion/catalogo/categorias",
                    new { nombre = " ", descripcion = "Inválida", activa = true })).StatusCode);

            var categoriaResponse = await EnviarJsonAsync(admin, HttpMethod.Post,
                "/api/administracion/catalogo/categorias",
                new { nombre = $"Categoria cobertura {sufijo}", descripcion = "Cobertura", activa = true });
            Assert.True(categoriaResponse.StatusCode == HttpStatusCode.Created,
                await categoriaResponse.Content.ReadAsStringAsync());
            using (var json = JsonDocument.Parse(await categoriaResponse.Content.ReadAsStringAsync()))
                idCategoria = json.RootElement.GetProperty("idCategoria").GetInt32();

            var categoriaUpdate = await EnviarJsonAsync(admin, HttpMethod.Put,
                $"/api/administracion/catalogo/categorias/{idCategoria}",
                new { nombre = $"Categoria cobertura {sufijo}", descripcion = "Actualizada", activa = true });
            Assert.Equal(HttpStatusCode.OK, categoriaUpdate.StatusCode);

            var marcaResponse = await EnviarJsonAsync(admin, HttpMethod.Post,
                "/api/administracion/catalogo/marcas",
                new { nombre = $"Marca cobertura {sufijo}", activa = true });
            Assert.True(marcaResponse.StatusCode == HttpStatusCode.Created,
                await marcaResponse.Content.ReadAsStringAsync());
            using (var json = JsonDocument.Parse(await marcaResponse.Content.ReadAsStringAsync()))
                idMarca = json.RootElement.GetProperty("idMarca").GetInt32();
            Assert.Equal(HttpStatusCode.Conflict,
                (await EnviarJsonAsync(admin, HttpMethod.Post,
                    "/api/administracion/catalogo/marcas",
                    new { nombre = $"Marca cobertura {sufijo}", activa = true })).StatusCode);

            var marcaUpdate = await EnviarJsonAsync(admin, HttpMethod.Put,
                $"/api/administracion/catalogo/marcas/{idMarca}",
                new { nombre = $"Marca cobertura {sufijo}", activa = true });
            Assert.Equal(HttpStatusCode.OK, marcaUpdate.StatusCode);

            var vinculo = await EnviarJsonAsync(admin, HttpMethod.Put,
                $"/api/administracion/catalogo/categorias/{idCategoria}/marcas/{idMarca}",
                new { activa = true });
            Assert.Equal(HttpStatusCode.OK, vinculo.StatusCode);
            Assert.Equal(HttpStatusCode.OK,
                (await admin.GetAsync(
                    $"/api/administracion/catalogo/categorias/{idCategoria}/marcas")).StatusCode);

            var tiposResponse = await admin.GetAsync(
                "/api/administracion/catalogo/tipos-evidencia");
            Assert.Equal(HttpStatusCode.OK, tiposResponse.StatusCode);
            int idTipoVista;
            int idTipoEtiqueta;
            using (var json = JsonDocument.Parse(await tiposResponse.Content.ReadAsStringAsync()))
            {
                var tipos = json.RootElement.EnumerateArray().ToList();
                idTipoVista = tipos.Single(t =>
                    t.GetProperty("codigo").GetString() == "VISTA_GENERAL")
                    .GetProperty("idTipoEvidencia").GetInt32();
                idTipoEtiqueta = tipos.Single(t =>
                    t.GetProperty("codigo").GetString() == "ETIQUETA")
                    .GetProperty("idTipoEvidencia").GetInt32();
            }

            var criterioResponse = await EnviarJsonAsync(admin, HttpMethod.Post,
                $"/api/administracion/catalogo/categorias/{idCategoria}/criterios",
                new
                {
                    nombre = "Condición cobertura",
                    descripcion = "Criterio de cobertura",
                    tipoDato = "NUMERICO",
                    obligatorio = true,
                    orden = 1,
                    activo = true
                });
            Assert.True(criterioResponse.StatusCode == HttpStatusCode.Created,
                await criterioResponse.Content.ReadAsStringAsync());
            using (var json = JsonDocument.Parse(await criterioResponse.Content.ReadAsStringAsync()))
                idCriterio = json.RootElement.GetProperty("idCriterio").GetInt32();
            Assert.Equal(HttpStatusCode.BadRequest,
                (await EnviarJsonAsync(admin, HttpMethod.Post,
                    $"/api/administracion/catalogo/categorias/{idCategoria}/criterios",
                    new
                    {
                        nombre = "Criterio inválido",
                        tipoDato = "INVALIDO",
                        obligatorio = true,
                        orden = 2,
                        activo = true
                    })).StatusCode);

            var criterioUpdate = await EnviarJsonAsync(admin, HttpMethod.Put,
                $"/api/administracion/catalogo/criterios/{idCriterio}",
                new
                {
                    nombre = "Condición cobertura",
                    descripcion = "Criterio actualizado",
                    tipoDato = "NUMERICO",
                    obligatorio = true,
                    orden = 1,
                    activo = true
                });
            Assert.Equal(HttpStatusCode.OK, criterioUpdate.StatusCode);
            Assert.Equal(HttpStatusCode.OK,
                (await admin.GetAsync(
                    $"/api/administracion/catalogo/categorias/{idCategoria}/criterios")).StatusCode);

            var requisitos = await EnviarJsonAsync(admin, HttpMethod.Put,
                $"/api/administracion/catalogo/categorias/{idCategoria}/requisitos",
                new
                {
                    campos = new[]
                    {
                        new { campo = "NOMBRE", obligatorio = true, longitudMaxima = 150, orden = 1 }
                    },
                    evidencias = new[]
                    {
                        new
                        {
                            idTipoEvidencia = idTipoVista,
                            obligatorio = true,
                            cantidadMinima = 1,
                            cantidadMaxima = 1,
                            orden = 1
                        },
                        new
                        {
                            idTipoEvidencia = idTipoEtiqueta,
                            obligatorio = false,
                            cantidadMinima = 1,
                            cantidadMaxima = 1,
                            orden = 2
                        }
                    }
                });
            Assert.Equal(HttpStatusCode.OK, requisitos.StatusCode);

            var candidatos = await admin.GetAsync(
                $"/api/administracion/especialistas/usuarios?buscar={Uri.EscapeDataString(correoEspecialista)}");
            Assert.Equal(HttpStatusCode.OK, candidatos.StatusCode);

            var especialistaResponse = await EnviarJsonAsync(admin, HttpMethod.Post,
                "/api/administracion/especialistas",
                new
                {
                    idUsuario = idUsuarioEspecialista,
                    estado = "ACTIVO",
                    aniosExperiencia = 4,
                    biografia = "Cobertura",
                    especialidades = new[]
                    {
                        new { idCategoria, idMarca = (int?)null, nivel = "GENERAL" }
                    }
                });
            Assert.True(especialistaResponse.StatusCode == HttpStatusCode.Created,
                await especialistaResponse.Content.ReadAsStringAsync());
            using (var json = JsonDocument.Parse(await especialistaResponse.Content.ReadAsStringAsync()))
                idsEspecialistas.Add(json.RootElement.GetProperty("idEspecialista").GetInt32());
            var idEspecialista = idsEspecialistas[0];
            Assert.Equal(HttpStatusCode.Conflict,
                (await EnviarJsonAsync(admin, HttpMethod.Post,
                    "/api/administracion/especialistas",
                    new
                    {
                        idUsuario = idUsuarioEspecialista,
                        estado = "ACTIVO",
                        aniosExperiencia = 4,
                        biografia = "Duplicado",
                        especialidades = new[]
                        {
                            new { idCategoria, idMarca = (int?)null, nivel = "GENERAL" }
                        }
                    })).StatusCode);
            Assert.Equal(HttpStatusCode.OK,
                (await admin.GetAsync("/api/administracion/especialistas")).StatusCode);
            var especialistaUpdate = await EnviarJsonAsync(admin, HttpMethod.Put,
                $"/api/administracion/especialistas/{idEspecialista}",
                new
                {
                    idUsuario = idUsuarioEspecialista,
                    estado = "ACTIVO",
                    aniosExperiencia = 5,
                    biografia = "Actualizado",
                    especialidades = new[]
                    {
                        new { idCategoria, idMarca = (int?)null, nivel = "AVANZADO" }
                    }
                });
            Assert.Equal(HttpStatusCode.OK, especialistaUpdate.StatusCode);

            await using (var db = await SqlIntegration.AbrirContextoAsync())
            {
                idSolicitud = await CrearSolicitudAsync(
                    db, idSolicitante, idCategoria, idTipoVista,
                    FlujoGradeX.EstadosSolicitud.PendienteAsignacion, folio);
                idEvidencia = await db.Evidencia.AsNoTracking()
                    .Where(e => e.IdSolicitud == idSolicitud)
                    .Select(e => e.IdEvidencia)
                    .SingleAsync();
            }
            var archivoInicial = Path.Combine(
                almacenamiento, "2026", "11", $"{folio}.png");
            Directory.CreateDirectory(Path.GetDirectoryName(archivoInicial)!);
            await File.WriteAllBytesAsync(archivoInicial, Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Y9Z4R8AAAAASUVORK5CYII="));

            var listado = await admin.GetAsync(
                "/api/administracion/solicitudes?estado=PENDIENTE_ASIGNACION&pagina=1&tamanoPagina=50");
            Assert.Equal(HttpStatusCode.OK, listado.StatusCode);
            using (var json = JsonDocument.Parse(await listado.Content.ReadAsStringAsync()))
            {
                Assert.Contains(json.RootElement.GetProperty("elementos").EnumerateArray(),
                    e => e.GetProperty("idSolicitud").GetInt32() == idSolicitud);
            }

            var compatibles = await admin.GetAsync(
                $"/api/administracion/solicitudes/{idSolicitud}/especialistas-compatibles");
            Assert.Equal(HttpStatusCode.OK, compatibles.StatusCode);
            using (var json = JsonDocument.Parse(await compatibles.Content.ReadAsStringAsync()))
            {
                Assert.True(json.RootElement.GetProperty("puedeAsignarse").GetBoolean());
                Assert.Contains(json.RootElement.GetProperty("especialistas").EnumerateArray(),
                    e => e.GetProperty("idEspecialista").GetInt32() == idEspecialista);
            }

            Assert.Equal(HttpStatusCode.BadRequest,
                (await EnviarJsonAsync(admin, HttpMethod.Post,
                    $"/api/administracion/solicitudes/{idSolicitud}/asignacion",
                    new { idEspecialista = 0 })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,
                (await EnviarJsonAsync(admin, HttpMethod.Post,
                    $"/api/administracion/solicitudes/{idSolicitud}/asignacion",
                    new { idEspecialista = 999999 })).StatusCode);
            var asignacion = await EnviarJsonAsync(admin, HttpMethod.Post,
                $"/api/administracion/solicitudes/{idSolicitud}/asignacion",
                new { idEspecialista });
            Assert.True(asignacion.StatusCode == HttpStatusCode.Created,
                await asignacion.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.Conflict,
                (await EnviarJsonAsync(admin, HttpMethod.Post,
                    $"/api/administracion/solicitudes/{idSolicitud}/asignacion",
                    new { idEspecialista })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,
                (await admin.GetAsync("/api/administracion/solicitudes/999999")).StatusCode);
            Assert.Equal(HttpStatusCode.OK,
                (await admin.GetAsync(
                    $"/api/administracion/solicitudes/{idSolicitud}")).StatusCode);
            using (var json = JsonDocument.Parse(await (await admin.GetAsync(
                $"/api/administracion/solicitudes/{idSolicitud}/especialistas-compatibles"))
                .Content.ReadAsStringAsync()))
            {
                Assert.False(json.RootElement.GetProperty("puedeAsignarse").GetBoolean());
            }
            var archivo = await especialista.GetAsync(
                $"/api/especialista/asignaciones/{idSolicitud}/evidencias/{idEvidencia}/archivo");
            Assert.Equal(HttpStatusCode.OK, archivo.StatusCode);
            Assert.Equal("image/png", archivo.Content.Headers.ContentType?.MediaType);

            var perfil = await solicitante.GetAsync("/api/perfil");
            Assert.Equal(HttpStatusCode.OK, perfil.StatusCode);
            var perfilUpdate = await EnviarJsonAsync(solicitante, HttpMethod.Put, "/api/perfil",
                new { nombreCompleto = "Solicitante Cobertura Actualizado", recibirNotificaciones = true });
            Assert.Equal(HttpStatusCode.OK, perfilUpdate.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest,
                (await EnviarJsonAsync(solicitante, HttpMethod.Put, "/api/perfil",
                    new { nombreCompleto = " ", recibirNotificaciones = true })).StatusCode);

            var notificaciones = await solicitante.GetAsync(
                "/api/notificaciones?pagina=1&tamanoPagina=50");
            Assert.Equal(HttpStatusCode.OK, notificaciones.StatusCode);
            long idNotificacion;
            using (var json = JsonDocument.Parse(await notificaciones.Content.ReadAsStringAsync()))
                idNotificacion = json.RootElement.GetProperty("elementos")[0]
                    .GetProperty("idNotificacion").GetInt64();
            Assert.Equal(HttpStatusCode.OK,
                (await solicitante.GetAsync("/api/notificaciones/no-leidas")).StatusCode);
            using var marcar = new HttpRequestMessage(
                HttpMethod.Patch, $"/api/notificaciones/{idNotificacion}/leida");
            marcar.Headers.Add("X-CSRF-Token", await ObtenerCsrfAsync(solicitante));
            Assert.Equal(HttpStatusCode.NoContent,
                (await solicitante.SendAsync(marcar)).StatusCode);
            using var marcarTodas = new HttpRequestMessage(
                HttpMethod.Patch, "/api/notificaciones/leer-todas");
            marcarTodas.Headers.Add("X-CSRF-Token", await ObtenerCsrfAsync(solicitante));
            Assert.Equal(HttpStatusCode.OK,
                (await solicitante.SendAsync(marcarTodas)).StatusCode);

            Assert.Equal(HttpStatusCode.OK,
                (await especialista.GetAsync(
                    $"/api/especialista/asignaciones/{idSolicitud}")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest,
                (await EnviarJsonAsync(especialista, HttpMethod.Post,
                    $"/api/especialista/asignaciones/{idSolicitud}/solicitar-evidencia",
                    new { descripcion = " " })).StatusCode);
            var solicitudEvidencia = await EnviarJsonAsync(especialista, HttpMethod.Post,
                $"/api/especialista/asignaciones/{idSolicitud}/solicitar-evidencia",
                new { descripcion = "Adjunta la etiqueta adicional para cobertura." });
            Assert.Equal(HttpStatusCode.NoContent, solicitudEvidencia.StatusCode);

            var detalleFalta = await solicitante.GetAsync($"/api/solicitudes/{idSolicitud}");
            Assert.Equal(HttpStatusCode.OK, detalleFalta.StatusCode);
            using (var json = JsonDocument.Parse(await detalleFalta.Content.ReadAsStringAsync()))
                Assert.Equal("FALTA_EVIDENCIA",
                    json.RootElement.GetProperty("estadoCodigo").GetString());

            var evidenciaAdicional = await SubirEvidenciaAsync(
                solicitante, idSolicitud, idTipoEtiqueta);
            Assert.True(evidenciaAdicional.StatusCode == HttpStatusCode.Created,
                await evidenciaAdicional.Content.ReadAsStringAsync());

            var dictamen = await EnviarJsonAsync(especialista, HttpMethod.Post,
                $"/api/especialista/asignaciones/{idSolicitud}/dictamen",
                new
                {
                    dictamen = FlujoGradeX.Dictamenes.Autentico,
                    conditionGrade = 8.0m,
                    technicalHealth = 90m,
                    observacionesGenerales = "Dictamen de cobertura",
                    resultados = new[]
                    {
                        new
                        {
                            idCriterio,
                            valorTexto = (string?)null,
                            valorNumerico = (decimal?)8.5m,
                            valorBooleano = (bool?)null,
                            observacion = "Resultado de cobertura"
                        }
                    }
                });
            Assert.True(dictamen.StatusCode == HttpStatusCode.OK,
                await dictamen.Content.ReadAsStringAsync());

            var certificado = await EnviarSinCuerpoAsync(especialista,
                $"/api/especialista/asignaciones/{idSolicitud}/certificado");
            Assert.True(certificado.StatusCode == HttpStatusCode.Created,
                await certificado.Content.ReadAsStringAsync());
            string numero;
            Guid token;
            using (var json = JsonDocument.Parse(await certificado.Content.ReadAsStringAsync()))
            {
                numero = json.RootElement.GetProperty("numeroCertificado").GetString()!;
                token = json.RootElement.GetProperty("qrToken").GetGuid();
            }
            Assert.Equal(HttpStatusCode.Conflict,
                (await EnviarSinCuerpoAsync(especialista,
                    $"/api/especialista/asignaciones/{idSolicitud}/certificado")).StatusCode);

            var certificados = await solicitante.GetAsync("/api/certificados");
            Assert.Equal(HttpStatusCode.OK, certificados.StatusCode);
            using (var json = JsonDocument.Parse(await certificados.Content.ReadAsStringAsync()))
            {
                Assert.Contains(json.RootElement.EnumerateArray(),
                    c => c.GetProperty("numeroCertificado").GetString() == numero);
            }
            Assert.Equal(HttpStatusCode.OK,
                (await anonimo.GetAsync(
                    $"/api/certificados/verificar/{token}")).StatusCode);

            Assert.Equal(HttpStatusCode.BadRequest,
                (await EnviarJsonAsync(solicitante, HttpMethod.Put,
                    "/api/perfil/contrasena",
                    new
                    {
                        contrasenaActual = "contraseña incorrecta",
                        contrasenaNueva = contrasenaNueva
                    })).StatusCode);
            var cambioContrasena = await EnviarJsonAsync(solicitante, HttpMethod.Put,
                "/api/perfil/contrasena",
                new { contrasenaActual = Contrasena, contrasenaNueva = contrasenaNueva });
            Assert.Equal(HttpStatusCode.NoContent, cambioContrasena.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await solicitante.GetAsync("/api/perfil")).StatusCode);
        }
        finally
        {
            await using var dbLimpieza = await SqlIntegration.AbrirContextoAsync();
            await SqlIntegration.LimpiarUsuarioAsync(
                dbLimpieza, correoSolicitante);
            foreach (var idEspecialista in idsEspecialistas)
            {
                await dbLimpieza.EspecialidadEspecialista
                    .Where(e => e.IdEspecialista == idEspecialista).ExecuteDeleteAsync();
                await dbLimpieza.Especialista
                    .Where(e => e.IdEspecialista == idEspecialista).ExecuteDeleteAsync();
            }
            await SqlIntegration.LimpiarUsuarioAsync(dbLimpieza, correoEspecialista);
            await SqlIntegration.LimpiarUsuarioAsync(dbLimpieza, correoAdmin);
            if (idCategoria > 0)
            {
                await dbLimpieza.CriterioEvaluacions
                    .Where(c => c.IdCategoria == idCategoria).ExecuteDeleteAsync();
                await dbLimpieza.CategoriaEvidenciaRequisitos
                    .Where(r => r.IdCategoria == idCategoria).ExecuteDeleteAsync();
                await dbLimpieza.CategoriaCampoRequisitos
                    .Where(r => r.IdCategoria == idCategoria).ExecuteDeleteAsync();
                await dbLimpieza.CategoriaMarcas
                    .Where(cm => cm.IdCategoria == idCategoria).ExecuteDeleteAsync();
                await dbLimpieza.Categoria
                    .Where(c => c.IdCategoria == idCategoria).ExecuteDeleteAsync();
            }
            if (idMarca > 0)
                await dbLimpieza.Marcas
                    .Where(m => m.IdMarca == idMarca).ExecuteDeleteAsync();
            if (Directory.Exists(almacenamiento))
                Directory.Delete(almacenamiento, recursive: true);
        }
    }

    private static async Task<HttpResponseMessage> EnviarJsonAsync(
        HttpClient cliente, HttpMethod metodo, string ruta, object cuerpo)
    {
        using var request = new HttpRequestMessage(metodo, ruta)
        {
            Content = JsonContent.Create(cuerpo)
        };
        request.Headers.Add("X-CSRF-Token", await ObtenerCsrfAsync(cliente));
        return await cliente.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> EnviarSinCuerpoAsync(
        HttpClient cliente, string ruta)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, ruta);
        request.Headers.Add("X-CSRF-Token", await ObtenerCsrfAsync(cliente));
        return await cliente.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> SubirEvidenciaAsync(
        HttpClient cliente, int idSolicitud, int idTipoEvidencia)
    {
        var png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Y9Z4R8AAAAASUVORK5CYII=");
        using var formulario = new MultipartFormDataContent();
        formulario.Add(new StringContent(
            idTipoEvidencia.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            "IdTipoEvidencia");
        formulario.Add(new StringContent("Etiqueta de cobertura"), "Descripcion");
        var archivo = new ByteArrayContent(png);
        archivo.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        formulario.Add(archivo, "Archivo", "cobertura.png");

        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/solicitudes/{idSolicitud}/evidencias")
        {
            Content = formulario
        };
        request.Headers.Add("X-CSRF-Token", await ObtenerCsrfAsync(cliente));
        return await cliente.SendAsync(request);
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

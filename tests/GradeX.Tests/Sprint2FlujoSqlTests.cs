using GradeX.Application.Asignaciones;
using GradeX.Application.Certificados;
using GradeX.Application.Evaluaciones;
using GradeX.Application.Notificaciones;
using GradeX.Application.Workflow;
using GradeX.Infrastructure.Persistence;
using GradeX.Infrastructure.Persistence.Commands;
using GradeX.Infrastructure.Persistence.Models;
using GradeX.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;

namespace GradeX.Tests;

public sealed class Sprint2FlujoSqlTests
{
    [LocalDbFact]
    public async Task FlujoCompletoAsignacionEvaluacionCertificadoYNotificaciones()
    {
        await using var db = await SqlIntegration.AbrirContextoAsync();
        var sufijo = Guid.NewGuid().ToString("N")[..10];
        var correoSolicitante = $"solicitante-s2-{sufijo}@example.test";
        var correoEspecialista = $"especialista-s2-{sufijo}@example.test";
        var escenario = new Escenario();
        try
        {
            var ids = await CrearCatalogoAsync(db, sufijo);
            escenario.IdCategoria = ids.IdCategoria;
            escenario.IdMarca = ids.IdMarca;
            escenario.IdCriterioNumerico = ids.IdCriterioNumerico;
            escenario.IdCriterioTexto = ids.IdCriterioTexto;

            var idSolicitante = await SqlIntegration.CrearUsuarioAsync(db, correoSolicitante);
            var idUsuarioEspecialista = await SqlIntegration.CrearUsuarioAsync(db, correoEspecialista);
            escenario.IdUsuarioSolicitante = idSolicitante;
            escenario.IdUsuarioEspecialista = idUsuarioEspecialista;

            var rol = await db.Rols.AsNoTracking()
                .SingleAsync(r => r.Nombre == "Especialista");
            db.UsuarioRols.Add(new UsuarioRol
            {
                IdUsuario = idUsuarioEspecialista,
                IdRol = rol.IdRol,
                FechaAsignacion = DateTime.UtcNow
            });
            var especialista = new Especialistum
            {
                IdUsuario = idUsuarioEspecialista,
                Estado = FlujoGradeX.EstadosEspecialista.Activo,
                AniosExperiencia = 8,
                FechaAlta = DateTime.UtcNow
            };
            db.Especialista.Add(especialista);
            await db.SaveChangesAsync();
            escenario.IdEspecialista = especialista.IdEspecialista;

            db.EspecialidadEspecialista.Add(new EspecialidadEspecialistum
            {
                IdEspecialista = especialista.IdEspecialista,
                IdCategoria = ids.IdCategoria,
                Nivel = FlujoGradeX.NivelesEspecialista.Experto,
                Activa = true,
                FechaRegistro = DateTime.UtcNow
            });

            var estadoPendiente = await db.EstadoSolicituds.AsNoTracking()
                .SingleAsync(e => e.Codigo == FlujoGradeX.EstadosSolicitud.PendienteAsignacion);
            var tipoEvidencia = await db.TipoEvidencia.AsNoTracking()
                .SingleAsync(t => t.Codigo == "VISTA_GENERAL");
            var tipoEvidenciaAdicional = await db.TipoEvidencia.AsNoTracking()
                .SingleAsync(t => t.Codigo == "ETIQUETA");
            var solicitud = new SolicitudEvaluacion
            {
                Folio = $"SOL-S2-{sufijo}",
                IdSolicitante = idSolicitante,
                IdEstadoSolicitud = estadoPendiente.IdEstadoSolicitud,
                FechaSolicitud = DateTime.UtcNow,
                FechaUltimoCambio = DateTime.UtcNow,
                IdProductoNavigation = new Producto
                {
                    IdPropietario = idSolicitante,
                    IdCategoria = ids.IdCategoria,
                    IdMarca = ids.IdMarca,
                    Nombre = "Producto Sprint 2",
                    Modelo = "Modelo S2",
                    FechaRegistro = DateTime.UtcNow,
                    Activo = true
                },
                Evidencia =
                [
                    new Evidencium
                    {
                        IdTipoEvidencia = tipoEvidencia.IdTipoEvidencia,
                        UrlArchivo = "2026/11/evidencia-s2.png",
                        NombreOriginal = "evidencia-s2.png",
                        TipoContenido = "image/png",
                        TamanoBytes = 100,
                        FechaCarga = DateTime.UtcNow,
                        Activa = true
                    }
                ]
            };
            db.SolicitudEvaluacions.Add(solicitud);
            await db.SaveChangesAsync();
            escenario.IdSolicitud = solicitud.IdSolicitud;

            var asignacionStore = new AsignacionStore(db);
            var asignada = await asignacionStore.AsignarAsync(
                new DatosAsignacion(solicitud.IdSolicitud, especialista.IdEspecialista),
                CancellationToken.None);
            Assert.Equal(EstadoAsignarEspecialista.Exito, asignada.Estado);
            Assert.Equal(FlujoGradeX.EstadosSolicitud.Asignada, asignada.Asignacion!.EstadoCodigo);
            db.ChangeTracker.Clear();

            var evaluacionStore = new EvaluacionStore(db, new NotificacionWriter(db));
            var listado = await evaluacionStore.ListarAsignacionesAsync(
                idUsuarioEspecialista, null, 1, 20, CancellationToken.None);
            Assert.Equal(1, listado.Total);
            Assert.Equal(FlujoGradeX.EstadosSolicitud.Asignada,
                Assert.Single(listado.Elementos).EstadoCodigo);

            var detalle = await evaluacionStore.ObtenerAsignacionAsync(
                solicitud.IdSolicitud, idUsuarioEspecialista, CancellationToken.None);
            Assert.NotNull(detalle);
            Assert.True(detalle.PuedeEvaluar);
            Assert.Equal(2, detalle.Criterios.Count);
            Assert.Single(detalle.Evidencias);

            var borrador = await evaluacionStore.GuardarAsync(
                solicitud.IdSolicitud, idUsuarioEspecialista,
                new DatosGuardarEvaluacion(8.5m, 92.5m, "Revisión inicial",
                [
                    new DatosResultadoCriterio(ids.IdCriterioNumerico, null, 9.5m, null, null),
                    new DatosResultadoCriterio(ids.IdCriterioTexto, "Sin hallazgos", null, null, null)
                ]),
                CancellationToken.None);
            Assert.Equal(EstadoOperacionEvaluacion.Exito, borrador.Estado);
            Assert.Equal(FlujoGradeX.EstadosSolicitud.EnEvaluacion,
                await EstadoSolicitudAsync(db, solicitud.IdSolicitud));

            var solicitudEvidencia = await evaluacionStore.SolicitarEvidenciaAsync(
                solicitud.IdSolicitud, idUsuarioEspecialista,
                new DatosSolicitudEvidencia(tipoEvidencia.IdTipoEvidencia,
                    "Adjunta una foto adicional de la etiqueta."),
                CancellationToken.None);
            Assert.Equal(EstadoOperacionEvaluacion.Exito, solicitudEvidencia.Estado);
            Assert.Equal(FlujoGradeX.EstadosSolicitud.FaltaEvidencia,
                await EstadoSolicitudAsync(db, solicitud.IdSolicitud));
            db.ChangeTracker.Clear();

            var bloqueada = await evaluacionStore.GuardarAsync(
                solicitud.IdSolicitud, idUsuarioEspecialista,
                new DatosGuardarEvaluacion(8.5m, 92.5m, "Intento durante falta de evidencia", []),
                CancellationToken.None);
            Assert.Equal(EstadoOperacionEvaluacion.EstadoInvalido, bloqueada.Estado);
            db.ChangeTracker.Clear();

            var notificacionSolicitud = await db.Notificacions.AsNoTracking()
                .Where(n => n.IdSolicitud == solicitud.IdSolicitud &&
                    n.IdUsuario == idSolicitante &&
                    n.IdTipoNotificacionNavigation.Codigo ==
                        FlujoGradeX.TiposNotificacion.SolicitudEvidencia)
                .SingleAsync();
            Assert.Contains("etiqueta", notificacionSolicitud.Mensaje);

            var solicitudStore = new SolicitudStore(db);
            var evidenciaMismoTipo = await solicitudStore.AgregarEvidenciaAsync(
                solicitud.IdSolicitud, idSolicitante, tipoEvidencia.IdTipoEvidencia,
                "2026/11/evidencia-s2-reemplazo.png", "evidencia-s2-reemplazo.png",
                "image/png", 120, new string('C', 64), "Intento de reemplazo",
                CancellationToken.None);
            Assert.Equal(GradeX.Application.Solicitudes.EstadoAgregarEvidencia.LimiteAlcanzado,
                evidenciaMismoTipo.Estado);
            Assert.True(await db.Evidencia.AsNoTracking()
                .AnyAsync(e => e.IdSolicitud == solicitud.IdSolicitud &&
                    e.IdTipoEvidencia == tipoEvidencia.IdTipoEvidencia && e.Activa));

            var evidenciaAdicional = await solicitudStore.AgregarEvidenciaAsync(
                solicitud.IdSolicitud, idSolicitante, tipoEvidenciaAdicional.IdTipoEvidencia,
                "2026/11/evidencia-s2-adicional.png", "evidencia-s2-adicional.png",
                "image/png", 120, new string('B', 64), "Etiqueta adicional",
                CancellationToken.None);
            Assert.Equal(GradeX.Application.Solicitudes.EstadoAgregarEvidencia.Exito,
                evidenciaAdicional.Estado);
            Assert.Equal(2, await db.Evidencia.AsNoTracking()
                .CountAsync(e => e.IdSolicitud == solicitud.IdSolicitud && e.Activa));
            Assert.Equal(FlujoGradeX.EstadosSolicitud.EnEvaluacion,
                await EstadoSolicitudAsync(db, solicitud.IdSolicitud));
            Assert.True(await db.Notificacions.AsNoTracking()
                .AnyAsync(n => n.IdSolicitud == solicitud.IdSolicitud &&
                    n.IdUsuario == idUsuarioEspecialista &&
                    n.IdTipoNotificacionNavigation.Codigo ==
                        FlujoGradeX.TiposNotificacion.EvidenciaRecibida));

            var sinCalificacion = await evaluacionStore.FinalizarAsync(
                solicitud.IdSolicitud, idUsuarioEspecialista,
                new DatosFinalizarEvaluacion(FlujoGradeX.Dictamenes.Autentico,
                    null, 95.0m, "Sin calificación",
                    [
                        new DatosResultadoCriterio(ids.IdCriterioNumerico, null, 9.5m, null, null),
                        new DatosResultadoCriterio(ids.IdCriterioTexto, "Sin hallazgos", null, null, null)
                    ]),
                CancellationToken.None);
            Assert.Equal(EstadoOperacionEvaluacion.DatosInvalidos, sinCalificacion.Estado);
            Assert.Equal("conditionGrade", sinCalificacion.Campo);

            var criterioSoloObservacion = await evaluacionStore.FinalizarAsync(
                solicitud.IdSolicitud, idUsuarioEspecialista,
                new DatosFinalizarEvaluacion(FlujoGradeX.Dictamenes.Autentico,
                    9.0m, 95.0m, "Criterio sin valor",
                    [
                        new DatosResultadoCriterio(ids.IdCriterioNumerico, null, null, null,
                            "Solo observación"),
                        new DatosResultadoCriterio(ids.IdCriterioTexto, "Sin hallazgos", null, null, null)
                    ]),
                CancellationToken.None);
            Assert.Equal(EstadoOperacionEvaluacion.CriterioObligatorioFaltante,
                criterioSoloObservacion.Estado);

            var final = await evaluacionStore.FinalizarAsync(
                solicitud.IdSolicitud, idUsuarioEspecialista,
                new DatosFinalizarEvaluacion(FlujoGradeX.Dictamenes.Autentico,
                    9.0m, 95.0m, "Producto auténtico",
                    [
                        new DatosResultadoCriterio(ids.IdCriterioNumerico, null, 9.5m, null, null),
                        new DatosResultadoCriterio(ids.IdCriterioTexto, "Sin hallazgos", null, null, null)
                    ]),
                CancellationToken.None);
            Assert.Equal(EstadoOperacionEvaluacion.Exito, final.Estado);
            Assert.Equal(FlujoGradeX.Dictamenes.Autentico, final.Evaluacion!.Dictamen);
            Assert.NotNull(final.Evaluacion.FechaFin);
            Assert.Equal(FlujoGradeX.EstadosSolicitud.Finalizada,
                await EstadoSolicitudAsync(db, solicitud.IdSolicitud));

            // Un cambio posterior del catálogo no debe invalidar ni reescribir
            // el dictamen ya finalizado.
            db.CriterioEvaluacions.Add(new CriterioEvaluacion
            {
                IdCategoria = ids.IdCategoria,
                Nombre = "Criterio posterior al dictamen",
                TipoDato = FlujoGradeX.TiposCriterio.Texto,
                Obligatorio = true,
                Orden = 3,
                Activo = true
            });
            await db.SaveChangesAsync();

            var certificadoStore = new CertificadoStore(db, new NotificacionWriter(db));
            var certificado = await certificadoStore.EmitirAsync(
                solicitud.IdSolicitud, idUsuarioEspecialista, CancellationToken.None);
            Assert.Equal(EstadoOperacionCertificado.Exito, certificado.Estado);
            Assert.NotNull(certificado.Certificado);
            Assert.StartsWith("GX-", certificado.Certificado.NumeroCertificado);
            Assert.Equal(FlujoGradeX.Dictamenes.Autentico, certificado.Certificado.Dictamen);
            Assert.Equal("Modelo S2", certificado.Certificado.Modelo);
            Assert.Equal(9.0m, certificado.Certificado.ConditionGrade);
            Assert.False(string.IsNullOrWhiteSpace(certificado.Certificado.Especialidad));

            var delSolicitante = await certificadoStore.ObtenerPorSolicitudAsync(
                solicitud.IdSolicitud, idSolicitante, CancellationToken.None);
            Assert.NotNull(delSolicitante);
            Assert.Equal(certificado.Certificado.IdCertificado, delSolicitante.IdCertificado);
            Assert.Equal("Modelo S2", delSolicitante.Modelo);
            Assert.Equal(9.0m, delSolicitante.ConditionGrade);
            Assert.Equal(certificado.Certificado.Especialidad, delSolicitante.Especialidad);

            var publico = await certificadoStore.VerificarAsync(
                certificado.Certificado.QrToken, CancellationToken.None);
            Assert.NotNull(publico);
            Assert.Equal("Producto Sprint 2", publico.NombreProducto);
            Assert.Equal("Modelo S2", publico.Modelo);
            Assert.Equal(FlujoGradeX.Dictamenes.Autentico, publico.Dictamen);
            Assert.Equal(9.0m, publico.ConditionGrade);
            Assert.False(string.IsNullOrWhiteSpace(publico.Especialidad));
            Assert.DoesNotContain(correoSolicitante, publico.ToString());
            Assert.DoesNotContain(correoEspecialista, publico.ToString());

            var noElegible = await certificadoStore.EmitirAsync(
                solicitud.IdSolicitud, idUsuarioEspecialista, CancellationToken.None);
            Assert.Equal(EstadoOperacionCertificado.YaExiste, noElegible.Estado);

            var notificaciones = new NotificacionStore(db);
            var listadoNotificaciones = await notificaciones.ListarAsync(
                idSolicitante, 1, 50, false, CancellationToken.None);
            Assert.True(listadoNotificaciones.Total >= 4);
            Assert.True(listadoNotificaciones.NoLeidas >= 4);
            var primera = listadoNotificaciones.Elementos[0];
            Assert.True(await notificaciones.MarcarLeidaAsync(
                primera.IdNotificacion, idSolicitante, CancellationToken.None));
            Assert.Equal(listadoNotificaciones.NoLeidas - 1,
                await notificaciones.ContarNoLeidasAsync(idSolicitante, CancellationToken.None));
            Assert.True(await notificaciones.MarcarTodasLeidasAsync(
                idSolicitante, CancellationToken.None) >= 3);
        }
        finally
        {
            await LimpiarAsync(db, escenario, correoSolicitante, correoEspecialista);
        }
    }

    private static async Task<string> EstadoSolicitudAsync(GradeXDbContext db, int idSolicitud) =>
        await db.SolicitudEvaluacions.AsNoTracking()
            .Where(s => s.IdSolicitud == idSolicitud)
            .Select(s => s.IdEstadoSolicitudNavigation.Codigo)
            .SingleAsync();

    private static async Task<(int IdCategoria, int IdMarca, int IdCriterioNumerico,
        int IdCriterioTexto)> CrearCatalogoAsync(GradeXDbContext db, string sufijo)
    {
        var categoria = new Categorium
        {
            Nombre = $"Categoria S2 {sufijo}",
            Descripcion = "Escenario aislado Sprint 2",
            Activa = true
        };
        var marca = new Marca
        {
            Nombre = $"Marca S2 {sufijo}",
            Activa = true
        };
        db.Categoria.Add(categoria);
        db.Marcas.Add(marca);
        await db.SaveChangesAsync();
        db.CategoriaMarcas.Add(new CategoriaMarca
        {
            IdCategoria = categoria.IdCategoria,
            IdMarca = marca.IdMarca,
            Activa = true
        });
        db.CategoriaCampoRequisitos.Add(new CategoriaCampoRequisito
        {
            IdCategoria = categoria.IdCategoria,
            Campo = "NOMBRE",
            Obligatorio = true,
            LongitudMaxima = 150,
            Orden = 1
        });
        var tipo = await db.TipoEvidencia.AsNoTracking()
            .SingleAsync(t => t.Codigo == "VISTA_GENERAL");
        db.CategoriaEvidenciaRequisitos.Add(new CategoriaEvidenciaRequisito
        {
            IdCategoria = categoria.IdCategoria,
            IdTipoEvidencia = tipo.IdTipoEvidencia,
            Obligatorio = true,
            CantidadMinima = 1,
            CantidadMaxima = 1,
            Orden = 1
        });
        var etiqueta = await db.TipoEvidencia.AsNoTracking()
            .SingleAsync(t => t.Codigo == "ETIQUETA");
        db.CategoriaEvidenciaRequisitos.Add(new CategoriaEvidenciaRequisito
        {
            IdCategoria = categoria.IdCategoria,
            IdTipoEvidencia = etiqueta.IdTipoEvidencia,
            Obligatorio = false,
            CantidadMinima = 1,
            CantidadMaxima = 1,
            Orden = 2
        });
        var criterioNumerico = new CriterioEvaluacion
        {
            IdCategoria = categoria.IdCategoria,
            Nombre = "Condición",
            TipoDato = FlujoGradeX.TiposCriterio.Numerico,
            Obligatorio = true,
            Orden = 1,
            Activo = true
        };
        var criterioTexto = new CriterioEvaluacion
        {
            IdCategoria = categoria.IdCategoria,
            Nombre = "Observación técnica",
            TipoDato = FlujoGradeX.TiposCriterio.Texto,
            Obligatorio = true,
            Orden = 2,
            Activo = true
        };
        db.CriterioEvaluacions.AddRange(criterioNumerico, criterioTexto);
        await db.SaveChangesAsync();
        return (categoria.IdCategoria, marca.IdMarca,
            criterioNumerico.IdCriterio, criterioTexto.IdCriterio);
    }

    private static async Task LimpiarAsync(
        GradeXDbContext db, Escenario escenario, string correoSolicitante,
        string correoEspecialista)
    {
        if (escenario.IdSolicitud > 0)
        {
            var evaluaciones = await db.Evaluacions
                .Where(e => e.IdSolicitud == escenario.IdSolicitud)
                .Select(e => e.IdEvaluacion)
                .ToListAsync();
            await db.Notificacions
                .Where(n => n.IdSolicitud == escenario.IdSolicitud)
                .ExecuteDeleteAsync();
            if (evaluaciones.Count > 0)
            {
                await db.Certificados
                    .Where(c => evaluaciones.Contains(c.IdEvaluacion))
                    .ExecuteDeleteAsync();
                await db.ResultadoCriterios
                    .Where(r => evaluaciones.Contains(r.IdEvaluacion))
                    .ExecuteDeleteAsync();
            }
        }
        await SqlIntegration.LimpiarUsuarioAsync(db, correoSolicitante);
        if (escenario.IdUsuarioEspecialista > 0)
        {
            await db.Notificacions
                .Where(n => n.IdUsuario == escenario.IdUsuarioEspecialista)
                .ExecuteDeleteAsync();
            await db.EspecialidadEspecialista
                .Where(e => e.IdEspecialista == escenario.IdEspecialista)
                .ExecuteDeleteAsync();
            await db.Especialista
                .Where(e => e.IdEspecialista == escenario.IdEspecialista)
                .ExecuteDeleteAsync();
        }
        await SqlIntegration.LimpiarUsuarioAsync(db, correoEspecialista);
        if (escenario.IdCategoria > 0)
        {
            await db.CriterioEvaluacions
                .Where(c => c.IdCategoria == escenario.IdCategoria)
                .ExecuteDeleteAsync();
            await db.CategoriaEvidenciaRequisitos
                .Where(r => r.IdCategoria == escenario.IdCategoria)
                .ExecuteDeleteAsync();
            await db.CategoriaCampoRequisitos
                .Where(r => r.IdCategoria == escenario.IdCategoria)
                .ExecuteDeleteAsync();
            await db.CategoriaMarcas
                .Where(cm => cm.IdCategoria == escenario.IdCategoria)
                .ExecuteDeleteAsync();
        }
        if (escenario.IdCategoria > 0)
            await db.Categoria.Where(c => c.IdCategoria == escenario.IdCategoria)
                .ExecuteDeleteAsync();
        if (escenario.IdMarca > 0)
            await db.Marcas.Where(m => m.IdMarca == escenario.IdMarca)
                .ExecuteDeleteAsync();
    }

    private sealed class Escenario
    {
        public int IdSolicitud { get; set; }
        public int IdCategoria { get; set; }
        public int IdMarca { get; set; }
        public int IdCriterioNumerico { get; set; }
        public int IdCriterioTexto { get; set; }
        public int IdUsuarioSolicitante { get; set; }
        public int IdUsuarioEspecialista { get; set; }
        public int IdEspecialista { get; set; }
    }
}

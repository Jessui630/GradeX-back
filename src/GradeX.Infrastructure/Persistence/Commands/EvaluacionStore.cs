using GradeX.Application.Evaluaciones;
using GradeX.Application.Workflow;
using GradeX.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace GradeX.Infrastructure.Persistence.Commands;

public sealed class EvaluacionStore(GradeXDbContext db, NotificacionWriter notificaciones)
    : IEvaluacionStore
{
    public async Task<ListadoAsignacionesEspecialista> ListarAsignacionesAsync(
        int idUsuario, string? estado, int pagina, int tamanoPagina,
        CancellationToken cancellationToken)
    {
        pagina = Math.Max(1, pagina);
        tamanoPagina = Math.Clamp(tamanoPagina, 1, 50);

        var especialista = await ObtenerEspecialistaAsync(idUsuario, cancellationToken);
        if (especialista is null)
            return new ListadoAsignacionesEspecialista([], pagina, tamanoPagina, 0);

        var consulta = db.AsignacionEspecialista
            .AsNoTracking()
            .Where(a => a.IdEspecialista == especialista.IdEspecialista && a.Activa);

        if (!string.IsNullOrWhiteSpace(estado))
        {
            var codigo = estado.Trim().ToUpperInvariant();
            consulta = consulta.Where(a =>
                a.IdSolicitudNavigation.IdEstadoSolicitudNavigation.Codigo == codigo);
        }

        var total = await consulta.CountAsync(cancellationToken);
        var filas = await consulta
            .OrderByDescending(a => a.FechaAsignacion)
            .ThenByDescending(a => a.IdAsignacion)
            .Skip((pagina - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .Select(a => new AsignacionEspecialistaResumen(
                a.IdSolicitud,
                a.IdSolicitudNavigation.Folio,
                a.IdSolicitudNavigation.IdProductoNavigation.Nombre,
                a.IdSolicitudNavigation.IdProductoNavigation.IdCategoriaNavigation.Nombre,
                a.IdSolicitudNavigation.IdProductoNavigation.IdMarcaNavigation != null
                    ? a.IdSolicitudNavigation.IdProductoNavigation.IdMarcaNavigation.Nombre
                    : null,
                a.IdSolicitudNavigation.IdEstadoSolicitudNavigation.Codigo,
                a.IdSolicitudNavigation.IdEstadoSolicitudNavigation.Nombre,
                a.IdSolicitudNavigation.FechaSolicitud,
                a.FechaAsignacion,
                a.IdSolicitudNavigation.Evidencia.Count(e => e.Activa),
                a.IdSolicitudNavigation.Evaluacion != null,
                a.IdSolicitudNavigation.Evaluacion != null
                    ? a.IdSolicitudNavigation.Evaluacion.FechaFin
                    : null))
            .ToListAsync(cancellationToken);

        return new ListadoAsignacionesEspecialista(filas, pagina, tamanoPagina, total);
    }

    public async Task<AsignacionEspecialistaDetalle?> ObtenerAsignacionAsync(
        int idSolicitud, int idUsuario, CancellationToken cancellationToken)
    {
        var especialista = await ObtenerEspecialistaAsync(idUsuario, cancellationToken);
        if (especialista is null)
            return null;

        var solicitud = await db.SolicitudEvaluacions
            .AsNoTracking()
            .Include(s => s.IdProductoNavigation).ThenInclude(p => p.IdCategoriaNavigation)
            .Include(s => s.IdProductoNavigation).ThenInclude(p => p.IdMarcaNavigation)
            .Include(s => s.IdEstadoSolicitudNavigation)
            .Include(s => s.Evidencia).ThenInclude(e => e.IdTipoEvidenciaNavigation)
            .Include(s => s.Evaluacion).ThenInclude(e => e!.ResultadoCriterios)
            .Include(s => s.Evaluacion).ThenInclude(e => e!.Certificado)
                .ThenInclude(c => c!.IdEstadoCertificadoNavigation)
            .Include(s => s.AsignacionEspecialistum)
            .AsSplitQuery()
            .Where(s => s.IdSolicitud == idSolicitud &&
                s.AsignacionEspecialistum != null &&
                s.AsignacionEspecialistum.IdEspecialista == especialista.IdEspecialista &&
                s.AsignacionEspecialistum.Activa)
            .SingleOrDefaultAsync(cancellationToken);

        if (solicitud is null)
            return null;

        var criterios = await CargarCriteriosAsync(
            solicitud.IdProductoNavigation.IdCategoria, cancellationToken);
        var evaluacion = MapearEvaluacion(solicitud.Evaluacion);
        var certificado = MapearCertificado(solicitud.Evaluacion?.Certificado);
        var puedeEvaluar = PuedeEvaluar(solicitud.IdEstadoSolicitudNavigation.Codigo) &&
            (solicitud.Evaluacion is null || solicitud.Evaluacion.FechaFin is null);
        var puedeEmitir = solicitud.Evaluacion is not null &&
            solicitud.Evaluacion.FechaFin is not null &&
            !string.IsNullOrWhiteSpace(solicitud.Evaluacion.Dictamen) &&
            solicitud.Evaluacion.Certificado is null &&
            CriteriosObligatoriosCompletos(criterios, evaluacion);

        return new AsignacionEspecialistaDetalle(
            solicitud.IdSolicitud,
            solicitud.Folio,
            solicitud.IdProductoNavigation.Nombre,
            solicitud.IdProductoNavigation.Modelo,
            solicitud.IdProductoNavigation.Sku,
            solicitud.IdProductoNavigation.NumeroSerie,
            solicitud.IdProductoNavigation.Imei,
            solicitud.IdProductoNavigation.Accesorios,
            solicitud.IdProductoNavigation.Descripcion,
            solicitud.ObservacionesSolicitante,
            solicitud.IdProductoNavigation.IdCategoria,
            solicitud.IdProductoNavigation.IdCategoriaNavigation.Nombre,
            solicitud.IdProductoNavigation.IdMarca,
            solicitud.IdProductoNavigation.IdMarcaNavigation?.Nombre,
            solicitud.IdEstadoSolicitudNavigation.Codigo,
            solicitud.IdEstadoSolicitudNavigation.Nombre,
            solicitud.FechaSolicitud,
            puedeEvaluar,
            puedeEvaluar,
            puedeEmitir,
            solicitud.Evidencia
                .Where(e => e.Activa)
                .OrderBy(e => e.FechaCarga)
                .Select(e => new EvidenciaEvaluacionResumen(
                    e.IdEvidencia,
                    e.IdTipoEvidencia,
                    e.IdTipoEvidenciaNavigation.Codigo,
                    e.IdTipoEvidenciaNavigation.Nombre,
                    e.Descripcion,
                    e.NombreOriginal,
                    e.TipoContenido,
                    e.TamanoBytes,
                    e.FechaCarga))
                .ToList(),
            criterios,
            evaluacion,
            certificado);
    }

    public async Task<ResultadoGuardarEvaluacion> GuardarAsync(
        int idSolicitud, int idUsuario, DatosGuardarEvaluacion datos,
        CancellationToken cancellationToken) =>
        await GuardarInternoAsync(idSolicitud, idUsuario, new DatosFinalizarEvaluacion(
            null, datos.ConditionGrade, datos.TechnicalHealth,
            datos.ObservacionesGenerales, datos.Resultados), finalizar: false,
            cancellationToken);

    public async Task<ResultadoGuardarEvaluacion> FinalizarAsync(
        int idSolicitud, int idUsuario, DatosFinalizarEvaluacion datos,
        CancellationToken cancellationToken) =>
        await GuardarInternoAsync(idSolicitud, idUsuario, datos, finalizar: true,
            cancellationToken);

    public async Task<ResultadoOperacionEspecialista> SolicitarEvidenciaAsync(
        int idSolicitud, int idUsuario, DatosSolicitudEvidencia datos,
        CancellationToken cancellationToken)
    {
        var descripcion = datos.Descripcion?.Trim();
        if (string.IsNullOrWhiteSpace(descripcion))
            return new ResultadoOperacionEspecialista(
                EstadoOperacionEvaluacion.DatosInvalidos, "descripcion",
                "Describe la evidencia adicional que necesitas.");
        if (descripcion.Length > 600)
            return new ResultadoOperacionEspecialista(
                EstadoOperacionEvaluacion.DatosInvalidos, "descripcion",
                "La descripción admite hasta 600 caracteres.");

        var especialista = await ObtenerEspecialistaAsync(idUsuario, cancellationToken);
        if (especialista is null)
            return new ResultadoOperacionEspecialista(
                EstadoOperacionEvaluacion.AsignacionNoEncontrada);

        var solicitud = await db.SolicitudEvaluacions
            .Where(s => s.IdSolicitud == idSolicitud &&
                s.AsignacionEspecialistum != null &&
                s.AsignacionEspecialistum.IdEspecialista == especialista.IdEspecialista &&
                s.AsignacionEspecialistum.Activa)
            .Select(s => new
            {
                s.IdSolicitud,
                s.Folio,
                s.IdSolicitante,
                s.IdEstadoSolicitud,
                EstadoCodigo = s.IdEstadoSolicitudNavigation.Codigo,
                IdCategoria = s.IdProductoNavigation.IdCategoria
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (solicitud is null)
            return new ResultadoOperacionEspecialista(
                EstadoOperacionEvaluacion.AsignacionNoEncontrada);
        if (solicitud.EstadoCodigo == FlujoGradeX.EstadosSolicitud.Finalizada)
            return new ResultadoOperacionEspecialista(
                EstadoOperacionEvaluacion.EstadoInvalido, Detalle:
                "La evaluación ya está finalizada.");

        string? tipoNombre = null;
        if (datos.IdTipoEvidencia is > 0)
        {
            tipoNombre = await db.CategoriaEvidenciaRequisitos
                .AsNoTracking()
                .Where(r => r.IdCategoria == solicitud.IdCategoria &&
                    r.IdTipoEvidencia == datos.IdTipoEvidencia)
                .Select(r => r.IdTipoEvidenciaNavigation.Nombre)
                .SingleOrDefaultAsync(cancellationToken);
            if (tipoNombre is null)
                return new ResultadoOperacionEspecialista(
                    EstadoOperacionEvaluacion.CriterioNoPertenece, "idTipoEvidencia",
                    "El tipo de evidencia no pertenece a la categoría de la solicitud.");
        }

        var estadoFalta = await db.EstadoSolicituds
            .AsNoTracking()
            .SingleOrDefaultAsync(
                e => e.Codigo == FlujoGradeX.EstadosSolicitud.FaltaEvidencia,
                cancellationToken);
        if (estadoFalta is null)
            return new ResultadoOperacionEspecialista(
                EstadoOperacionEvaluacion.EstadoNoConfigurado);

        var mensaje = tipoNombre is null
            ? descripcion
            : $"{descripcion} Tipo solicitado: {tipoNombre}.";
        await notificaciones.CrearAsync(
            solicitud.IdSolicitante,
            solicitud.IdSolicitud,
            FlujoGradeX.TiposNotificacion.SolicitudEvidencia,
            "Evidencia adicional solicitada",
            mensaje,
            cancellationToken);

        var actualizadas = await db.SolicitudEvaluacions
            .Where(s => s.IdSolicitud == idSolicitud &&
                s.IdEstadoSolicitud == solicitud.IdEstadoSolicitud)
            .ExecuteUpdateAsync(update => update
                .SetProperty(s => s.IdEstadoSolicitud, estadoFalta.IdEstadoSolicitud)
                .SetProperty(s => s.FechaUltimoCambio, DateTime.UtcNow),
                cancellationToken);

        if (actualizadas == 0)
            return new ResultadoOperacionEspecialista(
                EstadoOperacionEvaluacion.ConflictoConcurrente);

        await db.SaveChangesAsync(cancellationToken);
        return new ResultadoOperacionEspecialista(EstadoOperacionEvaluacion.Exito);
    }

    public async Task<ArchivoEvidenciaEspecialista?> ObtenerArchivoEvidenciaAsync(
        int idSolicitud, int idEvidencia, int idUsuario,
        CancellationToken cancellationToken)
    {
        var especialista = await ObtenerEspecialistaAsync(idUsuario, cancellationToken);
        if (especialista is null)
            return null;

        return await db.Evidencia
            .AsNoTracking()
            .Where(e => e.IdEvidencia == idEvidencia &&
                e.IdSolicitud == idSolicitud &&
                e.Activa &&
                e.IdSolicitudNavigation.AsignacionEspecialistum != null &&
                e.IdSolicitudNavigation.AsignacionEspecialistum.IdEspecialista ==
                    especialista.IdEspecialista &&
                e.IdSolicitudNavigation.AsignacionEspecialistum.Activa)
            .Select(e => new ArchivoEvidenciaEspecialista(
                e.UrlArchivo, e.TipoContenido, e.NombreOriginal))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<ResultadoGuardarEvaluacion> GuardarInternoAsync(
        int idSolicitud, int idUsuario, DatosFinalizarEvaluacion datos, bool finalizar,
        CancellationToken cancellationToken)
    {
        var especialista = await ObtenerEspecialistaAsync(idUsuario, cancellationToken);
        if (especialista is null)
            return new ResultadoGuardarEvaluacion(
                EstadoOperacionEvaluacion.AsignacionNoEncontrada);

        var solicitud = await db.SolicitudEvaluacions
            .Include(s => s.IdProductoNavigation)
            .Include(s => s.IdEstadoSolicitudNavigation)
            .Include(s => s.Evaluacion).ThenInclude(e => e!.ResultadoCriterios)
            .Where(s => s.IdSolicitud == idSolicitud &&
                s.AsignacionEspecialistum != null &&
                s.AsignacionEspecialistum.IdEspecialista == especialista.IdEspecialista &&
                s.AsignacionEspecialistum.Activa)
            .SingleOrDefaultAsync(cancellationToken);

        if (solicitud is null)
            return new ResultadoGuardarEvaluacion(
                EstadoOperacionEvaluacion.AsignacionNoEncontrada);

        var estado = await db.EstadoSolicituds
            .AsNoTracking()
            .Where(e => e.IdEstadoSolicitud == solicitud.IdEstadoSolicitud)
            .Select(e => e.Codigo)
            .SingleAsync(cancellationToken);
        if (estado == FlujoGradeX.EstadosSolicitud.Finalizada ||
            !PuedeEvaluar(estado))
            return new ResultadoGuardarEvaluacion(
                EstadoOperacionEvaluacion.EstadoInvalido, Detalle:
                "La solicitud no admite cambios de evaluación en su estado actual.");

        if (datos.ConditionGrade is < 0 or > 10)
            return new ResultadoGuardarEvaluacion(
                EstadoOperacionEvaluacion.DatosInvalidos, Campo: "conditionGrade",
                Detalle: "ConditionGrade debe estar entre 0 y 10.");
        if (datos.TechnicalHealth is < 0 or > 100)
            return new ResultadoGuardarEvaluacion(
                EstadoOperacionEvaluacion.DatosInvalidos, Campo: "technicalHealth",
                Detalle: "TechnicalHealth debe estar entre 0 y 100.");
        if (datos.ObservacionesGenerales?.Length > 1500)
            return new ResultadoGuardarEvaluacion(
                EstadoOperacionEvaluacion.DatosInvalidos, Campo: "observacionesGenerales",
                Detalle: "Las observaciones generales admiten hasta 1500 caracteres.");

        if (datos.Resultados.Any(r =>
                (r.ValorTexto?.Trim().Length ?? 0) > 1000 ||
                (r.Observacion?.Trim().Length ?? 0) > 1000))
            return new ResultadoGuardarEvaluacion(
                EstadoOperacionEvaluacion.DatosInvalidos, Campo: "criterios",
                Detalle: "Cada valor de criterio admite hasta 1000 caracteres.");

        var criterios = await CargarCriteriosAsync(
            solicitud.IdProductoNavigation.IdCategoria, cancellationToken);
        var criteriosPorId = criterios.ToDictionary(c => c.IdCriterio);
        var resultados = NormalizarResultados(datos.Resultados);
        foreach (var resultado in resultados)
        {
            if (!criteriosPorId.ContainsKey(resultado.IdCriterio))
                return new ResultadoGuardarEvaluacion(
                    EstadoOperacionEvaluacion.CriterioNoPertenece, Campo:
                    $"criterios.{resultado.IdCriterio}",
                    Detalle: "El criterio no pertenece a la categoría de la solicitud.");
        }

        if (finalizar)
        {
            if (datos.Dictamen is null ||
                !FlujoGradeX.Dictamenes.Todos.Contains(datos.Dictamen))
                return new ResultadoGuardarEvaluacion(
                    EstadoOperacionEvaluacion.DictamenInvalido, Campo: "dictamen",
                    Detalle: "Selecciona un dictamen válido.");
            var enviados = resultados.Select(r => r.IdCriterio).ToHashSet();
            var faltante = criterios.FirstOrDefault(c => c.Obligatorio && !enviados.Contains(c.IdCriterio));
            if (faltante is not null)
                return new ResultadoGuardarEvaluacion(
                    EstadoOperacionEvaluacion.CriterioObligatorioFaltante,
                    Campo: $"criterios.{faltante.IdCriterio}",
                    Detalle: $"Falta el criterio obligatorio: {faltante.Nombre}.");
        }

        Evaluacion evaluacion;
        if (solicitud.Evaluacion is null)
        {
            evaluacion = new Evaluacion
            {
                IdSolicitud = idSolicitud,
                IdEspecialista = especialista.IdEspecialista,
                FechaInicio = DateTime.UtcNow,
                ResultadoCriterios = []
            };
            db.Evaluacions.Add(evaluacion);
            solicitud.Evaluacion = evaluacion;
        }
        else
        {
            evaluacion = solicitud.Evaluacion;
            db.ResultadoCriterios.RemoveRange(evaluacion.ResultadoCriterios);
            evaluacion.ResultadoCriterios = [];
        }

        evaluacion.ConditionGrade = datos.ConditionGrade;
        evaluacion.TechnicalHealth = datos.TechnicalHealth;
        evaluacion.ObservacionesGenerales = Limpiar(datos.ObservacionesGenerales);
        foreach (var resultado in resultados)
            evaluacion.ResultadoCriterios.Add(new ResultadoCriterio
            {
                IdEvaluacion = evaluacion.IdEvaluacion,
                IdCriterio = resultado.IdCriterio,
                ValorTexto = resultado.ValorTexto,
                ValorNumerico = resultado.ValorNumerico,
                ValorBooleano = resultado.ValorBooleano,
                Observacion = resultado.Observacion
            });

        if (finalizar)
        {
            evaluacion.Dictamen = datos.Dictamen;
            evaluacion.FechaFin = DateTime.UtcNow;
            var estadoFinal = await db.EstadoSolicituds
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    e => e.Codigo == FlujoGradeX.EstadosSolicitud.Finalizada,
                    cancellationToken);
            if (estadoFinal is null)
                return new ResultadoGuardarEvaluacion(
                    EstadoOperacionEvaluacion.EstadoNoConfigurado);
            solicitud.IdEstadoSolicitud = estadoFinal.IdEstadoSolicitud;
            solicitud.FechaUltimoCambio = DateTime.UtcNow;
            await notificaciones.CrearAsync(
                solicitud.IdSolicitante,
                solicitud.IdSolicitud,
                FlujoGradeX.TiposNotificacion.CambioEstado,
                "Evaluación finalizada",
                $"La evaluación de la solicitud {solicitud.Folio} emitió el dictamen {datos.Dictamen}.",
                cancellationToken);
        }
        else
        {
            if (estado == FlujoGradeX.EstadosSolicitud.Asignada)
            {
                var estadoEvaluacion = await db.EstadoSolicituds
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        e => e.Codigo == FlujoGradeX.EstadosSolicitud.EnEvaluacion,
                        cancellationToken);
                if (estadoEvaluacion is null)
                    return new ResultadoGuardarEvaluacion(
                        EstadoOperacionEvaluacion.EstadoNoConfigurado);
                solicitud.IdEstadoSolicitud = estadoEvaluacion.IdEstadoSolicitud;
            }
            solicitud.FechaUltimoCambio = DateTime.UtcNow;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return new ResultadoGuardarEvaluacion(
                EstadoOperacionEvaluacion.ConflictoConcurrente);
        }

        return new ResultadoGuardarEvaluacion(
            EstadoOperacionEvaluacion.Exito, MapearEvaluacion(evaluacion));
    }

    private async Task<EspecialistaInfo?> ObtenerEspecialistaAsync(
        int idUsuario, CancellationToken cancellationToken) =>
        await db.Especialista
            .AsNoTracking()
            .Where(e => e.IdUsuario == idUsuario &&
                e.Estado == FlujoGradeX.EstadosEspecialista.Activo &&
                e.IdUsuarioNavigation.Activo)
            .Select(e => new EspecialistaInfo(
                e.IdEspecialista, e.IdUsuario,
                e.IdUsuarioNavigation.NombreCompleto))
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<IReadOnlyList<CriterioEvaluacionResumen>> CargarCriteriosAsync(
        int idCategoria, CancellationToken cancellationToken) =>
        await db.CriterioEvaluacions
            .AsNoTracking()
            .Where(c => c.IdCategoria == idCategoria && c.Activo)
            .OrderBy(c => c.Orden)
            .ThenBy(c => c.Nombre)
            .Select(c => new CriterioEvaluacionResumen(
                c.IdCriterio, c.Nombre, c.Descripcion, c.TipoDato, c.Obligatorio, c.Orden))
            .ToListAsync(cancellationToken);

    private static bool PuedeEvaluar(string estado) =>
        estado is FlujoGradeX.EstadosSolicitud.Asignada
            or FlujoGradeX.EstadosSolicitud.EnEvaluacion
            or FlujoGradeX.EstadosSolicitud.FaltaEvidencia;

    private static EvaluacionActual? MapearEvaluacion(Evaluacion? evaluacion) =>
        evaluacion is null
            ? null
            : new EvaluacionActual(
                evaluacion.IdEvaluacion,
                evaluacion.Dictamen,
                evaluacion.ConditionGrade,
                evaluacion.TechnicalHealth,
                evaluacion.ObservacionesGenerales,
                evaluacion.FechaInicio,
                evaluacion.FechaFin,
                evaluacion.ResultadoCriterios
                    .OrderBy(r => r.IdCriterio)
                    .Select(r => new ResultadoCriterioResumen(
                        r.IdCriterio, r.ValorTexto, r.ValorNumerico,
                        r.ValorBooleano, r.Observacion))
                    .ToList());

    private static CertificadoEstado? MapearCertificado(Certificado? certificado) =>
        certificado is null
            ? null
            : new CertificadoEstado(
                certificado.IdCertificado,
                certificado.NumeroCertificado,
                certificado.QrToken,
                certificado.FechaEmision,
                certificado.IdEstadoCertificadoNavigation.Codigo,
                certificado.IdEstadoCertificadoNavigation.Nombre);

    private static bool CriteriosObligatoriosCompletos(
        IReadOnlyList<CriterioEvaluacionResumen> criterios,
        EvaluacionActual? evaluacion)
    {
        if (evaluacion is null)
            return false;
        var respondidos = evaluacion.Resultados
            .Where(TieneValor)
            .Select(r => r.IdCriterio)
            .ToHashSet();
        return criterios
            .Where(c => c.Obligatorio)
            .All(c => respondidos.Contains(c.IdCriterio));
    }

    private static bool TieneValor(ResultadoCriterioResumen resultado) =>
        !string.IsNullOrWhiteSpace(resultado.ValorTexto) ||
        resultado.ValorNumerico is not null ||
        resultado.ValorBooleano is not null;

    private static List<DatosResultadoCriterio> NormalizarResultados(
        IReadOnlyList<DatosResultadoCriterio> resultados)
    {
        var vistos = new HashSet<int>();
        var normalizados = new List<DatosResultadoCriterio>();
        foreach (var resultado in resultados)
        {
            if (!vistos.Add(resultado.IdCriterio))
                continue;
            var texto = Limpiar(resultado.ValorTexto);
            var observacion = Limpiar(resultado.Observacion);
            if (texto is null && resultado.ValorNumerico is null &&
                resultado.ValorBooleano is null && observacion is null)
                continue;
            normalizados.Add(new DatosResultadoCriterio(
                resultado.IdCriterio, texto, resultado.ValorNumerico,
                resultado.ValorBooleano, observacion));
        }
        return normalizados;
    }

    private static string? Limpiar(string? valor)
    {
        var limpio = valor?.Trim();
        return string.IsNullOrWhiteSpace(limpio) ? null : limpio;
    }

    private sealed record EspecialistaInfo(int IdEspecialista, int IdUsuario, string Nombre);
}

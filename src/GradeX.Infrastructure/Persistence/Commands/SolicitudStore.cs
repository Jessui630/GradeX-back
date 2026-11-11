using System.Globalization;
using GradeX.Application.Solicitudes;
using GradeX.Infrastructure.Persistence.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GradeX.Infrastructure.Persistence.Commands;

public sealed class SolicitudStore(GradeXDbContext db) : ISolicitudStore
{
    private const string EstadoBorrador = "BORRADOR";
    private const string EstadoPendiente = "PENDIENTE_ASIGNACION";
    private const int MaxEvidenciasPorSolicitud = 12;

    public async Task<ResultadoCrearSolicitud> CrearBorradorAsync(
        DatosNuevaSolicitud datos, int idSolicitante, CancellationToken cancellationToken)
    {
        var existente = await db.SolicitudEvaluacions
            .AsNoTracking()
            .Where(s => s.ClaveIdempotencia == datos.ClaveIdempotencia)
            .Select(s => new
            {
                s.IdSolicitud,
                s.IdSolicitante,
                s.Folio,
                EstadoCodigo = s.IdEstadoSolicitudNavigation.Codigo,
                EstadoNombre = s.IdEstadoSolicitudNavigation.Nombre
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (existente is not null)
        {
            return existente.IdSolicitante == idSolicitante
                ? new ResultadoCrearSolicitud(
                    EstadoCrearSolicitud.Existente,
                    new SolicitudCreada(existente.IdSolicitud, existente.Folio,
                        existente.EstadoCodigo, existente.EstadoNombre))
                : new ResultadoCrearSolicitud(EstadoCrearSolicitud.ConflictoIdempotencia);
        }

        var categoria = await db.Categoria
            .AsNoTracking()
            .Where(c => c.IdCategoria == datos.IdCategoria && c.Activa)
            .Select(c => new { c.IdCategoria })
            .SingleOrDefaultAsync(cancellationToken);
        if (categoria is null)
            return new ResultadoCrearSolicitud(EstadoCrearSolicitud.CategoriaInvalida, Campo: "categoria");

        if (datos.IdMarca is > 0)
        {
            var marcaValida = await db.CategoriaMarcas
                .AsNoTracking()
                .AnyAsync(cm => cm.IdCategoria == datos.IdCategoria &&
                    cm.IdMarca == datos.IdMarca && cm.Activa &&
                    cm.IdMarcaNavigation.Activa, cancellationToken);
            if (!marcaValida)
                return new ResultadoCrearSolicitud(EstadoCrearSolicitud.MarcaInvalida, Campo: "marca");
        }

        var estadoBorrador = await db.EstadoSolicituds
            .AsNoTracking()
            .SingleOrDefaultAsync(e => e.Codigo == EstadoBorrador, cancellationToken);
        if (estadoBorrador is null)
            return new ResultadoCrearSolicitud(EstadoCrearSolicitud.RequisitosNoConfigurados);

        var folio = GenerarFolio();
        var ahora = DateTime.UtcNow;
        var producto = new Producto
        {
            IdPropietario = idSolicitante,
            IdCategoria = datos.IdCategoria,
            IdMarca = datos.IdMarca is > 0 ? datos.IdMarca : null,
            Nombre = datos.Nombre!,
            Modelo = datos.Modelo,
            Sku = datos.Sku,
            NumeroSerie = datos.NumeroSerie,
            Imei = datos.Imei,
            Accesorios = datos.Accesorios,
            Descripcion = datos.Descripcion,
            FechaRegistro = ahora,
            Activo = true
        };
        var solicitud = new SolicitudEvaluacion
        {
            Folio = folio,
            IdSolicitante = idSolicitante,
            IdEstadoSolicitud = estadoBorrador.IdEstadoSolicitud,
            ClaveIdempotencia = datos.ClaveIdempotencia,
            FechaSolicitud = ahora,
            FechaUltimoCambio = ahora,
            ObservacionesSolicitante = datos.Observaciones,
            IdProductoNavigation = producto
        };
        db.SolicitudEvaluacions.Add(solicitud);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return new ResultadoCrearSolicitud(
                EstadoCrearSolicitud.Exito,
                new SolicitudCreada(solicitud.IdSolicitud, folio, EstadoBorrador, estadoBorrador.Nombre));
        }
        catch (DbUpdateException ex) when (EsIdempotencia(ex))
        {
            db.ChangeTracker.Clear();
            var recuperada = await db.SolicitudEvaluacions
                .AsNoTracking()
                .Where(s => s.ClaveIdempotencia == datos.ClaveIdempotencia)
                .Select(s => new
                {
                    s.IdSolicitud,
                    s.IdSolicitante,
                    s.Folio,
                    EstadoCodigo = s.IdEstadoSolicitudNavigation.Codigo,
                    EstadoNombre = s.IdEstadoSolicitudNavigation.Nombre
                })
                .SingleOrDefaultAsync(cancellationToken);

            return recuperada is not null && recuperada.IdSolicitante == idSolicitante
                ? new ResultadoCrearSolicitud(
                    EstadoCrearSolicitud.Existente,
                    new SolicitudCreada(recuperada.IdSolicitud, recuperada.Folio,
                        recuperada.EstadoCodigo, recuperada.EstadoNombre))
                : new ResultadoCrearSolicitud(EstadoCrearSolicitud.ConflictoIdempotencia);
        }
    }

    public async Task<ResultadoEnviarSolicitud> EnviarAsync(
        int idSolicitud, int idSolicitante, CancellationToken cancellationToken)
    {
        var solicitud = await db.SolicitudEvaluacions
            .AsNoTracking()
            .Where(s => s.IdSolicitud == idSolicitud && s.IdSolicitante == idSolicitante)
            .Select(s => new
            {
                s.IdSolicitud,
                s.Folio,
                s.IdProductoNavigation.IdCategoria,
                EstadoCodigo = s.IdEstadoSolicitudNavigation.Codigo,
                EstadoNombre = s.IdEstadoSolicitudNavigation.Nombre
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (solicitud is null)
            return new ResultadoEnviarSolicitud(EstadoEnviarSolicitud.NoEncontrada);

        if (solicitud.EstadoCodigo == EstadoPendiente)
            return new ResultadoEnviarSolicitud(
                EstadoEnviarSolicitud.Exito,
                new SolicitudCreada(solicitud.IdSolicitud, solicitud.Folio,
                    solicitud.EstadoCodigo, solicitud.EstadoNombre));

        if (solicitud.EstadoCodigo != EstadoBorrador)
            return new ResultadoEnviarSolicitud(EstadoEnviarSolicitud.EstadoInvalido);

        var requisitos = await db.CategoriaEvidenciaRequisitos
            .AsNoTracking()
            .Where(r => r.IdCategoria == solicitud.IdCategoria && r.Obligatorio && r.CantidadMinima > 0)
            .Select(r => new
            {
                r.IdTipoEvidencia,
                r.IdTipoEvidenciaNavigation.Nombre,
                r.CantidadMinima
            })
            .ToListAsync(cancellationToken);
        if (requisitos.Count == 0)
            return new ResultadoEnviarSolicitud(EstadoEnviarSolicitud.RequisitosNoConfigurados);

        var conteos = await db.Evidencia
            .AsNoTracking()
            .Where(e => e.IdSolicitud == idSolicitud && e.Activa)
            .GroupBy(e => e.IdTipoEvidencia)
            .Select(g => new { g.Key, Cantidad = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Cantidad, cancellationToken);

        var faltantes = requisitos
            .Where(r => conteos.GetValueOrDefault(r.IdTipoEvidencia) < r.CantidadMinima)
            .Select(r => r.Nombre)
            .ToList();
        if (faltantes.Count > 0)
            return new ResultadoEnviarSolicitud(EstadoEnviarSolicitud.EvidenciaFaltante, Faltantes: faltantes);

        var estadoPendiente = await db.EstadoSolicituds
            .AsNoTracking()
            .SingleOrDefaultAsync(e => e.Codigo == EstadoPendiente, cancellationToken);
        var estadoBorrador = await db.EstadoSolicituds
            .AsNoTracking()
            .SingleOrDefaultAsync(e => e.Codigo == EstadoBorrador, cancellationToken);
        if (estadoPendiente is null || estadoBorrador is null)
            return new ResultadoEnviarSolicitud(EstadoEnviarSolicitud.RequisitosNoConfigurados);

        var ahora = DateTime.UtcNow;
        var actualizadas = await db.SolicitudEvaluacions
            .Where(s => s.IdSolicitud == idSolicitud && s.IdSolicitante == idSolicitante &&
                s.IdEstadoSolicitud == estadoBorrador.IdEstadoSolicitud)
            .ExecuteUpdateAsync(update => update
                .SetProperty(s => s.IdEstadoSolicitud, estadoPendiente.IdEstadoSolicitud)
                .SetProperty(s => s.FechaUltimoCambio, ahora), cancellationToken);

        if (actualizadas == 0)
        {
            var estadoActual = await db.SolicitudEvaluacions
                .AsNoTracking()
                .Where(s => s.IdSolicitud == idSolicitud && s.IdSolicitante == idSolicitante)
                .Select(s => s.IdEstadoSolicitudNavigation.Codigo)
                .SingleOrDefaultAsync(cancellationToken);
            if (estadoActual != EstadoPendiente)
                return new ResultadoEnviarSolicitud(EstadoEnviarSolicitud.EstadoInvalido);
        }

        return new ResultadoEnviarSolicitud(
            EstadoEnviarSolicitud.Exito,
            new SolicitudCreada(
                solicitud.IdSolicitud,
                solicitud.Folio,
                EstadoPendiente,
                estadoPendiente.Nombre));
    }

    public async Task<SolicitudListado> ListarAsync(
        int idSolicitante, int pagina, int tamanoPagina, string? estado,
        CancellationToken cancellationToken)
    {
        pagina = Math.Max(1, pagina);
        tamanoPagina = Math.Clamp(tamanoPagina, 1, 50);
        var consulta = db.SolicitudEvaluacions
            .AsNoTracking()
            .Where(s => s.IdSolicitante == idSolicitante);

        if (!string.IsNullOrWhiteSpace(estado))
        {
            var codigo = estado.Trim().ToUpperInvariant();
            consulta = consulta.Where(s => s.IdEstadoSolicitudNavigation.Codigo == codigo);
        }

        var total = await consulta.CountAsync(cancellationToken);
        var filas = await consulta
            .OrderByDescending(s => s.FechaSolicitud)
            .ThenByDescending(s => s.IdSolicitud)
            .Skip((pagina - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .Select(s => new
            {
                s.IdSolicitud,
                s.Folio,
                NombreProducto = s.IdProductoNavigation.Nombre,
                Categoria = s.IdProductoNavigation.IdCategoriaNavigation.Nombre,
                Marca = s.IdProductoNavigation.IdMarcaNavigation != null
                    ? s.IdProductoNavigation.IdMarcaNavigation.Nombre
                    : null,
                EstadoCodigo = s.IdEstadoSolicitudNavigation.Codigo,
                EstadoNombre = s.IdEstadoSolicitudNavigation.Nombre,
                s.FechaSolicitud,
                EvidenciasActivas = s.Evidencia.Count(e => e.Activa)
            })
            .ToListAsync(cancellationToken);

        return new SolicitudListado(
            filas.Select(f => new SolicitudResumen(
                f.IdSolicitud,
                f.Folio,
                f.NombreProducto,
                f.Categoria,
                f.Marca,
                f.EstadoCodigo,
                f.EstadoNombre,
                f.FechaSolicitud,
                f.EvidenciasActivas,
                AccionPendiente(f.EstadoCodigo))).ToList(),
            pagina,
            tamanoPagina,
            total);
    }

    public async Task<SolicitudDetalle?> ObtenerDetalleAsync(
        int idSolicitud, int idSolicitante, CancellationToken cancellationToken)
    {
        var solicitud = await db.SolicitudEvaluacions
            .AsNoTracking()
            .Include(s => s.IdProductoNavigation).ThenInclude(p => p.IdCategoriaNavigation)
            .Include(s => s.IdProductoNavigation).ThenInclude(p => p.IdMarcaNavigation)
            .Include(s => s.IdEstadoSolicitudNavigation)
            .Include(s => s.Evidencia).ThenInclude(e => e.IdTipoEvidenciaNavigation)
            .Include(s => s.AsignacionEspecialistum!).ThenInclude(a => a.IdEspecialistaNavigation)
                .ThenInclude(e => e.IdUsuarioNavigation)
            .Include(s => s.Evaluacion)
            .Where(s => s.IdSolicitud == idSolicitud && s.IdSolicitante == idSolicitante)
            .SingleOrDefaultAsync(cancellationToken);

        if (solicitud is null)
            return null;

        var producto = solicitud.IdProductoNavigation;
        var evidencias = solicitud.Evidencia
            .Where(e => e.Activa)
            .OrderBy(e => e.FechaCarga)
            .Select(e => new EvidenciaDetalle(
                e.IdEvidencia,
                e.IdTipoEvidencia,
                e.IdTipoEvidenciaNavigation.Codigo,
                e.IdTipoEvidenciaNavigation.Nombre,
                e.Descripcion,
                e.NombreOriginal,
                e.TipoContenido,
                e.TamanoBytes,
                e.FechaCarga))
            .ToList();

        var seguimiento = new List<SeguimientoEvento>
        {
            new(solicitud.FechaSolicitud, "Solicitud creada", solicitud.Folio),
            new(solicitud.FechaUltimoCambio, "Estado actual", solicitud.IdEstadoSolicitudNavigation.Nombre)
        };
        var asignacion = solicitud.AsignacionEspecialistum;
        EspecialistaAsignado? especialista = null;
        if (asignacion is not null)
        {
            especialista = new EspecialistaAsignado(
                asignacion.IdEspecialista,
                asignacion.IdEspecialistaNavigation.IdUsuarioNavigation.NombreCompleto);
            seguimiento.Add(new(asignacion.FechaAsignacion, "Especialista asignado", especialista.Nombre));
        }

        EvaluacionResumen? evaluacion = null;
        if (solicitud.Evaluacion is not null)
        {
            evaluacion = new EvaluacionResumen(
                solicitud.Evaluacion.Dictamen, solicitud.Evaluacion.FechaFin);
            seguimiento.Add(new(solicitud.Evaluacion.FechaInicio, "Evaluación iniciada", null));
            if (solicitud.Evaluacion.FechaFin is not null)
                seguimiento.Add(new(solicitud.Evaluacion.FechaFin.Value, "Evaluación finalizada", null));
        }

        return new SolicitudDetalle(
            solicitud.IdSolicitud,
            solicitud.Folio,
            producto.Nombre,
            producto.Modelo,
            producto.Sku,
            producto.NumeroSerie,
            producto.Imei,
            producto.Accesorios,
            producto.Descripcion,
            solicitud.ObservacionesSolicitante,
            producto.IdCategoria,
            producto.IdCategoriaNavigation.Nombre,
            producto.IdMarca,
            producto.IdMarcaNavigation?.Nombre,
            solicitud.IdEstadoSolicitudNavigation.Codigo,
            solicitud.IdEstadoSolicitudNavigation.Nombre,
            solicitud.IdEstadoSolicitudNavigation.Codigo == EstadoBorrador,
            solicitud.FechaSolicitud,
            solicitud.FechaUltimoCambio,
            AccionPendiente(solicitud.IdEstadoSolicitudNavigation.Codigo),
            evidencias,
            seguimiento.OrderBy(e => e.Fecha).ToList(),
            especialista,
            evaluacion,
            CertificadoDisponible: false);
    }

    public async Task<ResultadoAgregarEvidencia> AgregarEvidenciaAsync(
        int idSolicitud,
        int idSolicitante,
        int idTipoEvidencia,
        string claveAlmacenamiento,
        string nombreOriginal,
        string tipoContenido,
        long tamanoBytes,
        string hashSha256,
        string? descripcion,
        CancellationToken cancellationToken)
    {
        var solicitud = await db.SolicitudEvaluacions
            .Where(s => s.IdSolicitud == idSolicitud && s.IdSolicitante == idSolicitante)
            .Select(s => new
            {
                s.IdSolicitud,
                s.IdProductoNavigation.IdCategoria,
                EstadoCodigo = s.IdEstadoSolicitudNavigation.Codigo
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (solicitud is null)
            return new ResultadoAgregarEvidencia(EstadoAgregarEvidencia.NoEncontrada);
        if (solicitud.EstadoCodigo != EstadoBorrador)
            return new ResultadoAgregarEvidencia(EstadoAgregarEvidencia.EstadoInvalido);

        var totalActivas = await db.Evidencia
            .AsNoTracking()
            .CountAsync(e => e.IdSolicitud == idSolicitud && e.Activa, cancellationToken);
        if (totalActivas >= MaxEvidenciasPorSolicitud)
            return new ResultadoAgregarEvidencia(EstadoAgregarEvidencia.LimiteAlcanzado);

        var requisito = await db.CategoriaEvidenciaRequisitos
            .AsNoTracking()
            .Include(r => r.IdTipoEvidenciaNavigation)
            .Where(r => r.IdCategoria == solicitud.IdCategoria && r.IdTipoEvidencia == idTipoEvidencia)
            .SingleOrDefaultAsync(cancellationToken);
        if (requisito is null)
            return new ResultadoAgregarEvidencia(EstadoAgregarEvidencia.TipoNoPermitido);

        var existentes = await db.Evidencia
            .Where(e => e.IdSolicitud == idSolicitud && e.IdTipoEvidencia == idTipoEvidencia && e.Activa)
            .OrderBy(e => e.FechaCarga)
            .ToListAsync(cancellationToken);

        string? claveAnterior = null;
        var maximo = Math.Max(1, requisito.CantidadMaxima);
        if (existentes.Count >= maximo)
        {
            if (maximo > 1)
                return new ResultadoAgregarEvidencia(EstadoAgregarEvidencia.LimiteAlcanzado);

            var anterior = existentes[0];
            anterior.Activa = false;
            claveAnterior = anterior.UrlArchivo;
        }

        var evidencia = new Evidencium
        {
            IdSolicitud = idSolicitud,
            IdTipoEvidencia = idTipoEvidencia,
            UrlArchivo = claveAlmacenamiento,
            NombreOriginal = nombreOriginal,
            TipoContenido = tipoContenido,
            TamanoBytes = tamanoBytes,
            HashSha256 = hashSha256,
            Descripcion = descripcion,
            FechaCarga = DateTime.UtcNow,
            Activa = true
        };
        db.Evidencia.Add(evidencia);
        await db.SaveChangesAsync(cancellationToken);

        return new ResultadoAgregarEvidencia(
            EstadoAgregarEvidencia.Exito,
            new EvidenciaCreada(
                evidencia.IdEvidencia,
                evidencia.IdTipoEvidencia,
                requisito.IdTipoEvidenciaNavigation.Codigo,
                requisito.IdTipoEvidenciaNavigation.Nombre,
                evidencia.Descripcion,
                evidencia.NombreOriginal,
                evidencia.TipoContenido,
                evidencia.TamanoBytes,
                evidencia.FechaCarga),
            claveAnterior);
    }

    public async Task<ArchivoEvidencia?> ObtenerArchivoAsync(
        int idSolicitud, int idEvidencia, int idSolicitante, CancellationToken cancellationToken)
    {
        var archivo = await db.Evidencia
            .AsNoTracking()
            .Where(e => e.IdEvidencia == idEvidencia && e.IdSolicitud == idSolicitud &&
                e.Activa && e.IdSolicitudNavigation.IdSolicitante == idSolicitante)
            .Select(e => new ArchivoEvidencia(e.UrlArchivo, e.TipoContenido, e.NombreOriginal))
            .SingleOrDefaultAsync(cancellationToken);
        return archivo;
    }

    private static string GenerarFolio() =>
        $"SOL-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";

    private static string? AccionPendiente(string estado) => estado switch
    {
        EstadoBorrador => "Completa la información y envía la solicitud.",
        "FALTA_EVIDENCIA" => "Adjunta la evidencia solicitada.",
        EstadoPendiente => "Espera la asignación de un especialista.",
        "ASIGNADA" or "EN_EVALUACION" => "La evaluación está en curso.",
        "FINALIZADA" => null,
        _ => "Consulta el estado de tu solicitud."
    };

    private static bool EsIdempotencia(DbUpdateException exception) =>
        exception.InnerException is SqlException sql &&
        sql.Errors.Cast<SqlError>().Any(error =>
            error.Number is 2601 or 2627 &&
            error.Message.Contains("UQ_Solicitud_Idempotencia", StringComparison.Ordinal));
}

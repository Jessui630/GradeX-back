using GradeX.Application.Certificados;
using GradeX.Application.Workflow;
using GradeX.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace GradeX.Infrastructure.Persistence.Commands;

public sealed class CertificadoStore(
    GradeXDbContext db,
    NotificacionWriter notificaciones) : ICertificadoStore
{
    public async Task<ResultadoEmitirCertificado> EmitirAsync(
        int idSolicitud, int idUsuarioEspecialista,
        CancellationToken cancellationToken)
    {
        var especialista = await db.Especialista
            .AsNoTracking()
            .Where(e => e.IdUsuario == idUsuarioEspecialista &&
                e.Estado == FlujoGradeX.EstadosEspecialista.Activo &&
                e.IdUsuarioNavigation.Activo)
            .Select(e => new { e.IdEspecialista })
            .SingleOrDefaultAsync(cancellationToken);
        if (especialista is null)
            return new ResultadoEmitirCertificado(
                EstadoOperacionCertificado.NoAutorizado);

        var solicitud = await db.SolicitudEvaluacions
            .Include(s => s.IdProductoNavigation).ThenInclude(p => p.IdCategoriaNavigation)
            .Include(s => s.IdProductoNavigation).ThenInclude(p => p.IdMarcaNavigation)
            .Include(s => s.Evaluacion).ThenInclude(e => e!.ResultadoCriterios)
            .Include(s => s.Evaluacion).ThenInclude(e => e!.Certificado)
                .ThenInclude(c => c!.IdEstadoCertificadoNavigation)
            .Include(s => s.Evaluacion).ThenInclude(e => e!.Certificado)
                .ThenInclude(c => c!.IdEspecialidadNavigation)
                .ThenInclude(e => e!.IdCategoriaNavigation)
            .Include(s => s.Evaluacion).ThenInclude(e => e!.Certificado)
                .ThenInclude(c => c!.IdEspecialidadNavigation)
                .ThenInclude(e => e!.CategoriaMarca)
                .ThenInclude(cm => cm!.IdMarcaNavigation)
            .Include(s => s.Evaluacion).ThenInclude(e => e!.IdEspecialistaNavigation)
                .ThenInclude(e => e.IdUsuarioNavigation)
            .Include(s => s.AsignacionEspecialistum)
            .Where(s => s.IdSolicitud == idSolicitud &&
                s.AsignacionEspecialistum != null &&
                s.AsignacionEspecialistum.IdEspecialista == especialista.IdEspecialista &&
                s.AsignacionEspecialistum.Activa)
            .SingleOrDefaultAsync(cancellationToken);

        if (solicitud is null)
            return new ResultadoEmitirCertificado(
                EstadoOperacionCertificado.NoEncontrado);

        if (solicitud.Evaluacion is null)
            return new ResultadoEmitirCertificado(
                EstadoOperacionCertificado.NoElegible,
                Detalle: "La solicitud aún no tiene una evaluación.");

        if (solicitud.Evaluacion.FechaFin is null ||
            string.IsNullOrWhiteSpace(solicitud.Evaluacion.Dictamen))
            return new ResultadoEmitirCertificado(
                EstadoOperacionCertificado.NoElegible,
                Detalle: "La evaluación debe estar finalizada con dictamen.");

        if (solicitud.Evaluacion.Certificado is not null)
            return new ResultadoEmitirCertificado(
                EstadoOperacionCertificado.YaExiste,
                Mapear(solicitud, solicitud.Evaluacion.Certificado));

        var estado = await db.EstadoCertificados
            .AsNoTracking()
            .SingleOrDefaultAsync(
                e => e.Codigo == FlujoGradeX.EstadosCertificado.Emitido,
                cancellationToken);
        if (estado is null)
            return new ResultadoEmitirCertificado(
                EstadoOperacionCertificado.EstadoNoConfigurado);

        // La especialidad se toma del área que habilitó la asignación. Se conserva
        // aunque el administrador la haya desactivado después, para no reescribir
        // el histórico del certificado.
        var especialidad = await db.EspecialidadEspecialista
            .AsNoTracking()
            .Include(e => e.IdCategoriaNavigation)
            .Include(e => e.CategoriaMarca).ThenInclude(cm => cm!.IdMarcaNavigation)
            .Where(e => e.IdEspecialista == especialista.IdEspecialista &&
                e.IdCategoria == solicitud.IdProductoNavigation.IdCategoria)
            .OrderByDescending(e => e.Activa)
            .ThenByDescending(e => e.IdMarca == solicitud.IdProductoNavigation.IdMarca)
            .FirstOrDefaultAsync(cancellationToken);
        if (especialidad is null)
            return new ResultadoEmitirCertificado(
                EstadoOperacionCertificado.NoElegible,
                Detalle: "El especialista no tiene un área de especialidad registrada para la categoría.");

        var ahora = DateTime.UtcNow;
        var certificado = new Certificado
        {
            IdEvaluacion = solicitud.Evaluacion.IdEvaluacion,
            IdEstadoCertificado = estado.IdEstadoCertificado,
            IdEspecialidad = especialidad.IdEspecialidad,
            NumeroCertificado = GenerarNumero(ahora),
            QrToken = Guid.NewGuid(),
            FechaEmision = ahora,
            FechaVencimiento = null
        };
        db.Certificados.Add(certificado);
        await notificaciones.CrearAsync(
            solicitud.IdSolicitante,
            solicitud.IdSolicitud,
            FlujoGradeX.TiposNotificacion.CertificadoEmitido,
            "Certificado emitido",
            $"El certificado {certificado.NumeroCertificado} de la solicitud {solicitud.Folio} ya está disponible.",
            cancellationToken);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (EsDuplicado(ex))
        {
            db.ChangeTracker.Clear();
            return new ResultadoEmitirCertificado(
                EstadoOperacionCertificado.YaExiste,
                Detalle: "Ya existe un certificado para esta evaluación.");
        }

        certificado.IdEstadoCertificadoNavigation = estado;
        certificado.IdEspecialidadNavigation = especialidad;
        return new ResultadoEmitirCertificado(
            EstadoOperacionCertificado.Exito,
            Mapear(solicitud, certificado));
    }

    public async Task<CertificadoEmitido?> ObtenerPorSolicitudAsync(
        int idSolicitud, int idUsuarioSolicitante,
        CancellationToken cancellationToken)
    {
        var solicitud = await db.SolicitudEvaluacions
            .AsNoTracking()
            .Include(s => s.IdProductoNavigation).ThenInclude(p => p.IdCategoriaNavigation)
            .Include(s => s.IdProductoNavigation).ThenInclude(p => p.IdMarcaNavigation)
            .Include(s => s.Evaluacion).ThenInclude(e => e!.Certificado)
                .ThenInclude(c => c!.IdEstadoCertificadoNavigation)
            .Include(s => s.Evaluacion).ThenInclude(e => e!.Certificado)
                .ThenInclude(c => c!.IdEspecialidadNavigation)
                .ThenInclude(e => e!.IdCategoriaNavigation)
            .Include(s => s.Evaluacion).ThenInclude(e => e!.Certificado)
                .ThenInclude(c => c!.IdEspecialidadNavigation)
                .ThenInclude(e => e!.CategoriaMarca)
                .ThenInclude(cm => cm!.IdMarcaNavigation)
            .Include(s => s.Evaluacion).ThenInclude(e => e!.IdEspecialistaNavigation)
                .ThenInclude(e => e.IdUsuarioNavigation)
            .Where(s => s.IdSolicitud == idSolicitud &&
                s.IdSolicitante == idUsuarioSolicitante)
            .SingleOrDefaultAsync(cancellationToken);

        if (solicitud?.Evaluacion?.Certificado is null)
            return null;

        return Mapear(solicitud, solicitud.Evaluacion.Certificado);
    }

    public async Task<IReadOnlyList<CertificadoEmitido>> ListarPorSolicitanteAsync(
        int idUsuarioSolicitante, CancellationToken cancellationToken)
    {
        var certificados = await db.Certificados
            .AsNoTracking()
            .Include(c => c.IdEstadoCertificadoNavigation)
            .Include(c => c.IdEspecialidadNavigation)
                .ThenInclude(e => e!.IdCategoriaNavigation)
            .Include(c => c.IdEspecialidadNavigation)
                .ThenInclude(e => e!.CategoriaMarca)
                .ThenInclude(cm => cm!.IdMarcaNavigation)
            .Include(c => c.IdEvaluacionNavigation)
                .ThenInclude(e => e.IdSolicitudNavigation)
                .ThenInclude(s => s.IdProductoNavigation)
                .ThenInclude(p => p.IdCategoriaNavigation)
            .Include(c => c.IdEvaluacionNavigation)
                .ThenInclude(e => e.IdSolicitudNavigation)
                .ThenInclude(s => s.IdProductoNavigation)
                .ThenInclude(p => p.IdMarcaNavigation)
            .Include(c => c.IdEvaluacionNavigation)
                .ThenInclude(e => e.IdEspecialistaNavigation)
                .ThenInclude(e => e.IdUsuarioNavigation)
            .Where(c => c.IdEvaluacionNavigation.IdSolicitudNavigation.IdSolicitante ==
                idUsuarioSolicitante)
            .OrderByDescending(c => c.FechaEmision)
            .ThenByDescending(c => c.IdCertificado)
            .ToListAsync(cancellationToken);

        return certificados
            .Select(c => Mapear(c.IdEvaluacionNavigation.IdSolicitudNavigation, c))
            .ToList();
    }

    public async Task<CertificadoPublico?> VerificarAsync(
        Guid token, CancellationToken cancellationToken)
    {
        var certificado = await db.Certificados
            .AsNoTracking()
            .Include(c => c.IdEstadoCertificadoNavigation)
            .Include(c => c.IdEvaluacionNavigation)
                .ThenInclude(e => e.IdSolicitudNavigation)
                .ThenInclude(s => s.IdProductoNavigation)
                .ThenInclude(p => p.IdCategoriaNavigation)
            .Include(c => c.IdEvaluacionNavigation)
                .ThenInclude(e => e.IdSolicitudNavigation)
                .ThenInclude(s => s.IdProductoNavigation)
                .ThenInclude(p => p.IdMarcaNavigation)
            .Include(c => c.IdEvaluacionNavigation)
                .ThenInclude(e => e.IdEspecialistaNavigation)
                .ThenInclude(e => e.IdUsuarioNavigation)
            .Include(c => c.IdEspecialidadNavigation)
                .ThenInclude(e => e!.IdCategoriaNavigation)
            .Include(c => c.IdEspecialidadNavigation)
                .ThenInclude(e => e!.CategoriaMarca)
                .ThenInclude(cm => cm!.IdMarcaNavigation)
            .Where(c => c.QrToken == token)
            .SingleOrDefaultAsync(cancellationToken);

        return certificateToPublic(certificado);
    }

    public async Task<CertificadoPublico?> VerificarPorNumeroAsync(
        string numeroCertificado, CancellationToken cancellationToken)
    {
        var numero = numeroCertificado?.Trim();
        if (string.IsNullOrWhiteSpace(numero))
            return null;

        var certificado = await db.Certificados
            .AsNoTracking()
            .Include(c => c.IdEstadoCertificadoNavigation)
            .Include(c => c.IdEvaluacionNavigation)
                .ThenInclude(e => e.IdSolicitudNavigation)
                .ThenInclude(s => s.IdProductoNavigation)
                .ThenInclude(p => p.IdCategoriaNavigation)
            .Include(c => c.IdEvaluacionNavigation)
                .ThenInclude(e => e.IdSolicitudNavigation)
                .ThenInclude(s => s.IdProductoNavigation)
                .ThenInclude(p => p.IdMarcaNavigation)
            .Include(c => c.IdEvaluacionNavigation)
                .ThenInclude(e => e.IdEspecialistaNavigation)
                .ThenInclude(e => e.IdUsuarioNavigation)
            .Include(c => c.IdEspecialidadNavigation)
                .ThenInclude(e => e!.IdCategoriaNavigation)
            .Include(c => c.IdEspecialidadNavigation)
                .ThenInclude(e => e!.CategoriaMarca)
                .ThenInclude(cm => cm!.IdMarcaNavigation)
            .Where(c => c.NumeroCertificado == numero)
            .SingleOrDefaultAsync(cancellationToken);

        return certificateToPublic(certificado);
    }

    private static CertificadoPublico? certificateToPublic(Certificado? certificado)
    {
        if (certificado is null || certificado.IdEvaluacionNavigation is null)
            return null;
        var evaluacion = certificado.IdEvaluacionNavigation;
        var producto = evaluacion.IdSolicitudNavigation.IdProductoNavigation;
        return new CertificadoPublico(
            certificado.NumeroCertificado,
            certificado.IdEstadoCertificadoNavigation.Codigo,
            certificado.IdEstadoCertificadoNavigation.Nombre,
            producto.Nombre,
            producto.Modelo,
            producto.IdCategoriaNavigation.Nombre,
            producto.IdMarcaNavigation?.Nombre,
            evaluacion.Dictamen,
            evaluacion.ConditionGrade,
            certificado.FechaEmision,
            certificado.FechaVencimiento,
            evaluacion.IdEspecialistaNavigation?.IdUsuarioNavigation?.NombreCompleto,
            NombreEspecialidad(certificado.IdEspecialidadNavigation));
    }

    private static CertificadoEmitido Mapear(
        SolicitudEvaluacion solicitud, Certificado certificado)
    {
        var evaluacion = solicitud.Evaluacion ?? certificado.IdEvaluacionNavigation;
        return new(
            certificado.IdCertificado,
            solicitud.IdSolicitud,
            solicitud.Folio,
            certificado.NumeroCertificado,
            certificado.QrToken,
            certificado.FechaEmision,
            certificado.FechaVencimiento,
            certificado.IdEstadoCertificadoNavigation.Codigo,
            certificado.IdEstadoCertificadoNavigation.Nombre,
            solicitud.IdProductoNavigation.Nombre,
            solicitud.IdProductoNavigation.Modelo,
            solicitud.IdProductoNavigation.IdCategoriaNavigation.Nombre,
            solicitud.IdProductoNavigation.IdMarcaNavigation?.Nombre,
            evaluacion?.Dictamen,
            evaluacion?.ConditionGrade,
            evaluacion?.IdEspecialistaNavigation?.IdUsuarioNavigation?.NombreCompleto,
            NombreEspecialidad(certificado.IdEspecialidadNavigation));
    }

    private static string? NombreEspecialidad(EspecialidadEspecialistum? especialidad)
    {
        if (especialidad is null)
            return null;
        var categoria = especialidad.IdCategoriaNavigation?.Nombre;
        var marca = especialidad.CategoriaMarca?.IdMarcaNavigation?.Nombre;
        return string.IsNullOrWhiteSpace(marca)
            ? categoria
            : string.IsNullOrWhiteSpace(categoria) ? marca : $"{categoria} / {marca}";
    }

    private static bool EsDuplicado(DbUpdateException exception) =>
        exception.InnerException is Microsoft.Data.SqlClient.SqlException sql &&
        sql.Errors.Cast<Microsoft.Data.SqlClient.SqlError>()
            .Any(error => error.Number is 2601 or 2627);

    private static string GenerarNumero(DateTime ahora) =>
        $"GX-{ahora:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
}

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

        var criteriosObligatorios = await db.CriterioEvaluacions
            .AsNoTracking()
            .Where(c => c.IdCategoria == solicitud.IdProductoNavigation.IdCategoria &&
                c.Activo && c.Obligatorio)
            .Select(c => c.IdCriterio)
            .ToListAsync(cancellationToken);
        var respondidos = solicitud.Evaluacion.ResultadoCriterios
            .Where(TieneValor)
            .Select(r => r.IdCriterio)
            .ToHashSet();
        if (criteriosObligatorios.Any(c => !respondidos.Contains(c)))
            return new ResultadoEmitirCertificado(
                EstadoOperacionCertificado.NoElegible,
                Detalle: "Faltan criterios obligatorios por responder.");

        var estado = await db.EstadoCertificados
            .AsNoTracking()
            .SingleOrDefaultAsync(
                e => e.Codigo == FlujoGradeX.EstadosCertificado.Emitido,
                cancellationToken);
        if (estado is null)
            return new ResultadoEmitirCertificado(
                EstadoOperacionCertificado.EstadoNoConfigurado);

        var idEspecialidad = await db.EspecialidadEspecialista
            .AsNoTracking()
            .Where(e => e.IdEspecialista == especialista.IdEspecialista &&
                e.IdCategoria == solicitud.IdProductoNavigation.IdCategoria &&
                e.Activa)
            .OrderByDescending(e => e.IdMarca == solicitud.IdProductoNavigation.IdMarca)
            .Select(e => (int?)e.IdEspecialidad)
            .FirstOrDefaultAsync(cancellationToken);

        var ahora = DateTime.UtcNow;
        var certificado = new Certificado
        {
            IdEvaluacion = solicitud.Evaluacion.IdEvaluacion,
            IdEstadoCertificado = estado.IdEstadoCertificado,
            IdEspecialidad = idEspecialidad,
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
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return new ResultadoEmitirCertificado(
                EstadoOperacionCertificado.YaExiste,
                Detalle: "Ya existe un certificado para esta evaluación.");
        }

        certificado.IdEstadoCertificadoNavigation = estado;
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
            .Include(s => s.Evaluacion).ThenInclude(e => e!.IdEspecialistaNavigation)
                .ThenInclude(e => e.IdUsuarioNavigation)
            .Where(s => s.IdSolicitud == idSolicitud &&
                s.IdSolicitante == idUsuarioSolicitante)
            .SingleOrDefaultAsync(cancellationToken);

        if (solicitud?.Evaluacion?.Certificado is null)
            return null;

        return Mapear(solicitud, solicitud.Evaluacion.Certificado);
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
            producto.IdCategoriaNavigation.Nombre,
            producto.IdMarcaNavigation?.Nombre,
            evaluacion.Dictamen,
            certificado.FechaEmision,
            certificado.FechaVencimiento,
            evaluacion.IdEspecialistaNavigation?.IdUsuarioNavigation?.NombreCompleto);
    }

    private static CertificadoEmitido Mapear(
        SolicitudEvaluacion solicitud, Certificado certificado) =>
        new(
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
            solicitud.IdProductoNavigation.IdCategoriaNavigation.Nombre,
            solicitud.IdProductoNavigation.IdMarcaNavigation?.Nombre,
            solicitud.Evaluacion?.Dictamen,
            solicitud.Evaluacion?.IdEspecialistaNavigation?.IdUsuarioNavigation?.NombreCompleto);

    private static bool TieneValor(ResultadoCriterio resultado) =>
        !string.IsNullOrWhiteSpace(resultado.ValorTexto) ||
        resultado.ValorNumerico is not null ||
        resultado.ValorBooleano is not null;

    private static string GenerarNumero(DateTime ahora) =>
        $"GX-{ahora:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
}

using GradeX.Application.Notificaciones;
using GradeX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GradeX.Infrastructure.Persistence.Queries;

public sealed class NotificacionStore(GradeXDbContext db) : INotificacionStore
{
    public async Task<ListadoNotificaciones> ListarAsync(
        int idUsuario, int pagina, int tamanoPagina, bool soloNoLeidas,
        CancellationToken cancellationToken)
    {
        pagina = Math.Max(1, pagina);
        tamanoPagina = Math.Clamp(tamanoPagina, 1, 50);

        var consulta = db.Notificacions
            .AsNoTracking()
            .Where(n => n.IdUsuario == idUsuario);
        if (soloNoLeidas)
            consulta = consulta.Where(n => !n.Leida);

        var total = await consulta.CountAsync(cancellationToken);
        var noLeidas = await db.Notificacions
            .AsNoTracking()
            .CountAsync(n => n.IdUsuario == idUsuario && !n.Leida, cancellationToken);
        var filas = await consulta
            .OrderByDescending(n => n.FechaCreacion)
            .ThenByDescending(n => n.IdNotificacion)
            .Skip((pagina - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .Select(n => new NotificacionResumen(
                n.IdNotificacion,
                n.IdSolicitud,
                n.IdTipoNotificacionNavigation.Codigo,
                n.Titulo,
                n.Mensaje,
                n.Leida,
                n.FechaCreacion,
                n.FechaLectura))
            .ToListAsync(cancellationToken);

        return new ListadoNotificaciones(filas, pagina, tamanoPagina, total, noLeidas);
    }

    public async Task<bool> MarcarLeidaAsync(
        long idNotificacion, int idUsuario, CancellationToken cancellationToken)
    {
        var actualizadas = await db.Notificacions
            .Where(n => n.IdNotificacion == idNotificacion && n.IdUsuario == idUsuario)
            .ExecuteUpdateAsync(update => update
                .SetProperty(n => n.Leida, true)
                .SetProperty(n => n.FechaLectura, DateTime.UtcNow), cancellationToken);
        return actualizadas > 0;
    }

    public async Task<int> MarcarTodasLeidasAsync(
        int idUsuario, CancellationToken cancellationToken) =>
        await db.Notificacions
            .Where(n => n.IdUsuario == idUsuario && !n.Leida)
            .ExecuteUpdateAsync(update => update
                .SetProperty(n => n.Leida, true)
                .SetProperty(n => n.FechaLectura, DateTime.UtcNow), cancellationToken);

    public async Task<int> ContarNoLeidasAsync(
        int idUsuario, CancellationToken cancellationToken) =>
        await db.Notificacions
            .AsNoTracking()
            .CountAsync(n => n.IdUsuario == idUsuario && !n.Leida, cancellationToken);
}

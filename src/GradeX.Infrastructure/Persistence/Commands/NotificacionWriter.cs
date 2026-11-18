using GradeX.Application.Workflow;
using GradeX.Infrastructure.Persistence;
using GradeX.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace GradeX.Infrastructure.Persistence.Commands;

/// <summary>
/// Crea avisos dentro de la misma unidad de trabajo que el cambio de estado.
/// Si el usuario desactivó las notificaciones, el cambio de negocio continúa.
/// </summary>
public sealed class NotificacionWriter(GradeXDbContext db)
{
    public async Task CrearAsync(
        int idUsuario,
        int? idSolicitud,
        string codigoTipo,
        string titulo,
        string mensaje,
        CancellationToken cancellationToken)
    {
        var usuario = await db.Usuarios
            .AsNoTracking()
            .Where(u => u.IdUsuario == idUsuario)
            .Select(u => new { u.Activo, u.RecibirNotificaciones })
            .SingleOrDefaultAsync(cancellationToken);
        if (usuario is null || !usuario.Activo || !usuario.RecibirNotificaciones)
            return;

        var idTipo = await db.TipoNotificacions
            .AsNoTracking()
            .Where(t => t.Codigo == codigoTipo)
            .Select(t => (int?)t.IdTipoNotificacion)
            .SingleOrDefaultAsync(cancellationToken);
        if (idTipo is null)
            return;

        db.Notificacions.Add(new Notificacion
        {
            IdUsuario = idUsuario,
            IdSolicitud = idSolicitud,
            IdTipoNotificacion = idTipo.Value,
            Titulo = Limitar(titulo, 150),
            Mensaje = Limitar(mensaje, 600),
            Leida = false,
            FechaCreacion = DateTime.UtcNow
        });
    }

    private static string Limitar(string valor, int maximo) =>
        valor.Length <= maximo ? valor : valor[..maximo];
}

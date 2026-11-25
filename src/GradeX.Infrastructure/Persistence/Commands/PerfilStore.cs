using GradeX.Application.Perfil;
using GradeX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GradeX.Infrastructure.Persistence.Commands;

public sealed class PerfilStore(GradeXDbContext db) : IPerfilStore
{
    public async Task<PerfilUsuario?> ObtenerAsync(
        int idUsuario, CancellationToken cancellationToken) =>
        await db.Usuarios
            .AsNoTracking()
            .Where(u => u.IdUsuario == idUsuario && u.Activo)
            .Select(u => new PerfilUsuario(
                u.IdUsuario,
                u.NombreCompleto,
                u.Correo,
                u.RecibirNotificaciones,
                u.UsuarioRols
                    .Where(ur => ur.IdRolNavigation.Activo)
                    .Select(ur => ur.IdRolNavigation.Nombre)
                    .ToList(),
                u.FechaCreacion))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<PerfilUsuario?> ActualizarAsync(
        int idUsuario, string nombreCompleto, bool recibirNotificaciones,
        CancellationToken cancellationToken)
    {
        var usuario = await db.Usuarios
            .SingleOrDefaultAsync(u => u.IdUsuario == idUsuario && u.Activo, cancellationToken);
        if (usuario is null)
            return null;

        usuario.NombreCompleto = nombreCompleto;
        usuario.RecibirNotificaciones = recibirNotificaciones;
        usuario.FechaActualizacion = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return await ObtenerAsync(idUsuario, cancellationToken);
    }

    public async Task<string?> ObtenerHashAsync(
        int idUsuario, CancellationToken cancellationToken) =>
        await db.Usuarios
            .AsNoTracking()
            .Where(u => u.IdUsuario == idUsuario && u.Activo)
            .Select(u => u.PasswordHash)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<bool> ActualizarContrasenaAsync(
        int idUsuario, string passwordHash, CancellationToken cancellationToken)
    {
        // El cambio de contraseña y la revocación de todas las sesiones deben
        // ser atómicos: si falla la revocación, no se deja una contraseña nueva
        // con sesiones antiguas todavía válidas.
        await using var transaccion = await db.Database.BeginTransactionAsync(cancellationToken);
        var actualizadas = await db.Usuarios
            .Where(u => u.IdUsuario == idUsuario && u.Activo)
            .ExecuteUpdateAsync(update => update
                .SetProperty(u => u.PasswordHash, passwordHash)
                .SetProperty(u => u.FechaActualizacion, DateTime.UtcNow),
                cancellationToken);
        if (actualizadas > 0)
        {
            var ahora = DateTime.UtcNow;
            await db.SesionUsuarios
                .Where(s => s.IdUsuario == idUsuario && s.FechaRevocacion == null)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(s => s.FechaRevocacion, ahora),
                    cancellationToken);
        }
        await transaccion.CommitAsync(cancellationToken);
        return actualizadas > 0;
    }
}

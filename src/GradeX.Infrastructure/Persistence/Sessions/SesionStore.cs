using System.Security.Cryptography;
using System.Text;
using GradeX.Application.Cuentas;
using GradeX.Application.Sesiones;
using GradeX.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace GradeX.Infrastructure.Persistence.Sessions;

public sealed class SesionStore(GradeXDbContext db) : ISesionStore
{
    public async Task<SesionActiva> CrearAsync(
        UsuarioAutenticado usuario, TimeSpan duracion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(usuario);
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var ahora = DateTime.UtcNow;
        var sesion = new SesionUsuario
        {
            IdSesion = Guid.NewGuid(),
            IdUsuario = usuario.IdUsuario,
            TokenHash = Hash(token),
            FechaCreacion = ahora,
            FechaExpiracion = ahora.Add(duracion)
        };
        db.SesionUsuarios.Add(sesion);
        await db.SaveChangesAsync(cancellationToken);

        var antiguas = ahora.AddDays(-1);
        await db.SesionUsuarios
            .Where(s => s.FechaExpiracion < antiguas ||
                (s.FechaRevocacion != null && s.FechaRevocacion < antiguas))
            .ExecuteDeleteAsync(cancellationToken);

        return new SesionActiva(
            sesion.IdSesion,
            usuario.IdUsuario,
            usuario.NombreCompleto,
            usuario.Correo,
            usuario.Roles,
            new DateTimeOffset(sesion.FechaExpiracion, TimeSpan.Zero),
            token);
    }

    public async Task<SesionActiva?> ObtenerAsync(string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(token))
            return null;

        var hash = Hash(token);
        var ahora = DateTime.UtcNow;
        var sesion = await db.SesionUsuarios
            .AsNoTracking()
            .Where(s => s.TokenHash == hash && s.FechaRevocacion == null && s.FechaExpiracion > ahora)
            .Select(s => new
            {
                s.IdSesion,
                s.IdUsuario,
                s.IdUsuarioNavigation.NombreCompleto,
                s.IdUsuarioNavigation.Correo,
                Roles = s.IdUsuarioNavigation.UsuarioRols
                    .Where(ur => ur.IdRolNavigation.Activo)
                    .Select(ur => ur.IdRolNavigation.Nombre)
                    .ToList(),
                s.FechaExpiracion
            })
            .SingleOrDefaultAsync(cancellationToken);

        return sesion is null
            ? null
            : new SesionActiva(
                sesion.IdSesion,
                sesion.IdUsuario,
                sesion.NombreCompleto,
                sesion.Correo,
                sesion.Roles,
                new DateTimeOffset(sesion.FechaExpiracion, TimeSpan.Zero),
                token);
    }

    public async Task EliminarAsync(string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(token))
            return;

        var hash = Hash(token);
        var ahora = DateTime.UtcNow;
        await db.SesionUsuarios
            .Where(s => s.TokenHash == hash && s.FechaRevocacion == null)
            .ExecuteUpdateAsync(update => update.SetProperty(s => s.FechaRevocacion, ahora), cancellationToken);
    }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

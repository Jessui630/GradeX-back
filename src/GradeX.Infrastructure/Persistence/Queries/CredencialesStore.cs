using GradeX.Application.Cuentas;
using GradeX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GradeX.Infrastructure.Persistence.Queries;

public sealed class CredencialesStore(GradeXDbContext db) : ICredencialesStore
{
    public async Task<CredencialesUsuario?> BuscarPorCorreoAsync(
        string correo, CancellationToken cancellationToken)
    {
        var datos = await db.Usuarios
            .AsNoTracking()
            .Where(u => u.Correo == correo)
            .Select(u => new
            {
                u.IdUsuario,
                u.NombreCompleto,
                u.Correo,
                u.PasswordHash,
                u.Activo,
                Roles = u.UsuarioRols
                    .Where(ur => ur.IdRolNavigation.Activo)
                    .Select(ur => ur.IdRolNavigation.Nombre)
                    .ToList()
            })
            .SingleOrDefaultAsync(cancellationToken);

        return datos is null
            ? null
            : new CredencialesUsuario(
                datos.IdUsuario,
                datos.NombreCompleto,
                datos.Correo,
                datos.PasswordHash,
                datos.Activo,
                datos.Roles);
    }
}

using GradeX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GradeX.Tests;

public sealed class RolesOperativosSqlTests
{
    [LocalDbFact]
    public async Task RolesBaseEstanActivosEnLaBaseAislada()
    {
        await using var db = await SqlIntegration.AbrirContextoAsync();

        var roles = await db.Rols.AsNoTracking()
            .Where(r => r.Nombre == "Solicitante" ||
                r.Nombre == "Especialista" ||
                r.Nombre == "Administrador")
            .OrderBy(r => r.Nombre)
            .Select(r => new { r.Nombre, r.Activo })
            .ToListAsync();

        Assert.Equal(3, roles.Count);
        Assert.All(roles, rol => Assert.True(rol.Activo));
        Assert.Contains(roles, rol => rol.Nombre == "Solicitante");
        Assert.Contains(roles, rol => rol.Nombre == "Especialista");
        Assert.Contains(roles, rol => rol.Nombre == "Administrador");
    }
}

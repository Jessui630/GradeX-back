using GradeX.Application.Cuentas;
using GradeX.Infrastructure.Persistence.Sessions;
using Microsoft.EntityFrameworkCore;

namespace GradeX.Tests;

public sealed class SesionStoreSqlTests
{
    [LocalDbFact]
    public async Task SesionPersisteSeComparteYSeRevocaEntreInstancias()
    {
        var correo = $"sesion-{Guid.NewGuid():N}@example.test";
        await using var primeraBase = await SqlIntegration.AbrirContextoAsync();
        var idUsuario = await SqlIntegration.CrearUsuarioAsync(primeraBase, correo);
        try
        {
            var usuario = new UsuarioAutenticado(idUsuario, "Usuario de prueba", correo, ["Solicitante"]);
            var primera = new SesionStore(primeraBase);
            var sesion = await primera.CrearAsync(usuario, TimeSpan.FromMinutes(5), CancellationToken.None);

            await using var segundaBase = await SqlIntegration.AbrirContextoAsync();
            var segunda = new SesionStore(segundaBase);
            var vista = await segunda.ObtenerAsync(sesion.Token, CancellationToken.None);

            Assert.NotNull(vista);
            Assert.Equal(idUsuario, vista.IdUsuario);
            Assert.Equal("Solicitante", Assert.Single(vista.Roles));

            await segunda.EliminarAsync(sesion.Token, CancellationToken.None);
            Assert.Null(await primera.ObtenerAsync(sesion.Token, CancellationToken.None));

            var fila = await primeraBase.SesionUsuarios.AsNoTracking()
                .SingleAsync(s => s.IdSesion == sesion.IdSesion);
            Assert.NotNull(fila.FechaRevocacion);
            Assert.NotEqual(sesion.Token, fila.TokenHash);
            Assert.Equal(64, fila.TokenHash.Length);
        }
        finally
        {
            await SqlIntegration.LimpiarUsuarioAsync(primeraBase, correo);
        }
    }

    [LocalDbFact]
    public async Task RevocarUnaSesionNoInvalidaLasDemas()
    {
        var correo = $"sesion-{Guid.NewGuid():N}@example.test";
        await using var db = await SqlIntegration.AbrirContextoAsync();
        var idUsuario = await SqlIntegration.CrearUsuarioAsync(db, correo);
        try
        {
            var store = new SesionStore(db);
            var usuario = new UsuarioAutenticado(idUsuario, "Usuario de prueba", correo, ["Solicitante"]);
            var primera = await store.CrearAsync(usuario, TimeSpan.FromMinutes(5), CancellationToken.None);
            var segunda = await store.CrearAsync(usuario, TimeSpan.FromMinutes(5), CancellationToken.None);

            await store.EliminarAsync(primera.Token, CancellationToken.None);

            Assert.Null(await store.ObtenerAsync(primera.Token, CancellationToken.None));
            Assert.NotNull(await store.ObtenerAsync(segunda.Token, CancellationToken.None));
        }
        finally
        {
            await SqlIntegration.LimpiarUsuarioAsync(db, correo);
        }
    }

    [LocalDbFact]
    public async Task SesionExpiradaNoEsValida()
    {
        var correo = $"sesion-{Guid.NewGuid():N}@example.test";
        await using var db = await SqlIntegration.AbrirContextoAsync();
        var idUsuario = await SqlIntegration.CrearUsuarioAsync(db, correo);
        try
        {
            var store = new SesionStore(db);
            var sesion = await store.CrearAsync(
                new UsuarioAutenticado(idUsuario, "Usuario de prueba", correo, ["Solicitante"]),
                TimeSpan.FromSeconds(-1),
                CancellationToken.None);

            Assert.Null(await store.ObtenerAsync(sesion.Token, CancellationToken.None));
        }
        finally
        {
            await SqlIntegration.LimpiarUsuarioAsync(db, correo);
        }
    }
}

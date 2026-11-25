using GradeX.Application.Administracion;
using GradeX.Application.Workflow;
using GradeX.Infrastructure.Persistence;
using GradeX.Infrastructure.Persistence.Commands;
using GradeX.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace GradeX.Tests;

public sealed class CoberturaAdicionalSqlTests
{
    [LocalDbFact]
    public async Task CatalogoAdminCubreConflictosYRecursosInexistentes()
    {
        await using var db = await SqlIntegration.AbrirContextoAsync();
        var store = new CatalogoAdminStore(db);
        var sufijo = Guid.NewGuid().ToString("N")[..10];
        var catA = await store.CrearCategoriaAsync(
            new DatosCategoriaAdmin($"Categoria A {sufijo}", "A", true),
            CancellationToken.None);
        var catB = await store.CrearCategoriaAsync(
            new DatosCategoriaAdmin($"Categoria B {sufijo}", "B", true),
            CancellationToken.None);
        var marcaA = await store.CrearMarcaAsync(
            new DatosMarcaAdmin($"Marca A {sufijo}", true), CancellationToken.None);
        var marcaB = await store.CrearMarcaAsync(
            new DatosMarcaAdmin($"Marca B {sufijo}", true), CancellationToken.None);
        Assert.Equal(EstadoOperacionAdmin.Exito, catA.Estado);
        Assert.Equal(EstadoOperacionAdmin.Exito, catB.Estado);
        Assert.Equal(EstadoOperacionAdmin.Exito, marcaA.Estado);
        Assert.Equal(EstadoOperacionAdmin.Exito, marcaB.Estado);
        var idCatA = catA.Valor!.IdCategoria;
        var idCatB = catB.Valor!.IdCategoria;
        var idMarcaA = marcaA.Valor!.IdMarca;
        var idMarcaB = marcaB.Valor!.IdMarca;
        try
        {
            Assert.Equal(EstadoOperacionAdmin.DatosInvalidos,
                (await store.CrearCategoriaAsync(
                    new DatosCategoriaAdmin(" ", null, true),
                    CancellationToken.None)).Estado);
            Assert.Equal(EstadoOperacionAdmin.DatosInvalidos,
                (await store.CrearCategoriaAsync(
                    new DatosCategoriaAdmin(new string('x', 101), null, true),
                    CancellationToken.None)).Estado);
            Assert.Equal(EstadoOperacionAdmin.DatosInvalidos,
                (await store.CrearCategoriaAsync(
                    new DatosCategoriaAdmin("Categoria", new string('x', 301), true),
                    CancellationToken.None)).Estado);
            Assert.Equal(EstadoOperacionAdmin.DatosInvalidos,
                (await store.CrearMarcaAsync(
                    new DatosMarcaAdmin(" ", true), CancellationToken.None)).Estado);
            Assert.Equal(EstadoOperacionAdmin.DatosInvalidos,
                (await store.CrearMarcaAsync(
                    new DatosMarcaAdmin(new string('x', 101), true),
                    CancellationToken.None)).Estado);
            Assert.Equal(EstadoOperacionAdmin.DatosInvalidos,
                (await store.ActualizarCategoriaAsync(
                    idCatA, new DatosCategoriaAdmin(" ", null, true),
                    CancellationToken.None)).Estado);
            Assert.Equal(EstadoOperacionAdmin.DatosInvalidos,
                (await store.ActualizarCategoriaAsync(
                    idCatA,
                    new DatosCategoriaAdmin(new string('x', 101), null, true),
                    CancellationToken.None)).Estado);
            Assert.Equal(EstadoOperacionAdmin.DatosInvalidos,
                (await store.ActualizarCategoriaAsync(
                    idCatA,
                    new DatosCategoriaAdmin("Categoria", new string('x', 301), true),
                    CancellationToken.None)).Estado);
            Assert.Equal(EstadoOperacionAdmin.DatosInvalidos,
                (await store.ActualizarMarcaAsync(
                    idMarcaA, new DatosMarcaAdmin(" ", true),
                    CancellationToken.None)).Estado);
            Assert.Equal(EstadoOperacionAdmin.DatosInvalidos,
                (await store.ActualizarMarcaAsync(
                    idMarcaA, new DatosMarcaAdmin(new string('x', 101), true),
                    CancellationToken.None)).Estado);

            Assert.Equal(EstadoOperacionAdmin.Conflicto,
                (await store.CrearCategoriaAsync(
                    new DatosCategoriaAdmin(catA.Valor.Nombre, "Duplicada", true),
                    CancellationToken.None)).Estado);
            Assert.Equal(EstadoOperacionAdmin.Conflicto,
                (await store.CrearMarcaAsync(
                    new DatosMarcaAdmin(marcaA.Valor.Nombre, true),
                    CancellationToken.None)).Estado);
            Assert.Equal(EstadoOperacionAdmin.Conflicto,
                (await store.ActualizarCategoriaAsync(
                    idCatB,
                    new DatosCategoriaAdmin(catA.Valor.Nombre, "Duplicada", true),
                    CancellationToken.None)).Estado);
            Assert.Equal(EstadoOperacionAdmin.Conflicto,
                (await store.ActualizarMarcaAsync(
                    idMarcaB,
                    new DatosMarcaAdmin(marcaA.Valor.Nombre, true),
                    CancellationToken.None)).Estado);

            Assert.Equal(EstadoOperacionAdmin.NoEncontrado,
                (await store.ActualizarCategoriaAsync(
                    999999, new DatosCategoriaAdmin("Inexistente", null, true),
                    CancellationToken.None)).Estado);
            Assert.Equal(EstadoOperacionAdmin.NoEncontrado,
                (await store.ActualizarMarcaAsync(
                    999999, new DatosMarcaAdmin("Inexistente", true),
                    CancellationToken.None)).Estado);
            Assert.Equal(EstadoOperacionAdmin.NoEncontrado,
                (await store.VincularMarcaAsync(
                    999999, idMarcaA, true, CancellationToken.None)).Estado);
            Assert.Equal(EstadoOperacionAdmin.NoEncontrado,
                (await store.VincularMarcaAsync(
                    idCatA, 999999, true, CancellationToken.None)).Estado);
            Assert.Null(await store.ObtenerRequisitosAsync(
                999999, CancellationToken.None));

            var tipo = await db.TipoEvidencia.AsNoTracking()
                .SingleAsync(t => t.Codigo == "VISTA_GENERAL");
            var campo = new RequisitoCampoAdmin("NOMBRE", true, 150, 1);
            var evidencia = new RequisitoEvidenciaAdmin(
                tipo.IdTipoEvidencia, tipo.Codigo, tipo.Nombre, true, 1, 1, 1);
            Assert.Equal(EstadoOperacionAdmin.NoEncontrado,
                (await store.ActualizarRequisitosAsync(
                    999999,
                    new DatosRequisitosCategoria([campo], [evidencia]),
                    CancellationToken.None)).Estado);

            var criterio = new DatosCriterioAdmin(
                "Condición", "Criterio", FlujoGradeX.TiposCriterio.Numerico,
                true, 1, true);
            Assert.Equal(EstadoOperacionAdmin.NoEncontrado,
                (await store.CrearCriterioAsync(
                    999999, criterio, CancellationToken.None)).Estado);
            Assert.Equal(EstadoOperacionAdmin.NoEncontrado,
                (await store.ActualizarCriterioAsync(
                    999999, criterio, CancellationToken.None)).Estado);
            Assert.Equal(EstadoOperacionAdmin.DatosInvalidos,
                (await store.CrearCriterioAsync(
                    idCatA,
                    new DatosCriterioAdmin(" ", null, FlujoGradeX.TiposCriterio.Texto,
                        false, 1, true),
                    CancellationToken.None)).Estado);
            Assert.Equal(EstadoOperacionAdmin.DatosInvalidos,
                (await store.CrearCriterioAsync(
                    idCatA,
                    new DatosCriterioAdmin("Criterio", null, "INVALIDO",
                        false, 1, true),
                    CancellationToken.None)).Estado);
            Assert.Equal(EstadoOperacionAdmin.DatosInvalidos,
                (await store.CrearCriterioAsync(
                    idCatA,
                    new DatosCriterioAdmin(new string('x', 151), null,
                        FlujoGradeX.TiposCriterio.Texto, false, 1, true),
                    CancellationToken.None)).Estado);
            Assert.Equal(EstadoOperacionAdmin.DatosInvalidos,
                (await store.CrearCriterioAsync(
                    idCatA,
                    new DatosCriterioAdmin("Criterio", new string('x', 501),
                        FlujoGradeX.TiposCriterio.Texto, false, 1, true),
                    CancellationToken.None)).Estado);
            Assert.Equal(EstadoOperacionAdmin.DatosInvalidos,
                (await store.CrearCriterioAsync(
                    idCatA,
                    new DatosCriterioAdmin("Criterio", null,
                        FlujoGradeX.TiposCriterio.Texto, false, 0, true),
                    CancellationToken.None)).Estado);

            var vinculo = await store.VincularMarcaAsync(
                idCatA, idMarcaA, true, CancellationToken.None);
            Assert.Equal(EstadoOperacionAdmin.Exito, vinculo.Estado);
            var desvinculo = await store.VincularMarcaAsync(
                idCatA, idMarcaA, false, CancellationToken.None);
            Assert.Equal(EstadoOperacionAdmin.Exito, desvinculo.Estado);
            Assert.False(desvinculo.Valor!.Activa);
            Assert.Contains(await store.ListarCategoriaMarcasAsync(
                idCatA, CancellationToken.None), m => m.IdMarca == idMarcaA);
            Assert.Contains(await store.ListarCategoriasAsync(CancellationToken.None),
                c => c.IdCategoria == idCatA);
            Assert.Contains(await store.ListarMarcasAsync(CancellationToken.None),
                m => m.IdMarca == idMarcaA);
        }
        finally
        {
            await LimpiarCategoriaAsync(db, idCatA);
            await LimpiarCategoriaAsync(db, idCatB);
            await db.Marcas.Where(m => m.IdMarca == idMarcaA ||
                m.IdMarca == idMarcaB).ExecuteDeleteAsync();
        }
    }

    [LocalDbFact]
    public async Task EspecialistaAdminCubreUsuarioRolYActualizacion()
    {
        await using var db = await SqlIntegration.AbrirContextoAsync();
        var sufijo = Guid.NewGuid().ToString("N")[..10];
        var correo = $"especialista-validacion-{sufijo}@example.test";
        var correoInactivo = $"especialista-inactivo-{sufijo}@example.test";
        var idUsuario = await SqlIntegration.CrearUsuarioAsync(db, correo);
        var idInactivo = await SqlIntegration.CrearUsuarioAsync(db, correoInactivo);
        await db.Usuarios.Where(u => u.IdUsuario == idInactivo)
            .ExecuteUpdateAsync(update => update.SetProperty(u => u.Activo, false));
        db.ChangeTracker.Clear();
        var categoria = new Categorium
        {
            Nombre = $"Categoria especialista validacion {sufijo}",
            Activa = true
        };
        db.Categoria.Add(categoria);
        await db.SaveChangesAsync();
        var store = new EspecialistaAdminStore(db, new NotificacionWriter(db));
        var datos = new DatosEspecialistaAdmin(
            idUsuario,
            FlujoGradeX.EstadosEspecialista.Activo,
            5,
            "Perfil de validación",
            [new DatosEspecialidadAdmin(
                categoria.IdCategoria, null, FlujoGradeX.NivelesEspecialista.Avanzado)]);
        var idEspecialista = 0;
        try
        {
            var creado = await store.CrearAsync(datos, CancellationToken.None);
            Assert.Equal(EstadoOperacionAdmin.Exito, creado.Estado);
            idEspecialista = creado.Valor!.IdEspecialista;

            Assert.Equal(EstadoOperacionAdmin.Conflicto,
                (await store.CrearAsync(datos, CancellationToken.None)).Estado);
            Assert.Equal(EstadoOperacionAdmin.NoEncontrado,
                (await store.CrearAsync(datos with { IdUsuario = 999999 },
                    CancellationToken.None)).Estado);
            Assert.Equal(EstadoOperacionAdmin.DatosInvalidos,
                (await store.CrearAsync(datos with { IdUsuario = idInactivo },
                    CancellationToken.None)).Estado);
            Assert.Equal(EstadoOperacionAdmin.NoEncontrado,
                (await store.ActualizarAsync(999999, datos,
                    CancellationToken.None)).Estado);
            Assert.Equal(EstadoOperacionAdmin.DatosInvalidos,
                (await store.ActualizarAsync(idEspecialista,
                    datos with { IdUsuario = idInactivo },
                    CancellationToken.None)).Estado);
        }
        finally
        {
            await db.EspecialidadEspecialista
                .Where(e => e.IdCategoria == categoria.IdCategoria ||
                    e.IdEspecialista == idEspecialista).ExecuteDeleteAsync();
            await db.Especialista
                .Where(e => e.IdUsuario == idUsuario || e.IdUsuario == idInactivo)
                .ExecuteDeleteAsync();
            await db.Categoria.Where(c => c.IdCategoria == categoria.IdCategoria)
                .ExecuteDeleteAsync();
            await SqlIntegration.LimpiarUsuarioAsync(db, correo);
            await SqlIntegration.LimpiarUsuarioAsync(db, correoInactivo);
        }
    }

    private static async Task LimpiarCategoriaAsync(GradeXDbContext db, int idCategoria)
    {
        await db.CriterioEvaluacions
            .Where(c => c.IdCategoria == idCategoria).ExecuteDeleteAsync();
        await db.CategoriaCampoRequisitos
            .Where(r => r.IdCategoria == idCategoria).ExecuteDeleteAsync();
        await db.CategoriaEvidenciaRequisitos
            .Where(r => r.IdCategoria == idCategoria).ExecuteDeleteAsync();
        await db.CategoriaMarcas
            .Where(cm => cm.IdCategoria == idCategoria).ExecuteDeleteAsync();
        await db.Categoria
            .Where(c => c.IdCategoria == idCategoria).ExecuteDeleteAsync();
    }
}

using GradeX.Application.Administracion;
using GradeX.Application.Workflow;
using GradeX.Infrastructure.Persistence;
using GradeX.Infrastructure.Persistence.Commands;
using GradeX.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace GradeX.Tests;

public sealed class CoberturaSqlTests
{
    [LocalDbFact]
    public async Task CatalogoAdminRechazaRequisitosInvalidos()
    {
        await using var db = await SqlIntegration.AbrirContextoAsync();
        var store = new CatalogoAdminStore(db);
        var sufijo = Guid.NewGuid().ToString("N")[..10];
        var categoria = await store.CrearCategoriaAsync(
            new DatosCategoriaAdmin($"Categoria validacion {sufijo}", null, true),
            CancellationToken.None);
        Assert.Equal(EstadoOperacionAdmin.Exito, categoria.Estado);
        var idCategoria = categoria.Valor!.IdCategoria;
        try
        {
            var tipo = await db.TipoEvidencia.AsNoTracking()
                .SingleAsync(t => t.Codigo == "VISTA_GENERAL");
            var campo = new RequisitoCampoAdmin("NOMBRE", true, 150, 1);
            var evidencia = new RequisitoEvidenciaAdmin(
                tipo.IdTipoEvidencia, tipo.Codigo, tipo.Nombre, true, 1, 1, 1);

            async Task<EstadoOperacionAdmin> Enviar(DatosRequisitosCategoria datos) =>
                (await store.ActualizarRequisitosAsync(
                    idCategoria, datos, CancellationToken.None)).Estado;

            Assert.Equal(EstadoOperacionAdmin.DatosInvalidos,
                await Enviar(new DatosRequisitosCategoria([], [])));
            Assert.Equal(EstadoOperacionAdmin.DatosInvalidos,
                await Enviar(new DatosRequisitosCategoria([campo, campo], [evidencia])));
            Assert.Equal(EstadoOperacionAdmin.DatosInvalidos,
                await Enviar(new DatosRequisitosCategoria(
                    [new RequisitoCampoAdmin("MARCA", true, 150, 1)], [evidencia])));
            Assert.Equal(EstadoOperacionAdmin.DatosInvalidos,
                await Enviar(new DatosRequisitosCategoria(
                    [new RequisitoCampoAdmin("NOMBRE", false, 150, 1)], [evidencia])));
            Assert.Equal(EstadoOperacionAdmin.DatosInvalidos,
                await Enviar(new DatosRequisitosCategoria(
                    [campo, new RequisitoCampoAdmin("NO_EXISTE", true, 150, 2)],
                    [evidencia])));
            Assert.Equal(EstadoOperacionAdmin.DatosInvalidos,
                await Enviar(new DatosRequisitosCategoria(
                    [new RequisitoCampoAdmin("NOMBRE", true, 0, 1)], [evidencia])));
            Assert.Equal(EstadoOperacionAdmin.DatosInvalidos,
                await Enviar(new DatosRequisitosCategoria([campo], [])));
            Assert.Equal(EstadoOperacionAdmin.DatosInvalidos,
                await Enviar(new DatosRequisitosCategoria([campo], [evidencia, evidencia])));
            Assert.Equal(EstadoOperacionAdmin.DatosInvalidos,
                await Enviar(new DatosRequisitosCategoria(
                    [campo], [evidencia with { IdTipoEvidencia = 999999 }])));
            Assert.Equal(EstadoOperacionAdmin.DatosInvalidos,
                await Enviar(new DatosRequisitosCategoria(
                    [campo], [evidencia with { CantidadMinima = 2, CantidadMaxima = 1 }])));
            Assert.Equal(EstadoOperacionAdmin.DatosInvalidos,
                await Enviar(new DatosRequisitosCategoria(
                    [campo], [evidencia with { Obligatorio = false, CantidadMinima = 0 }])));
            Assert.Equal(EstadoOperacionAdmin.NoEncontrado,
                (await store.ActualizarRequisitosAsync(
                    999999,
                    new DatosRequisitosCategoria([campo], [evidencia]),
                    CancellationToken.None)).Estado);
        }
        finally
        {
            await db.CategoriaCampoRequisitos
                .Where(r => r.IdCategoria == idCategoria).ExecuteDeleteAsync();
            await db.CategoriaEvidenciaRequisitos
                .Where(r => r.IdCategoria == idCategoria).ExecuteDeleteAsync();
            await db.Categoria
                .Where(c => c.IdCategoria == idCategoria).ExecuteDeleteAsync();
        }
    }

    [LocalDbFact]
    public async Task EspecialistaAdminRechazaDatosInvalidos()
    {
        await using var db = await SqlIntegration.AbrirContextoAsync();
        var store = new EspecialistaAdminStore(db, new NotificacionWriter(db));
        var sufijo = Guid.NewGuid().ToString("N")[..10];
        var correo = $"validacion-especialista-{sufijo}@example.test";
        var idUsuario = await SqlIntegration.CrearUsuarioAsync(db, correo);
        var activa = new Categorium
        {
            Nombre = $"Categoria activa {sufijo}",
            Activa = true
        };
        var inactiva = new Categorium
        {
            Nombre = $"Categoria inactiva {sufijo}",
            Activa = false
        };
        db.Categoria.AddRange(activa, inactiva);
        await db.SaveChangesAsync();
        try
        {
            var areaValida = new DatosEspecialidadAdmin(
                activa.IdCategoria, null, FlujoGradeX.NivelesEspecialista.Avanzado);

            async Task<EstadoOperacionAdmin> Enviar(DatosEspecialistaAdmin datos) =>
                (await store.CrearAsync(datos, CancellationToken.None)).Estado;

            Assert.Equal(EstadoOperacionAdmin.DatosInvalidos,
                await Enviar(new DatosEspecialistaAdmin(
                    0, FlujoGradeX.EstadosEspecialista.Activo, 1, null, [areaValida])));
            Assert.Equal(EstadoOperacionAdmin.DatosInvalidos,
                await Enviar(new DatosEspecialistaAdmin(
                    idUsuario, FlujoGradeX.EstadosEspecialista.Activo, -1, null, [areaValida])));
            Assert.Equal(EstadoOperacionAdmin.DatosInvalidos,
                await Enviar(new DatosEspecialistaAdmin(
                    idUsuario, FlujoGradeX.EstadosEspecialista.Activo, 1,
                    new string('x', 601), [areaValida])));
            Assert.Equal(EstadoOperacionAdmin.DatosInvalidos,
                await Enviar(new DatosEspecialistaAdmin(
                    idUsuario, "INVALIDO", 1, null, [areaValida])));
            Assert.Equal(EstadoOperacionAdmin.DatosInvalidos,
                await Enviar(new DatosEspecialistaAdmin(
                    idUsuario, FlujoGradeX.EstadosEspecialista.Activo, 1, null, [])));
            Assert.Equal(EstadoOperacionAdmin.DatosInvalidos,
                await Enviar(new DatosEspecialistaAdmin(
                    idUsuario, FlujoGradeX.EstadosEspecialista.Activo, 1, null,
                    [areaValida, areaValida])));
            Assert.Equal(EstadoOperacionAdmin.DatosInvalidos,
                await Enviar(new DatosEspecialistaAdmin(
                    idUsuario, FlujoGradeX.EstadosEspecialista.Activo, 1, null,
                    [new DatosEspecialidadAdmin(activa.IdCategoria, null, "INVALIDO")])));
            Assert.Equal(EstadoOperacionAdmin.DatosInvalidos,
                await Enviar(new DatosEspecialistaAdmin(
                    idUsuario, FlujoGradeX.EstadosEspecialista.Activo, 1, null,
                    [new DatosEspecialidadAdmin(inactiva.IdCategoria, null, null)])));
            Assert.Equal(EstadoOperacionAdmin.DatosInvalidos,
                await Enviar(new DatosEspecialistaAdmin(
                    idUsuario, FlujoGradeX.EstadosEspecialista.Activo, 1, null,
                    [new DatosEspecialidadAdmin(activa.IdCategoria, 999999, null)])));
        }
        finally
        {
            await db.EspecialidadEspecialista
                .Where(e => e.IdCategoria == activa.IdCategoria ||
                    e.IdCategoria == inactiva.IdCategoria).ExecuteDeleteAsync();
            await db.Categoria
                .Where(c => c.IdCategoria == activa.IdCategoria ||
                    c.IdCategoria == inactiva.IdCategoria).ExecuteDeleteAsync();
            await SqlIntegration.LimpiarUsuarioAsync(db, correo);
        }
    }
}

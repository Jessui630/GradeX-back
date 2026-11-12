using GradeX.Application.Solicitudes;
using GradeX.Infrastructure.Persistence;
using GradeX.Infrastructure.Persistence.Commands;
using GradeX.Infrastructure.Persistence.Models;
using GradeX.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace GradeX.Tests;

public sealed class SolicitudStoreSqlTests
{
    [LocalDbFact]
    public async Task FlujoCompletoCrearEvidenciarEnviarYConsultar()
    {
        var correo = $"solicitud-{Guid.NewGuid():N}@example.test";
        var otroCorreo = $"solicitud-{Guid.NewGuid():N}@example.test";
        await using var db = await SqlIntegration.AbrirContextoAsync();
        var idUsuario = await SqlIntegration.CrearUsuarioAsync(db, correo);
        var idOtro = await SqlIntegration.CrearUsuarioAsync(db, otroCorreo);
        try
        {
            var categoria = await db.Categoria.AsNoTracking().FirstAsync(c => c.Activa);
            var requisitos = await new CatalogoProductoStore(db)
                .ObtenerRequisitosAsync(categoria.IdCategoria, CancellationToken.None);
            Assert.NotNull(requisitos);
            var tipoObligatorio = requisitos.Evidencias.First(e => e.Obligatorio);

            var store = new SolicitudStore(db);
            var datos = new DatosNuevaSolicitud(
                categoria.IdCategoria, null, "Producto de prueba", "Modelo", "SKU-1",
                "SERIE-1", null, "Caja", "Descripción", "Observación",
                Guid.NewGuid());
            var creada = await store.CrearBorradorAsync(datos, idUsuario, CancellationToken.None);
            Assert.Equal(EstadoCrearSolicitud.Exito, creada.Estado);
            Assert.NotNull(creada.Solicitud);
            Assert.Equal("BORRADOR", creada.Solicitud.EstadoCodigo);

            var repetida = await store.CrearBorradorAsync(datos, idUsuario, CancellationToken.None);
            Assert.Equal(EstadoCrearSolicitud.Existente, repetida.Estado);
            Assert.Equal(creada.Solicitud.IdSolicitud, repetida.Solicitud!.IdSolicitud);

            var listado = await store.ListarAsync(idUsuario, 1, 10, null, CancellationToken.None);
            Assert.Equal(1, listado.Total);
            Assert.Equal("BORRADOR", Assert.Single(listado.Elementos).EstadoCodigo);
            Assert.Equal(0, (await store.ListarAsync(idOtro, 1, 10, null, CancellationToken.None)).Total);
            Assert.Single((await store.ListarAsync(idUsuario, 1, 1, null, CancellationToken.None)).Elementos);
            Assert.Equal(1,
                (await store.ListarAsync(idUsuario, 0, 0, null, CancellationToken.None)).Pagina);
            Assert.Equal(1,
                (await store.ListarAsync(idUsuario, 0, 0, null, CancellationToken.None)).TamanoPagina);
            Assert.Empty((await store.ListarAsync(
                idUsuario, int.MaxValue, 50, null, CancellationToken.None)).Elementos);
            Assert.Single((await store.ListarAsync(
                idUsuario, 1, 10, "BORRADOR", CancellationToken.None)).Elementos);
            Assert.Empty((await store.ListarAsync(
                idUsuario, 1, 10, "ESTADO_INEXISTENTE", CancellationToken.None)).Elementos);

            Assert.Null(await store.ObtenerDetalleAsync(
                creada.Solicitud.IdSolicitud, idOtro, CancellationToken.None));
            Assert.Null(await store.ObtenerDetalleAsync(
                int.MaxValue, idUsuario, CancellationToken.None));
            Assert.Equal(EstadoEnviarSolicitud.NoEncontrada,
                (await store.EnviarAsync(creada.Solicitud.IdSolicitud, idOtro, CancellationToken.None)).Estado);

            var sinEvidencia = await store.EnviarAsync(
                creada.Solicitud.IdSolicitud, idUsuario, CancellationToken.None);
            Assert.Equal(EstadoEnviarSolicitud.EvidenciaFaltante, sinEvidencia.Estado);

            var evidencia = await store.AgregarEvidenciaAsync(
                creada.Solicitud.IdSolicitud,
                idUsuario,
                tipoObligatorio.IdTipoEvidencia,
                "2026/11/evidencia-prueba.png",
                "evidencia-prueba.png",
                "image/png",
                128,
                new string('A', 64),
                "Vista general",
                CancellationToken.None);
            Assert.Equal(EstadoAgregarEvidencia.Exito, evidencia.Estado);

            var enviada = await store.EnviarAsync(
                creada.Solicitud.IdSolicitud, idUsuario, CancellationToken.None);
            Assert.Equal(EstadoEnviarSolicitud.Exito, enviada.Estado);
            Assert.Equal("PENDIENTE_ASIGNACION", enviada.Solicitud!.EstadoCodigo);

            var deNuevo = await store.EnviarAsync(
                creada.Solicitud.IdSolicitud, idUsuario, CancellationToken.None);
            Assert.Equal(EstadoEnviarSolicitud.Exito, deNuevo.Estado);

            Assert.Single((await store.ListarAsync(
                idUsuario, 1, 10, "PENDIENTE_ASIGNACION", CancellationToken.None)).Elementos);
            Assert.Empty((await store.ListarAsync(
                idUsuario, 2, 1, null, CancellationToken.None)).Elementos);

            var detalle = await store.ObtenerDetalleAsync(
                creada.Solicitud.IdSolicitud, idUsuario, CancellationToken.None);
            Assert.NotNull(detalle);
            Assert.Equal("PENDIENTE_ASIGNACION", detalle.EstadoCodigo);
            Assert.Single(detalle.Evidencias);
            Assert.NotEmpty(detalle.Seguimiento);

            var archivo = await store.ObtenerArchivoAsync(
                creada.Solicitud.IdSolicitud,
                detalle.Evidencias[0].IdEvidencia,
                idUsuario,
                CancellationToken.None);
            Assert.NotNull(archivo);
            Assert.Equal("image/png", archivo.TipoContenido);
            Assert.Null(await store.ObtenerArchivoAsync(
                creada.Solicitud.IdSolicitud,
                detalle.Evidencias[0].IdEvidencia,
                idOtro,
                CancellationToken.None));
        }
        finally
        {
            await SqlIntegration.LimpiarUsuarioAsync(db, correo);
            await SqlIntegration.LimpiarUsuarioAsync(db, otroCorreo);
        }
    }

    [LocalDbFact]
    public async Task CategoriaInactivaYMarcaInvalidaSonRechazadas()
    {
        var correo = $"solicitud-{Guid.NewGuid():N}@example.test";
        await using var db = await SqlIntegration.AbrirContextoAsync();
        var idUsuario = await SqlIntegration.CrearUsuarioAsync(db, correo);
        var categoria = new Categorium
        {
            Nombre = $"CategoriaInactiva-{Guid.NewGuid():N}",
            Activa = false
        };
        db.Categoria.Add(categoria);
        await db.SaveChangesAsync();
        try
        {
            var store = new SolicitudStore(db);
            var inactiva = await store.CrearBorradorAsync(
                new DatosNuevaSolicitud(categoria.IdCategoria, null, "Producto", null, null,
                    null, null, null, null, null, Guid.NewGuid()),
                idUsuario,
                CancellationToken.None);
            Assert.Equal(EstadoCrearSolicitud.CategoriaInvalida, inactiva.Estado);

            var activa = await db.Categoria.AsNoTracking().FirstAsync(c => c.Activa);
            var marca = await store.CrearBorradorAsync(
                new DatosNuevaSolicitud(activa.IdCategoria, int.MaxValue, "Producto", null, null,
                    null, null, null, null, null, Guid.NewGuid()),
                idUsuario,
                CancellationToken.None);
            Assert.Equal(EstadoCrearSolicitud.MarcaInvalida, marca.Estado);
        }
        finally
        {
            db.Categoria.Remove(categoria);
            await db.SaveChangesAsync();
            await SqlIntegration.LimpiarUsuarioAsync(db, correo);
        }
    }

    [LocalDbFact]
    public async Task FalloAlInsertarSolicitudRevierteProducto()
    {
        var correo = $"solicitud-{Guid.NewGuid():N}@example.test";
        var interceptor = new FalloSolicitud();
        await using var db = await SqlIntegration.AbrirContextoAsync(interceptor);
        var idUsuario = await SqlIntegration.CrearUsuarioAsync(db, correo);
        try
        {
            var categoria = await db.Categoria.AsNoTracking().FirstAsync(c => c.Activa);
            var store = new SolicitudStore(db);
            await Assert.ThrowsAsync<DbUpdateException>(() => store.CrearBorradorAsync(
                new DatosNuevaSolicitud(categoria.IdCategoria, null, "Producto fallido", null, null,
                    null, null, null, null, null, Guid.NewGuid()),
                idUsuario,
                CancellationToken.None));

            Assert.False(await db.Productos.AsNoTracking()
                .AnyAsync(p => p.IdPropietario == idUsuario && p.Nombre == "Producto fallido"));
        }
        finally
        {
            await SqlIntegration.LimpiarUsuarioAsync(db, correo);
        }
    }

    private sealed class FalloSolicitud : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("INSERT INTO [SolicitudEvaluacion]", StringComparison.Ordinal))
                throw new InvalidOperationException("Fallo sintético de solicitud.");
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}

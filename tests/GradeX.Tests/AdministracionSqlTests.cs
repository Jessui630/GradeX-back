using GradeX.Application.Administracion;
using GradeX.Application.Cuentas;
using GradeX.Application.Perfil;
using GradeX.Application.Workflow;
using GradeX.Infrastructure.Persistence;
using GradeX.Infrastructure.Persistence.Commands;
using Microsoft.EntityFrameworkCore;

namespace GradeX.Tests;

public sealed class AdministracionSqlTests
{
    [LocalDbFact]
    public async Task CatalogoAdminCreaCategoriaMarcaCriteriosYRequisitos()
    {
        await using var db = await SqlIntegration.AbrirContextoAsync();
        var store = new CatalogoAdminStore(db);
        var sufijo = Guid.NewGuid().ToString("N")[..10];
        var idCategoria = 0;
        var idMarca = 0;
        try
        {
            var categoria = await store.CrearCategoriaAsync(
                new DatosCategoriaAdmin($"Categoria Admin {sufijo}", "Descripción", true),
                CancellationToken.None);
            Assert.Equal(EstadoOperacionAdmin.Exito, categoria.Estado);
            idCategoria = categoria.Valor!.IdCategoria;

            var marca = await store.CrearMarcaAsync(
                new DatosMarcaAdmin($"Marca Admin {sufijo}", true),
                CancellationToken.None);
            Assert.Equal(EstadoOperacionAdmin.Exito, marca.Estado);
            idMarca = marca.Valor!.IdMarca;

            var vinculo = await store.VincularMarcaAsync(
                idCategoria, idMarca, true, CancellationToken.None);
            Assert.Equal(EstadoOperacionAdmin.Exito, vinculo.Estado);
            Assert.True(vinculo.Valor!.Activa);

            var criterio = await store.CrearCriterioAsync(
                idCategoria,
                new DatosCriterioAdmin("Estado general", "Criterio de prueba",
                    FlujoGradeX.TiposCriterio.Numerico, true, 1, true),
                CancellationToken.None);
            Assert.Equal(EstadoOperacionAdmin.Exito, criterio.Estado);

            var tipo = await db.TipoEvidencia.AsNoTracking()
                .SingleAsync(t => t.Codigo == "VISTA_GENERAL");
            var requisitos = await store.ActualizarRequisitosAsync(
                idCategoria,
                new DatosRequisitosCategoria(
                    [new RequisitoCampoAdmin("NOMBRE", true, 150, 1)],
                    [new RequisitoEvidenciaAdmin(tipo.IdTipoEvidencia,
                        tipo.Codigo, tipo.Nombre, true, 1, 1, 1)]),
                CancellationToken.None);
            Assert.Equal(EstadoOperacionAdmin.Exito, requisitos.Estado);
            Assert.Single(requisitos.Valor!.Campos);
            Assert.Single(requisitos.Valor.Evidencias);

            var actualizada = await store.ActualizarCategoriaAsync(
                idCategoria,
                new DatosCategoriaAdmin(
                    $"Categoria Admin {sufijo} actualizada", "Nueva", true),
                CancellationToken.None);
            Assert.Equal(EstadoOperacionAdmin.Exito, actualizada.Estado);
            Assert.Contains("actualizada", actualizada.Valor!.Nombre);

            var listado = await store.ListarCategoriasAsync(CancellationToken.None);
            Assert.Contains(listado, c => c.IdCategoria == idCategoria);
        }
        finally
        {
            if (idCategoria > 0)
            {
                await db.CriterioEvaluacions
                    .Where(c => c.IdCategoria == idCategoria).ExecuteDeleteAsync();
                await db.CategoriaEvidenciaRequisitos
                    .Where(r => r.IdCategoria == idCategoria).ExecuteDeleteAsync();
                await db.CategoriaCampoRequisitos
                    .Where(r => r.IdCategoria == idCategoria).ExecuteDeleteAsync();
                await db.CategoriaMarcas
                    .Where(cm => cm.IdCategoria == idCategoria).ExecuteDeleteAsync();
                await db.Categoria.Where(c => c.IdCategoria == idCategoria)
                    .ExecuteDeleteAsync();
            }
            if (idMarca > 0)
                await db.Marcas.Where(m => m.IdMarca == idMarca).ExecuteDeleteAsync();
        }
    }

    [LocalDbFact]
    public async Task EspecialistaAdminAcreditaUsuarioYActualizaEstado()
    {
        await using var db = await SqlIntegration.AbrirContextoAsync();
        var sufijo = Guid.NewGuid().ToString("N")[..10];
        var correo = $"candidato-{sufijo}@example.test";
        var idUsuario = await SqlIntegration.CrearUsuarioAsync(db, correo);
        var idCategoria = 0;
        var idEspecialista = 0;
        try
        {
            var categoria = new GradeX.Infrastructure.Persistence.Models.Categorium
            {
                Nombre = $"Categoria Especialista {sufijo}",
                Activa = true
            };
            db.Categoria.Add(categoria);
            await db.SaveChangesAsync();
            idCategoria = categoria.IdCategoria;

            var store = new EspecialistaAdminStore(db, new NotificacionWriter(db));
            var creado = await store.CrearAsync(
                new DatosEspecialistaAdmin(
                    idUsuario,
                    FlujoGradeX.EstadosEspecialista.Activo,
                    10,
                    "Especialista de prueba",
                    [new DatosEspecialidadAdmin(
                        idCategoria, null, FlujoGradeX.NivelesEspecialista.Avanzado)]),
                CancellationToken.None);
            Assert.Equal(EstadoOperacionAdmin.Exito, creado.Estado);
            idEspecialista = creado.Valor!.IdEspecialista;
            Assert.True(await db.UsuarioRols.AsNoTracking()
                .AnyAsync(ur => ur.IdUsuario == idUsuario &&
                    ur.IdRolNavigation.Nombre == "Especialista"));
            Assert.Single(creado.Valor.Especialidades);

            var actualizado = await store.ActualizarAsync(
                idEspecialista,
                new DatosEspecialistaAdmin(
                    idUsuario,
                    FlujoGradeX.EstadosEspecialista.Suspendido,
                    11,
                    "Actualizado",
                    [new DatosEspecialidadAdmin(
                        idCategoria, null, FlujoGradeX.NivelesEspecialista.Experto)]),
                CancellationToken.None);
            Assert.Equal(EstadoOperacionAdmin.Exito, actualizado.Estado);
            Assert.Equal(FlujoGradeX.EstadosEspecialista.Suspendido,
                actualizado.Valor!.Estado);
            Assert.Equal((short?)11, actualizado.Valor.AniosExperiencia);
        }
        finally
        {
            await db.Notificacions.Where(n => n.IdUsuario == idUsuario).ExecuteDeleteAsync();
            if (idEspecialista > 0)
            {
                await db.EspecialidadEspecialista
                    .Where(e => e.IdEspecialista == idEspecialista).ExecuteDeleteAsync();
                await db.Especialista
                    .Where(e => e.IdEspecialista == idEspecialista).ExecuteDeleteAsync();
            }
            if (idCategoria > 0)
                await db.Categoria.Where(c => c.IdCategoria == idCategoria)
                    .ExecuteDeleteAsync();
            await SqlIntegration.LimpiarUsuarioAsync(db, correo);
        }
    }

    [LocalDbFact]
    public async Task PerfilActualizaNombrePreferenciaYContrasena()
    {
        await using var db = await SqlIntegration.AbrirContextoAsync();
        var sufijo = Guid.NewGuid().ToString("N")[..10];
        var correo = $"perfil-{sufijo}@example.test";
        var idUsuario = await SqlIntegration.CrearUsuarioAsync(db, correo);
        var hasher = new GradeX.Infrastructure.Security.ContrasenaHasher();
        var inicial = "Contraseña inicial segura 2026";
        var nueva = "Contraseña nueva segura 2027";
        try
        {
            await db.Usuarios.Where(u => u.IdUsuario == idUsuario).ExecuteUpdateAsync(
                update => update.SetProperty(u => u.PasswordHash, hasher.Hash(inicial)));

            var store = new PerfilStore(db);
            var perfil = await store.ObtenerAsync(idUsuario, CancellationToken.None);
            Assert.NotNull(perfil);
            Assert.True(perfil.RecibirNotificaciones);

            var actualizar = new ActualizarPerfil(store);
            var resultado = await actualizar.EjecutarAsync(
                idUsuario,
                new DatosPerfil("Nombre Actualizado", false),
                CancellationToken.None);
            Assert.Equal(EstadoPerfil.Exito, resultado.Estado);
            Assert.Equal("Nombre Actualizado", resultado.Perfil!.NombreCompleto);
            Assert.False(resultado.Perfil.RecibirNotificaciones);

            var cambiar = new CambiarContrasena(
                store, hasher, new ContrasenaNoComprometida());
            var incorrecta = await cambiar.EjecutarAsync(
                idUsuario,
                new DatosCambioContrasena("incorrecta", nueva),
                CancellationToken.None);
            Assert.Equal(EstadoPerfil.ContrasenaIncorrecta, incorrecta.Estado);

            var cambiada = await cambiar.EjecutarAsync(
                idUsuario,
                new DatosCambioContrasena(inicial, nueva),
                CancellationToken.None);
            Assert.Equal(EstadoPerfil.Exito, cambiada.Estado);
            var hash = await store.ObtenerHashAsync(idUsuario, CancellationToken.None);
            Assert.NotNull(hash);
            Assert.True(hasher.Verificar(hash, nueva));
        }
        finally
        {
            await SqlIntegration.LimpiarUsuarioAsync(db, correo);
        }
    }

    private sealed class ContrasenaNoComprometida : IContrasenaComprometida
    {
        public ValueTask<EstadoContrasenaComprometida> EstaComprometidaAsync(
            string contrasenaNormalizada, CancellationToken cancellationToken) =>
            ValueTask.FromResult(EstadoContrasenaComprometida.NoEncontrada);
    }
}

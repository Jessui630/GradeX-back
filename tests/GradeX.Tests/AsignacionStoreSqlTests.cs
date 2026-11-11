using GradeX.Application.Asignaciones;
using GradeX.Infrastructure.Persistence;
using GradeX.Infrastructure.Persistence.Commands;
using GradeX.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace GradeX.Tests;

public sealed class AsignacionStoreSqlTests
{
    [LocalDbFact]
    public async Task EstadoAsignadaEstaConfiguradoEnLaBaseAislada()
    {
        await using var db = await SqlIntegration.AbrirContextoAsync();

        Assert.True(await db.EstadoSolicituds.AsNoTracking()
            .AnyAsync(e => e.Codigo == "ASIGNADA"));
    }

    [LocalDbFact]
    public async Task CompatiblesFiltraCategoriaMarcaEstadoYArea()
    {
        await using var db = await SqlIntegration.AbrirContextoAsync();
        var escenario = await CrearEscenarioAsync(db);
        try
        {
            var store = new AsignacionStore(db);

            var sinMarca = await store.ObtenerCompatiblesAsync(
                escenario.IdSolicitudSinMarca, CancellationToken.None);
            Assert.NotNull(sinMarca);
            Assert.True(sinMarca.PuedeAsignarse);
            var unico = Assert.Single(sinMarca.Especialistas);
            Assert.Equal(escenario.IdEspecialistaCategoria, unico.IdEspecialista);
            Assert.False(unico.CoincideMarca);

            var conMarca = await store.ObtenerCompatiblesAsync(
                escenario.IdSolicitudConMarca, CancellationToken.None);
            Assert.NotNull(conMarca);
            Assert.Equal(2, conMarca.Especialistas.Count);
            Assert.Equal(escenario.IdEspecialistaMarca, conMarca.Especialistas[0].IdEspecialista);
            Assert.True(conMarca.Especialistas[0].CoincideMarca);
            Assert.Equal(escenario.IdEspecialistaCategoria, conMarca.Especialistas[1].IdEspecialista);

            var listado = await store.ListarSolicitudesAsync(
                "PENDIENTE_ASIGNACION", 1, 50, CancellationToken.None);
            Assert.True(listado.Total >= 2);
            Assert.DoesNotContain(listado.Elementos,
                fila => fila.IdSolicitud == escenario.IdSolicitudBorrador);
            Assert.Equal(1, listado.Elementos.Single(
                fila => fila.IdSolicitud == escenario.IdSolicitudSinMarca).EspecialistasCompatibles);
            Assert.Equal(2, listado.Elementos.Single(
                fila => fila.IdSolicitud == escenario.IdSolicitudConMarca).EspecialistasCompatibles);
        }
        finally
        {
            await LimpiarEscenarioAsync(db, escenario);
        }
    }

    [LocalDbFact]
    public async Task AsignacionPersisteFechaEspecialistaEstadoYTrazabilidad()
    {
        await using var db = await SqlIntegration.AbrirContextoAsync();
        var escenario = await CrearEscenarioAsync(db);
        try
        {
            var store = new AsignacionStore(db);
            var antes = DateTime.UtcNow.AddMinutes(-1);

            var resultado = await store.AsignarAsync(
                new DatosAsignacion(escenario.IdSolicitudSinMarca, escenario.IdEspecialistaCategoria),
                CancellationToken.None);

            Assert.Equal(EstadoAsignarEspecialista.Exito, resultado.Estado);
            Assert.NotNull(resultado.Asignacion);
            Assert.Equal("ASIGNADA", resultado.Asignacion.EstadoCodigo);
            Assert.Equal(escenario.IdEspecialistaCategoria, resultado.Asignacion.IdEspecialista);

            var asignacion = await db.AsignacionEspecialista.AsNoTracking()
                .SingleAsync(a => a.IdSolicitud == escenario.IdSolicitudSinMarca);
            Assert.True(asignacion.Activa);
            Assert.True(asignacion.FechaAsignacion >= antes);
            Assert.Equal(escenario.IdEspecialistaCategoria, asignacion.IdEspecialista);

            var estado = await db.SolicitudEvaluacions.AsNoTracking()
                .Where(s => s.IdSolicitud == escenario.IdSolicitudSinMarca)
                .Select(s => s.IdEstadoSolicitudNavigation.Codigo)
                .SingleAsync();
            Assert.Equal("ASIGNADA", estado);

            var detalle = await store.ObtenerSolicitudAsync(
                escenario.IdSolicitudSinMarca, CancellationToken.None);
            Assert.NotNull(detalle);
            Assert.False(detalle.PuedeAsignarse);
            Assert.NotNull(detalle.Asignacion);
            Assert.StartsWith("especialista-categoria-", detalle.Asignacion.Especialista);
            Assert.Empty(detalle.EspecialistasCompatibles);

            var compatibles = await store.ObtenerCompatiblesAsync(
                escenario.IdSolicitudSinMarca, CancellationToken.None);
            Assert.NotNull(compatibles);
            Assert.False(compatibles.PuedeAsignarse);
            Assert.Empty(compatibles.Especialistas);

            var duplicada = await store.AsignarAsync(
                new DatosAsignacion(escenario.IdSolicitudSinMarca, escenario.IdEspecialistaMarca),
                CancellationToken.None);
            Assert.Equal(EstadoAsignarEspecialista.YaAsignada, duplicada.Estado);
            Assert.NotNull(duplicada.AsignacionExistente);
            Assert.Equal(1, await db.AsignacionEspecialista.AsNoTracking()
                .CountAsync(a => a.IdSolicitud == escenario.IdSolicitudSinMarca && a.Activa));
        }
        finally
        {
            await LimpiarEscenarioAsync(db, escenario);
        }
    }

    [LocalDbFact]
    public async Task EspecialistaIncompatibleOInactivoNoModificaLaSolicitud()
    {
        await using var db = await SqlIntegration.AbrirContextoAsync();
        var escenario = await CrearEscenarioAsync(db);
        try
        {
            var store = new AsignacionStore(db);

            var inactivo = await store.AsignarAsync(
                new DatosAsignacion(
                    escenario.IdSolicitudSinMarca, escenario.IdEspecialistaInactivo),
                CancellationToken.None);
            Assert.Equal(EstadoAsignarEspecialista.SinCompatibilidad, inactivo.Estado);

            var inactivoEspecialista = await store.AsignarAsync(
                new DatosAsignacion(
                    escenario.IdSolicitudSinMarca, escenario.IdEspecialistaUsuarioInactivo),
                CancellationToken.None);
            Assert.Equal(EstadoAsignarEspecialista.SinCompatibilidad, inactivoEspecialista.Estado);

            var inexistente = await store.AsignarAsync(
                new DatosAsignacion(escenario.IdSolicitudSinMarca, int.MaxValue),
                CancellationToken.None);
            Assert.Equal(EstadoAsignarEspecialista.EspecialistaNoEncontrado, inexistente.Estado);

            Assert.Equal("PENDIENTE_ASIGNACION",
                await db.SolicitudEvaluacions.AsNoTracking()
                    .Where(s => s.IdSolicitud == escenario.IdSolicitudSinMarca)
                    .Select(s => s.IdEstadoSolicitudNavigation.Codigo)
                    .SingleAsync());
            Assert.False(await db.AsignacionEspecialista.AsNoTracking()
                .AnyAsync(a => a.IdSolicitud == escenario.IdSolicitudSinMarca));
        }
        finally
        {
            await LimpiarEscenarioAsync(db, escenario);
        }
    }

    [LocalDbFact]
    public async Task SolicitudEnOtroEstadoSeRechazaSinAsignacion()
    {
        await using var db = await SqlIntegration.AbrirContextoAsync();
        var escenario = await CrearEscenarioAsync(db);
        try
        {
            var store = new AsignacionStore(db);

            var resultado = await store.AsignarAsync(
                new DatosAsignacion(escenario.IdSolicitudBorrador, escenario.IdEspecialistaCategoria),
                CancellationToken.None);

            Assert.Equal(EstadoAsignarEspecialista.SolicitudNoAsignable, resultado.Estado);
            Assert.Equal("BORRADOR", resultado.EstadoSolicitudCodigo);
            Assert.False(await db.AsignacionEspecialista.AsNoTracking()
                .AnyAsync(a => a.IdSolicitud == escenario.IdSolicitudBorrador));
        }
        finally
        {
            await LimpiarEscenarioAsync(db, escenario);
        }
    }

    [LocalDbFact]
    public async Task DosAsignacionesConcurrentesSoloPersistenUna()
    {
        await using var db = await SqlIntegration.AbrirContextoAsync();
        var escenario = await CrearEscenarioAsync(db);
        await using var dbConcurrente = await SqlIntegration.AbrirContextoAsync();
        try
        {
            var primera = new AsignacionStore(db).AsignarAsync(
                new DatosAsignacion(escenario.IdSolicitudSinMarca, escenario.IdEspecialistaCategoria),
                CancellationToken.None);
            var segunda = new AsignacionStore(dbConcurrente).AsignarAsync(
                new DatosAsignacion(escenario.IdSolicitudSinMarca, escenario.IdEspecialistaMarca),
                CancellationToken.None);

            var resultados = await Task.WhenAll(primera, segunda);

            Assert.Equal(1, resultados.Count(r => r.Estado == EstadoAsignarEspecialista.Exito));
            Assert.All(resultados.Where(r => r.Estado != EstadoAsignarEspecialista.Exito),
                r => Assert.Contains(r.Estado, new[]
                {
                    EstadoAsignarEspecialista.YaAsignada,
                    EstadoAsignarEspecialista.ConflictoConcurrente
                }));
            Assert.Equal(1, await db.AsignacionEspecialista.AsNoTracking()
                .CountAsync(a => a.IdSolicitud == escenario.IdSolicitudSinMarca && a.Activa));
            Assert.Equal("ASIGNADA", await db.SolicitudEvaluacions.AsNoTracking()
                .Where(s => s.IdSolicitud == escenario.IdSolicitudSinMarca)
                .Select(s => s.IdEstadoSolicitudNavigation.Codigo)
                .SingleAsync());
        }
        finally
        {
            await LimpiarEscenarioAsync(db, escenario);
        }
    }

    private static async Task<Escenario> CrearEscenarioAsync(GradeXDbContext db)
    {
        var sufijo = Guid.NewGuid().ToString("N")[..10];
        var ahora = DateTime.UtcNow;
        var categoria = new Categorium
        {
            Nombre = $"Categoria HU006 {sufijo}",
            Descripcion = "Escenario aislado HU-006",
            Activa = true
        };
        var marcaA = new Marca { Nombre = $"Marca HU006 A {sufijo}", Activa = true };
        var marcaB = new Marca { Nombre = $"Marca HU006 B {sufijo}", Activa = true };
        db.Categoria.Add(categoria);
        db.Marcas.AddRange(marcaA, marcaB);
        await db.SaveChangesAsync();

        db.CategoriaMarcas.AddRange(
            new CategoriaMarca
            {
                IdCategoria = categoria.IdCategoria,
                IdMarca = marcaA.IdMarca,
                Activa = true
            },
            new CategoriaMarca
            {
                IdCategoria = categoria.IdCategoria,
                IdMarca = marcaB.IdMarca,
                Activa = true
            });
        await db.SaveChangesAsync();

        var idsUsuarios = new List<int>();
        var solicitante = await CrearUsuarioAsync(
            db, $"solicitante-hu006-{sufijo}@example.test", activo: true);
        idsUsuarios.Add(solicitante.IdUsuario);

        var categoriaEspecialista = await CrearEspecialistaAsync(
            db, $"especialista-categoria-{sufijo}@example.test", "ACTIVO", true, idsUsuarios);
        var marcaEspecialista = await CrearEspecialistaAsync(
            db, $"especialista-marca-{sufijo}@example.test", "ACTIVO", true, idsUsuarios);
        var inactivo = await CrearEspecialistaAsync(
            db, $"especialista-inactivo-{sufijo}@example.test", "INACTIVO", true, idsUsuarios);
        var usuarioInactivo = await CrearEspecialistaAsync(
            db, $"especialista-usuario-inactivo-{sufijo}@example.test", "ACTIVO", false, idsUsuarios);
        var areaInactiva = await CrearEspecialistaAsync(
            db, $"especialista-area-inactiva-{sufijo}@example.test", "ACTIVO", true, idsUsuarios);
        var otraMarca = await CrearEspecialistaAsync(
            db, $"especialista-otra-marca-{sufijo}@example.test", "ACTIVO", true, idsUsuarios);

        db.EspecialidadEspecialista.AddRange(
            Area(categoriaEspecialista, categoria.IdCategoria, null, true),
            Area(marcaEspecialista, categoria.IdCategoria, marcaA.IdMarca, true),
            Area(inactivo, categoria.IdCategoria, null, true),
            Area(usuarioInactivo, categoria.IdCategoria, null, true),
            Area(areaInactiva, categoria.IdCategoria, null, false),
            Area(otraMarca, categoria.IdCategoria, marcaB.IdMarca, true));
        await db.SaveChangesAsync();

        var estadoPendiente = await db.EstadoSolicituds.AsNoTracking()
            .SingleAsync(e => e.Codigo == "PENDIENTE_ASIGNACION");
        var estadoBorrador = await db.EstadoSolicituds.AsNoTracking()
            .SingleAsync(e => e.Codigo == "BORRADOR");

        var sinMarca = CrearSolicitud(
            solicitante.IdUsuario, categoria.IdCategoria, null, estadoPendiente.IdEstadoSolicitud,
            $"SOL-HU006-{sufijo}-A");
        var conMarca = CrearSolicitud(
            solicitante.IdUsuario, categoria.IdCategoria, marcaA.IdMarca, estadoPendiente.IdEstadoSolicitud,
            $"SOL-HU006-{sufijo}-B");
        var borrador = CrearSolicitud(
            solicitante.IdUsuario, categoria.IdCategoria, null, estadoBorrador.IdEstadoSolicitud,
            $"SOL-HU006-{sufijo}-C");
        db.SolicitudEvaluacions.AddRange(sinMarca, conMarca, borrador);
        await db.SaveChangesAsync();

        return new Escenario(
            categoria.IdCategoria,
            marcaA.IdMarca,
            marcaB.IdMarca,
            sinMarca.IdSolicitud,
            conMarca.IdSolicitud,
            borrador.IdSolicitud,
            categoriaEspecialista,
            marcaEspecialista,
            inactivo,
            usuarioInactivo,
            areaInactiva,
            otraMarca,
            idsUsuarios);
    }

    private static EspecialidadEspecialistum Area(
        int idEspecialista, int idCategoria, int? idMarca, bool activa) =>
        new()
        {
            IdEspecialista = idEspecialista,
            IdCategoria = idCategoria,
            IdMarca = idMarca,
            Nivel = "EXPERTO",
            Activa = activa,
            FechaRegistro = DateTime.UtcNow
        };

    private static async Task<Usuario> CrearUsuarioAsync(
        GradeXDbContext db, string correo, bool activo)
    {
        var usuario = new Usuario
        {
            NombreCompleto = correo,
            Correo = correo,
            PasswordHash = "hash-sintetico",
            Activo = activo,
            FechaCreacion = DateTime.UtcNow
        };
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();
        return usuario;
    }

    private static async Task<int> CrearEspecialistaAsync(
        GradeXDbContext db,
        string correo,
        string estado,
        bool usuarioActivo,
        List<int> idsUsuarios)
    {
        var usuario = await CrearUsuarioAsync(db, correo, usuarioActivo);
        idsUsuarios.Add(usuario.IdUsuario);
        var especialista = new Especialistum
        {
            IdUsuario = usuario.IdUsuario,
            Estado = estado,
            FechaAlta = DateTime.UtcNow
        };
        db.Especialista.Add(especialista);
        await db.SaveChangesAsync();
        return especialista.IdEspecialista;
    }

    private static SolicitudEvaluacion CrearSolicitud(
        int idSolicitante,
        int idCategoria,
        int? idMarca,
        int idEstado,
        string folio) =>
        new()
        {
            Folio = folio,
            IdSolicitante = idSolicitante,
            IdEstadoSolicitud = idEstado,
            FechaSolicitud = DateTime.UtcNow,
            FechaUltimoCambio = DateTime.UtcNow,
            IdProductoNavigation = new Producto
            {
                IdPropietario = idSolicitante,
                IdCategoria = idCategoria,
                IdMarca = idMarca,
                Nombre = "Producto de prueba HU-006",
                FechaRegistro = DateTime.UtcNow,
                Activo = true
            }
        };

    private static async Task LimpiarEscenarioAsync(GradeXDbContext db, Escenario escenario)
    {
        var solicitudes = new[]
        {
            escenario.IdSolicitudSinMarca,
            escenario.IdSolicitudConMarca,
            escenario.IdSolicitudBorrador
        };
        await db.AsignacionEspecialista
            .Where(a => solicitudes.Contains(a.IdSolicitud))
            .ExecuteDeleteAsync();
        await db.Evaluacions
            .Where(e => solicitudes.Contains(e.IdSolicitud))
            .ExecuteDeleteAsync();
        await db.Evidencia
            .Where(e => solicitudes.Contains(e.IdSolicitud))
            .ExecuteDeleteAsync();
        await db.SolicitudEvaluacions
            .Where(s => solicitudes.Contains(s.IdSolicitud))
            .ExecuteDeleteAsync();
        await db.Productos
            .Where(p => escenario.IdsUsuarios.Contains(p.IdPropietario))
            .ExecuteDeleteAsync();
        await db.EspecialidadEspecialista
            .Where(e => escenario.IdsEspecialistas.Contains(e.IdEspecialista))
            .ExecuteDeleteAsync();
        await db.Especialista
            .Where(e => escenario.IdsEspecialistas.Contains(e.IdEspecialista))
            .ExecuteDeleteAsync();
        await db.SesionUsuarios
            .Where(s => escenario.IdsUsuarios.Contains(s.IdUsuario))
            .ExecuteDeleteAsync();
        await db.UsuarioRols
            .Where(ur => escenario.IdsUsuarios.Contains(ur.IdUsuario))
            .ExecuteDeleteAsync();
        await db.Usuarios
            .Where(u => escenario.IdsUsuarios.Contains(u.IdUsuario))
            .ExecuteDeleteAsync();
        await db.CategoriaMarcas
            .Where(cm => cm.IdCategoria == escenario.IdCategoria)
            .ExecuteDeleteAsync();
        await db.Marcas
            .Where(m => m.IdMarca == escenario.IdMarcaA || m.IdMarca == escenario.IdMarcaB)
            .ExecuteDeleteAsync();
        await db.Categoria
            .Where(c => c.IdCategoria == escenario.IdCategoria)
            .ExecuteDeleteAsync();
    }

    private sealed record Escenario(
        int IdCategoria,
        int IdMarcaA,
        int IdMarcaB,
        int IdSolicitudSinMarca,
        int IdSolicitudConMarca,
        int IdSolicitudBorrador,
        int IdEspecialistaCategoria,
        int IdEspecialistaMarca,
        int IdEspecialistaInactivo,
        int IdEspecialistaUsuarioInactivo,
        int IdEspecialistaAreaInactiva,
        int IdEspecialistaOtraMarca,
        IReadOnlyCollection<int> IdsUsuarios)
    {
        public IReadOnlyCollection<int> IdsEspecialistas =>
        [
            IdEspecialistaCategoria,
            IdEspecialistaMarca,
            IdEspecialistaInactivo,
            IdEspecialistaUsuarioInactivo,
            IdEspecialistaAreaInactiva,
            IdEspecialistaOtraMarca
        ];
    }
}

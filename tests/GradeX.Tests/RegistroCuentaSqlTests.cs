using GradeX.Application.Cuentas;
using GradeX.Infrastructure.Persistence;
using GradeX.Infrastructure.Persistence.Commands;
using GradeX.Infrastructure.Persistence.Models;
using GradeX.Infrastructure.Persistence.Queries;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using System.Globalization;

namespace GradeX.Tests;

public sealed class RegistroCuentaSqlTests
{
    [LocalDbFact]
    public async Task CorreoEnMayusculasRespetaIndiceUnico()
    {
        var correo = $"test-{Guid.NewGuid():N}@example.test";
        await using var contexto = await AbrirContextoAsync();
        try
        {
            var store = new RegistroCuentaStore(contexto);
            Assert.Equal(EstadoRegistroCuenta.Exito, (await store.RegistrarAsync("Test", correo, "hash-sintetico", CancellationToken.None)).Estado);
            Assert.True(await store.ExisteCorreoAsync(correo.ToUpperInvariant(), CancellationToken.None));
            Assert.Equal(EstadoRegistroCuenta.CorreoDuplicado, (await store.RegistrarAsync("Test", correo.ToUpperInvariant(), "hash-sintetico", CancellationToken.None)).Estado);
        }
        finally
        {
            await contexto.UsuarioRols.Where(r => r.IdUsuarioNavigation.Correo == correo).ExecuteDeleteAsync();
            await contexto.Usuarios.Where(u => u.Correo == correo).ExecuteDeleteAsync();
        }
    }

    [LocalDbFact]
    public async Task CancelacionNoCreaUsuario()
    {
        var correo = $"test-{Guid.NewGuid():N}@example.test";
        await using var contexto = await AbrirContextoAsync();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new RegistroCuentaStore(contexto)
            .RegistrarAsync("Test", correo, "hash-sintetico", cts.Token));
        Assert.False(await contexto.Usuarios.AsNoTracking().AnyAsync(u => u.Correo == correo));
    }

    [LocalDbFact]
    public async Task FalloAlInsertarVinculoRevierteUsuario()
    {
        var correo = $"test-{Guid.NewGuid():N}@example.test";
        var interceptor = new FalloVinculo();
        await using var contexto = await AbrirContextoAsync(interceptor);
        var excepcion = await Assert.ThrowsAsync<DbUpdateException>(() => new RegistroCuentaStore(contexto)
            .RegistrarAsync("Test", correo, "hash-sintetico", CancellationToken.None));
        Assert.IsType<InvalidOperationException>(excepcion.InnerException);
        Assert.True(interceptor.Interceptado);
        Assert.False(await contexto.Usuarios.AsNoTracking().AnyAsync(u => u.Correo == correo));
    }

    [LocalDbFact]
    public async Task RegistroConcurrenteSoloCreaUnaCuentaYUnRol()
    {
        var correo = $"test-{Guid.NewGuid():N}@example.test";
        await using var first = await AbrirContextoAsync();
        await using var second = await AbrirContextoAsync();
        try
        {
            var tareas = new[]
            {
                new RegistroCuentaStore(first).RegistrarAsync("Test", correo, "hash-sintetico", CancellationToken.None),
                new RegistroCuentaStore(second).RegistrarAsync("Test", correo, "hash-sintetico", CancellationToken.None)
            };
            var resultados = await Task.WhenAll(tareas);
            Assert.Single(resultados, r => r.Estado == EstadoRegistroCuenta.Exito);
            Assert.Single(resultados, r => r.Estado == EstadoRegistroCuenta.CorreoDuplicado);
            var usuarios = await first.Usuarios.AsNoTracking().Where(u => u.Correo == correo).ToListAsync();
            var usuario = Assert.Single(usuarios);
            var roles = await first.UsuarioRols.AsNoTracking().Where(r => r.IdUsuario == usuario.IdUsuario)
                .Select(r => r.IdRolNavigation.Nombre).ToListAsync();
            Assert.Equal("Solicitante", Assert.Single(roles));
        }
        finally
        {
            await first.UsuarioRols.Where(r => r.IdUsuarioNavigation.Correo == correo).ExecuteDeleteAsync();
            await first.Usuarios.Where(u => u.Correo == correo).ExecuteDeleteAsync();
        }
    }

    [LocalDbFact]
    public async Task RolInexistenteNoPersisteUsuario()
    {
        var correo = $"test-{Guid.NewGuid():N}@example.test";
        await using var contexto = await AbrirContextoAsync();
        var resultado = await new RegistroCuentaStore(contexto, $"RolInexistente-{Guid.NewGuid():N}")
            .RegistrarAsync("Test", correo, "hash-sintetico", CancellationToken.None);

        Assert.Equal(EstadoRegistroCuenta.RolNoDisponible, resultado.Estado);
        Assert.False(await contexto.Usuarios.AsNoTracking().AnyAsync(u => u.Correo == correo));
    }

    [LocalDbFact]
    public async Task RolInactivoNoPersisteUsuario()
    {
        var correo = $"test-{Guid.NewGuid():N}@example.test";
        var nombreRol = $"RolInactivo-{Guid.NewGuid():N}";
        await using var contexto = await AbrirContextoAsync();
        var rol = new Rol { Nombre = nombreRol, Activo = false };
        contexto.Rols.Add(rol);
        await contexto.SaveChangesAsync();
        try
        {
            var resultado = await new RegistroCuentaStore(contexto, nombreRol)
                .RegistrarAsync("Test", correo, "hash-sintetico", CancellationToken.None);

            Assert.Equal(EstadoRegistroCuenta.RolNoDisponible, resultado.Estado);
            Assert.False(await contexto.Usuarios.AsNoTracking().AnyAsync(u => u.Correo == correo));
        }
        finally
        {
            contexto.Rols.Remove(rol);
            await contexto.SaveChangesAsync();
        }
    }

    [LocalDbFact]
    public async Task IndiceUnicoYCollationEfectiva()
    {
        await using var contexto = await AbrirContextoAsync();
        await contexto.Database.OpenConnectionAsync();
        try
        {
            var indice = await ScalarAsync(contexto, """
                SELECT COUNT(*)
                FROM sys.indexes i
                JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
                JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                WHERE i.name = 'UQ_Usuario_Correo' AND c.name = 'Correo'
                  AND i.is_unique = 1 AND i.is_disabled = 0 AND i.has_filter = 0
                """);
            Assert.Equal(1, indice);

            var mayusculas = await ScalarAsync(contexto,
                "SELECT CASE WHEN CONVERT(nvarchar(254), 'a@example.test') = CONVERT(nvarchar(254), 'A@example.test') THEN 1 ELSE 0 END");
            var espacioFinal = await ScalarAsync(contexto,
                "SELECT CASE WHEN CONVERT(nvarchar(254), 'a@example.test') = CONVERT(nvarchar(254), 'a@example.test ') THEN 1 ELSE 0 END");

            Assert.Equal(1, mayusculas);
            Assert.Equal(1, espacioFinal);
        }
        finally
        {
            await contexto.Database.CloseConnectionAsync();
        }
    }

    [LocalDbFact]
    public async Task CredencialesStoreDevuelveUsuarioActivoYRolesReales()
    {
        var correo = $"test-{Guid.NewGuid():N}@example.test";
        await using var contexto = await AbrirContextoAsync();
        try
        {
            var creado = await new RegistroCuentaStore(contexto)
                .RegistrarAsync("Test", correo, "hash-sintetico", CancellationToken.None);
            Assert.Equal(EstadoRegistroCuenta.Exito, creado.Estado);

            var credenciales = await new CredencialesStore(contexto)
                .BuscarPorCorreoAsync(correo, CancellationToken.None);

            Assert.NotNull(credenciales);
            Assert.True(credenciales.Activo);
            Assert.Equal("hash-sintetico", credenciales.PasswordHash);
            Assert.Equal("Solicitante", Assert.Single(credenciales.Roles));
        }
        finally
        {
            await contexto.UsuarioRols.Where(r => r.IdUsuarioNavigation.Correo == correo).ExecuteDeleteAsync();
            await contexto.Usuarios.Where(u => u.Correo == correo).ExecuteDeleteAsync();
        }
    }

    private static async Task<int> ScalarAsync(GradeXDbContext contexto, string sql)
    {
        await using var command = contexto.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private static async Task<GradeXDbContext> AbrirContextoAsync(DbCommandInterceptor? interceptor = null)
    {
        var cadena = Environment.GetEnvironmentVariable("GRADEX_INTEGRATION_TEST_CONNECTION")!;
        var builder = new SqlConnectionStringBuilder(cadena);
        if (!string.Equals(builder.DataSource, @"(localdb)\MSSQLLocalDB", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(builder.InitialCatalog, "GradeX_IntegrationTests", StringComparison.Ordinal) || !builder.IntegratedSecurity)
            throw new InvalidOperationException("Destino de integración no permitido.");

        var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT DB_NAME(), CONVERT(nvarchar(128), SERVERPROPERTY('InstanceName'))";
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync() || reader.GetString(0) != "GradeX_IntegrationTests" ||
            !reader.GetString(1).StartsWith("LOCALDB#", StringComparison.OrdinalIgnoreCase))
        {
            await connection.DisposeAsync();
            throw new InvalidOperationException("Destino efectivo de integración no permitido.");
        }
        await reader.DisposeAsync();
        var optionsBuilder = new DbContextOptionsBuilder<GradeXDbContext>().UseSqlServer(connection, contextOwnsConnection: true);
        if (interceptor is not null) optionsBuilder.AddInterceptors(interceptor);
        var options = optionsBuilder.Options;
        return new GradeXDbContext(options);
    }

    private sealed class FalloVinculo : DbCommandInterceptor
    {
        public bool Interceptado { get; private set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("INSERT INTO [UsuarioRol]", StringComparison.Ordinal))
            {
                Interceptado = true;
                throw new InvalidOperationException("Fallo sintético de vínculo.");
            }
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private sealed class LocalDbFactAttribute : FactAttribute
    {
        public LocalDbFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GRADEX_INTEGRATION_TEST_CONNECTION")))
                Skip = "Requiere conexión explícita a GradeX_IntegrationTests en MSSQLLocalDB.";
        }
    }
}

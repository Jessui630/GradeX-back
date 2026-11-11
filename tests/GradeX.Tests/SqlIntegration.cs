using GradeX.Application.Cuentas;
using GradeX.Infrastructure.Persistence;
using GradeX.Infrastructure.Persistence.Commands;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GradeX.Tests;

internal static class SqlIntegration
{
    public static async Task<GradeXDbContext> AbrirContextoAsync(
        DbCommandInterceptor? interceptor = null)
    {
        var cadena = Environment.GetEnvironmentVariable("GRADEX_INTEGRATION_TEST_CONNECTION")!;
        var builder = new SqlConnectionStringBuilder(cadena);
        if (!string.Equals(builder.DataSource, @"(localdb)\MSSQLLocalDB", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(builder.InitialCatalog, "GradeX_IntegrationTests", StringComparison.Ordinal) ||
            !builder.IntegratedSecurity)
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
        var optionsBuilder = new DbContextOptionsBuilder<GradeXDbContext>()
            .UseSqlServer(connection, contextOwnsConnection: true);
        if (interceptor is not null) optionsBuilder.AddInterceptors(interceptor);
        return new GradeXDbContext(optionsBuilder.Options);
    }

    public static async Task<int> CrearUsuarioAsync(GradeXDbContext db, string correo)
    {
        var resultado = await new RegistroCuentaStore(db)
            .RegistrarAsync("Usuario de prueba", correo, "hash-sintetico", CancellationToken.None);
        if (resultado.Estado != EstadoRegistroCuenta.Exito)
            throw new InvalidOperationException($"No se pudo crear el usuario de prueba: {resultado.Estado}.");
        return resultado.IdUsuario!.Value;
    }

    public static async Task LimpiarUsuarioAsync(GradeXDbContext db, string correo)
    {
        var ids = await db.Usuarios.Where(u => u.Correo == correo)
            .Select(u => u.IdUsuario).ToListAsync();
        var solicitudes = await db.SolicitudEvaluacions
            .Where(s => ids.Contains(s.IdSolicitante)).Select(s => s.IdSolicitud).ToListAsync();
        await db.AsignacionEspecialista.Where(a => solicitudes.Contains(a.IdSolicitud)).ExecuteDeleteAsync();
        await db.Evaluacions.Where(e => solicitudes.Contains(e.IdSolicitud)).ExecuteDeleteAsync();
        await db.Evidencia.Where(e => solicitudes.Contains(e.IdSolicitud)).ExecuteDeleteAsync();
        await db.SolicitudEvaluacions.Where(s => solicitudes.Contains(s.IdSolicitud)).ExecuteDeleteAsync();
        await db.Productos.Where(p => ids.Contains(p.IdPropietario)).ExecuteDeleteAsync();
        await db.SesionUsuarios.Where(s => ids.Contains(s.IdUsuario)).ExecuteDeleteAsync();
        await db.UsuarioRols.Where(ur => ids.Contains(ur.IdUsuario)).ExecuteDeleteAsync();
        await db.Usuarios.Where(u => ids.Contains(u.IdUsuario)).ExecuteDeleteAsync();
    }
}

internal sealed class LocalDbFactAttribute : FactAttribute
{
    public LocalDbFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GRADEX_INTEGRATION_TEST_CONNECTION")))
            Skip = "Requiere conexión explícita a GradeX_IntegrationTests en MSSQLLocalDB.";
    }
}

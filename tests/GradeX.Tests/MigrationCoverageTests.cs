using GradeX.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GradeX.Tests;

public sealed class MigrationCoverageTests
{
    [LocalDbFact]
    public async Task MigracionesSeAplicanEnBaseNuevaYRegistranHistorial()
    {
        var env = Environment.GetEnvironmentVariable("GRADEX_INTEGRATION_TEST_CONNECTION")!;
        var envBuilder = new SqlConnectionStringBuilder(env);
        Assert.Equal(@"(localdb)\MSSQLLocalDB", envBuilder.DataSource,
            ignoreCase: true);

        var database = $"GradeX_Coverage_{Guid.NewGuid():N}";
        var master = new SqlConnectionStringBuilder
        {
            DataSource = envBuilder.DataSource,
            InitialCatalog = "master",
            IntegratedSecurity = true,
            TrustServerCertificate = true,
            ConnectTimeout = 30
        };
        using var masterConnection = new SqlConnection(master.ConnectionString);
        await masterConnection.OpenAsync();
        await using (var create = masterConnection.CreateCommand())
        {
            create.CommandText = $"CREATE DATABASE [{database}]";
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            var target = new SqlConnectionStringBuilder(master.ConnectionString)
            {
                InitialCatalog = database,
                Pooling = false
            };
            var options = new DbContextOptionsBuilder<GradeXDbContext>()
                .UseSqlServer(target.ConnectionString)
                .Options;
            await using var db = new GradeXDbContext(options);
            await db.Database.MigrateAsync();

            var history = await db.Database
                .SqlQueryRaw<string>("SELECT MigrationId FROM __EFMigrationsHistory")
                .ToListAsync();
            Assert.True(history.Count >= 7,
                $"Se esperaban al menos 7 migraciones y se encontraron {history.Count}.");
            Assert.Contains("20261118191412_RepararCatalogoSprint1", history);
            Assert.Contains("20261118195907_Sprint2EstadosNotificaciones", history);
            Assert.True(await db.Categoria.AnyAsync());
            Assert.True(await db.EstadoSolicituds
                .AnyAsync(e => e.Codigo == "ASIGNADA"));
            Assert.True(await db.TipoEvidencia.AnyAsync(t => t.Codigo == "ETIQUETA"));
        }
        finally
        {
            await using var drop = masterConnection.CreateCommand();
            drop.CommandText =
                $"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
                $"DROP DATABASE [{database}]";
            await drop.ExecuteNonQueryAsync();
        }
    }
}

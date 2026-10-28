using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GradeX.Infrastructure.Persistence;

/// <summary>
/// Permite que las herramientas de EF Core generen migraciones y scripts sin
/// arrancar la API ni depender de user-secrets o de una base disponible.
/// </summary>
public sealed class DesignTimeDbContextFactory
    : IDesignTimeDbContextFactory<GradeXDbContext>
{
    private const string DefaultConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=GradeX_DesignTime;" +
        "Trusted_Connection=True;TrustServerCertificate=True";

    public GradeXDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("GRADEX_DESIGN_TIME_CONNECTION")
            ?? DefaultConnectionString;

        var options = new DbContextOptionsBuilder<GradeXDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new GradeXDbContext(options);
    }
}

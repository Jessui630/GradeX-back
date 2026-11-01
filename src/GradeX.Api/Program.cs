using GradeX.Api.Diagnostics;
using GradeX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var checkSql = args.Contains("--check-sql", StringComparer.Ordinal);
try
{
    var builder = WebApplication.CreateBuilder(args);
    if (checkSql)
    {
        builder.Logging.ClearProviders();
    }

    var connectionString = builder.Configuration
        .GetConnectionString("GradeX")
        ?? throw new InvalidOperationException(
            "No se encontró la conexión GradeX."
        );

    builder.Services.AddDbContext<GradeXDbContext>(
        options => options.UseSqlServer(connectionString)
    );

    // Add services to the container.

    builder.Services.AddControllers();
    // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
    builder.Services.AddOpenApi();

    var app = builder.Build();

    if (checkSql)
    {
        await using (app)
        {
            Environment.ExitCode = await SqlConnectivityCheck.RunAsync(app.Services);
        }
        return;
    }

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.UseHttpsRedirection();

    app.UseAuthorization();

    app.MapControllers();

    app.Run();
}
catch (Exception) when (checkSql)
{
    Console.Error.WriteLine("SQL connectivity check failed.");
    Environment.ExitCode = 1;
}

public partial class Program { }

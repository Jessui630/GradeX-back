using System.Data;
using GradeX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GradeX.Api.Diagnostics;

public static class SqlConnectivityCheck
{
    public static async Task<int> RunAsync(IServiceProvider services)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        return await RunAsync(async cancellationToken =>
        {
            await using var scope = services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<GradeXDbContext>();
            var connection = context.Database.GetDbConnection();
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1 AS Value";
            command.CommandType = CommandType.Text;
            command.CommandTimeout = 15;
            return await command.ExecuteScalarAsync(cancellationToken);
        }, timeout.Token);
    }

    public static async Task<int> RunAsync(
        Func<CancellationToken, Task<object?>> query,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await query(cancellationToken);
            if (!cancellationToken.IsCancellationRequested && result is int value && value == 1)
            {
                Console.WriteLine("SQL connectivity check succeeded.");
                return 0;
            }
        }
        catch (Exception)
        {
        }

        Console.Error.WriteLine("SQL connectivity check failed.");
        return 1;
    }
}

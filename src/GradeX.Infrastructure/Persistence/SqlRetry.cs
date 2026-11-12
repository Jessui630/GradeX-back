using Microsoft.Data.SqlClient;

namespace GradeX.Infrastructure.Persistence;

/// <summary>
/// Reintentos acotados para lecturas ante errores transitorios de Azure SQL.
/// No se usa en escrituras ni en transacciones explícitas para no duplicar efectos.
/// </summary>
public static class SqlRetry
{
    private const int MaxIntentos = 3;

    private static readonly HashSet<int> ErroresTransitorios =
    [
        20, 64, 233, -2,
        1205,
        40197, 40501, 40540, 40613,
        49918, 49919, 49920,
        10053, 10054, 10060,
        10928, 10929
    ];

    public static bool EsTransitorio(SqlException exception) =>
        exception.Errors.Cast<SqlError>().Any(error => ErroresTransitorios.Contains(error.Number));

    public static async Task<T> EjecutarAsync<T>(
        Func<CancellationToken, Task<T>> operacion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operacion);

        for (var intento = 1; ; intento++)
        {
            try
            {
                return await operacion(cancellationToken);
            }
            catch (SqlException exception) when (intento < MaxIntentos && EsTransitorio(exception))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250 * intento), cancellationToken);
            }
        }
    }
}

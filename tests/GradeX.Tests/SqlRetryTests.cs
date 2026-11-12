using GradeX.Infrastructure.Persistence;

namespace GradeX.Tests;

public sealed class SqlRetryTests
{
    [Fact]
    public async Task OperacionExitosaNoSeReintenta()
    {
        var intentos = 0;

        var resultado = await SqlRetry.EjecutarAsync<int>(_ =>
        {
            intentos++;
            return Task.FromResult(42);
        }, CancellationToken.None);

        Assert.Equal(42, resultado);
        Assert.Equal(1, intentos);
    }

    [Fact]
    public async Task ErrorNoSqlSePropagaSinReintentar()
    {
        var intentos = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SqlRetry.EjecutarAsync<int>(_ =>
            {
                intentos++;
                throw new InvalidOperationException("fallo sintético");
            }, CancellationToken.None));

        Assert.Equal(1, intentos);
    }
}

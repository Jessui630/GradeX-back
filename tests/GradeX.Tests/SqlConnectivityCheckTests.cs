using System.Diagnostics;
using GradeX.Api.Diagnostics;

namespace GradeX.Tests;

public class SqlConnectivityCheckTests
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(0, 1)]
    [InlineData(2, 1)]
    [InlineData(null, 1)]
    public async Task ResultMustBeExactlyOne(int? result, int expected)
    {
        var exitCode = await SqlConnectivityCheck.RunAsync(
            _ => Task.FromResult<object?>(result), CancellationToken.None);

        Assert.Equal(expected, exitCode);
    }

    [Fact]
    public async Task ExceptionReturnsFailure()
    {
        var exitCode = await SqlConnectivityCheck.RunAsync(
            _ => throw new InvalidOperationException("sensitive-test-value"), CancellationToken.None);

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task CancellationReturnsFailure()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var exitCode = await SqlConnectivityCheck.RunAsync(
            token => Task.FromCanceled<object?>(token), cancellation.Token);

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task ProcessFailsSafelyWithoutStartingHttp()
    {
        using var process = new Process();
        process.StartInfo.FileName = "dotnet";
        process.StartInfo.ArgumentList.Add(typeof(Program).Assembly.Location);
        process.StartInfo.ArgumentList.Add("--check-sql");
        process.StartInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        process.StartInfo.Environment["ConnectionStrings__GradeX"] = "invalid-secret-marker";
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        Assert.True(process.Start());
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        await process.WaitForExitAsync(timeout.Token);
        var output = await stdout + await stderr;

        Assert.NotEqual(0, process.ExitCode);
        Assert.Contains("SQL connectivity check failed.", output, StringComparison.Ordinal);
        Assert.DoesNotContain("invalid-secret-marker", output, StringComparison.Ordinal);
        Assert.DoesNotContain("Now listening", output, StringComparison.OrdinalIgnoreCase);
    }
}

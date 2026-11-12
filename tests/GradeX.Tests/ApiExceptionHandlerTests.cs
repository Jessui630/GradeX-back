using System.Text.Json;
using GradeX.Api.Security;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace GradeX.Tests;

public sealed class ApiExceptionHandlerTests
{
    [Fact]
    public async Task ExcepcionNoTransitoriaDevuelveProblemDetails500ConTraceId()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/solicitudes";
        context.Response.Body = new MemoryStream();
        context.RequestServices = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();
        context.Features.Set<IExceptionHandlerFeature>(
            new Feature(new InvalidOperationException("fallo sintético")));

        await ApiExceptionHandler.HandleAsync(context);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);
        context.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal(500, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Error interno", json.RootElement.GetProperty("title").GetString());
        Assert.False(string.IsNullOrWhiteSpace(
            json.RootElement.GetProperty("traceId").GetString()));
    }

    private sealed class Feature(Exception error) : IExceptionHandlerFeature
    {
        public Exception Error { get; } = error;

        public string Path { get; } = "/api/solicitudes";

        public Endpoint? Endpoint { get; }
    }
}

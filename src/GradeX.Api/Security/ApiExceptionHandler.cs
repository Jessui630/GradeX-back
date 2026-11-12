using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using GradeX.Infrastructure.Persistence;

namespace GradeX.Api.Security;

public static class ApiExceptionHandler
{
    private static readonly Action<ILogger, string, string, string, Exception?> RegistrarError =
        LoggerMessage.Define<string, string, string>(
            LogLevel.Error,
            new EventId(1, "ErrorNoControlado"),
            "Error no controlado en {Method} {Path}. TraceId: {TraceId}");

    public static async Task HandleAsync(HttpContext context)
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        var logger = context.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("GradeX.Api.Errors");

        RegistrarError(
            logger,
            context.Request.Method,
            context.Request.Path,
            context.TraceIdentifier,
            exception);

        if (context.Response.HasStarted)
            return;

        var sqlException = BuscarSqlException(exception);
        var transitorio = sqlException is not null && SqlRetry.EsTransitorio(sqlException);
        var estado = transitorio
            ? StatusCodes.Status503ServiceUnavailable
            : StatusCodes.Status500InternalServerError;

        var problema = new ProblemDetails
        {
            Status = estado,
            Title = transitorio ? "Servicio no disponible" : "Error interno",
            Detail = transitorio
                ? "El servicio de datos no está disponible temporalmente. Intenta nuevamente."
                : "No fue posible completar la operación."
        };
        problema.Extensions["traceId"] = context.TraceIdentifier;

        context.Response.Clear();
        context.Response.StatusCode = estado;
        if (transitorio)
            context.Response.Headers.RetryAfter = "5";
        await context.Response.WriteAsJsonAsync(
            problema,
            options: null,
            contentType: "application/problem+json",
            cancellationToken: context.RequestAborted);
    }

    private static SqlException? BuscarSqlException(Exception? exception)
    {
        while (exception is not null)
        {
            if (exception is SqlException sqlException)
                return sqlException;
            exception = exception.InnerException;
        }

        return null;
    }
}

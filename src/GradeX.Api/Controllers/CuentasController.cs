using GradeX.Application.Cuentas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GradeX.Api.Controllers;

public sealed record RegistrarCuentaRequest(string? NombreCompleto, string? Correo, string? Contrasena);

public sealed record CuentaCreadaResponse(int IdUsuario);

[ApiController]
[Route("api/cuentas")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CuentasController(RegistrarCuenta registrarCuenta) : ControllerBase
{
    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting("registro")]
    [Consumes("application/json")]
    [RequestSizeLimit(4_096)]
    [ProducesResponseType<CuentaCreadaResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Registrar(
        [FromBody] RegistrarCuentaRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return Validacion("nombreCompleto", "La solicitud no contiene datos.");

        var resultado = await registrarCuenta.EjecutarAsync(
            new RegistroCuenta(request.NombreCompleto, request.Correo, request.Contrasena),
            cancellationToken);

        return resultado.Estado switch
        {
            EstadoRegistroCuenta.Exito => StatusCode(
                StatusCodes.Status201Created,
                new CuentaCreadaResponse(resultado.IdUsuario!.Value)),
            EstadoRegistroCuenta.NombreInvalido => Validacion(
                "nombreCompleto", "El nombre completo es obligatorio y admite hasta 150 caracteres."),
            EstadoRegistroCuenta.CorreoInvalido => Validacion(
                "correo", "El correo electrónico no tiene un formato válido."),
            EstadoRegistroCuenta.ContrasenaInvalida => Validacion(
                "contrasena",
                "La contraseña debe tener entre 15 y 128 caracteres y no estar en listas de contraseñas comprometidas."),
            EstadoRegistroCuenta.CorreoDuplicado => Problema(
                StatusCodes.Status409Conflict,
                "No se pudo registrar la cuenta",
                "El correo ya está registrado.",
                "correo",
                "El correo ya está registrado."),
            EstadoRegistroCuenta.ComprobacionContrasenaNoDisponible => Problema(
                StatusCodes.Status503ServiceUnavailable,
                "Servicio no disponible",
                "No fue posible verificar la contraseña en este momento. Intenta nuevamente."),
            EstadoRegistroCuenta.RolNoDisponible => Problema(
                StatusCodes.Status503ServiceUnavailable,
                "Servicio no disponible",
                "El registro no está disponible temporalmente. Intenta nuevamente."),
            _ => Problema(
                StatusCodes.Status500InternalServerError,
                "Error interno",
                "No fue posible completar el registro.")
        };
    }

    private ActionResult Validacion(string campo, string mensaje) =>
        ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
        {
            [campo] = [mensaje]
        })
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Solicitud inválida"
        });

    private static ObjectResult Problema(int estado, string titulo, string detalle) =>
        new(new ProblemDetails { Status = estado, Title = titulo, Detail = detalle })
        {
            StatusCode = estado,
            ContentTypes = { "application/problem+json" }
        };

    private static ObjectResult Problema(
        int estado, string titulo, string detalle, string campo, string mensaje)
    {
        var problema = new ProblemDetails { Status = estado, Title = titulo, Detail = detalle };
        problema.Extensions["errors"] = new Dictionary<string, string[]>
        {
            [campo] = [mensaje]
        };
        return new ObjectResult(problema)
        {
            StatusCode = estado,
            ContentTypes = { "application/problem+json" }
        };
    }
}

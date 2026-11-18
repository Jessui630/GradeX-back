using System.Globalization;
using System.Security.Claims;
using GradeX.Api.Security;
using GradeX.Application.Perfil;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GradeX.Api.Controllers;

public sealed record ActualizarPerfilRequest(
    string? NombreCompleto,
    bool? RecibirNotificaciones);

public sealed record CambiarContrasenaRequest(
    string? ContrasenaActual,
    string? ContrasenaNueva);

[ApiController]
[Authorize]
[Route("api/perfil")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PerfilController(
    IPerfilStore perfil,
    ActualizarPerfil actualizarPerfil,
    CambiarContrasena cambiarContrasena) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PerfilUsuario>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Obtener(CancellationToken cancellationToken)
    {
        if (!TryObtenerUsuario(out var idUsuario))
            return NoAutenticado();

        var resultado = await perfil.ObtenerAsync(idUsuario, cancellationToken);
        return resultado is null
            ? Problema(StatusCodes.Status404NotFound, "Perfil no encontrado",
                "La cuenta no existe o está inactiva.")
            : Ok(resultado);
    }

    [HttpPut]
    [ValidateCsrf]
    [EnableRateLimiting("solicitudes")]
    [Consumes("application/json")]
    [RequestSizeLimit(8_192)]
    [ProducesResponseType<PerfilUsuario>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Actualizar(
        [FromBody] ActualizarPerfilRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryObtenerUsuario(out var idUsuario))
            return NoAutenticado();
        if (request is null)
            return Validacion("nombreCompleto", "La solicitud no contiene datos.");

        var resultado = await actualizarPerfil.EjecutarAsync(
            idUsuario,
            new DatosPerfil(request.NombreCompleto, request.RecibirNotificaciones),
            cancellationToken);
        return Resultado(resultado);
    }

    [HttpPut("contrasena")]
    [ValidateCsrf]
    [EnableRateLimiting("login")]
    [Consumes("application/json")]
    [RequestSizeLimit(8_192)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CambiarContrasena(
        [FromBody] CambiarContrasenaRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryObtenerUsuario(out var idUsuario))
            return NoAutenticado();
        if (request is null)
            return Validacion("contrasenaActual", "La solicitud no contiene datos.");

        var resultado = await cambiarContrasena.EjecutarAsync(
            idUsuario,
            new DatosCambioContrasena(request.ContrasenaActual, request.ContrasenaNueva),
            cancellationToken);
        if (resultado.Estado == EstadoPerfil.Exito)
            return NoContent();
        return Resultado(resultado);
    }

    private ActionResult Resultado(ResultadoPerfil resultado) =>
        resultado.Estado switch
        {
            EstadoPerfil.Exito when resultado.Perfil is not null =>
                Ok(resultado.Perfil),
            EstadoPerfil.Exito => NoContent(),
            EstadoPerfil.NoEncontrado => Problema(
                StatusCodes.Status404NotFound, "Perfil no encontrado",
                "La cuenta no existe o está inactiva."),
            EstadoPerfil.DatosInvalidos or EstadoPerfil.ContrasenaInvalida or
            EstadoPerfil.ContrasenaIncorrecta => Validacion(
                resultado.Campo ?? "perfil",
                resultado.Detalle ?? "Los datos no son válidos."),
            EstadoPerfil.ServicioNoDisponible => Problema(
                StatusCodes.Status503ServiceUnavailable, "Servicio no disponible",
                resultado.Detalle ?? "No fue posible completar la operación."),
            _ => Problema(StatusCodes.Status500InternalServerError, "Error interno",
                "No fue posible completar la operación.")
        };

    private bool TryObtenerUsuario(out int idUsuario) =>
        int.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out idUsuario) && idUsuario > 0;

    private static ObjectResult NoAutenticado() =>
        Problema(StatusCodes.Status401Unauthorized, "No autenticado",
            "Inicia sesión para continuar.");

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
}

using GradeX.Api.Security;
using GradeX.Application.Asignaciones;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GradeX.Api.Controllers;

public sealed record AsignarEspecialistaRequest(int? IdEspecialista);

[ApiController]
[Authorize(Policy = "Administrador")]
[Route("api/administracion/solicitudes")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdministracionSolicitudesController(
    AsignarEspecialista asignarEspecialista,
    IAsignacionStore asignaciones) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ListadoSolicitudesAsignacion>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(
        [FromQuery] string? estado = null,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanoPagina = 20,
        CancellationToken cancellationToken = default)
    {
        var listado = await asignaciones.ListarSolicitudesAsync(
            estado, pagina, tamanoPagina, cancellationToken);
        return Ok(listado);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<SolicitudAsignacionDetalle>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Detalle(int id, CancellationToken cancellationToken)
    {
        var detalle = await asignaciones.ObtenerSolicitudAsync(id, cancellationToken);
        return detalle is null
            ? Problema(StatusCodes.Status404NotFound, "Solicitud no encontrada",
                "La solicitud indicada no existe.")
            : Ok(detalle);
    }

    [HttpGet("{id:int}/especialistas-compatibles")]
    [ProducesResponseType<CompatiblesSolicitud>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Compatibles(int id, CancellationToken cancellationToken)
    {
        var resultado = await asignaciones.ObtenerCompatiblesAsync(id, cancellationToken);
        return resultado is null
            ? Problema(StatusCodes.Status404NotFound, "Solicitud no encontrada",
                "La solicitud indicada no existe.")
            : Ok(resultado);
    }

    [HttpPost("{id:int}/asignacion")]
    [ValidateCsrf]
    [EnableRateLimiting("asignaciones")]
    [Consumes("application/json")]
    [RequestSizeLimit(4_096)]
    [ProducesResponseType<AsignacionCreada>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Asignar(
        int id,
        [FromBody] AsignarEspecialistaRequest? request,
        CancellationToken cancellationToken)
    {
        if (request?.IdEspecialista is not > 0)
            return Validacion(
                "idEspecialista", "Selecciona un especialista válido para la asignación.");

        var resultado = await asignarEspecialista.EjecutarAsync(
            new DatosAsignacion(id, request.IdEspecialista.Value), cancellationToken);

        return resultado.Estado switch
        {
            EstadoAsignarEspecialista.Exito => Created(
                $"/api/administracion/solicitudes/{id}", resultado.Asignacion),
            EstadoAsignarEspecialista.DatosInvalidos => Validacion(
                "idEspecialista", "La solicitud y el especialista son obligatorios."),
            EstadoAsignarEspecialista.SolicitudNoEncontrada => Problema(
                StatusCodes.Status404NotFound, "Solicitud no encontrada",
                "La solicitud indicada no existe."),
            EstadoAsignarEspecialista.EspecialistaNoEncontrado => Problema(
                StatusCodes.Status404NotFound, "Especialista no encontrado",
                "El especialista indicado no existe."),
            EstadoAsignarEspecialista.SolicitudNoAsignable => Problema(
                StatusCodes.Status409Conflict, "Solicitud no asignable",
                $"La solicitud está en estado {resultado.EstadoSolicitudNombre ?? resultado.EstadoSolicitudCodigo} y no admite una nueva asignación."),
            EstadoAsignarEspecialista.YaAsignada => ConflictoAsignacion(resultado.AsignacionExistente),
            EstadoAsignarEspecialista.SinCompatibilidad => Problema(
                StatusCodes.Status422UnprocessableEntity, "Especialista no compatible",
                "El especialista no está habilitado o no tiene compatibilidad registrada con la categoría o marca de la solicitud."),
            EstadoAsignarEspecialista.EstadoNoConfigurado => Problema(
                StatusCodes.Status503ServiceUnavailable, "Servicio no disponible",
                "El estado de asignación no está configurado. Contacta al administrador."),
            EstadoAsignarEspecialista.ConflictoConcurrente => Problema(
                StatusCodes.Status409Conflict, "Conflicto de concurrencia",
                "La solicitud fue modificada por otra asignación. Actualiza la vista e intenta nuevamente."),
            _ => Problema(StatusCodes.Status500InternalServerError, "Error interno",
                "No fue posible asignar el especialista.")
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

    private static ObjectResult ConflictoAsignacion(AsignacionActual? asignacion)
    {
        var problema = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Asignación existente",
            Detail = "La solicitud ya tiene un especialista asignado."
        };
        if (asignacion is not null)
            problema.Extensions["asignacionActual"] = asignacion;
        return new ObjectResult(problema)
        {
            StatusCode = StatusCodes.Status409Conflict,
            ContentTypes = { "application/problem+json" }
        };
    }

    private static ObjectResult Problema(int estado, string titulo, string detalle) =>
        new(new ProblemDetails { Status = estado, Title = titulo, Detail = detalle })
        {
            StatusCode = estado,
            ContentTypes = { "application/problem+json" }
        };
}

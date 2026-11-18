using GradeX.Api.Security;
using GradeX.Application.Administracion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GradeX.Api.Controllers;

public sealed record EspecialidadRequest(int IdCategoria, int? IdMarca, string? Nivel);
public sealed record EspecialistaRequest(
    int IdUsuario,
    string? Estado,
    short? AniosExperiencia,
    string? Biografia,
    IReadOnlyList<EspecialidadRequest>? Especialidades);

[ApiController]
[Authorize(Policy = "Administrador")]
[Route("api/administracion/especialistas")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdministracionEspecialistasController(
    IEspecialistaAdminStore especialistas) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<EspecialistaAdmin>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(CancellationToken cancellationToken) =>
        Ok(await especialistas.ListarAsync(cancellationToken));

    [HttpGet("usuarios")]
    [ProducesResponseType<IReadOnlyList<UsuarioCandidatoEspecialista>>(
        StatusCodes.Status200OK)]
    public async Task<IActionResult> Usuarios(
        [FromQuery] string? buscar, CancellationToken cancellationToken) =>
        Ok(await especialistas.BuscarUsuariosAsync(buscar, cancellationToken));

    [HttpPost]
    [ValidateCsrf]
    [EnableRateLimiting("asignaciones")]
    [ProducesResponseType<EspecialistaAdmin>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Crear(
        [FromBody] EspecialistaRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return Validacion("idUsuario", "La solicitud no contiene datos.");

        var resultado = await especialistas.CrearAsync(
            Mapear(request), cancellationToken);
        return Resultado(resultado, creado: true);
    }

    [HttpPut("{id:int}")]
    [ValidateCsrf]
    [EnableRateLimiting("asignaciones")]
    [ProducesResponseType<EspecialistaAdmin>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Actualizar(
        int id, [FromBody] EspecialistaRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return Validacion("idUsuario", "La solicitud no contiene datos.");

        var resultado = await especialistas.ActualizarAsync(
            id, Mapear(request), cancellationToken);
        return Resultado(resultado);
    }

    private static DatosEspecialistaAdmin Mapear(EspecialistaRequest request) =>
        new(
            request.IdUsuario,
            request.Estado,
            request.AniosExperiencia,
            request.Biografia,
            request.Especialidades?.Select(e => new DatosEspecialidadAdmin(
                e.IdCategoria, e.IdMarca, e.Nivel)).ToList() ?? []);

    private ActionResult Resultado<T>(ResultadoAdmin<T> resultado, bool creado = false) =>
        resultado.Estado switch
        {
            EstadoOperacionAdmin.Exito => creado
                ? StatusCode(StatusCodes.Status201Created, resultado.Valor)
                : Ok(resultado.Valor),
            EstadoOperacionAdmin.NoEncontrado => Problema(
                StatusCodes.Status404NotFound, "No encontrado",
                resultado.Detalle ?? "El especialista o usuario indicado no existe."),
            EstadoOperacionAdmin.DatosInvalidos => Validacion(
                resultado.Campo ?? "solicitud",
                resultado.Detalle ?? "Los datos no son válidos."),
            EstadoOperacionAdmin.Conflicto => Problema(
                StatusCodes.Status409Conflict, "Conflicto",
                resultado.Detalle ?? "El usuario ya tiene un perfil de especialista."),
            EstadoOperacionAdmin.NoPermitido => Problema(
                StatusCodes.Status403Forbidden, "No permitido",
                resultado.Detalle ?? "La operación no está permitida."),
            EstadoOperacionAdmin.EstadoNoConfigurado => Problema(
                StatusCodes.Status503ServiceUnavailable, "Servicio no disponible",
                resultado.Detalle ?? "Falta configuración del sistema."),
            _ => Problema(StatusCodes.Status500InternalServerError, "Error interno",
                "No fue posible completar la operación.")
        };

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

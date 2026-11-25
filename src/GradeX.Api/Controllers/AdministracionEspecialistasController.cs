using System.Text.Json.Serialization;
using GradeX.Api.Security;
using GradeX.Application.Administracion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GradeX.Api.Controllers;

public sealed record EspecialidadRequest(
    [property: JsonRequired] int IdCategoria,
    int? IdMarca,
    string? Nivel);
public sealed record EspecialistaRequest(
    [property: JsonRequired] int IdUsuario,
    string? Estado,
    short? AniosExperiencia,
    string? Biografia,
    IReadOnlyList<EspecialidadRequest>? Especialidades);

[ApiController]
[Authorize(Policy = "Administrador")]
[Route("api/administracion/especialistas")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdministracionEspecialistasController(
    IEspecialistaAdminStore especialistas) : ApiControllerBase
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
        return ResultadoAdmin(resultado, creado: true);
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
        return ResultadoAdmin(resultado);
    }

    private static DatosEspecialistaAdmin Mapear(EspecialistaRequest request) =>
        new(
            request.IdUsuario,
            request.Estado,
            request.AniosExperiencia,
            request.Biografia,
            request.Especialidades?.Select(e => new DatosEspecialidadAdmin(
                e.IdCategoria, e.IdMarca, e.Nivel)).ToList() ?? []);

}

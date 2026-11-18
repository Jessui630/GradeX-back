using System.Text.Json.Serialization;
using GradeX.Api.Security;
using GradeX.Application.Evaluaciones;
using GradeX.Application.Solicitudes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GradeX.Api.Controllers;

public sealed record ResultadoCriterioRequest(
    [property: JsonRequired] int IdCriterio,
    string? ValorTexto,
    decimal? ValorNumerico,
    bool? ValorBooleano,
    string? Observacion);

public sealed record GuardarEvaluacionRequest(
    decimal? ConditionGrade,
    decimal? TechnicalHealth,
    string? ObservacionesGenerales,
    IReadOnlyList<ResultadoCriterioRequest>? Resultados);

public sealed record FinalizarEvaluacionRequest(
    string? Dictamen,
    decimal? ConditionGrade,
    decimal? TechnicalHealth,
    string? ObservacionesGenerales,
    IReadOnlyList<ResultadoCriterioRequest>? Resultados);

public sealed record SolicitarEvidenciaRequest(
    int? IdTipoEvidencia,
    string? Descripcion);

[ApiController]
[Authorize(Policy = "Especialista")]
[Route("api/especialista/asignaciones")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class EspecialistaAsignacionesController(
    IEvaluacionStore evaluaciones,
    GradeX.Application.Certificados.ICertificadoStore certificados,
    IEvidenciaStorage almacenamiento) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<ListadoAsignacionesEspecialista>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(
        [FromQuery] string? estado = null,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanoPagina = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryObtenerUsuario(out var idUsuario))
            return NoAutenticado();

        return Ok(await evaluaciones.ListarAsignacionesAsync(
            idUsuario, estado, pagina, tamanoPagina, cancellationToken));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<AsignacionEspecialistaDetalle>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Detalle(int id, CancellationToken cancellationToken)
    {
        if (!TryObtenerUsuario(out var idUsuario))
            return NoAutenticado();

        var detalle = await evaluaciones.ObtenerAsignacionAsync(
            id, idUsuario, cancellationToken);
        return detalle is null
            ? NoEncontrada()
            : Ok(detalle);
    }

    [HttpPut("{id:int}/evaluacion")]
    [ValidateCsrf]
    [EnableRateLimiting("asignaciones")]
    [Consumes("application/json")]
    [RequestSizeLimit(128_000)]
    [ProducesResponseType<EvaluacionActual>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Guardar(
        int id,
        [FromBody] GuardarEvaluacionRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryObtenerUsuario(out var idUsuario))
            return NoAutenticado();
        if (request is null)
            return Validacion("evaluacion", "La solicitud no contiene datos.");

        var resultado = await evaluaciones.GuardarAsync(
            id, idUsuario,
            new DatosGuardarEvaluacion(
                request.ConditionGrade,
                request.TechnicalHealth,
                request.ObservacionesGenerales,
                Mapear(request.Resultados)),
            cancellationToken);
        return ResultadoEvaluacion(resultado);
    }

    [HttpPost("{id:int}/dictamen")]
    [ValidateCsrf]
    [EnableRateLimiting("asignaciones")]
    [Consumes("application/json")]
    [RequestSizeLimit(128_000)]
    [ProducesResponseType<EvaluacionActual>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Finalizar(
        int id,
        [FromBody] FinalizarEvaluacionRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryObtenerUsuario(out var idUsuario))
            return NoAutenticado();
        if (request is null)
            return Validacion("dictamen", "La solicitud no contiene datos.");

        var resultado = await evaluaciones.FinalizarAsync(
            id, idUsuario,
            new DatosFinalizarEvaluacion(
                request.Dictamen,
                request.ConditionGrade,
                request.TechnicalHealth,
                request.ObservacionesGenerales,
                Mapear(request.Resultados)),
            cancellationToken);
        return ResultadoEvaluacion(resultado);
    }

    [HttpPost("{id:int}/solicitar-evidencia")]
    [ValidateCsrf]
    [EnableRateLimiting("asignaciones")]
    [Consumes("application/json")]
    [RequestSizeLimit(8_192)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SolicitarEvidencia(
        int id,
        [FromBody] SolicitarEvidenciaRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryObtenerUsuario(out var idUsuario))
            return NoAutenticado();
        if (request is null)
            return Validacion("descripcion", "La solicitud no contiene datos.");

        var resultado = await evaluaciones.SolicitarEvidenciaAsync(
            id, idUsuario,
            new DatosSolicitudEvidencia(request.IdTipoEvidencia, request.Descripcion),
            cancellationToken);

        return resultado.Estado switch
        {
            EstadoOperacionEvaluacion.Exito => NoContent(),
            EstadoOperacionEvaluacion.AsignacionNoEncontrada => NoEncontrada(),
            EstadoOperacionEvaluacion.EstadoInvalido => Problema(
                StatusCodes.Status409Conflict, "Estado no editable",
                resultado.Detalle ?? "La solicitud no admite una solicitud de evidencia."),
            EstadoOperacionEvaluacion.DatosInvalidos or
            EstadoOperacionEvaluacion.CriterioNoPertenece => Validacion(
                resultado.Campo ?? "descripcion",
                resultado.Detalle ?? "Los datos no son válidos."),
            EstadoOperacionEvaluacion.EstadoNoConfigurado => Problema(
                StatusCodes.Status503ServiceUnavailable, "Servicio no disponible",
                "El estado de falta de evidencia no está configurado."),
            EstadoOperacionEvaluacion.ConflictoConcurrente => Problema(
                StatusCodes.Status409Conflict, "Conflicto de concurrencia",
                "La solicitud cambió de estado. Actualiza la vista e intenta nuevamente."),
            _ => Problema(StatusCodes.Status500InternalServerError, "Error interno",
                "No fue posible registrar la solicitud de evidencia.")
        };
    }

    [HttpPost("{id:int}/certificado")]
    [ValidateCsrf]
    [EnableRateLimiting("asignaciones")]
    [ProducesResponseType<GradeX.Application.Certificados.CertificadoEmitido>(
        StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> EmitirCertificado(
        int id, CancellationToken cancellationToken)
    {
        if (!TryObtenerUsuario(out var idUsuario))
            return NoAutenticado();

        var resultado = await certificados.EmitirAsync(id, idUsuario, cancellationToken);
        return resultado.Estado switch
        {
            GradeX.Application.Certificados.EstadoOperacionCertificado.Exito =>
                StatusCode(StatusCodes.Status201Created, resultado.Certificado),
            GradeX.Application.Certificados.EstadoOperacionCertificado.NoEncontrado =>
                NoEncontrada(),
            GradeX.Application.Certificados.EstadoOperacionCertificado.NoAutorizado =>
                Problema(StatusCodes.Status403Forbidden, "Acceso denegado",
                    "No tienes una asignación activa sobre esta solicitud."),
            GradeX.Application.Certificados.EstadoOperacionCertificado.YaExiste =>
                Problema(StatusCodes.Status409Conflict, "Certificado existente",
                    resultado.Detalle ?? "La evaluación ya tiene un certificado emitido."),
            GradeX.Application.Certificados.EstadoOperacionCertificado.NoElegible =>
                Problema(StatusCodes.Status422UnprocessableEntity, "Evaluación no elegible",
                    resultado.Detalle ?? "La evaluación no cumple las condiciones para certificar."),
            GradeX.Application.Certificados.EstadoOperacionCertificado.EstadoNoConfigurado =>
                Problema(StatusCodes.Status503ServiceUnavailable, "Servicio no disponible",
                    "El estado de certificado no está configurado."),
            _ => Problema(StatusCodes.Status500InternalServerError, "Error interno",
                "No fue posible emitir el certificado.")
        };
    }

    [HttpGet("{id:int}/evidencias/{idEvidencia:int}/archivo")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archivo(
        int id, int idEvidencia, CancellationToken cancellationToken)
    {
        if (!TryObtenerUsuario(out var idUsuario))
            return NoAutenticado();

        var archivo = await evaluaciones.ObtenerArchivoEvidenciaAsync(
            id, idEvidencia, idUsuario, cancellationToken);
        if (archivo is null)
            return NoEncontrada();

        var stream = await almacenamiento.AbrirAsync(
            archivo.ClaveAlmacenamiento, cancellationToken);
        return stream is null
            ? NoEncontrada()
            : File(stream, archivo.TipoContenido ?? "application/octet-stream");
    }

    private ActionResult ResultadoEvaluacion(ResultadoGuardarEvaluacion resultado) =>
        resultado.Estado switch
        {
            EstadoOperacionEvaluacion.Exito => Ok(resultado.Evaluacion),
            EstadoOperacionEvaluacion.AsignacionNoEncontrada => NoEncontrada(),
            EstadoOperacionEvaluacion.EstadoInvalido => Problema(
                StatusCodes.Status409Conflict, "Estado no editable",
                resultado.Detalle ?? "La solicitud no admite cambios de evaluación."),
            EstadoOperacionEvaluacion.DatosInvalidos or
            EstadoOperacionEvaluacion.CriterioNoPertenece or
            EstadoOperacionEvaluacion.DictamenInvalido or
            EstadoOperacionEvaluacion.CriterioObligatorioFaltante => Validacion(
                resultado.Campo ?? "evaluacion",
                resultado.Detalle ?? "Los datos de la evaluación no son válidos."),
            EstadoOperacionEvaluacion.EstadoNoConfigurado => Problema(
                StatusCodes.Status503ServiceUnavailable, "Servicio no disponible",
                "El estado de evaluación no está configurado."),
            EstadoOperacionEvaluacion.ConflictoConcurrente => Problema(
                StatusCodes.Status409Conflict, "Conflicto de concurrencia",
                "La evaluación fue modificada. Actualiza la vista e intenta nuevamente."),
            _ => Problema(StatusCodes.Status500InternalServerError, "Error interno",
                "No fue posible guardar la evaluación.")
        };

    private static List<DatosResultadoCriterio> Mapear(
        IReadOnlyList<ResultadoCriterioRequest>? resultados) =>
        resultados?.Select(r => new DatosResultadoCriterio(
            r.IdCriterio, r.ValorTexto, r.ValorNumerico,
            r.ValorBooleano, r.Observacion)).ToList() ?? [];

    private static ObjectResult NoEncontrada() =>
        Problema(StatusCodes.Status404NotFound, "Asignación no encontrada",
            "La solicitud no existe o no está asignada a tu perfil.");
}

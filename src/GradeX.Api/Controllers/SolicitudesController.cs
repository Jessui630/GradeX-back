using System.Globalization;
using System.Security.Claims;
using GradeX.Api.Security;
using GradeX.Application.Solicitudes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GradeX.Api.Controllers;

public sealed record CrearSolicitudRequest(
    int IdCategoria,
    int? IdMarca,
    string? Nombre,
    string? Modelo,
    string? Sku,
    string? NumeroSerie,
    string? Imei,
    string? Accesorios,
    string? Descripcion,
    string? Observaciones);

public sealed record SubirEvidenciaRequest(
    int IdTipoEvidencia,
    string? Descripcion,
    IFormFile? Archivo);

[ApiController]
[Authorize(Policy = "Solicitante")]
[Route("api/solicitudes")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SolicitudesController(
    RegistrarSolicitud registrarSolicitud,
    AgregarEvidencia agregarEvidencia,
    ISolicitudStore solicitudes,
    IEvidenciaStorage almacenamiento) : ControllerBase
{
    [HttpPost]
    [ValidateCsrf]
    [EnableRateLimiting("solicitudes")]
    [Consumes("application/json")]
    [RequestSizeLimit(16_384)]
    [ProducesResponseType<SolicitudCreada>(StatusCodes.Status201Created)]
    [ProducesResponseType<SolicitudCreada>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Crear(
        [FromBody] CrearSolicitudRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryObtenerUsuario(out var idSolicitante))
            return NoAutenticado();

        if (!Guid.TryParse(Request.Headers["Idempotency-Key"], out var clave) || clave == Guid.Empty)
            return Validacion("idempotencyKey", "La cabecera Idempotency-Key debe contener un UUID válido.");

        if (request is null)
            return Validacion("solicitud", "La solicitud no contiene datos.");

        var resultado = await registrarSolicitud.EjecutarAsync(
            new DatosNuevaSolicitud(
                request.IdCategoria,
                request.IdMarca,
                request.Nombre,
                request.Modelo,
                request.Sku,
                request.NumeroSerie,
                request.Imei,
                request.Accesorios,
                request.Descripcion,
                request.Observaciones,
                clave),
            idSolicitante,
            cancellationToken);

        return resultado.Estado switch
        {
            EstadoCrearSolicitud.Exito => Created(
                $"/api/solicitudes/{resultado.Solicitud!.IdSolicitud}", resultado.Solicitud),
            EstadoCrearSolicitud.Existente => Ok(resultado.Solicitud),
            EstadoCrearSolicitud.DatosInvalidos => Validacion(
                resultado.Campo ?? "solicitud", "El dato indicado es obligatorio o no tiene un formato válido."),
            EstadoCrearSolicitud.CategoriaInvalida => Validacion(
                "idCategoria", "La categoría no existe o está inactiva."),
            EstadoCrearSolicitud.MarcaInvalida => Validacion(
                "idMarca", "La marca no está disponible para la categoría seleccionada."),
            EstadoCrearSolicitud.RequisitosNoConfigurados => Problema(
                StatusCodes.Status503ServiceUnavailable,
                "Servicio no disponible",
                "La categoría no tiene requisitos configurados."),
            EstadoCrearSolicitud.ConflictoIdempotencia => Problema(
                StatusCodes.Status409Conflict,
                "Conflicto de idempotencia",
                "La clave de idempotencia ya fue utilizada."),
            _ => Problema(StatusCodes.Status500InternalServerError, "Error interno",
                "No fue posible crear la solicitud.")
        };
    }

    [HttpPost("{id:int}/enviar")]
    [ValidateCsrf]
    [EnableRateLimiting("solicitudes")]
    [ProducesResponseType<SolicitudCreada>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Enviar(int id, CancellationToken cancellationToken)
    {
        if (!TryObtenerUsuario(out var idSolicitante))
            return NoAutenticado();

        var resultado = await solicitudes.EnviarAsync(id, idSolicitante, cancellationToken);
        return resultado.Estado switch
        {
            EstadoEnviarSolicitud.Exito => Ok(resultado.Solicitud),
            EstadoEnviarSolicitud.NoEncontrada => NoEncontrada(),
            EstadoEnviarSolicitud.EstadoInvalido => Problema(
                StatusCodes.Status409Conflict, "Solicitud no editable",
                "La solicitud ya no se encuentra en estado borrador."),
            EstadoEnviarSolicitud.EvidenciaFaltante => Validacion(
                "evidencias", "Falta evidencia obligatoria: " +
                    string.Join(", ", resultado.Faltantes ?? [])),
            EstadoEnviarSolicitud.RequisitosNoConfigurados => Problema(
                StatusCodes.Status503ServiceUnavailable, "Servicio no disponible",
                "La categoría no tiene requisitos de evidencia configurados."),
            _ => Problema(StatusCodes.Status500InternalServerError, "Error interno",
                "No fue posible enviar la solicitud.")
        };
    }

    [HttpPost("{id:int}/evidencias")]
    [ValidateCsrf]
    [EnableRateLimiting("solicitudes")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(9 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 9 * 1024 * 1024)]
    [ProducesResponseType<EvidenciaCreada>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubirEvidencia(
        int id,
        [FromForm] SubirEvidenciaRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryObtenerUsuario(out var idSolicitante))
            return NoAutenticado();

        if (request?.Archivo is null)
            return Validacion("archivo", "Selecciona una imagen.");
        if (request.Archivo.Length <= 0 || request.Archivo.Length > 8 * 1024 * 1024)
            return Validacion("archivo", "La imagen debe pesar entre 1 byte y 8 MB.");

        await using var stream = request.Archivo.OpenReadStream();
        var resultado = await agregarEvidencia.EjecutarAsync(
            id,
            idSolicitante,
            new DatosEvidencia(
                request.IdTipoEvidencia,
                request.Archivo.FileName,
                request.Archivo.ContentType,
                request.Archivo.Length,
                request.Descripcion,
                stream),
            cancellationToken);

        return resultado.Estado switch
        {
            EstadoAgregarEvidencia.Exito => StatusCode(
                StatusCodes.Status201Created, resultado.Evidencia),
            EstadoAgregarEvidencia.NoEncontrada => NoEncontrada(),
            EstadoAgregarEvidencia.EstadoInvalido => Problema(
                StatusCodes.Status409Conflict, "Solicitud no editable",
                "La solicitud ya fue enviada."),
            EstadoAgregarEvidencia.TipoNoPermitido => Validacion(
                "idTipoEvidencia", "El tipo de evidencia no está permitido para la categoría."),
            EstadoAgregarEvidencia.LimiteAlcanzado => Problema(
                StatusCodes.Status409Conflict, "Límite de evidencias",
                "Ya se alcanzó el número máximo de imágenes para este tipo."),
            EstadoAgregarEvidencia.ArchivoInvalido => Validacion(
                "archivo", "La imagen no es válida o su formato no está permitido."),
            EstadoAgregarEvidencia.AlmacenamientoNoDisponible => Problema(
                StatusCodes.Status503ServiceUnavailable, "Servicio no disponible",
                "No fue posible guardar la evidencia. Intenta nuevamente."),
            _ => Problema(StatusCodes.Status500InternalServerError, "Error interno",
                "No fue posible guardar la evidencia.")
        };
    }

    [HttpGet]
    [ProducesResponseType<SolicitudListado>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanoPagina = 10,
        [FromQuery] string? estado = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryObtenerUsuario(out var idSolicitante))
            return NoAutenticado();
        var listado = await solicitudes.ListarAsync(
            idSolicitante, pagina, tamanoPagina, estado, cancellationToken);
        return Ok(listado);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<SolicitudDetalle>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Detalle(int id, CancellationToken cancellationToken)
    {
        if (!TryObtenerUsuario(out var idSolicitante))
            return NoAutenticado();
        var detalle = await solicitudes.ObtenerDetalleAsync(id, idSolicitante, cancellationToken);
        return detalle is null ? NoEncontrada() : Ok(detalle);
    }

    [HttpGet("{id:int}/evidencias/{idEvidencia:int}/archivo")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archivo(
        int id, int idEvidencia, CancellationToken cancellationToken)
    {
        if (!TryObtenerUsuario(out var idSolicitante))
            return NoAutenticado();

        var archivo = await solicitudes.ObtenerArchivoAsync(
            id, idEvidencia, idSolicitante, cancellationToken);
        if (archivo is null)
            return NoEncontrada();

        var stream = await almacenamiento.AbrirAsync(archivo.ClaveAlmacenamiento, cancellationToken);
        if (stream is null)
            return NoEncontrada();

        return File(stream, archivo.TipoContenido ?? "application/octet-stream");
    }

    private bool TryObtenerUsuario(out int idUsuario) =>
        int.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out idUsuario) && idUsuario > 0;

    private static ObjectResult NoAutenticado() =>
        Problema(StatusCodes.Status401Unauthorized, "No autenticado", "Inicia sesión para continuar.");

    private static ObjectResult NoEncontrada() =>
        Problema(StatusCodes.Status404NotFound, "Solicitud no encontrada",
            "La solicitud no existe o no te pertenece.");

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

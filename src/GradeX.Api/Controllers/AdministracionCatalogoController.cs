using System.Globalization;
using System.Security.Claims;
using GradeX.Api.Security;
using GradeX.Application.Administracion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GradeX.Api.Controllers;

public sealed record CategoriaRequest(string? Nombre, string? Descripcion, bool? Activa);
public sealed record MarcaRequest(string? Nombre, bool? Activa);
public sealed record CriterioRequest(
    string? Nombre, string? Descripcion, string? TipoDato,
    bool Obligatorio, short Orden, bool Activo);
public sealed record RequisitoCampoRequest(
    string? Campo, bool Obligatorio, int? LongitudMaxima, int Orden);
public sealed record RequisitoEvidenciaRequest(
    int IdTipoEvidencia, bool Obligatorio, int CantidadMinima,
    int CantidadMaxima, int Orden);
public sealed record RequisitosCategoriaRequest(
    IReadOnlyList<RequisitoCampoRequest>? Campos,
    IReadOnlyList<RequisitoEvidenciaRequest>? Evidencias);
public sealed record VinculoMarcaRequest(bool Activa);

[ApiController]
[Authorize(Policy = "Administrador")]
[Route("api/administracion/catalogo")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdministracionCatalogoController(
    ICatalogoAdminStore catalogo) : ControllerBase
{
    [HttpGet("categorias")]
    [ProducesResponseType<IReadOnlyList<CategoriaAdmin>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Categorias(CancellationToken cancellationToken) =>
        Ok(await catalogo.ListarCategoriasAsync(cancellationToken));

    [HttpPost("categorias")]
    [ValidateCsrf]
    [EnableRateLimiting("asignaciones")]
    [ProducesResponseType<CategoriaAdmin>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearCategoria(
        [FromBody] CategoriaRequest? request, CancellationToken cancellationToken)
    {
        if (!TryObtenerUsuario(out _))
            return NoAutenticado();
        if (request is null)
            return Validacion("nombre", "La solicitud no contiene datos.");

        var resultado = await catalogo.CrearCategoriaAsync(
            new DatosCategoriaAdmin(request.Nombre, request.Descripcion,
                request.Activa ?? true), cancellationToken);
        return Resultado(resultado, creado: true);
    }

    [HttpPut("categorias/{id:int}")]
    [ValidateCsrf]
    [EnableRateLimiting("asignaciones")]
    [ProducesResponseType<CategoriaAdmin>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarCategoria(
        int id, [FromBody] CategoriaRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryObtenerUsuario(out _))
            return NoAutenticado();
        if (request is null)
            return Validacion("nombre", "La solicitud no contiene datos.");

        var resultado = await catalogo.ActualizarCategoriaAsync(
            id, new DatosCategoriaAdmin(request.Nombre, request.Descripcion,
                request.Activa ?? true), cancellationToken);
        return Resultado(resultado);
    }

    [HttpGet("marcas")]
    [ProducesResponseType<IReadOnlyList<MarcaAdmin>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Marcas(CancellationToken cancellationToken) =>
        Ok(await catalogo.ListarMarcasAsync(cancellationToken));

    [HttpGet("tipos-evidencia")]
    [ProducesResponseType<IReadOnlyList<TipoEvidenciaAdmin>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> TiposEvidencia(CancellationToken cancellationToken) =>
        Ok(await catalogo.ListarTiposEvidenciaAsync(cancellationToken));

    [HttpPost("marcas")]
    [ValidateCsrf]
    [EnableRateLimiting("asignaciones")]
    [ProducesResponseType<MarcaAdmin>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearMarca(
        [FromBody] MarcaRequest? request, CancellationToken cancellationToken)
    {
        if (!TryObtenerUsuario(out _))
            return NoAutenticado();
        if (request is null)
            return Validacion("nombre", "La solicitud no contiene datos.");

        var resultado = await catalogo.CrearMarcaAsync(
            new DatosMarcaAdmin(request.Nombre, request.Activa ?? true),
            cancellationToken);
        return Resultado(resultado, creado: true);
    }

    [HttpPut("marcas/{id:int}")]
    [ValidateCsrf]
    [EnableRateLimiting("asignaciones")]
    [ProducesResponseType<MarcaAdmin>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarMarca(
        int id, [FromBody] MarcaRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryObtenerUsuario(out _))
            return NoAutenticado();
        if (request is null)
            return Validacion("nombre", "La solicitud no contiene datos.");

        var resultado = await catalogo.ActualizarMarcaAsync(
            id, new DatosMarcaAdmin(request.Nombre, request.Activa ?? true),
            cancellationToken);
        return Resultado(resultado);
    }

    [HttpGet("categorias/{id:int}/marcas")]
    [ProducesResponseType<IReadOnlyList<CategoriaMarcaAdmin>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CategoriaMarcas(
        int id, CancellationToken cancellationToken) =>
        Ok(await catalogo.ListarCategoriaMarcasAsync(id, cancellationToken));

    [HttpPut("categorias/{id:int}/marcas/{idMarca:int}")]
    [ValidateCsrf]
    [EnableRateLimiting("asignaciones")]
    [ProducesResponseType<CategoriaMarcaAdmin>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> VincularMarca(
        int id, int idMarca, [FromBody] VinculoMarcaRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryObtenerUsuario(out _))
            return NoAutenticado();
        if (request is null)
            return Validacion("activa", "La solicitud no contiene datos.");

        var resultado = await catalogo.VincularMarcaAsync(
            id, idMarca, request.Activa, cancellationToken);
        return Resultado(resultado);
    }

    [HttpGet("categorias/{id:int}/requisitos")]
    [ProducesResponseType<RequisitosCategoriaAdmin>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Requisitos(
        int id, CancellationToken cancellationToken)
    {
        var requisitos = await catalogo.ObtenerRequisitosAsync(id, cancellationToken);
        return requisitos is null
            ? Problema(StatusCodes.Status404NotFound, "Categoría no encontrada",
                "La categoría indicada no existe.")
            : Ok(requisitos);
    }

    [HttpPut("categorias/{id:int}/requisitos")]
    [ValidateCsrf]
    [EnableRateLimiting("asignaciones")]
    [ProducesResponseType<RequisitosCategoriaAdmin>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActualizarRequisitos(
        int id, [FromBody] RequisitosCategoriaRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryObtenerUsuario(out _))
            return NoAutenticado();
        if (request is null)
            return Validacion("requisitos", "La solicitud no contiene datos.");

        var resultado = await catalogo.ActualizarRequisitosAsync(
            id,
            new DatosRequisitosCategoria(
                request.Campos?.Select(c => new RequisitoCampoAdmin(
                    c.Campo ?? string.Empty, c.Obligatorio,
                    c.LongitudMaxima, c.Orden)).ToList() ?? [],
                request.Evidencias?.Select(e => new RequisitoEvidenciaAdmin(
                    e.IdTipoEvidencia, string.Empty, string.Empty,
                    e.Obligatorio, e.CantidadMinima, e.CantidadMaxima,
                    e.Orden)).ToList() ?? []),
            cancellationToken);
        return Resultado(resultado);
    }

    [HttpGet("categorias/{id:int}/criterios")]
    [ProducesResponseType<IReadOnlyList<CriterioAdmin>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Criterios(
        int id, CancellationToken cancellationToken) =>
        Ok(await catalogo.ListarCriteriosAsync(id, cancellationToken));

    [HttpPost("categorias/{id:int}/criterios")]
    [ValidateCsrf]
    [EnableRateLimiting("asignaciones")]
    [ProducesResponseType<CriterioAdmin>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearCriterio(
        int id, [FromBody] CriterioRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryObtenerUsuario(out _))
            return NoAutenticado();
        if (request is null)
            return Validacion("nombre", "La solicitud no contiene datos.");

        var resultado = await catalogo.CrearCriterioAsync(
            id, Mapear(request), cancellationToken);
        return Resultado(resultado, creado: true);
    }

    [HttpPut("criterios/{idCriterio:int}")]
    [ValidateCsrf]
    [EnableRateLimiting("asignaciones")]
    [ProducesResponseType<CriterioAdmin>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarCriterio(
        int idCriterio, [FromBody] CriterioRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryObtenerUsuario(out _))
            return NoAutenticado();
        if (request is null)
            return Validacion("nombre", "La solicitud no contiene datos.");

        var resultado = await catalogo.ActualizarCriterioAsync(
            idCriterio, Mapear(request), cancellationToken);
        return Resultado(resultado);
    }

    private static DatosCriterioAdmin Mapear(CriterioRequest request) =>
        new(request.Nombre, request.Descripcion, request.TipoDato,
            request.Obligatorio, request.Orden, request.Activo);

    private ActionResult Resultado<T>(ResultadoAdmin<T> resultado, bool creado = false) =>
        resultado.Estado switch
        {
            EstadoOperacionAdmin.Exito => creado
                ? StatusCode(StatusCodes.Status201Created, resultado.Valor)
                : Ok(resultado.Valor),
            EstadoOperacionAdmin.NoEncontrado => Problema(
                StatusCodes.Status404NotFound, "No encontrado",
                resultado.Detalle ?? "El recurso indicado no existe."),
            EstadoOperacionAdmin.DatosInvalidos => Validacion(
                resultado.Campo ?? "solicitud",
                resultado.Detalle ?? "Los datos no son válidos."),
            EstadoOperacionAdmin.Conflicto => Problema(
                StatusCodes.Status409Conflict, "Conflicto",
                resultado.Detalle ?? "El recurso entra en conflicto con otro existente."),
            EstadoOperacionAdmin.NoPermitido => Problema(
                StatusCodes.Status403Forbidden, "No permitido",
                resultado.Detalle ?? "La operación no está permitida."),
            EstadoOperacionAdmin.EstadoNoConfigurado => Problema(
                StatusCodes.Status503ServiceUnavailable, "Servicio no disponible",
                resultado.Detalle ?? "Falta configuración del sistema."),
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

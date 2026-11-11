using GradeX.Api.Security;
using GradeX.Application.Cuentas;
using GradeX.Application.Sesiones;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GradeX.Api.Controllers;

public sealed record IniciarSesionRequest(string? Correo, string? Contrasena);

public sealed record SesionResponse(
    int IdUsuario,
    string NombreCompleto,
    string Correo,
    IReadOnlyList<string> Roles);

public sealed record CsrfResponse(string Token);

[ApiController]
[Route("api/sesiones")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SesionesController(
    IniciarSesion iniciarSesion,
    ISesionStore sesiones,
    IConfiguration configuration,
    IWebHostEnvironment environment) : ControllerBase
{
    [HttpGet("csrf")]
    [AllowAnonymous]
    [ProducesResponseType<CsrfResponse>(StatusCodes.Status200OK)]
    public IActionResult ObtenerCsrf()
    {
        var token = CsrfProtection.CrearToken();
        Response.Cookies.Append(
            CsrfProtection.CookieName,
            token,
            OpcionesCookie(DateTimeOffset.UtcNow.AddHours(2)));
        return Ok(new CsrfResponse(token));
    }

    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [ValidateCsrf]
    [Consumes("application/json")]
    [RequestSizeLimit(4_096)]
    [ProducesResponseType<SesionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Iniciar(
        [FromBody] IniciarSesionRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return Problema(StatusCodes.Status400BadRequest, "Solicitud inválida",
                "La solicitud no contiene datos.");

        var resultado = await iniciarSesion.EjecutarAsync(
            new DatosInicioSesion(request.Correo, request.Contrasena),
            cancellationToken);

        if (resultado.Estado != EstadoInicioSesion.Exito || resultado.Usuario is null)
            return Problema(StatusCodes.Status401Unauthorized, "Credenciales inválidas",
                "El correo o la contraseña no son correctos.");

        var minutos = Math.Clamp(configuration.GetValue("Sesion:DuracionMinutos", 30), 5, 1_440);
        var sesion = await sesiones.CrearAsync(
            resultado.Usuario, TimeSpan.FromMinutes(minutos), cancellationToken);
        Response.Cookies.Append(SesionConstantes.Cookie, sesion.Token, OpcionesCookie(sesion.Expira));
        return Ok(Respuesta(sesion));
    }

    [HttpGet("actual")]
    [Authorize]
    [ProducesResponseType<SesionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Actual(CancellationToken cancellationToken)
    {
        Request.Cookies.TryGetValue(SesionConstantes.Cookie, out var token);
        var sesion = await sesiones.ObtenerAsync(token, cancellationToken);
        return sesion is null
            ? Problema(StatusCodes.Status401Unauthorized, "Sesión no válida",
                "Inicia sesión para continuar.")
            : Ok(Respuesta(sesion));
    }

    [HttpDelete("actual")]
    [Authorize]
    [ValidateCsrf]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Cerrar(CancellationToken cancellationToken)
    {
        Request.Cookies.TryGetValue(SesionConstantes.Cookie, out var token);
        await sesiones.EliminarAsync(token, cancellationToken);
        Response.Cookies.Delete(SesionConstantes.Cookie, OpcionesCookie(DateTimeOffset.UtcNow));
        return NoContent();
    }

    private static SesionResponse Respuesta(SesionActiva sesion) =>
        new(sesion.IdUsuario, sesion.NombreCompleto, sesion.Correo, sesion.Roles);

    private CookieOptions OpcionesCookie(DateTimeOffset expira)
    {
        var crossSite = configuration.GetValue("Sesion:CrossSite", false);
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = !environment.IsDevelopment(),
            SameSite = crossSite && !environment.IsDevelopment()
                ? SameSiteMode.None
                : SameSiteMode.Strict,
            IsEssential = true,
            Path = "/",
            Expires = expira
        };
    }

    private static ObjectResult Problema(int estado, string titulo, string detalle) =>
        new(new ProblemDetails { Status = estado, Title = titulo, Detail = detalle })
        {
            StatusCode = estado,
            ContentTypes = { "application/problem+json" }
        };
}

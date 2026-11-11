using System.Security.Claims;
using System.Text.Encodings.Web;
using GradeX.Application.Sesiones;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace GradeX.Api.Security;

public sealed class SesionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ISesionStore sesiones)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Cookies.TryGetValue(SesionConstantes.Cookie, out var token))
            return AuthenticateResult.NoResult();

        var sesion = await sesiones.ObtenerAsync(token, Context.RequestAborted);
        if (sesion is null)
            return AuthenticateResult.Fail("La sesión no es válida o ha expirado.");

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, sesion.IdUsuario.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, sesion.NombreCompleto),
            new(ClaimTypes.Email, sesion.Correo)
        };
        claims.AddRange(sesion.Roles.Select(rol => new Claim(ClaimTypes.Role, rol)));

        var identidad = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identidad);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        await EscribirProblemaAsync(StatusCodes.Status401Unauthorized, "No autenticado",
            "Inicia sesión para continuar.");
    }

    protected override async Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        await EscribirProblemaAsync(StatusCodes.Status403Forbidden, "Acceso denegado",
            "No tienes permisos para realizar esta operación.");
    }

    private async Task EscribirProblemaAsync(int estado, string titulo, string detalle)
    {
        Response.StatusCode = estado;
        await Response.WriteAsJsonAsync(
            new ProblemDetails { Status = estado, Title = titulo, Detail = detalle },
            options: null,
            contentType: "application/problem+json",
            cancellationToken: Context.RequestAborted);
    }
}

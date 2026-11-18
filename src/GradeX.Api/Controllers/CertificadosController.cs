using System.Globalization;
using System.Security.Claims;
using GradeX.Application.Certificados;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GradeX.Api.Controllers;

[ApiController]
[Route("api/certificados")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CertificadosController(ICertificadoStore certificados) : ControllerBase
{
    [HttpGet("solicitud/{idSolicitud:int}")]
    [Authorize]
    [ProducesResponseType<CertificadoEmitido>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PorSolicitud(
        int idSolicitud, CancellationToken cancellationToken)
    {
        if (!TryObtenerUsuario(out var idUsuario))
            return NoAutenticado();

        var certificado = await certificados.ObtenerPorSolicitudAsync(
            idSolicitud, idUsuario, cancellationToken);
        return certificado is null
            ? Problema(StatusCodes.Status404NotFound, "Certificado no disponible",
                "La solicitud no tiene un certificado emitido.")
            : Ok(certificado);
    }

    [HttpGet("verificar/{token:guid}")]
    [AllowAnonymous]
    [ProducesResponseType<CertificadoPublico>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Verificar(
        Guid token, CancellationToken cancellationToken)
    {
        var certificado = await certificados.VerificarAsync(token, cancellationToken);
        return certificado is null
            ? Problema(StatusCodes.Status404NotFound, "Certificado no encontrado",
                "El código de verificación no corresponde a un certificado emitido.")
            : Ok(certificado);
    }

    [HttpGet("verificar/numero/{numero}")]
    [AllowAnonymous]
    [ProducesResponseType<CertificadoPublico>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> VerificarPorNumero(
        string numero, CancellationToken cancellationToken)
    {
        var certificado = await certificados.VerificarPorNumeroAsync(
            numero, cancellationToken);
        return certificado is null
            ? Problema(StatusCodes.Status404NotFound, "Certificado no encontrado",
                "El número no corresponde a un certificado emitido.")
            : Ok(certificado);
    }

    private bool TryObtenerUsuario(out int idUsuario) =>
        int.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out idUsuario) && idUsuario > 0;

    private static ObjectResult NoAutenticado() =>
        Problema(StatusCodes.Status401Unauthorized, "No autenticado",
            "Inicia sesión para continuar.");

    private static ObjectResult Problema(int estado, string titulo, string detalle) =>
        new(new ProblemDetails { Status = estado, Title = titulo, Detail = detalle })
        {
            StatusCode = estado,
            ContentTypes = { "application/problem+json" }
        };
}

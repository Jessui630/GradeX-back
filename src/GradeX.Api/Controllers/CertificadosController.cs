using GradeX.Application.Certificados;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GradeX.Api.Controllers;

[ApiController]
[Route("api/certificados")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CertificadosController(ICertificadoStore certificados) : ApiControllerBase
{
    [HttpGet]
    [Authorize(Policy = "Solicitante")]
    [ProducesResponseType<IReadOnlyList<CertificadoEmitido>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(CancellationToken cancellationToken)
    {
        if (!TryObtenerUsuario(out var idUsuario))
            return NoAutenticado();

        return Ok(await certificados.ListarPorSolicitanteAsync(
            idUsuario, cancellationToken));
    }

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
    [EnableRateLimiting("certificados")]
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
    [EnableRateLimiting("certificados")]
    [ProducesResponseType<CertificadoPublico>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> VerificarPorNumero(
        string numero, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(numero) || numero.Length is < 3 or > 40)
            return NoLocalizado();

        var certificado = await certificados.VerificarPorNumeroAsync(
            numero, cancellationToken);
        return certificado is null
            ? NoLocalizado()
            : Ok(certificado);
    }

    private static ObjectResult NoLocalizado() =>
        Problema(StatusCodes.Status404NotFound, "Certificado no encontrado",
            "El número no corresponde a un certificado emitido.");
}

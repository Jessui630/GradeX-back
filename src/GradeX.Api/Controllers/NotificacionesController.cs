using GradeX.Application.Notificaciones;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GradeX.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/notificaciones")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class NotificacionesController(INotificacionStore notificaciones) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<ListadoNotificaciones>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanoPagina = 20,
        [FromQuery] bool soloNoLeidas = false,
        CancellationToken cancellationToken = default)
    {
        if (!TryObtenerUsuario(out var idUsuario))
            return NoAutenticado();

        return Ok(await notificaciones.ListarAsync(
            idUsuario, pagina, tamanoPagina, soloNoLeidas, cancellationToken));
    }

    [HttpGet("no-leidas")]
    [ProducesResponseType<ContadorNoLeidas>(StatusCodes.Status200OK)]
    public async Task<IActionResult> NoLeidas(CancellationToken cancellationToken)
    {
        if (!TryObtenerUsuario(out var idUsuario))
            return NoAutenticado();

        return Ok(new ContadorNoLeidas(
            await notificaciones.ContarNoLeidasAsync(idUsuario, cancellationToken)));
    }

    [HttpPatch("{id:long}/leida")]
    [EnableRateLimiting("solicitudes")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarcarLeida(long id, CancellationToken cancellationToken)
    {
        if (!TryObtenerUsuario(out var idUsuario))
            return NoAutenticado();

        return await notificaciones.MarcarLeidaAsync(id, idUsuario, cancellationToken)
            ? NoContent()
            : Problema(StatusCodes.Status404NotFound, "Notificación no encontrada",
                "La notificación no existe o no te pertenece.");
    }

    [HttpPatch("leer-todas")]
    [EnableRateLimiting("solicitudes")]
    [ProducesResponseType<ContadorNoLeidas>(StatusCodes.Status200OK)]
    public async Task<IActionResult> MarcarTodas(CancellationToken cancellationToken)
    {
        if (!TryObtenerUsuario(out var idUsuario))
            return NoAutenticado();

        var actualizadas = await notificaciones.MarcarTodasLeidasAsync(
            idUsuario, cancellationToken);
        return Ok(new ContadorNoLeidas(0, actualizadas));
    }

}

public sealed record ContadorNoLeidas(int NoLeidas, int Actualizadas = 0);

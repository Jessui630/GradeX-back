using GradeX.Application.Productos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GradeX.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/catalogo")]
public sealed class CatalogoController(ICatalogoProductoStore catalogo) : ControllerBase
{
    [HttpGet("categorias")]
    [ProducesResponseType<IReadOnlyList<CategoriaResumen>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Categorias(CancellationToken cancellationToken) =>
        Ok(await catalogo.ListarCategoriasAsync(cancellationToken));

    [HttpGet("categorias/{id:int}/marcas")]
    [ProducesResponseType<IReadOnlyList<MarcaResumen>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Marcas(int id, CancellationToken cancellationToken) =>
        Ok(await catalogo.ListarMarcasAsync(id, cancellationToken));

    [HttpGet("categorias/{id:int}/requisitos")]
    [ProducesResponseType<RequisitosCategoria>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Requisitos(int id, CancellationToken cancellationToken)
    {
        var requisitos = await catalogo.ObtenerRequisitosAsync(id, cancellationToken);
        if (requisitos is null)
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Categoría no disponible",
                Detail = "La categoría no existe o está inactiva."
            });
        return Ok(requisitos);
    }
}

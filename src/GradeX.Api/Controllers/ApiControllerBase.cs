using System.Globalization;
using System.Security.Claims;
using GradeX.Application.Administracion;
using Microsoft.AspNetCore.Mvc;

namespace GradeX.Api.Controllers;

/// <summary>
/// Respuestas y lectura de identidad compartidas por los controladores.
/// Evita duplicar el mismo contrato de errores en cada módulo.
/// </summary>
public abstract class ApiControllerBase : ControllerBase
{
    protected bool TryObtenerUsuario(out int idUsuario) =>
        int.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out idUsuario) && idUsuario > 0;

    protected ObjectResult NoAutenticado() =>
        Problema(StatusCodes.Status401Unauthorized, "No autenticado",
            "Inicia sesión para continuar.");

    protected ActionResult Validacion(string campo, string mensaje) =>
        ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
        {
            [campo] = [mensaje]
        })
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Solicitud inválida"
        });

    protected ActionResult ResultadoAdmin<T>(
        ResultadoAdmin<T> resultado, bool creado = false) =>
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

    protected static ObjectResult Problema(int estado, string titulo, string detalle) =>
        new(new ProblemDetails { Status = estado, Title = titulo, Detail = detalle })
        {
            StatusCode = estado,
            ContentTypes = { "application/problem+json" }
        };
}


using System.Text.Json;
using GradeX.Api.Controllers;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace GradeX.Tests;

public class HealthControllerTests
{
    [Fact]
    public void GetDebeRetornarEstadoOk()
    {
        // Arrange
        var controller = new HealthController();

        // Act
        var resultado = controller.Get();

        // Assert
        var respuesta = Assert.IsType<OkObjectResult>(
            resultado
        );

        Assert.Equal(200, respuesta.StatusCode);

        var json = JsonSerializer.Serialize(respuesta.Value);

        using var documento = JsonDocument.Parse(json);

        Assert.Equal(
            "ok",
            documento.RootElement
                .GetProperty("status")
                .GetString()
        );
    }

    [Fact]
    public void GetDebeRetornarNombreDeAplicacion()
    {
        var controller = new HealthController();

        var resultado = controller.Get();

        var respuesta = Assert.IsType<OkObjectResult>(
            resultado
        );

        var json = JsonSerializer.Serialize(respuesta.Value);

        using var documento = JsonDocument.Parse(json);

        Assert.Equal(
            "GradeX API",
            documento.RootElement
                .GetProperty("application")
                .GetString()
        );
    }
}

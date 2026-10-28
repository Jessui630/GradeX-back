using System.Text.Json;
using GradeX.Api.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace GradeX.Tests;

public class HealthResponseContractTests
{
    [Fact]
    public void GetDebeRetornarVersionActual()
    {
        var controller = new HealthController();

        var respuesta = Assert.IsType<OkObjectResult>(controller.Get());
        using var documento = JsonDocument.Parse(JsonSerializer.Serialize(respuesta.Value));

        Assert.Equal(200, respuesta.StatusCode);
        Assert.Equal("0.1.0", documento.RootElement.GetProperty("version").GetString());
    }
}

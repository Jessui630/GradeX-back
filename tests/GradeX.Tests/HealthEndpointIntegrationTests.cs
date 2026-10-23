using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace GradeX.Tests;

public class HealthEndpointIntegrationTests
{
    [Fact]
    public async Task GetApiHealthDebeResponderOk()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["ConnectionStrings:GradeX"] =
                                "Server=gradex-test.invalid;Database=GradeX_Test;Trusted_Connection=True;Connect Timeout=1;TrustServerCertificate=True"
                        }
                    );
                });
            });
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            }
        );

        using var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync()
        );
        var root = document.RootElement;

        Assert.Equal("ok", root.GetProperty("status").GetString());
        Assert.Equal(
            "GradeX API",
            root.GetProperty("application").GetString()
        );
        Assert.Equal("0.1.0", root.GetProperty("version").GetString());
    }
}

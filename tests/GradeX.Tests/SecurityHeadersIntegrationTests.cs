using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GradeX.Tests;

public sealed class SecurityHeadersIntegrationTests
{
    [Fact]
    public async Task ProduccionAgregaCabecerasDeSeguridadYHsts()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
                builder.UseSetting(
                    "ConnectionStrings:GradeX",
                    "Server=gradex-test.invalid;Database=GradeX_Test;" +
                    "Trusted_Connection=True;Connect Timeout=1;TrustServerCertificate=True");
            });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://gradex.test")
        });

        using var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("nosniff",
            response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY",
            response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer",
            response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Contains("max-age=31536000",
            response.Headers.GetValues("Strict-Transport-Security").Single(),
            StringComparison.Ordinal);
        Assert.Equal("none",
            response.Headers.GetValues("X-Permitted-Cross-Domain-Policies").Single());
    }
}

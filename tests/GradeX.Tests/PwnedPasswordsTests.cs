using System.Net;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using GradeX.Application.Cuentas;
using GradeX.Infrastructure.Persistence.Commands;
using GradeX.Infrastructure.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace GradeX.Tests;

public sealed class PwnedPasswordsTests
{
    private const string Password = "Una frase de prueba larga";
    [SuppressMessage("Security", "CA5350", Justification = "Vector sintético del protocolo Pwned Passwords, no almacenamiento de credenciales.")]
    private static string Suffix => Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(Password)))[5..];

    [Fact]
    public async Task CoincidenciaPositivaConPadding()
    {
        var handler = new Handler(_ => new(HttpStatusCode.OK) { Content = new StringContent($"{Suffix}:42\r\n{new string('A', 35)}:0\r\n") });
        var result = await Check(handler);
        Assert.Equal(EstadoContrasenaComprometida.Comprometida, result);
        Assert.Equal("true", handler.Padding);
        Assert.Equal(5, handler.PrefixLength);
        Assert.Equal("api.pwnedpasswords.com", handler.Host);
        Assert.Equal("https", handler.Scheme);
        Assert.False(handler.HasContent);
    }

    [Fact]
    public async Task PaddingYCoincidenciaSimilarNoSonPositivas()
    {
        var similar = (Suffix[0] == 'A' ? "B" : "A") + Suffix[1..];
        Assert.Equal(EstadoContrasenaComprometida.NoEncontrada,
            await Check(new Handler(_ => new(HttpStatusCode.OK) { Content = new StringContent($"{Suffix}:0\n{similar}:10\n") })));
    }

    [Theory]
    [InlineData("")]
    [InlineData("malformada")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA:-1")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA:1\n\nAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA:2")]
    public async Task RespuestaInvalidaFallaCerrado(string body) =>
        Assert.Equal(EstadoContrasenaComprometida.NoDisponible,
            await Check(new Handler(_ => new(HttpStatusCode.OK) { Content = new StringContent(body) })));

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.Found)]
    public async Task ErrorHttpFallaCerrado(HttpStatusCode status) =>
        Assert.Equal(EstadoContrasenaComprometida.NoDisponible, await Check(new Handler(_ => new(status))));

    [Fact]
    public async Task TimeoutFallaCerrado() =>
        Assert.Equal(EstadoContrasenaComprometida.NoDisponible, await Check(new Handler(_ => throw new TaskCanceledException())));

    [Fact]
    public async Task CancelacionDelLlamadorSePropaga()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        using var client = new HttpClient(new Handler(_ => throw new TaskCanceledException()));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await new PwnedPasswords(client).EstaComprometidaAsync(Password, cts.Token));
    }

    [Fact]
    public void ContenedorUsaImplementacionReal()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:GradeX", "Server=gradex-test.invalid;Database=GradeX_Test;Integrated Security=true"));
        using var scope = factory.Services.CreateScope();
        Assert.IsType<PwnedPasswords>(scope.ServiceProvider.GetRequiredService<IContrasenaComprometida>());
        Assert.IsType<RegistroCuentaStore>(scope.ServiceProvider.GetRequiredService<IRegistroCuentaStore>());
        Assert.IsType<ContrasenaHasher>(scope.ServiceProvider.GetRequiredService<IContrasenaHasher>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<RegistrarCuenta>());
    }

    private static async Task<EstadoContrasenaComprometida> Check(Handler handler)
    {
        using var client = new HttpClient(handler);
        return await new PwnedPasswords(client).EstaComprometidaAsync(Password, CancellationToken.None);
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        public string? Padding { get; private set; }
        public int PrefixLength { get; private set; }
        public string? Host { get; private set; }
        public string? Scheme { get; private set; }
        public bool HasContent { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Padding = request.Headers.GetValues("Add-Padding").Single();
            PrefixLength = request.RequestUri!.Segments[^1].Length;
            Host = request.RequestUri.Host;
            Scheme = request.RequestUri.Scheme;
            HasContent = request.Content is not null;
            return Task.FromResult(response(request));
        }
    }
}

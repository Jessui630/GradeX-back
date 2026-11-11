using System.Net;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using GradeX.Application.Cuentas;
using GradeX.Infrastructure.Security;

namespace GradeX.Tests;

public sealed class PwnedPasswordsLiveTests
{
    [LiveFact]
    [SuppressMessage("Security", "CA5350", Justification = "Vector público del protocolo Pwned Passwords, no almacenamiento de credenciales.")]
    public async Task ProveedorOficialDevuelveCoincidenciaPositiva()
    {
        const string sample = "123456789012345";
        var hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(sample)));
        using var handler = new HttpClientHandler { AllowAutoRedirect = false };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("https://api.pwnedpasswords.com/range/" + hash[..5]));
        request.Headers.Add("Add-Padding", "true");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(body.Split('\n'), line => line.StartsWith(hash[5..] + ":", StringComparison.Ordinal) &&
            long.TryParse(line[(line.IndexOf(':') + 1)..].Trim(), out var count) && count > 0);
        Assert.Equal(EstadoContrasenaComprometida.Comprometida,
            await new PwnedPasswords(client).EstaComprometidaAsync(sample, CancellationToken.None));
    }

    private sealed class LiveFactAttribute : FactAttribute
    {
        public LiveFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("GRADEX_PWNED_LIVE_TEST") != "1")
                Skip = "Prueba externa optativa no habilitada.";
        }
    }
}

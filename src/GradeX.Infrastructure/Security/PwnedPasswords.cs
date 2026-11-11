using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using GradeX.Application.Cuentas;
using Microsoft.Extensions.Logging;

namespace GradeX.Infrastructure.Security;

public sealed class PwnedPasswords(HttpClient client, ILogger<PwnedPasswords>? logger = null) : IContrasenaComprometida
{
    private const int MaxBytes = 2_000_000;
    private static readonly Action<ILogger, string, Exception?> RegistrarNoDisponible =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(1, "PwnedPasswordsNoDisponible"),
            "Pwned Passwords no disponible: {Motivo}");

    [SuppressMessage("Security", "CA5350", Justification = "SHA-1 es obligatorio para el protocolo k-anonimato de Pwned Passwords; no se usa para almacenar credenciales.")]
    public async ValueTask<EstadoContrasenaComprometida> EstaComprometidaAsync(
        string contrasenaNormalizada, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contrasenaNormalizada);
        var hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(contrasenaNormalizada)));
        using var request = new HttpRequestMessage(HttpMethod.Get,
            new Uri("https://api.pwnedpasswords.com/range/" + hash[..5]));
        request.Headers.Add("Add-Padding", "true");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (response.StatusCode != HttpStatusCode.OK || response.Content.Headers.ContentLength > MaxBytes)
                return NoDisponible($"Pwned Passwords respondió {response.StatusCode}.");

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var buffer = new MemoryStream();
            var bytes = new byte[8192];
            int count;
            while ((count = await stream.ReadAsync(bytes, timeout.Token)) != 0)
            {
                if (buffer.Length + count > MaxBytes) return NoDisponible("Respuesta de Pwned Passwords demasiado grande.");
                buffer.Write(bytes, 0, count);
            }

            var data = buffer.ToArray();
            if (data.Any(b => b > 127)) return NoDisponible("Respuesta de Pwned Passwords no ASCII.");
            var lines = Encoding.ASCII.GetString(data).Split('\n');
            var found = false;
            var validLines = 0;
            for (var i = 0; i < lines.Length; i++)
            {
                var raw = lines[i];
                var line = raw.EndsWith('\r') ? raw[..^1] : raw;
                if (line.Length == 0 && i == lines.Length - 1 && raw.Length == 0) continue;
                if (line.Length < 37 || line[35] != ':' ||
                    !line.AsSpan(0, 35).ToString().All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F') ||
                    !long.TryParse(line.AsSpan(36), NumberStyles.None, CultureInfo.InvariantCulture, out var occurrences))
                    return NoDisponible("Respuesta de Pwned Passwords malformada.");

                validLines++;
                if (occurrences > 0 && line.AsSpan(0, 35).SequenceEqual(hash.AsSpan(5))) found = true;
            }

            return validLines == 0
                ? NoDisponible("Respuesta de Pwned Passwords vacía.")
                : found
                    ? EstadoContrasenaComprometida.Comprometida
                    : EstadoContrasenaComprometida.NoEncontrada;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return NoDisponible("Tiempo de espera agotado al consultar Pwned Passwords.");
        }
        catch (HttpRequestException ex)
        {
            return NoDisponible($"Error de red al consultar Pwned Passwords: {ex.GetType().Name}.");
        }
        catch (IOException ex)
        {
            return NoDisponible($"Error de lectura al consultar Pwned Passwords: {ex.GetType().Name}.");
        }
    }

    private EstadoContrasenaComprometida NoDisponible(string motivo)
    {
        if (logger is not null)
            RegistrarNoDisponible(logger, motivo, null);
        return EstadoContrasenaComprometida.NoDisponible;
    }
}

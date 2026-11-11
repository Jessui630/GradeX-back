using System.Collections.Concurrent;
using System.Security.Cryptography;
using GradeX.Application.Cuentas;
using GradeX.Application.Sesiones;

namespace GradeX.Tests;

internal sealed class InMemorySesionStore : ISesionStore
{
    private readonly ConcurrentDictionary<string, SesionActiva> sesiones = new(StringComparer.Ordinal);

    public Task<SesionActiva> CrearAsync(
        UsuarioAutenticado usuario, TimeSpan duracion, CancellationToken cancellationToken)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var sesion = new SesionActiva(
            Guid.NewGuid(), usuario.IdUsuario, usuario.NombreCompleto, usuario.Correo,
            usuario.Roles, DateTimeOffset.UtcNow.Add(duracion), token);
        sesiones[token] = sesion;
        return Task.FromResult(sesion);
    }

    public Task<SesionActiva?> ObtenerAsync(string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(token) || !sesiones.TryGetValue(token, out var sesion))
            return Task.FromResult<SesionActiva?>(null);
        if (sesion.Expira <= DateTimeOffset.UtcNow)
        {
            sesiones.TryRemove(token, out _);
            return Task.FromResult<SesionActiva?>(null);
        }
        return Task.FromResult<SesionActiva?>(sesion);
    }

    public Task EliminarAsync(string? token, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(token))
            sesiones.TryRemove(token, out _);
        return Task.CompletedTask;
    }
}

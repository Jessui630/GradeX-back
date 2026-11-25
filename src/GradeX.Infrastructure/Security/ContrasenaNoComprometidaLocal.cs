using GradeX.Application.Cuentas;

namespace GradeX.Infrastructure.Security;

/// <summary>
/// Implementación de sustitución para entornos locales de prueba cuando no hay
/// salida al servicio Pwned Passwords. No debe registrarse en producción:
/// Program solo la usa si Contrasenas:PwnedPasswords:Habilitado=false.
/// </summary>
public sealed class ContrasenaNoComprometidaLocal : IContrasenaComprometida
{
    public ValueTask<EstadoContrasenaComprometida> EstaComprometidaAsync(
        string contrasenaNormalizada, CancellationToken cancellationToken) =>
        ValueTask.FromResult(EstadoContrasenaComprometida.NoEncontrada);
}

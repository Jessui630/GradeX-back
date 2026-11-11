namespace GradeX.Application.Cuentas;

public sealed class RegistroCuenta(string? nombreCompleto, string? correo, string? contrasena)
{
    public string? NombreCompleto { get; } = nombreCompleto;
    public string? Correo { get; } = correo;
    public string? Contrasena { get; } = contrasena;
}

public enum EstadoRegistroCuenta
{
    Exito,
    NombreInvalido,
    CorreoInvalido,
    ContrasenaInvalida,
    CorreoDuplicado,
    RolNoDisponible,
    ComprobacionContrasenaNoDisponible
}

public sealed record ResultadoRegistroCuenta(EstadoRegistroCuenta Estado, int? IdUsuario = null);

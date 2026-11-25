namespace GradeX.Application.Perfil;

public sealed record PerfilUsuario(
    int IdUsuario,
    string NombreCompleto,
    string Correo,
    bool RecibirNotificaciones,
    IReadOnlyList<string> Roles,
    DateTime FechaCreacion);

public sealed record DatosPerfil(string? NombreCompleto, bool? RecibirNotificaciones);

public sealed record DatosCambioContrasena(string? ContrasenaActual, string? ContrasenaNueva);

public enum EstadoPerfil
{
    Exito,
    NoEncontrado,
    DatosInvalidos,
    ContrasenaIncorrecta,
    ContrasenaInvalida,
    ServicioNoDisponible
}

public sealed record ResultadoPerfil(
    EstadoPerfil Estado,
    PerfilUsuario? Perfil = null,
    string? Campo = null,
    string? Detalle = null);

public interface IPerfilStore
{
    Task<PerfilUsuario?> ObtenerAsync(int idUsuario, CancellationToken cancellationToken);
    Task<PerfilUsuario?> ActualizarAsync(
        int idUsuario, string nombreCompleto, bool recibirNotificaciones,
        CancellationToken cancellationToken);
    Task<string?> ObtenerHashAsync(int idUsuario, CancellationToken cancellationToken);
    Task<bool> ActualizarContrasenaAsync(
        int idUsuario, string passwordHash, CancellationToken cancellationToken);
}

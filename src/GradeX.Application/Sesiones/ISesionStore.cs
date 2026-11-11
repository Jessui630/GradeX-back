using GradeX.Application.Cuentas;

namespace GradeX.Application.Sesiones;

public sealed record SesionActiva(
    Guid IdSesion,
    int IdUsuario,
    string NombreCompleto,
    string Correo,
    IReadOnlyList<string> Roles,
    DateTimeOffset Expira,
    string Token);

public interface ISesionStore
{
    Task<SesionActiva> CrearAsync(
        UsuarioAutenticado usuario, TimeSpan duracion, CancellationToken cancellationToken);

    Task<SesionActiva?> ObtenerAsync(string? token, CancellationToken cancellationToken);

    Task EliminarAsync(string? token, CancellationToken cancellationToken);
}

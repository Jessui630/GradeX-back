namespace GradeX.Application.Cuentas;

public sealed record CredencialesUsuario(
    int IdUsuario,
    string NombreCompleto,
    string Correo,
    string PasswordHash,
    bool Activo,
    IReadOnlyList<string> Roles);

public interface ICredencialesStore
{
    Task<CredencialesUsuario?> BuscarPorCorreoAsync(string correo, CancellationToken cancellationToken);
}

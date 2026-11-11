namespace GradeX.Application.Cuentas;

public interface IRegistroCuentaStore
{
    Task<bool> ExisteCorreoAsync(string correo, CancellationToken cancellationToken);

    Task<ResultadoRegistroCuenta> RegistrarAsync(
        string nombreCompleto, string correo, string passwordHash, CancellationToken cancellationToken);
}

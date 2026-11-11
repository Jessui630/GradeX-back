namespace GradeX.Application.Cuentas;

public interface IContrasenaHasher
{
    string Hash(string contrasena);

    bool Verificar(string? passwordHash, string contrasena);
}

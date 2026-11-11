namespace GradeX.Application.Cuentas;

public sealed record DatosInicioSesion(string? Correo, string? Contrasena);

public enum EstadoInicioSesion
{
    Exito,
    CredencialesInvalidas
}

public sealed record UsuarioAutenticado(
    int IdUsuario,
    string NombreCompleto,
    string Correo,
    IReadOnlyList<string> Roles);

public sealed record ResultadoInicioSesion(EstadoInicioSesion Estado, UsuarioAutenticado? Usuario = null);

public sealed class IniciarSesion(ICredencialesStore store, IContrasenaHasher hasher)
{
    public async Task<ResultadoInicioSesion> EjecutarAsync(
        DatosInicioSesion datos, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var correo = NormalizacionCuenta.Correo(datos.Correo);
        var contrasena = NormalizacionCuenta.ContrasenaParaVerificacion(datos.Contrasena);
        var credenciales = correo is null
            ? null
            : await store.BuscarPorCorreoAsync(correo, cancellationToken);

        // La verificación se ejecuta también sin usuario para reducir diferencias de tiempo
        // que permitan enumerar cuentas.
        var valida = hasher.Verificar(credenciales?.PasswordHash, contrasena ?? string.Empty);
        if (credenciales is null || !credenciales.Activo || !valida || credenciales.Roles.Count == 0)
            return new ResultadoInicioSesion(EstadoInicioSesion.CredencialesInvalidas);

        return new ResultadoInicioSesion(
            EstadoInicioSesion.Exito,
            new UsuarioAutenticado(
                credenciales.IdUsuario,
                credenciales.NombreCompleto,
                credenciales.Correo,
                credenciales.Roles));
    }
}

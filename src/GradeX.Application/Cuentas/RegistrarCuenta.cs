namespace GradeX.Application.Cuentas;

public sealed class RegistrarCuenta(
    IRegistroCuentaStore store,
    IContrasenaHasher hasher,
    IContrasenaComprometida contrasenas)
{
    public async Task<ResultadoRegistroCuenta> EjecutarAsync(
        RegistroCuenta registro, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registro);

        var nombre = NormalizacionCuenta.Nombre(registro.NombreCompleto);
        if (nombre is null)
            return new ResultadoRegistroCuenta(EstadoRegistroCuenta.NombreInvalido);

        var correo = NormalizacionCuenta.Correo(registro.Correo);
        if (correo is null)
            return new ResultadoRegistroCuenta(EstadoRegistroCuenta.CorreoInvalido);

        var contrasena = NormalizacionCuenta.Contrasena(registro.Contrasena);
        if (contrasena is null)
            return new ResultadoRegistroCuenta(EstadoRegistroCuenta.ContrasenaInvalida);

        var comprobacion = await contrasenas.EstaComprometidaAsync(contrasena, cancellationToken);
        if (comprobacion == EstadoContrasenaComprometida.Comprometida)
            return new ResultadoRegistroCuenta(EstadoRegistroCuenta.ContrasenaInvalida);
        if (comprobacion != EstadoContrasenaComprometida.NoEncontrada)
            return new ResultadoRegistroCuenta(EstadoRegistroCuenta.ComprobacionContrasenaNoDisponible);

        if (await store.ExisteCorreoAsync(correo, cancellationToken))
            return new ResultadoRegistroCuenta(EstadoRegistroCuenta.CorreoDuplicado);

        return await store.RegistrarAsync(nombre, correo, hasher.Hash(contrasena), cancellationToken);
    }
}

using GradeX.Application.Cuentas;

namespace GradeX.Application.Perfil;

public sealed class ActualizarPerfil(IPerfilStore perfil)
{
    public async Task<ResultadoPerfil> EjecutarAsync(
        int idUsuario, DatosPerfil datos, CancellationToken cancellationToken = default)
    {
        var nombre = NormalizacionCuenta.Nombre(datos.NombreCompleto);
        if (nombre is null)
            return new ResultadoPerfil(
                EstadoPerfil.DatosInvalidos, Campo: "nombreCompleto",
                Detalle: "El nombre completo es obligatorio y admite hasta 150 caracteres.");

        var actual = await perfil.ObtenerAsync(idUsuario, cancellationToken);
        if (actual is null)
            return new ResultadoPerfil(EstadoPerfil.NoEncontrado);

        var preferencia = datos.RecibirNotificaciones ?? actual.RecibirNotificaciones;
        var actualizado = await perfil.ActualizarAsync(
            idUsuario, nombre, preferencia, cancellationToken);
        return actualizado is null
            ? new ResultadoPerfil(EstadoPerfil.NoEncontrado)
            : new ResultadoPerfil(EstadoPerfil.Exito, actualizado);
    }
}

public sealed class CambiarContrasena(
    IPerfilStore perfil,
    IContrasenaHasher hasher,
    IContrasenaComprometida contrasenas)
{
    public async Task<ResultadoPerfil> EjecutarAsync(
        int idUsuario, DatosCambioContrasena datos,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(datos.ContrasenaActual))
            return new ResultadoPerfil(
                EstadoPerfil.DatosInvalidos, Campo: "contrasenaActual",
                Detalle: "Escribe tu contraseña actual.");

        var nueva = NormalizacionCuenta.Contrasena(datos.ContrasenaNueva);
        if (nueva is null)
            return new ResultadoPerfil(
                EstadoPerfil.ContrasenaInvalida, Campo: "contrasenaNueva",
                Detalle: "La contraseña debe tener entre 15 y 128 caracteres y no contener caracteres de control.");

        var hash = await perfil.ObtenerHashAsync(idUsuario, cancellationToken);
        if (hash is null)
            return new ResultadoPerfil(EstadoPerfil.NoEncontrado);
        if (!hasher.Verificar(hash, datos.ContrasenaActual!))
            return new ResultadoPerfil(
                EstadoPerfil.ContrasenaIncorrecta, Campo: "contrasenaActual",
                Detalle: "La contraseña actual no es correcta.");

        var comprobacion = await contrasenas.EstaComprometidaAsync(nueva, cancellationToken);
        if (comprobacion == EstadoContrasenaComprometida.Comprometida)
            return new ResultadoPerfil(
                EstadoPerfil.ContrasenaInvalida, Campo: "contrasenaNueva",
                Detalle: "La nueva contraseña aparece en listas de contraseñas comprometidas.");
        if (comprobacion != EstadoContrasenaComprometida.NoEncontrada)
            return new ResultadoPerfil(
                EstadoPerfil.ServicioNoDisponible,
                Detalle: "No fue posible verificar la contraseña en este momento.");

        return await perfil.ActualizarContrasenaAsync(
            idUsuario, hasher.Hash(nueva), cancellationToken)
            ? new ResultadoPerfil(EstadoPerfil.Exito)
            : new ResultadoPerfil(EstadoPerfil.NoEncontrado);
    }
}

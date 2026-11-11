namespace GradeX.Application.Asignaciones;

public sealed class AsignarEspecialista(IAsignacionStore asignaciones)
{
    public async Task<ResultadoAsignarEspecialista> EjecutarAsync(
        DatosAsignacion datos, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        if (datos.IdSolicitud <= 0 || datos.IdEspecialista <= 0)
            return new ResultadoAsignarEspecialista(EstadoAsignarEspecialista.DatosInvalidos);

        return await asignaciones.AsignarAsync(datos, cancellationToken);
    }
}

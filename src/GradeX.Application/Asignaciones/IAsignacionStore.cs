namespace GradeX.Application.Asignaciones;

public sealed record DatosAsignacion(int IdSolicitud, int IdEspecialista);

public sealed record AsignacionActual(
    int IdAsignacion,
    int IdEspecialista,
    string Especialista,
    DateTime FechaAsignacion,
    bool Activa);

public sealed record EspecialistaCompatible(
    int IdEspecialista,
    string Nombre,
    short? AniosExperiencia,
    string Nivel,
    int IdCategoria,
    string Categoria,
    int? IdMarca,
    string? Marca,
    int CargaActiva,
    bool CoincideMarca);

public sealed record SolicitudAsignacionResumen(
    int IdSolicitud,
    string Folio,
    string NombreProducto,
    string Categoria,
    string? Marca,
    string EstadoCodigo,
    string EstadoNombre,
    DateTime FechaSolicitud,
    int EspecialistasCompatibles,
    AsignacionActual? Asignacion);

public sealed record ListadoSolicitudesAsignacion(
    IReadOnlyList<SolicitudAsignacionResumen> Elementos,
    int Pagina,
    int TamanoPagina,
    int Total);

public sealed record SolicitudAsignacionDetalle(
    int IdSolicitud,
    string Folio,
    string NombreProducto,
    string? Modelo,
    int IdCategoria,
    string Categoria,
    int? IdMarca,
    string? Marca,
    string EstadoCodigo,
    string EstadoNombre,
    DateTime FechaSolicitud,
    DateTime FechaUltimoCambio,
    bool PuedeAsignarse,
    AsignacionActual? Asignacion,
    IReadOnlyList<EspecialistaCompatible> EspecialistasCompatibles);

public sealed record CompatiblesSolicitud(
    int IdSolicitud,
    string Folio,
    string Categoria,
    string? Marca,
    string EstadoCodigo,
    bool PuedeAsignarse,
    AsignacionActual? Asignacion,
    IReadOnlyList<EspecialistaCompatible> Especialistas);

public sealed record AsignacionCreada(
    int IdAsignacion,
    int IdSolicitud,
    string Folio,
    int IdEspecialista,
    string Especialista,
    DateTime FechaAsignacion,
    string EstadoCodigo,
    string EstadoNombre);

public enum EstadoAsignarEspecialista
{
    Exito,
    DatosInvalidos,
    SolicitudNoEncontrada,
    EspecialistaNoEncontrado,
    SolicitudNoAsignable,
    SinCompatibilidad,
    YaAsignada,
    EstadoNoConfigurado,
    ConflictoConcurrente
}

public sealed record ResultadoAsignarEspecialista(
    EstadoAsignarEspecialista Estado,
    AsignacionCreada? Asignacion = null,
    AsignacionActual? AsignacionExistente = null,
    string? EstadoSolicitudCodigo = null,
    string? EstadoSolicitudNombre = null);

public interface IAsignacionStore
{
    Task<ListadoSolicitudesAsignacion> ListarSolicitudesAsync(
        string? estado, int pagina, int tamanoPagina, CancellationToken cancellationToken);

    Task<SolicitudAsignacionDetalle?> ObtenerSolicitudAsync(
        int idSolicitud, CancellationToken cancellationToken);

    Task<CompatiblesSolicitud?> ObtenerCompatiblesAsync(
        int idSolicitud, CancellationToken cancellationToken);

    Task<ResultadoAsignarEspecialista> AsignarAsync(
        DatosAsignacion datos, CancellationToken cancellationToken);
}

public static class CompatibilidadEspecialista
{
    public static bool EsCompatible(
        int idCategoriaSolicitud,
        int? idMarcaSolicitud,
        int idCategoriaEspecialista,
        int? idMarcaEspecialista) =>
        idCategoriaSolicitud == idCategoriaEspecialista &&
        (idMarcaSolicitud is null
            ? idMarcaEspecialista is null
            : idMarcaEspecialista is null || idMarcaEspecialista == idMarcaSolicitud);
}

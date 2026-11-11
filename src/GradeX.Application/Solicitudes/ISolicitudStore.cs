namespace GradeX.Application.Solicitudes;

public sealed record DatosNuevaSolicitud(
    int IdCategoria,
    int? IdMarca,
    string? Nombre,
    string? Modelo,
    string? Sku,
    string? NumeroSerie,
    string? Imei,
    string? Accesorios,
    string? Descripcion,
    string? Observaciones,
    Guid ClaveIdempotencia);

public enum EstadoCrearSolicitud
{
    Exito,
    Existente,
    DatosInvalidos,
    CategoriaInvalida,
    MarcaInvalida,
    RequisitosNoConfigurados,
    ConflictoIdempotencia
}

public sealed record SolicitudCreada(
    int IdSolicitud, string Folio, string EstadoCodigo, string EstadoNombre);

public sealed record ResultadoCrearSolicitud(
    EstadoCrearSolicitud Estado,
    SolicitudCreada? Solicitud = null,
    string? Campo = null);

public enum EstadoEnviarSolicitud
{
    Exito,
    NoEncontrada,
    EstadoInvalido,
    EvidenciaFaltante,
    RequisitosNoConfigurados
}

public sealed record ResultadoEnviarSolicitud(
    EstadoEnviarSolicitud Estado,
    SolicitudCreada? Solicitud = null,
    IReadOnlyList<string>? Faltantes = null);

public sealed record DatosEvidencia(
    int IdTipoEvidencia,
    string NombreOriginal,
    string? TipoContenido,
    long LongitudDeclarada,
    string? Descripcion,
    Stream Contenido);

public enum EstadoAgregarEvidencia
{
    Exito,
    NoEncontrada,
    EstadoInvalido,
    TipoNoPermitido,
    LimiteAlcanzado,
    ArchivoInvalido,
    AlmacenamientoNoDisponible
}

public sealed record EvidenciaCreada(
    int IdEvidencia,
    int IdTipoEvidencia,
    string Codigo,
    string Nombre,
    string? Descripcion,
    string? NombreOriginal,
    string? TipoContenido,
    long? TamanoBytes,
    DateTime FechaCarga);

public sealed record ResultadoAgregarEvidencia(
    EstadoAgregarEvidencia Estado,
    EvidenciaCreada? Evidencia = null,
    string? ClaveAnterior = null);

public sealed record EvidenciaDetalle(
    int IdEvidencia,
    int IdTipoEvidencia,
    string Codigo,
    string Nombre,
    string? Descripcion,
    string? NombreOriginal,
    string? TipoContenido,
    long? TamanoBytes,
    DateTime FechaCarga);

public sealed record SolicitudResumen(
    int IdSolicitud,
    string Folio,
    string NombreProducto,
    string Categoria,
    string? Marca,
    string EstadoCodigo,
    string EstadoNombre,
    DateTime FechaSolicitud,
    int EvidenciasActivas,
    string? AccionPendiente);

public sealed record SolicitudListado(
    IReadOnlyList<SolicitudResumen> Elementos,
    int Pagina,
    int TamanoPagina,
    int Total);

public sealed record SeguimientoEvento(DateTime Fecha, string Titulo, string? Detalle);

public sealed record EspecialistaAsignado(int IdEspecialista, string Nombre);

public sealed record EvaluacionResumen(string? Dictamen, DateTime? FechaFin);

public sealed record SolicitudDetalle(
    int IdSolicitud,
    string Folio,
    string NombreProducto,
    string? Modelo,
    string? Sku,
    string? NumeroSerie,
    string? Imei,
    string? Accesorios,
    string? Descripcion,
    string? Observaciones,
    int IdCategoria,
    string Categoria,
    int? IdMarca,
    string? Marca,
    string EstadoCodigo,
    string EstadoNombre,
    bool EsBorrador,
    DateTime FechaSolicitud,
    DateTime FechaUltimoCambio,
    string? AccionPendiente,
    IReadOnlyList<EvidenciaDetalle> Evidencias,
    IReadOnlyList<SeguimientoEvento> Seguimiento,
    EspecialistaAsignado? Especialista,
    EvaluacionResumen? Evaluacion,
    bool CertificadoDisponible);

public sealed record ArchivoEvidencia(
    string ClaveAlmacenamiento,
    string? TipoContenido,
    string? NombreOriginal);

public interface ISolicitudStore
{
    Task<ResultadoCrearSolicitud> CrearBorradorAsync(
        DatosNuevaSolicitud datos, int idSolicitante, CancellationToken cancellationToken);

    Task<ResultadoEnviarSolicitud> EnviarAsync(
        int idSolicitud, int idSolicitante, CancellationToken cancellationToken);

    Task<SolicitudListado> ListarAsync(
        int idSolicitante, int pagina, int tamanoPagina, string? estado,
        CancellationToken cancellationToken);

    Task<SolicitudDetalle?> ObtenerDetalleAsync(
        int idSolicitud, int idSolicitante, CancellationToken cancellationToken);

    Task<ResultadoAgregarEvidencia> AgregarEvidenciaAsync(
        int idSolicitud,
        int idSolicitante,
        int idTipoEvidencia,
        string claveAlmacenamiento,
        string nombreOriginal,
        string tipoContenido,
        long tamanoBytes,
        string hashSha256,
        string? descripcion,
        CancellationToken cancellationToken);

    Task<ArchivoEvidencia?> ObtenerArchivoAsync(
        int idSolicitud, int idEvidencia, int idSolicitante, CancellationToken cancellationToken);
}

public interface IEvidenciaStorage
{
    Task<string> GuardarAsync(Stream contenido, string extension, CancellationToken cancellationToken);

    Task<Stream?> AbrirAsync(string clave, CancellationToken cancellationToken);

    Task EliminarAsync(string clave, CancellationToken cancellationToken);
}

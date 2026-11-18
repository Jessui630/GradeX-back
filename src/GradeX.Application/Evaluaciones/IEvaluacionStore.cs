namespace GradeX.Application.Evaluaciones;

public sealed record CriterioEvaluacionResumen(
    int IdCriterio,
    string Nombre,
    string? Descripcion,
    string TipoDato,
    bool Obligatorio,
    short Orden);

public sealed record ResultadoCriterioResumen(
    int IdCriterio,
    string? ValorTexto,
    decimal? ValorNumerico,
    bool? ValorBooleano,
    string? Observacion);

public sealed record EvidenciaEvaluacionResumen(
    int IdEvidencia,
    int IdTipoEvidencia,
    string Codigo,
    string Nombre,
    string? Descripcion,
    string? NombreOriginal,
    string? TipoContenido,
    long? TamanoBytes,
    DateTime FechaCarga);

public sealed record EvaluacionActual(
    int IdEvaluacion,
    string? Dictamen,
    decimal? ConditionGrade,
    decimal? TechnicalHealth,
    string? ObservacionesGenerales,
    DateTime FechaInicio,
    DateTime? FechaFin,
    IReadOnlyList<ResultadoCriterioResumen> Resultados);

public sealed record AsignacionEspecialistaResumen(
    int IdSolicitud,
    string Folio,
    string NombreProducto,
    string Categoria,
    string? Marca,
    string EstadoCodigo,
    string EstadoNombre,
    DateTime FechaSolicitud,
    DateTime FechaAsignacion,
    int EvidenciasActivas,
    bool EvaluacionIniciada,
    DateTime? FechaFinalizacion);

public sealed record ListadoAsignacionesEspecialista(
    IReadOnlyList<AsignacionEspecialistaResumen> Elementos,
    int Pagina,
    int TamanoPagina,
    int Total);

public sealed record AsignacionEspecialistaDetalle(
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
    DateTime FechaSolicitud,
    bool PuedeEvaluar,
    bool PuedeSolicitarEvidencia,
    bool PuedeEmitirCertificado,
    IReadOnlyList<EvidenciaEvaluacionResumen> Evidencias,
    IReadOnlyList<CriterioEvaluacionResumen> Criterios,
    EvaluacionActual? Evaluacion,
    CertificadoEstado? Certificado);

public sealed record CertificadoEstado(
    int IdCertificado,
    string NumeroCertificado,
    Guid QrToken,
    DateTime FechaEmision,
    string EstadoCodigo,
    string EstadoNombre);

public sealed record DatosResultadoCriterio(
    int IdCriterio,
    string? ValorTexto,
    decimal? ValorNumerico,
    bool? ValorBooleano,
    string? Observacion);

public sealed record DatosGuardarEvaluacion(
    decimal? ConditionGrade,
    decimal? TechnicalHealth,
    string? ObservacionesGenerales,
    IReadOnlyList<DatosResultadoCriterio> Resultados);

public sealed record DatosFinalizarEvaluacion(
    string? Dictamen,
    decimal? ConditionGrade,
    decimal? TechnicalHealth,
    string? ObservacionesGenerales,
    IReadOnlyList<DatosResultadoCriterio> Resultados);

public sealed record DatosSolicitudEvidencia(
    int? IdTipoEvidencia,
    string? Descripcion);

public enum EstadoOperacionEvaluacion
{
    Exito,
    AsignacionNoEncontrada,
    EstadoInvalido,
    DatosInvalidos,
    CriterioNoPertenece,
    DictamenInvalido,
    CriterioObligatorioFaltante,
    EstadoNoConfigurado,
    ConflictoConcurrente
}

public sealed record ResultadoGuardarEvaluacion(
    EstadoOperacionEvaluacion Estado,
    EvaluacionActual? Evaluacion = null,
    string? Campo = null,
    string? Detalle = null);

public sealed record ResultadoOperacionEspecialista(
    EstadoOperacionEvaluacion Estado,
    string? Campo = null,
    string? Detalle = null);

public interface IEvaluacionStore
{
    Task<ListadoAsignacionesEspecialista> ListarAsignacionesAsync(
        int idUsuario, string? estado, int pagina, int tamanoPagina,
        CancellationToken cancellationToken);

    Task<AsignacionEspecialistaDetalle?> ObtenerAsignacionAsync(
        int idSolicitud, int idUsuario, CancellationToken cancellationToken);

    Task<ResultadoGuardarEvaluacion> GuardarAsync(
        int idSolicitud, int idUsuario, DatosGuardarEvaluacion datos,
        CancellationToken cancellationToken);

    Task<ResultadoGuardarEvaluacion> FinalizarAsync(
        int idSolicitud, int idUsuario, DatosFinalizarEvaluacion datos,
        CancellationToken cancellationToken);

    Task<ResultadoOperacionEspecialista> SolicitarEvidenciaAsync(
        int idSolicitud, int idUsuario, DatosSolicitudEvidencia datos,
        CancellationToken cancellationToken);

    Task<ArchivoEvidenciaEspecialista?> ObtenerArchivoEvidenciaAsync(
        int idSolicitud, int idEvidencia, int idUsuario,
        CancellationToken cancellationToken);
}

public sealed record ArchivoEvidenciaEspecialista(
    string ClaveAlmacenamiento,
    string? TipoContenido,
    string? NombreOriginal);

namespace GradeX.Application.Certificados;

public sealed record CertificadoEmitido(
    int IdCertificado,
    int IdSolicitud,
    string Folio,
    string NumeroCertificado,
    Guid QrToken,
    DateTime FechaEmision,
    DateTime? FechaVencimiento,
    string EstadoCodigo,
    string EstadoNombre,
    string NombreProducto,
    string? Modelo,
    string Categoria,
    string? Marca,
    string? Dictamen,
    decimal? ConditionGrade,
    string? Especialista,
    string? Especialidad);

public sealed record CertificadoPublico(
    string NumeroCertificado,
    string EstadoCodigo,
    string EstadoNombre,
    string NombreProducto,
    string? Modelo,
    string Categoria,
    string? Marca,
    string? Dictamen,
    decimal? ConditionGrade,
    DateTime FechaEmision,
    DateTime? FechaVencimiento,
    string? Especialista,
    string? Especialidad);

public enum EstadoOperacionCertificado
{
    Exito,
    NoEncontrado,
    NoAutorizado,
    NoElegible,
    YaExiste,
    EstadoNoConfigurado
}

public sealed record ResultadoEmitirCertificado(
    EstadoOperacionCertificado Estado,
    CertificadoEmitido? Certificado = null,
    string? Detalle = null);

public interface ICertificadoStore
{
    Task<ResultadoEmitirCertificado> EmitirAsync(
        int idSolicitud, int idUsuarioEspecialista,
        CancellationToken cancellationToken);

    Task<CertificadoEmitido?> ObtenerPorSolicitudAsync(
        int idSolicitud, int idUsuarioSolicitante,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CertificadoEmitido>> ListarPorSolicitanteAsync(
        int idUsuarioSolicitante, CancellationToken cancellationToken);

    Task<CertificadoPublico?> VerificarAsync(
        Guid token, CancellationToken cancellationToken);

    Task<CertificadoPublico?> VerificarPorNumeroAsync(
        string numeroCertificado, CancellationToken cancellationToken);
}

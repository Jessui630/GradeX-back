namespace GradeX.Application.Administracion;

public sealed record CategoriaAdmin(
    int IdCategoria,
    string Nombre,
    string? Descripcion,
    bool Activa,
    int Productos,
    int Marcas);

public sealed record MarcaAdmin(
    int IdMarca,
    string Nombre,
    bool Activa,
    int Productos);

public sealed record CategoriaMarcaAdmin(
    int IdMarca,
    string Nombre,
    bool Activa);

public sealed record CriterioAdmin(
    int IdCriterio,
    string Nombre,
    string? Descripcion,
    string TipoDato,
    bool Obligatorio,
    short Orden,
    bool Activo,
    int Resultados);

public sealed record RequisitoCampoAdmin(
    string Campo,
    bool Obligatorio,
    int? LongitudMaxima,
    int Orden);

public sealed record TipoEvidenciaAdmin(
    int IdTipoEvidencia,
    string Codigo,
    string Nombre);

public sealed record RequisitoEvidenciaAdmin(
    int IdTipoEvidencia,
    string Codigo,
    string Nombre,
    bool Obligatorio,
    int CantidadMinima,
    int CantidadMaxima,
    int Orden);

public sealed record RequisitosCategoriaAdmin(
    int IdCategoria,
    IReadOnlyList<RequisitoCampoAdmin> Campos,
    IReadOnlyList<RequisitoEvidenciaAdmin> Evidencias);

public sealed record UsuarioCandidatoEspecialista(
    int IdUsuario,
    string NombreCompleto,
    string Correo,
    bool Activo,
    bool YaEsEspecialista);

public sealed record EspecialidadAdmin(
    int IdCategoria,
    string Categoria,
    int? IdMarca,
    string? Marca,
    string Nivel,
    bool Activa);

public sealed record EspecialistaAdmin(
    int IdEspecialista,
    int IdUsuario,
    string Nombre,
    string Correo,
    string Estado,
    short? AniosExperiencia,
    string? Biografia,
    DateTime FechaAlta,
    int CargaActiva,
    IReadOnlyList<EspecialidadAdmin> Especialidades);

public sealed record DatosCategoriaAdmin(
    string? Nombre,
    string? Descripcion,
    bool Activa);

public sealed record DatosMarcaAdmin(string? Nombre, bool Activa);

public sealed record DatosCriterioAdmin(
    string? Nombre,
    string? Descripcion,
    string? TipoDato,
    bool Obligatorio,
    short Orden,
    bool Activo);

public sealed record DatosRequisitosCategoria(
    IReadOnlyList<RequisitoCampoAdmin> Campos,
    IReadOnlyList<RequisitoEvidenciaAdmin> Evidencias);

public sealed record DatosEspecialidadAdmin(
    int IdCategoria,
    int? IdMarca,
    string? Nivel);

public sealed record DatosEspecialistaAdmin(
    int IdUsuario,
    string? Estado,
    short? AniosExperiencia,
    string? Biografia,
    IReadOnlyList<DatosEspecialidadAdmin> Especialidades);

public enum EstadoOperacionAdmin
{
    Exito,
    NoEncontrado,
    DatosInvalidos,
    Conflicto,
    NoPermitido,
    EstadoNoConfigurado
}

public sealed record ResultadoAdmin<T>(
    EstadoOperacionAdmin Estado,
    T? Valor = default,
    string? Campo = null,
    string? Detalle = null);

public interface ICatalogoAdminStore
{
    Task<IReadOnlyList<CategoriaAdmin>> ListarCategoriasAsync(CancellationToken cancellationToken);
    Task<ResultadoAdmin<CategoriaAdmin>> CrearCategoriaAsync(
        DatosCategoriaAdmin datos, CancellationToken cancellationToken);
    Task<ResultadoAdmin<CategoriaAdmin>> ActualizarCategoriaAsync(
        int idCategoria, DatosCategoriaAdmin datos, CancellationToken cancellationToken);
    Task<IReadOnlyList<MarcaAdmin>> ListarMarcasAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<TipoEvidenciaAdmin>> ListarTiposEvidenciaAsync(
        CancellationToken cancellationToken);
    Task<ResultadoAdmin<MarcaAdmin>> CrearMarcaAsync(
        DatosMarcaAdmin datos, CancellationToken cancellationToken);
    Task<ResultadoAdmin<MarcaAdmin>> ActualizarMarcaAsync(
        int idMarca, DatosMarcaAdmin datos, CancellationToken cancellationToken);
    Task<IReadOnlyList<CategoriaMarcaAdmin>> ListarCategoriaMarcasAsync(
        int idCategoria, CancellationToken cancellationToken);
    Task<ResultadoAdmin<CategoriaMarcaAdmin>> VincularMarcaAsync(
        int idCategoria, int idMarca, bool activa, CancellationToken cancellationToken);
    Task<RequisitosCategoriaAdmin?> ObtenerRequisitosAsync(
        int idCategoria, CancellationToken cancellationToken);
    Task<ResultadoAdmin<RequisitosCategoriaAdmin>> ActualizarRequisitosAsync(
        int idCategoria, DatosRequisitosCategoria datos,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<CriterioAdmin>> ListarCriteriosAsync(
        int idCategoria, CancellationToken cancellationToken);
    Task<ResultadoAdmin<CriterioAdmin>> CrearCriterioAsync(
        int idCategoria, DatosCriterioAdmin datos, CancellationToken cancellationToken);
    Task<ResultadoAdmin<CriterioAdmin>> ActualizarCriterioAsync(
        int idCriterio, DatosCriterioAdmin datos, CancellationToken cancellationToken);
}

public interface IEspecialistaAdminStore
{
    Task<IReadOnlyList<EspecialistaAdmin>> ListarAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<UsuarioCandidatoEspecialista>> BuscarUsuariosAsync(
        string? buscar, CancellationToken cancellationToken);
    Task<ResultadoAdmin<EspecialistaAdmin>> CrearAsync(
        DatosEspecialistaAdmin datos, CancellationToken cancellationToken);
    Task<ResultadoAdmin<EspecialistaAdmin>> ActualizarAsync(
        int idEspecialista, DatosEspecialistaAdmin datos,
        CancellationToken cancellationToken);
}

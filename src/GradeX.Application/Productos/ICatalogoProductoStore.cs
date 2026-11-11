namespace GradeX.Application.Productos;

public sealed record CategoriaResumen(int IdCategoria, string Nombre, string? Descripcion);

public sealed record MarcaResumen(int IdMarca, string Nombre);

public sealed record CampoRequisito(
    string Campo, string Etiqueta, bool Obligatorio, int? LongitudMaxima, int Orden);

public sealed record EvidenciaRequisito(
    int IdTipoEvidencia,
    string Codigo,
    string Nombre,
    bool Obligatorio,
    int CantidadMinima,
    int CantidadMaxima,
    int Orden);

public sealed record RequisitosCategoria(
    int IdCategoria,
    IReadOnlyList<CampoRequisito> Campos,
    IReadOnlyList<EvidenciaRequisito> Evidencias);

public interface ICatalogoProductoStore
{
    Task<IReadOnlyList<CategoriaResumen>> ListarCategoriasAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<MarcaResumen>> ListarMarcasAsync(int idCategoria, CancellationToken cancellationToken);

    Task<RequisitosCategoria?> ObtenerRequisitosAsync(int idCategoria, CancellationToken cancellationToken);
}

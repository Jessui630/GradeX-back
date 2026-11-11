using GradeX.Application.Productos;
using GradeX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GradeX.Infrastructure.Persistence.Queries;

public sealed class CatalogoProductoStore(GradeXDbContext db) : ICatalogoProductoStore
{
    public async Task<IReadOnlyList<CategoriaResumen>> ListarCategoriasAsync(
        CancellationToken cancellationToken) =>
        await db.Categoria
            .AsNoTracking()
            .Where(c => c.Activa)
            .OrderBy(c => c.Nombre)
            .Select(c => new CategoriaResumen(c.IdCategoria, c.Nombre, c.Descripcion))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MarcaResumen>> ListarMarcasAsync(
        int idCategoria, CancellationToken cancellationToken) =>
        await db.CategoriaMarcas
            .AsNoTracking()
            .Where(cm => cm.IdCategoria == idCategoria && cm.Activa &&
                cm.IdCategoriaNavigation.Activa && cm.IdMarcaNavigation.Activa)
            .OrderBy(cm => cm.IdMarcaNavigation.Nombre)
            .Select(cm => new MarcaResumen(cm.IdMarca, cm.IdMarcaNavigation.Nombre))
            .ToListAsync(cancellationToken);

    public async Task<RequisitosCategoria?> ObtenerRequisitosAsync(
        int idCategoria, CancellationToken cancellationToken)
    {
        var categoria = await db.Categoria
            .AsNoTracking()
            .Where(c => c.IdCategoria == idCategoria && c.Activa)
            .Select(c => new { c.IdCategoria })
            .SingleOrDefaultAsync(cancellationToken);
        if (categoria is null)
            return null;

        var campos = await db.CategoriaCampoRequisitos
            .AsNoTracking()
            .Where(r => r.IdCategoria == idCategoria)
            .OrderBy(r => r.Orden)
            .Select(r => new { r.Campo, r.Obligatorio, r.LongitudMaxima, r.Orden })
            .ToListAsync(cancellationToken);

        var camposRequisito = campos
            .Select(r => new CampoRequisito(
                r.Campo, Etiqueta(r.Campo), r.Obligatorio, r.LongitudMaxima, r.Orden))
            .ToList();

        var evidencias = await db.CategoriaEvidenciaRequisitos
            .AsNoTracking()
            .Where(r => r.IdCategoria == idCategoria)
            .OrderBy(r => r.Orden)
            .Select(r => new EvidenciaRequisito(
                r.IdTipoEvidencia,
                r.IdTipoEvidenciaNavigation.Codigo,
                r.IdTipoEvidenciaNavigation.Nombre,
                r.Obligatorio,
                r.CantidadMinima,
                r.CantidadMaxima,
                r.Orden))
            .ToListAsync(cancellationToken);

        return new RequisitosCategoria(idCategoria, camposRequisito, evidencias);
    }

    private static string Etiqueta(string campo) => campo.ToUpperInvariant() switch
    {
        "NOMBRE" => "Nombre del producto",
        "MODELO" => "Modelo",
        "SKU" => "SKU",
        "NUMERO_SERIE" => "Número de serie",
        "IMEI" => "IMEI",
        "ACCESORIOS" => "Accesorios",
        "DESCRIPCION" => "Descripción",
        "MARCA" => "Marca",
        _ => campo
    };
}

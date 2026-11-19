using GradeX.Application.Solicitudes;
using GradeX.Infrastructure.Persistence;
using GradeX.Infrastructure.Persistence.Models;
using GradeX.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;

namespace GradeX.Tests;

public sealed class CatalogoSqlTests
{
    private static readonly string[] CategoriasOficiales =
    [
        "Sneakers",
        "Ropa",
        "Coleccionables",
        "TCG",
        "Consolas",
        "Teléfonos",
        "Laptops",
        "Computadoras y componentes"
    ];

    private static readonly string[] TiposEvidenciaOficiales =
    [
        "VISTA_GENERAL",
        "ETIQUETA",
        "SKU",
        "NUMERO_SERIE",
        "CAJA",
        "ACCESORIOS",
        "DEFECTOS"
    ];

    [LocalDbFact]
    public async Task CatalogoOficialEstaCompletoYEsCoherente()
    {
        await using var db = await SqlIntegration.AbrirContextoAsync();

        var categorias = await db.Categoria.AsNoTracking()
            .Where(c => c.Activa)
            .ToListAsync();

        Assert.NotEmpty(categorias);
        foreach (var nombre in CategoriasOficiales)
            Assert.Contains(categorias, c => c.Nombre == nombre);

        foreach (var categoria in categorias)
        {
            var requisitos = await new CatalogoProductoStore(db)
                .ObtenerRequisitosAsync(categoria.IdCategoria, CancellationToken.None);

            Assert.NotNull(requisitos);
            Assert.Contains(requisitos.Campos,
                c => c.Campo == "NOMBRE" && c.Obligatorio && c.LongitudMaxima == 150);
            Assert.NotEmpty(requisitos.Evidencias);
            Assert.Contains(requisitos.Evidencias, e => e.Obligatorio && e.CantidadMinima > 0);
            Assert.All(requisitos.Evidencias,
                e => Assert.True(e.CantidadMaxima >= e.CantidadMinima,
                    $"Categoría {categoria.Nombre}, evidencia {e.Codigo}: máximo menor que mínimo."));

            var vista = Assert.Single(requisitos.Evidencias,
                e => e.Codigo == "VISTA_GENERAL");
            Assert.True(vista.Obligatorio);
            Assert.Equal(1, vista.CantidadMinima);
            Assert.Equal(1, vista.CantidadMaxima);
        }

        var estados = await db.EstadoSolicituds.AsNoTracking()
            .Select(e => e.Codigo)
            .ToListAsync();
        Assert.Contains("BORRADOR", estados);
        Assert.Contains("PENDIENTE_ASIGNACION", estados);

        var tipos = await db.TipoEvidencia.AsNoTracking()
            .Select(t => t.Codigo)
            .ToListAsync();
        foreach (var codigo in TiposEvidenciaOficiales)
            Assert.Contains(codigo, tipos);
    }

    [LocalDbFact]
    public async Task ReparacionEsIdempotenteSobreCatalogoExistente()
    {
        await using var db = await SqlIntegration.AbrirContextoAsync();

        var antes = await ContarCatalogoAsync(db);

        await db.Database.ExecuteSqlRawAsync("""
            DECLARE @Estados TABLE (Codigo nvarchar(40) NOT NULL);
            INSERT INTO @Estados (Codigo) VALUES (N'BORRADOR'), (N'PENDIENTE_ASIGNACION');
            INSERT INTO dbo.EstadoSolicitud (Codigo, Nombre, OrdenVisual, EsFinal)
            SELECT e.Codigo, e.Codigo, 1, 0
            FROM @Estados e
            WHERE NOT EXISTS (
                SELECT 1 FROM dbo.EstadoSolicitud s WHERE s.Codigo = e.Codigo);

            IF NOT EXISTS (SELECT 1 FROM dbo.Categoria WHERE Nombre = N'Sneakers')
                INSERT INTO dbo.Categoria (Nombre, Descripcion, Activa)
                VALUES (N'Sneakers', N'Calzado deportivo y urbano', 1);

            INSERT INTO dbo.CategoriaCampoRequisito
                (IdCategoria, Campo, Obligatorio, LongitudMaxima, Orden)
            SELECT c.IdCategoria, N'NOMBRE', 1, 150, 1
            FROM dbo.Categoria c
            WHERE c.Nombre = N'Sneakers'
              AND NOT EXISTS (
                  SELECT 1 FROM dbo.CategoriaCampoRequisito r
                  WHERE r.IdCategoria = c.IdCategoria AND r.Campo = N'NOMBRE');
            """);

        var despues = await ContarCatalogoAsync(db);

        Assert.Equal(antes.Categorias, despues.Categorias);
        Assert.Equal(antes.Tipos, despues.Tipos);
        Assert.Equal(antes.Campos, despues.Campos);
        Assert.Equal(antes.Evidencias, despues.Evidencias);
        Assert.Equal(antes.Estados, despues.Estados);
    }

    private static async Task<(int Categorias, int Tipos, int Campos, int Evidencias, int Estados)>
        ContarCatalogoAsync(GradeXDbContext db) =>
        (
            await db.Categoria.CountAsync(),
            await db.TipoEvidencia.CountAsync(),
            await db.CategoriaCampoRequisitos.CountAsync(),
            await db.CategoriaEvidenciaRequisitos.CountAsync(),
            await db.EstadoSolicituds.CountAsync()
        );
}

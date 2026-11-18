using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GradeX.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Repara el catálogo del Sprint 1 cuando la migración histórica quedó
    /// registrada sin haber sembrado sus datos, o cuando faltan filas concretas.
    /// Es idempotente: no elimina ni sobrescribe catálogo personalizado.
    /// </summary>
    public partial class RepararCatalogoSprint1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @Estados TABLE
                (
                    Codigo nvarchar(40) NOT NULL,
                    Nombre nvarchar(100) NOT NULL,
                    OrdenVisual smallint NOT NULL,
                    EsFinal bit NOT NULL
                );
                INSERT INTO @Estados (Codigo, Nombre, OrdenVisual, EsFinal) VALUES
                    (N'BORRADOR', N'Borrador', 1, 0),
                    (N'PENDIENTE_ASIGNACION', N'Pendiente de asignación', 2, 0);

                INSERT INTO dbo.EstadoSolicitud (Codigo, Nombre, OrdenVisual, EsFinal)
                SELECT e.Codigo, e.Nombre, e.OrdenVisual, e.EsFinal
                FROM @Estados e
                WHERE NOT EXISTS (
                    SELECT 1 FROM dbo.EstadoSolicitud s WHERE s.Codigo = e.Codigo);

                DECLARE @Tipos TABLE
                (
                    Codigo nvarchar(40) NOT NULL,
                    Nombre nvarchar(100) NOT NULL
                );
                INSERT INTO @Tipos (Codigo, Nombre) VALUES
                    (N'VISTA_GENERAL', N'Vista general'),
                    (N'ETIQUETA', N'Etiqueta'),
                    (N'SKU', N'SKU'),
                    (N'NUMERO_SERIE', N'Número de serie'),
                    (N'CAJA', N'Caja'),
                    (N'ACCESORIOS', N'Accesorios'),
                    (N'DEFECTOS', N'Defectos visibles');

                INSERT INTO dbo.TipoEvidencia (Codigo, Nombre)
                SELECT t.Codigo, t.Nombre
                FROM @Tipos t
                WHERE NOT EXISTS (
                    SELECT 1 FROM dbo.TipoEvidencia e WHERE e.Codigo = t.Codigo);

                DECLARE @Categorias TABLE
                (
                    Nombre nvarchar(100) NOT NULL,
                    Descripcion nvarchar(300) NULL
                );
                INSERT INTO @Categorias (Nombre, Descripcion) VALUES
                    (N'Sneakers', N'Calzado deportivo y urbano'),
                    (N'Ropa', N'Prendas y accesorios textiles'),
                    (N'Coleccionables', N'Artículos de colección'),
                    (N'TCG', N'Juegos de cartas coleccionables'),
                    (N'Consolas', N'Consolas y videojuegos'),
                    (N'Teléfonos', N'Teléfonos y dispositivos móviles'),
                    (N'Laptops', N'Computadoras portátiles'),
                    (N'Computadoras y componentes', N'Equipos y componentes de cómputo');

                INSERT INTO dbo.Categoria (Nombre, Descripcion, Activa)
                SELECT o.Nombre, o.Descripcion, 1
                FROM @Categorias o
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM dbo.Categoria c
                    WHERE c.Nombre COLLATE Latin1_General_CI_AI =
                        o.Nombre COLLATE Latin1_General_CI_AI);

                INSERT INTO dbo.CategoriaCampoRequisito
                    (IdCategoria, Campo, Obligatorio, LongitudMaxima, Orden)
                SELECT c.IdCategoria, N'NOMBRE', 1, 150, 1
                FROM dbo.Categoria c
                INNER JOIN @Categorias o ON
                    c.Nombre COLLATE Latin1_General_CI_AI =
                    o.Nombre COLLATE Latin1_General_CI_AI
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM dbo.CategoriaCampoRequisito r
                    WHERE r.IdCategoria = c.IdCategoria AND r.Campo = N'NOMBRE');

                INSERT INTO dbo.CategoriaEvidenciaRequisito
                    (IdCategoria, IdTipoEvidencia, Obligatorio, CantidadMinima, CantidadMaxima, Orden)
                SELECT
                    c.IdCategoria,
                    e.IdTipoEvidencia,
                    CASE WHEN e.Codigo = N'VISTA_GENERAL' THEN 1 ELSE 0 END,
                    CASE WHEN e.Codigo = N'VISTA_GENERAL' THEN 1 ELSE 0 END,
                    CASE WHEN e.Codigo = N'VISTA_GENERAL' THEN 1 ELSE 3 END,
                    CASE e.Codigo
                        WHEN N'VISTA_GENERAL' THEN 1
                        WHEN N'ETIQUETA' THEN 2
                        WHEN N'SKU' THEN 3
                        WHEN N'NUMERO_SERIE' THEN 4
                        WHEN N'CAJA' THEN 5
                        WHEN N'ACCESORIOS' THEN 6
                        ELSE 7
                    END
                FROM dbo.Categoria c
                INNER JOIN @Categorias o ON
                    c.Nombre COLLATE Latin1_General_CI_AI =
                    o.Nombre COLLATE Latin1_General_CI_AI
                CROSS JOIN dbo.TipoEvidencia e
                WHERE e.Codigo IN (SELECT Codigo FROM @Tipos)
                  AND NOT EXISTS (
                      SELECT 1
                      FROM dbo.CategoriaEvidenciaRequisito r
                      WHERE r.IdCategoria = c.IdCategoria
                        AND r.IdTipoEvidencia = e.IdTipoEvidencia);

                -- La vista general es la evidencia obligatoria y debe admitir una sola imagen.
                UPDATE r
                SET r.Obligatorio = 1,
                    r.CantidadMinima = 1,
                    r.CantidadMaxima = 1
                FROM dbo.CategoriaEvidenciaRequisito r
                INNER JOIN dbo.TipoEvidencia e ON e.IdTipoEvidencia = r.IdTipoEvidencia
                INNER JOIN dbo.Categoria c ON c.IdCategoria = r.IdCategoria
                INNER JOIN @Categorias o ON
                    c.Nombre COLLATE Latin1_General_CI_AI =
                    o.Nombre COLLATE Latin1_General_CI_AI
                WHERE e.Codigo = N'VISTA_GENERAL'
                  AND (r.Obligatorio = 0 OR r.CantidadMinima <> 1 OR r.CantidadMaxima <> 1);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // La reparación es aditiva. No se eliminan categorías ni requisitos
            // porque podrían contener configuración o solicitudes reales.
        }
    }
}

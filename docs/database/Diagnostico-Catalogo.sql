-- GradeX - diagnóstico de solo lectura del catálogo del Sprint 1.
-- No modifica datos. Ejecutar únicamente contra la base autorizada.
--
-- sqlcmd -S sql-gradex-students.database.windows.net -d GradeX_DevCloud -G -I -i docs/database/Diagnostico-Catalogo.sql
--
-- Resultado esperado para el catálogo oficial:
--   Categorías activas: 8, tipos de evidencia: 7, estados base: 2
--   Cada categoría debe tener el campo NOMBRE y 7 requisitos de evidencia.
--   VISTA_GENERAL debe ser obligatoria, con mínimo 1 y máximo 1.

SET NOCOUNT ON;

SELECT DB_NAME() AS BaseDeDatos, @@SERVERNAME AS Servidor, SUSER_SNAME() AS Usuario;

SELECT MigrationId, ProductVersion
FROM dbo.__EFMigrationsHistory
ORDER BY MigrationId;

SELECT 'Categoria' AS Entidad, COUNT(*) AS Total FROM dbo.Categoria
UNION ALL SELECT 'Categoria activa', COUNT(*) FROM dbo.Categoria WHERE Activa = 1
UNION ALL SELECT 'TipoEvidencia', COUNT(*) FROM dbo.TipoEvidencia
UNION ALL SELECT 'EstadoSolicitud', COUNT(*) FROM dbo.EstadoSolicitud
UNION ALL SELECT 'CategoriaCampoRequisito', COUNT(*) FROM dbo.CategoriaCampoRequisito
UNION ALL SELECT 'CategoriaEvidenciaRequisito', COUNT(*) FROM dbo.CategoriaEvidenciaRequisito;

SELECT
    c.IdCategoria,
    c.Nombre,
    c.Activa,
    (SELECT COUNT(*) FROM dbo.CategoriaCampoRequisito r
     WHERE r.IdCategoria = c.IdCategoria) AS Campos,
    (SELECT COUNT(*) FROM dbo.CategoriaEvidenciaRequisito r
     WHERE r.IdCategoria = c.IdCategoria) AS Evidencias,
    (SELECT COUNT(*) FROM dbo.CategoriaEvidenciaRequisito r
     WHERE r.IdCategoria = c.IdCategoria AND r.Obligatorio = 1 AND r.CantidadMinima > 0)
        AS EvidenciasObligatorias
FROM dbo.Categoria c
ORDER BY c.Nombre;

SELECT
    c.Nombre AS Categoria,
    e.Codigo,
    e.Nombre AS TipoEvidencia,
    r.Obligatorio,
    r.CantidadMinima,
    r.CantidadMaxima,
    r.Orden
FROM dbo.CategoriaEvidenciaRequisito r
INNER JOIN dbo.Categoria c ON c.IdCategoria = r.IdCategoria
INNER JOIN dbo.TipoEvidencia e ON e.IdTipoEvidencia = r.IdTipoEvidencia
ORDER BY c.Nombre, r.Orden;

SELECT Codigo, Nombre, OrdenVisual, EsFinal
FROM dbo.EstadoSolicitud
ORDER BY OrdenVisual;

-- Categorías oficiales ausentes o inactivas que explicarían que la PWA no
-- muestre opciones en el paso Categoría.
SELECT o.Nombre AS CategoriaOficialFaltante
FROM (VALUES
    (N'Sneakers'), (N'Ropa'), (N'Coleccionables'), (N'TCG'), (N'Consolas'),
    (N'Teléfonos'), (N'Laptops'), (N'Computadoras y componentes')
) AS o(Nombre)
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Categoria c
    WHERE c.Nombre COLLATE Latin1_General_CI_AI =
        o.Nombre COLLATE Latin1_General_CI_AI
      AND c.Activa = 1);

-- Categorías sin evidencia obligatoria: harían fallar el envío con 503.
SELECT c.IdCategoria, c.Nombre
FROM dbo.Categoria c
WHERE c.Activa = 1
  AND NOT EXISTS (
      SELECT 1 FROM dbo.CategoriaEvidenciaRequisito r
      WHERE r.IdCategoria = c.IdCategoria
        AND r.Obligatorio = 1
        AND r.CantidadMinima > 0);

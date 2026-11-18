-- GradeX - diagnóstico de solo lectura para Sprint 2-3.
-- No modifica datos. Ejecutar solo contra la base autorizada.
--
-- sqlcmd -S sql-gradex-students.database.windows.net -d GradeX_DevCloud -G -I -i docs/database/Diagnostico-Sprint2-3.sql
--
-- Revisa migraciones, estados, notificaciones, especialistas, asignaciones,
-- evaluaciones y certificados antes de autorizar cualquier cambio.

SET NOCOUNT ON;

SELECT DB_NAME() AS BaseDeDatos, @@SERVERNAME AS Servidor, SUSER_SNAME() AS Usuario;

SELECT MigrationId, ProductVersion
FROM dbo.__EFMigrationsHistory
ORDER BY MigrationId;

SELECT 'EstadoSolicitud' AS Entidad, Codigo, Nombre, OrdenVisual, EsFinal
FROM dbo.EstadoSolicitud
ORDER BY OrdenVisual;

SELECT 'EstadoCertificado' AS Entidad, Codigo, Nombre
FROM dbo.EstadoCertificado
ORDER BY Codigo;

SELECT 'TipoNotificacion' AS Entidad, Codigo, Nombre
FROM dbo.TipoNotificacion
ORDER BY Codigo;

SELECT
    COL_LENGTH('dbo.Usuario', 'RecibirNotificaciones') AS LongitudColumnaPreferencia,
    (SELECT COUNT(*) FROM dbo.Usuario) AS Usuarios,
    (SELECT COUNT(*) FROM dbo.Usuario WHERE RecibirNotificaciones = 1) AS ConAvisos,
    (SELECT COUNT(*) FROM dbo.Usuario WHERE RecibirNotificaciones = 0) AS SinAvisos;

SELECT
    (SELECT COUNT(*) FROM dbo.Categoria) AS Categorias,
    (SELECT COUNT(*) FROM dbo.Categoria WHERE Activa = 1) AS CategoriasActivas,
    (SELECT COUNT(*) FROM dbo.CriterioEvaluacion) AS Criterios,
    (SELECT COUNT(*) FROM dbo.CriterioEvaluacion WHERE Activo = 1) AS CriteriosActivos,
    (SELECT COUNT(*) FROM dbo.TipoEvidencia) AS TiposEvidencia,
    (SELECT COUNT(*) FROM dbo.CategoriaEvidenciaRequisito) AS RequisitosEvidencia;

SELECT
    s.Estado AS EstadoEspecialista,
    COUNT(*) AS Total,
    SUM(CASE WHEN u.Activo = 1 THEN 1 ELSE 0 END) AS UsuariosActivos
FROM dbo.Especialista s
INNER JOIN dbo.Usuario u ON u.IdUsuario = s.IdUsuario
GROUP BY s.Estado;

SELECT
    e.Codigo AS EstadoSolicitud,
    COUNT(*) AS Total
FROM dbo.SolicitudEvaluacion se
INNER JOIN dbo.EstadoSolicitud e ON e.IdEstadoSolicitud = se.IdEstadoSolicitud
GROUP BY e.Codigo
ORDER BY e.Codigo;

SELECT
    (SELECT COUNT(*) FROM dbo.AsignacionEspecialista WHERE Activa = 1) AS AsignacionesActivas,
    (SELECT COUNT(*) FROM dbo.Evaluacion) AS Evaluaciones,
    (SELECT COUNT(*) FROM dbo.Evaluacion WHERE FechaFin IS NOT NULL) AS EvaluacionesFinalizadas,
    (SELECT COUNT(*) FROM dbo.Certificado) AS Certificados,
    (SELECT COUNT(*) FROM dbo.Notificacion) AS Notificaciones,
    (SELECT COUNT(*) FROM dbo.Notificacion WHERE Leida = 0) AS NotificacionesNoLeidas;

-- Asignaciones activas sin especialista activo o sin usuario activo.
SELECT a.IdAsignacion, a.IdSolicitud, a.IdEspecialista, e.Estado, u.Activo AS UsuarioActivo
FROM dbo.AsignacionEspecialista a
INNER JOIN dbo.Especialista e ON e.IdEspecialista = a.IdEspecialista
INNER JOIN dbo.Usuario u ON u.IdUsuario = e.IdUsuario
WHERE a.Activa = 1
  AND (e.Estado <> N'ACTIVO' OR u.Activo = 0);

-- Evaluaciones cuya solicitud no está asignada al especialista evaluador.
SELECT ev.IdEvaluacion, ev.IdSolicitud, ev.IdEspecialista
FROM dbo.Evaluacion ev
WHERE NOT EXISTS (
    SELECT 1
    FROM dbo.AsignacionEspecialista a
    WHERE a.IdSolicitud = ev.IdSolicitud
      AND a.IdEspecialista = ev.IdEspecialista
      AND a.Activa = 1);

-- Certificados sin evaluación finalizada.
SELECT c.IdCertificado, c.NumeroCertificado, c.IdEvaluacion, ev.FechaFin, ev.Dictamen
FROM dbo.Certificado c
INNER JOIN dbo.Evaluacion ev ON ev.IdEvaluacion = c.IdEvaluacion
WHERE ev.FechaFin IS NULL OR ev.Dictamen IS NULL;

-- Notificaciones sin tipo válido.
SELECT n.IdNotificacion, n.IdUsuario, n.IdSolicitud, n.IdTipoNotificacion
FROM dbo.Notificacion n
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.TipoNotificacion t
    WHERE t.IdTipoNotificacion = n.IdTipoNotificacion);

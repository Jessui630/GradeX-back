-- GradeX - migración pendiente Sprint 2-3 (estados, notificaciones y preferencia).
-- NO EJECUTAR EN AZURE SIN AUTORIZACIÓN EXPLÍCITA.
-- Requiere que las migraciones del Sprint 1 estén aplicadas o que se ejecute
-- después de Produccion-Registro-Pendientes.sql y Reparar-Catalogo-Sprint1.sql.
-- No elimina datos; la columna nueva tiene DEFAULT 1 y las siembras son idempotentes.
-- Revisar el destino y conservar respaldo/PITR antes de ejecutar.
--
-- sqlcmd -S <servidor> -d GradeX_DevCloud -G -I -i docs/database/Sprint2-3-Pendientes.sql

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118195907_Sprint2EstadosNotificaciones'
)
BEGIN
    ALTER TABLE [Usuario] ADD [RecibirNotificaciones] bit NOT NULL CONSTRAINT [DF_Usuario_RecibirNotificaciones] DEFAULT CAST(1 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118195907_Sprint2EstadosNotificaciones'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.EstadoSolicitud WHERE Codigo = N'ASIGNADA')
        INSERT INTO dbo.EstadoSolicitud (Codigo, Nombre, OrdenVisual, EsFinal)
        VALUES (N'ASIGNADA', N'Asignada', 3, 0);
    IF NOT EXISTS (SELECT 1 FROM dbo.EstadoSolicitud WHERE Codigo = N'EN_EVALUACION')
        INSERT INTO dbo.EstadoSolicitud (Codigo, Nombre, OrdenVisual, EsFinal)
        VALUES (N'EN_EVALUACION', N'En evaluación', 4, 0);
    IF NOT EXISTS (SELECT 1 FROM dbo.EstadoSolicitud WHERE Codigo = N'FALTA_EVIDENCIA')
        INSERT INTO dbo.EstadoSolicitud (Codigo, Nombre, OrdenVisual, EsFinal)
        VALUES (N'FALTA_EVIDENCIA', N'Falta evidencia', 5, 0);
    IF NOT EXISTS (SELECT 1 FROM dbo.EstadoSolicitud WHERE Codigo = N'FINALIZADA')
        INSERT INTO dbo.EstadoSolicitud (Codigo, Nombre, OrdenVisual, EsFinal)
        VALUES (N'FINALIZADA', N'Finalizada', 6, 1);

    IF NOT EXISTS (SELECT 1 FROM dbo.EstadoCertificado WHERE Codigo = N'EMITIDO')
        INSERT INTO dbo.EstadoCertificado (Codigo, Nombre)
        VALUES (N'EMITIDO', N'Emitido');

    IF NOT EXISTS (SELECT 1 FROM dbo.TipoNotificacion WHERE Codigo = N'CAMBIO_ESTADO')
        INSERT INTO dbo.TipoNotificacion (Codigo, Nombre)
        VALUES (N'CAMBIO_ESTADO', N'Cambio de estado');
    IF NOT EXISTS (SELECT 1 FROM dbo.TipoNotificacion WHERE Codigo = N'SOLICITUD_EVIDENCIA')
        INSERT INTO dbo.TipoNotificacion (Codigo, Nombre)
        VALUES (N'SOLICITUD_EVIDENCIA', N'Solicitud de evidencia');
    IF NOT EXISTS (SELECT 1 FROM dbo.TipoNotificacion WHERE Codigo = N'EVIDENCIA_RECIBIDA')
        INSERT INTO dbo.TipoNotificacion (Codigo, Nombre)
        VALUES (N'EVIDENCIA_RECIBIDA', N'Evidencia recibida');
    IF NOT EXISTS (SELECT 1 FROM dbo.TipoNotificacion WHERE Codigo = N'CERTIFICADO_EMITIDO')
        INSERT INTO dbo.TipoNotificacion (Codigo, Nombre)
        VALUES (N'CERTIFICADO_EMITIDO', N'Certificado emitido');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118195907_Sprint2EstadosNotificaciones'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261118195907_Sprint2EstadosNotificaciones', N'10.0.13');
END;

COMMIT;
GO


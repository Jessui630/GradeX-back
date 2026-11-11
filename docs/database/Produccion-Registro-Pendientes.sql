-- GradeX - migraciones pendientes para restablecer el registro real.
-- Cubre 20261108214534_AddSprint1Solicitudes y 20261111190603_SeedRolesOperativos.
-- Generado con: dotnet ef migrations script 20261022203833_AddCertificateSpecialtyReference 20261111190603_SeedRolesOperativos --idempotent
-- Revisar el servidor y la base de destino antes de ejecutar. No ejecutar sin autorizacion.
-- No ejecutar Down ni recrear la base. Recuperacion ante fallo: restaurar/PITR segun el procedimiento aprobado.

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108214534_AddSprint1Solicitudes'
)
BEGIN
    ALTER TABLE [SolicitudEvaluacion] ADD [ClaveIdempotencia] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108214534_AddSprint1Solicitudes'
)
BEGIN
    ALTER TABLE [Evidencia] ADD [HashSha256] nvarchar(64) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108214534_AddSprint1Solicitudes'
)
BEGIN
    ALTER TABLE [Evidencia] ADD [NombreOriginal] nvarchar(255) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108214534_AddSprint1Solicitudes'
)
BEGIN
    ALTER TABLE [Evidencia] ADD [TamanoBytes] bigint NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108214534_AddSprint1Solicitudes'
)
BEGIN
    ALTER TABLE [Evidencia] ADD [TipoContenido] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108214534_AddSprint1Solicitudes'
)
BEGIN
    CREATE TABLE [CategoriaCampoRequisito] (
        [IdCategoria] int NOT NULL,
        [Campo] nvarchar(30) NOT NULL,
        [Obligatorio] bit NOT NULL DEFAULT CAST(1 AS bit),
        [LongitudMaxima] int NULL,
        [Orden] int NOT NULL,
        CONSTRAINT [PK_CategoriaCampoRequisito] PRIMARY KEY ([IdCategoria], [Campo]),
        CONSTRAINT [FK_CategoriaCampoRequisito_Categoria] FOREIGN KEY ([IdCategoria]) REFERENCES [Categoria] ([IdCategoria]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108214534_AddSprint1Solicitudes'
)
BEGIN
    CREATE TABLE [CategoriaEvidenciaRequisito] (
        [IdCategoria] int NOT NULL,
        [IdTipoEvidencia] int NOT NULL,
        [Obligatorio] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CantidadMinima] int NOT NULL DEFAULT 1,
        [CantidadMaxima] int NOT NULL DEFAULT 1,
        [Orden] int NOT NULL,
        CONSTRAINT [PK_CategoriaEvidenciaRequisito] PRIMARY KEY ([IdCategoria], [IdTipoEvidencia]),
        CONSTRAINT [FK_CategoriaEvidenciaRequisito_Categoria] FOREIGN KEY ([IdCategoria]) REFERENCES [Categoria] ([IdCategoria]) ON DELETE CASCADE,
        CONSTRAINT [FK_CategoriaEvidenciaRequisito_Tipo] FOREIGN KEY ([IdTipoEvidencia]) REFERENCES [TipoEvidencia] ([IdTipoEvidencia])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108214534_AddSprint1Solicitudes'
)
BEGIN
    CREATE TABLE [SesionUsuario] (
        [IdSesion] uniqueidentifier NOT NULL,
        [IdUsuario] int NOT NULL,
        [TokenHash] nchar(64) NOT NULL,
        [FechaCreacion] datetime2(0) NOT NULL CONSTRAINT [DF_SesionUsuario_FechaCreacion] DEFAULT ((sysdatetime())),
        [FechaExpiracion] datetime2(0) NOT NULL,
        [FechaRevocacion] datetime2(0) NULL,
        CONSTRAINT [PK_SesionUsuario] PRIMARY KEY ([IdSesion]),
        CONSTRAINT [FK_SesionUsuario_Usuario] FOREIGN KEY ([IdUsuario]) REFERENCES [Usuario] ([IdUsuario]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108214534_AddSprint1Solicitudes'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UQ_Solicitud_Idempotencia] ON [SolicitudEvaluacion] ([ClaveIdempotencia]) WHERE ([ClaveIdempotencia] IS NOT NULL)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108214534_AddSprint1Solicitudes'
)
BEGIN
    CREATE INDEX [IX_CategoriaEvidenciaRequisito_IdTipoEvidencia] ON [CategoriaEvidenciaRequisito] ([IdTipoEvidencia]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108214534_AddSprint1Solicitudes'
)
BEGIN
    CREATE INDEX [IX_SesionUsuario_Expiracion] ON [SesionUsuario] ([FechaExpiracion]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108214534_AddSprint1Solicitudes'
)
BEGIN
    CREATE INDEX [IX_SesionUsuario_Usuario] ON [SesionUsuario] ([IdUsuario]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108214534_AddSprint1Solicitudes'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_SesionUsuario_TokenHash] ON [SesionUsuario] ([TokenHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108214534_AddSprint1Solicitudes'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.EstadoSolicitud WHERE Codigo = N'BORRADOR')
        INSERT INTO dbo.EstadoSolicitud (Codigo, Nombre, OrdenVisual, EsFinal)
        VALUES (N'BORRADOR', N'Borrador', 1, 0);
    IF NOT EXISTS (SELECT 1 FROM dbo.EstadoSolicitud WHERE Codigo = N'PENDIENTE_ASIGNACION')
        INSERT INTO dbo.EstadoSolicitud (Codigo, Nombre, OrdenVisual, EsFinal)
        VALUES (N'PENDIENTE_ASIGNACION', N'Pendiente de asignación', 2, 0);

    IF NOT EXISTS (SELECT 1 FROM dbo.TipoEvidencia WHERE Codigo = N'VISTA_GENERAL')
        INSERT INTO dbo.TipoEvidencia (Codigo, Nombre) VALUES (N'VISTA_GENERAL', N'Vista general');
    IF NOT EXISTS (SELECT 1 FROM dbo.TipoEvidencia WHERE Codigo = N'ETIQUETA')
        INSERT INTO dbo.TipoEvidencia (Codigo, Nombre) VALUES (N'ETIQUETA', N'Etiqueta');
    IF NOT EXISTS (SELECT 1 FROM dbo.TipoEvidencia WHERE Codigo = N'SKU')
        INSERT INTO dbo.TipoEvidencia (Codigo, Nombre) VALUES (N'SKU', N'SKU');
    IF NOT EXISTS (SELECT 1 FROM dbo.TipoEvidencia WHERE Codigo = N'NUMERO_SERIE')
        INSERT INTO dbo.TipoEvidencia (Codigo, Nombre) VALUES (N'NUMERO_SERIE', N'Número de serie');
    IF NOT EXISTS (SELECT 1 FROM dbo.TipoEvidencia WHERE Codigo = N'CAJA')
        INSERT INTO dbo.TipoEvidencia (Codigo, Nombre) VALUES (N'CAJA', N'Caja');
    IF NOT EXISTS (SELECT 1 FROM dbo.TipoEvidencia WHERE Codigo = N'ACCESORIOS')
        INSERT INTO dbo.TipoEvidencia (Codigo, Nombre) VALUES (N'ACCESORIOS', N'Accesorios');
    IF NOT EXISTS (SELECT 1 FROM dbo.TipoEvidencia WHERE Codigo = N'DEFECTOS')
        INSERT INTO dbo.TipoEvidencia (Codigo, Nombre) VALUES (N'DEFECTOS', N'Defectos visibles');

    IF NOT EXISTS (SELECT 1 FROM dbo.Categoria WHERE Nombre = N'Sneakers')
        INSERT INTO dbo.Categoria (Nombre, Descripcion, Activa) VALUES (N'Sneakers', N'Calzado deportivo y urbano', 1);
    IF NOT EXISTS (SELECT 1 FROM dbo.Categoria WHERE Nombre = N'Ropa')
        INSERT INTO dbo.Categoria (Nombre, Descripcion, Activa) VALUES (N'Ropa', N'Prendas y accesorios textiles', 1);
    IF NOT EXISTS (SELECT 1 FROM dbo.Categoria WHERE Nombre = N'Coleccionables')
        INSERT INTO dbo.Categoria (Nombre, Descripcion, Activa) VALUES (N'Coleccionables', N'Artículos de colección', 1);
    IF NOT EXISTS (SELECT 1 FROM dbo.Categoria WHERE Nombre = N'TCG')
        INSERT INTO dbo.Categoria (Nombre, Descripcion, Activa) VALUES (N'TCG', N'Juegos de cartas coleccionables', 1);
    IF NOT EXISTS (SELECT 1 FROM dbo.Categoria WHERE Nombre = N'Consolas')
        INSERT INTO dbo.Categoria (Nombre, Descripcion, Activa) VALUES (N'Consolas', N'Consolas y videojuegos', 1);
    IF NOT EXISTS (SELECT 1 FROM dbo.Categoria WHERE Nombre = N'Teléfonos')
        INSERT INTO dbo.Categoria (Nombre, Descripcion, Activa) VALUES (N'Teléfonos', N'Teléfonos y dispositivos móviles', 1);
    IF NOT EXISTS (SELECT 1 FROM dbo.Categoria WHERE Nombre = N'Laptops')
        INSERT INTO dbo.Categoria (Nombre, Descripcion, Activa) VALUES (N'Laptops', N'Computadoras portátiles', 1);
    IF NOT EXISTS (SELECT 1 FROM dbo.Categoria WHERE Nombre = N'Computadoras y componentes')
        INSERT INTO dbo.Categoria (Nombre, Descripcion, Activa) VALUES (N'Computadoras y componentes', N'Equipos y componentes de cómputo', 1);

    INSERT INTO dbo.CategoriaCampoRequisito (IdCategoria, Campo, Obligatorio, LongitudMaxima, Orden)
    SELECT c.IdCategoria, N'NOMBRE', 1, 150, 1
    FROM dbo.Categoria c
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.CategoriaCampoRequisito r
        WHERE r.IdCategoria = c.IdCategoria AND r.Campo = N'NOMBRE');

    INSERT INTO dbo.CategoriaEvidenciaRequisito
        (IdCategoria, IdTipoEvidencia, Obligatorio, CantidadMinima, CantidadMaxima, Orden)
    SELECT c.IdCategoria, t.IdTipoEvidencia,
        CASE WHEN t.Codigo = N'VISTA_GENERAL' THEN 1 ELSE 0 END,
        CASE WHEN t.Codigo = N'VISTA_GENERAL' THEN 1 ELSE 0 END,
        3,
        CASE t.Codigo
            WHEN N'VISTA_GENERAL' THEN 1
            WHEN N'ETIQUETA' THEN 2
            WHEN N'SKU' THEN 3
            WHEN N'NUMERO_SERIE' THEN 4
            WHEN N'CAJA' THEN 5
            WHEN N'ACCESORIOS' THEN 6
            ELSE 7
        END
    FROM dbo.Categoria c
    CROSS JOIN dbo.TipoEvidencia t
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.CategoriaEvidenciaRequisito r
        WHERE r.IdCategoria = c.IdCategoria AND r.IdTipoEvidencia = t.IdTipoEvidencia);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108214534_AddSprint1Solicitudes'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261108214534_AddSprint1Solicitudes', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111190603_SeedRolesOperativos'
)
BEGIN
    IF EXISTS (
        SELECT 1 FROM dbo.Rol
        WHERE Nombre = N'Solicitante' AND Activo = 0)
        THROW 51000, N'El rol Solicitante existe pero está inactivo; requiere revisión manual antes de habilitar el registro.', 1;

    IF NOT EXISTS (SELECT 1 FROM dbo.Rol WHERE Nombre = N'Solicitante')
        INSERT INTO dbo.Rol (Nombre, Descripcion, Activo)
        VALUES (N'Solicitante', N'Usuario que registra productos y solicita evaluaciones.', 1);

    IF NOT EXISTS (SELECT 1 FROM dbo.Rol WHERE Nombre = N'Especialista')
        INSERT INTO dbo.Rol (Nombre, Descripcion, Activo)
        VALUES (N'Especialista', N'Especialista acreditado que evalúa solicitudes.', 1);

    IF NOT EXISTS (SELECT 1 FROM dbo.Rol WHERE Nombre = N'Administrador')
        INSERT INTO dbo.Rol (Nombre, Descripcion, Activo)
        VALUES (N'Administrador', N'Administrador autorizado de GradeX.', 1);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111190603_SeedRolesOperativos'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261111190603_SeedRolesOperativos', N'10.0.13');
END;

COMMIT;
GO

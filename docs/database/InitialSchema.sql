IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
CREATE TABLE [Categoria] (
    [IdCategoria] int NOT NULL IDENTITY,
    [Nombre] nvarchar(100) NOT NULL,
    [Descripcion] nvarchar(300) NULL,
    [Activa] bit NOT NULL CONSTRAINT [DF_Categoria_Activa] DEFAULT CAST(1 AS bit),
    CONSTRAINT [PK_Categoria] PRIMARY KEY ([IdCategoria])
);

CREATE TABLE [EstadoCertificado] (
    [IdEstadoCertificado] int NOT NULL IDENTITY,
    [Codigo] nvarchar(30) NOT NULL,
    [Nombre] nvarchar(80) NOT NULL,
    CONSTRAINT [PK_EstadoCertificado] PRIMARY KEY ([IdEstadoCertificado])
);

CREATE TABLE [EstadoSolicitud] (
    [IdEstadoSolicitud] int NOT NULL IDENTITY,
    [Codigo] nvarchar(40) NOT NULL,
    [Nombre] nvarchar(100) NOT NULL,
    [OrdenVisual] smallint NOT NULL,
    [EsFinal] bit NOT NULL,
    CONSTRAINT [PK_EstadoSolicitud] PRIMARY KEY ([IdEstadoSolicitud])
);

CREATE TABLE [Marca] (
    [IdMarca] int NOT NULL IDENTITY,
    [Nombre] nvarchar(100) NOT NULL,
    [Activa] bit NOT NULL CONSTRAINT [DF_Marca_Activa] DEFAULT CAST(1 AS bit),
    CONSTRAINT [PK_Marca] PRIMARY KEY ([IdMarca])
);

CREATE TABLE [Rol] (
    [IdRol] int NOT NULL IDENTITY,
    [Nombre] nvarchar(50) NOT NULL,
    [Descripcion] nvarchar(200) NULL,
    [Activo] bit NOT NULL CONSTRAINT [DF_Rol_Activo] DEFAULT CAST(1 AS bit),
    CONSTRAINT [PK_Rol] PRIMARY KEY ([IdRol])
);

CREATE TABLE [TipoEvidencia] (
    [IdTipoEvidencia] int NOT NULL IDENTITY,
    [Codigo] nvarchar(40) NOT NULL,
    [Nombre] nvarchar(100) NOT NULL,
    CONSTRAINT [PK_TipoEvidencia] PRIMARY KEY ([IdTipoEvidencia])
);

CREATE TABLE [TipoNotificacion] (
    [IdTipoNotificacion] int NOT NULL IDENTITY,
    [Codigo] nvarchar(50) NOT NULL,
    [Nombre] nvarchar(120) NOT NULL,
    CONSTRAINT [PK_TipoNotificacion] PRIMARY KEY ([IdTipoNotificacion])
);

CREATE TABLE [Usuario] (
    [IdUsuario] int NOT NULL IDENTITY,
    [NombreCompleto] nvarchar(150) NOT NULL,
    [Correo] nvarchar(254) NOT NULL,
    [PasswordHash] nvarchar(255) NOT NULL,
    [Activo] bit NOT NULL CONSTRAINT [DF_Usuario_Activo] DEFAULT CAST(1 AS bit),
    [FechaCreacion] datetime2(0) NOT NULL CONSTRAINT [DF_Usuario_FechaCreacion] DEFAULT ((sysdatetime())),
    [FechaActualizacion] datetime2(0) NULL,
    CONSTRAINT [PK_Usuario] PRIMARY KEY ([IdUsuario])
);

CREATE TABLE [CriterioEvaluacion] (
    [IdCriterio] int NOT NULL IDENTITY,
    [IdCategoria] int NOT NULL,
    [Nombre] nvarchar(150) NOT NULL,
    [Descripcion] nvarchar(500) NULL,
    [TipoDato] nvarchar(20) NOT NULL,
    [Obligatorio] bit NOT NULL CONSTRAINT [DF_Criterio_Obligatorio] DEFAULT CAST(1 AS bit),
    [Orden] smallint NOT NULL CONSTRAINT [DF_Criterio_Orden] DEFAULT CAST(1 AS smallint),
    [Activo] bit NOT NULL CONSTRAINT [DF_Criterio_Activo] DEFAULT CAST(1 AS bit),
    CONSTRAINT [PK_CriterioEvaluacion] PRIMARY KEY ([IdCriterio]),
    CONSTRAINT [FK_Criterio_Categoria] FOREIGN KEY ([IdCategoria]) REFERENCES [Categoria] ([IdCategoria])
);

CREATE TABLE [CategoriaMarca] (
    [IdCategoria] int NOT NULL,
    [IdMarca] int NOT NULL,
    [Activa] bit NOT NULL CONSTRAINT [DF_CategoriaMarca_Activa] DEFAULT CAST(1 AS bit),
    CONSTRAINT [PK_CategoriaMarca] PRIMARY KEY ([IdCategoria], [IdMarca]),
    CONSTRAINT [FK_CategoriaMarca_Categoria] FOREIGN KEY ([IdCategoria]) REFERENCES [Categoria] ([IdCategoria]),
    CONSTRAINT [FK_CategoriaMarca_Marca] FOREIGN KEY ([IdMarca]) REFERENCES [Marca] ([IdMarca])
);

CREATE TABLE [Especialista] (
    [IdEspecialista] int NOT NULL IDENTITY,
    [IdUsuario] int NOT NULL,
    [AniosExperiencia] smallint NULL,
    [Estado] nvarchar(20) NOT NULL CONSTRAINT [DF_Especialista_Estado] DEFAULT N'PENDIENTE',
    [Biografia] nvarchar(600) NULL,
    [FechaAlta] datetime2(0) NOT NULL CONSTRAINT [DF_Especialista_FechaAlta] DEFAULT ((sysdatetime())),
    CONSTRAINT [PK_Especialista] PRIMARY KEY ([IdEspecialista]),
    CONSTRAINT [FK_Especialista_Usuario] FOREIGN KEY ([IdUsuario]) REFERENCES [Usuario] ([IdUsuario])
);

CREATE TABLE [UsuarioRol] (
    [IdUsuario] int NOT NULL,
    [IdRol] int NOT NULL,
    [FechaAsignacion] datetime2(0) NOT NULL CONSTRAINT [DF_UsuarioRol_Fecha] DEFAULT ((sysdatetime())),
    CONSTRAINT [PK_UsuarioRol] PRIMARY KEY ([IdUsuario], [IdRol]),
    CONSTRAINT [FK_UsuarioRol_Rol] FOREIGN KEY ([IdRol]) REFERENCES [Rol] ([IdRol]),
    CONSTRAINT [FK_UsuarioRol_Usuario] FOREIGN KEY ([IdUsuario]) REFERENCES [Usuario] ([IdUsuario])
);

CREATE TABLE [Producto] (
    [IdProducto] int NOT NULL IDENTITY,
    [IdPropietario] int NOT NULL,
    [IdCategoria] int NOT NULL,
    [IdMarca] int NULL,
    [Nombre] nvarchar(150) NOT NULL,
    [Modelo] nvarchar(150) NULL,
    [SKU] nvarchar(80) NULL,
    [NumeroSerie] nvarchar(120) NULL,
    [IMEI] nvarchar(30) NULL,
    [Accesorios] nvarchar(500) NULL,
    [Descripcion] nvarchar(1000) NULL,
    [FechaRegistro] datetime2(0) NOT NULL CONSTRAINT [DF_Producto_FechaRegistro] DEFAULT ((sysdatetime())),
    [Activo] bit NOT NULL CONSTRAINT [DF_Producto_Activo] DEFAULT CAST(1 AS bit),
    CONSTRAINT [PK_Producto] PRIMARY KEY ([IdProducto]),
    CONSTRAINT [FK_Producto_Categoria] FOREIGN KEY ([IdCategoria]) REFERENCES [Categoria] ([IdCategoria]),
    CONSTRAINT [FK_Producto_CategoriaMarca] FOREIGN KEY ([IdCategoria], [IdMarca]) REFERENCES [CategoriaMarca] ([IdCategoria], [IdMarca]),
    CONSTRAINT [FK_Producto_Marca] FOREIGN KEY ([IdMarca]) REFERENCES [Marca] ([IdMarca]),
    CONSTRAINT [FK_Producto_Propietario] FOREIGN KEY ([IdPropietario]) REFERENCES [Usuario] ([IdUsuario])
);

CREATE TABLE [EspecialidadEspecialista] (
    [IdEspecialidad] int NOT NULL IDENTITY,
    [IdEspecialista] int NOT NULL,
    [IdCategoria] int NOT NULL,
    [IdMarca] int NULL,
    [Nivel] nvarchar(20) NOT NULL CONSTRAINT [DF_Especialidad_Nivel] DEFAULT N'GENERAL',
    [Activa] bit NOT NULL CONSTRAINT [DF_Especialidad_Activa] DEFAULT CAST(1 AS bit),
    [FechaRegistro] datetime2(0) NOT NULL CONSTRAINT [DF_Especialidad_Fecha] DEFAULT ((sysdatetime())),
    CONSTRAINT [PK_EspecialidadEspecialista] PRIMARY KEY ([IdEspecialidad]),
    CONSTRAINT [FK_Especialidad_Categoria] FOREIGN KEY ([IdCategoria]) REFERENCES [Categoria] ([IdCategoria]),
    CONSTRAINT [FK_Especialidad_CategoriaMarca] FOREIGN KEY ([IdCategoria], [IdMarca]) REFERENCES [CategoriaMarca] ([IdCategoria], [IdMarca]),
    CONSTRAINT [FK_Especialidad_Especialista] FOREIGN KEY ([IdEspecialista]) REFERENCES [Especialista] ([IdEspecialista])
);

CREATE TABLE [SolicitudEvaluacion] (
    [IdSolicitud] int NOT NULL IDENTITY,
    [Folio] nvarchar(30) NOT NULL,
    [IdProducto] int NOT NULL,
    [IdSolicitante] int NOT NULL,
    [IdEstadoSolicitud] int NOT NULL,
    [FechaSolicitud] datetime2(0) NOT NULL CONSTRAINT [DF_Solicitud_FechaSolicitud] DEFAULT ((sysdatetime())),
    [FechaUltimoCambio] datetime2(0) NOT NULL CONSTRAINT [DF_Solicitud_FechaCambio] DEFAULT ((sysdatetime())),
    [ObservacionesSolicitante] nvarchar(1000) NULL,
    CONSTRAINT [PK_SolicitudEvaluacion] PRIMARY KEY ([IdSolicitud]),
    CONSTRAINT [FK_Solicitud_Estado] FOREIGN KEY ([IdEstadoSolicitud]) REFERENCES [EstadoSolicitud] ([IdEstadoSolicitud]),
    CONSTRAINT [FK_Solicitud_Producto] FOREIGN KEY ([IdProducto]) REFERENCES [Producto] ([IdProducto]),
    CONSTRAINT [FK_Solicitud_Solicitante] FOREIGN KEY ([IdSolicitante]) REFERENCES [Usuario] ([IdUsuario])
);

CREATE TABLE [AsignacionEspecialista] (
    [IdAsignacion] int NOT NULL IDENTITY,
    [IdSolicitud] int NOT NULL,
    [IdEspecialista] int NOT NULL,
    [FechaAsignacion] datetime2(0) NOT NULL CONSTRAINT [DF_Asignacion_Fecha] DEFAULT ((sysdatetime())),
    [FechaFin] datetime2(0) NULL,
    [Activa] bit NOT NULL CONSTRAINT [DF_Asignacion_Activa] DEFAULT CAST(1 AS bit),
    [MotivoFin] nvarchar(300) NULL,
    CONSTRAINT [PK_AsignacionEspecialista] PRIMARY KEY ([IdAsignacion]),
    CONSTRAINT [FK_Asignacion_Especialista] FOREIGN KEY ([IdEspecialista]) REFERENCES [Especialista] ([IdEspecialista]),
    CONSTRAINT [FK_Asignacion_Solicitud] FOREIGN KEY ([IdSolicitud]) REFERENCES [SolicitudEvaluacion] ([IdSolicitud])
);

CREATE TABLE [Evaluacion] (
    [IdEvaluacion] int NOT NULL IDENTITY,
    [IdSolicitud] int NOT NULL,
    [IdEspecialista] int NOT NULL,
    [Dictamen] nvarchar(20) NULL,
    [ConditionGrade] decimal(4,2) NULL,
    [TechnicalHealth] decimal(5,2) NULL,
    [ObservacionesGenerales] nvarchar(1500) NULL,
    [FechaInicio] datetime2(0) NOT NULL CONSTRAINT [DF_Evaluacion_FechaInicio] DEFAULT ((sysdatetime())),
    [FechaFin] datetime2(0) NULL,
    CONSTRAINT [PK_Evaluacion] PRIMARY KEY ([IdEvaluacion]),
    CONSTRAINT [FK_Evaluacion_Especialista] FOREIGN KEY ([IdEspecialista]) REFERENCES [Especialista] ([IdEspecialista]),
    CONSTRAINT [FK_Evaluacion_Solicitud] FOREIGN KEY ([IdSolicitud]) REFERENCES [SolicitudEvaluacion] ([IdSolicitud])
);

CREATE TABLE [Evidencia] (
    [IdEvidencia] int NOT NULL IDENTITY,
    [IdSolicitud] int NOT NULL,
    [IdTipoEvidencia] int NOT NULL,
    [UrlArchivo] nvarchar(1000) NOT NULL,
    [Descripcion] nvarchar(500) NULL,
    [FechaCarga] datetime2(0) NOT NULL CONSTRAINT [DF_Evidencia_FechaCarga] DEFAULT ((sysdatetime())),
    [Activa] bit NOT NULL CONSTRAINT [DF_Evidencia_Activa] DEFAULT CAST(1 AS bit),
    CONSTRAINT [PK_Evidencia] PRIMARY KEY ([IdEvidencia]),
    CONSTRAINT [FK_Evidencia_Solicitud] FOREIGN KEY ([IdSolicitud]) REFERENCES [SolicitudEvaluacion] ([IdSolicitud]),
    CONSTRAINT [FK_Evidencia_Tipo] FOREIGN KEY ([IdTipoEvidencia]) REFERENCES [TipoEvidencia] ([IdTipoEvidencia])
);

CREATE TABLE [Notificacion] (
    [IdNotificacion] bigint NOT NULL IDENTITY,
    [IdUsuario] int NOT NULL,
    [IdSolicitud] int NULL,
    [IdTipoNotificacion] int NOT NULL,
    [Titulo] nvarchar(150) NOT NULL,
    [Mensaje] nvarchar(600) NOT NULL,
    [Leida] bit NOT NULL,
    [FechaCreacion] datetime2(0) NOT NULL CONSTRAINT [DF_Notificacion_Fecha] DEFAULT ((sysdatetime())),
    [FechaLectura] datetime2(0) NULL,
    CONSTRAINT [PK_Notificacion] PRIMARY KEY ([IdNotificacion]),
    CONSTRAINT [FK_Notificacion_Solicitud] FOREIGN KEY ([IdSolicitud]) REFERENCES [SolicitudEvaluacion] ([IdSolicitud]),
    CONSTRAINT [FK_Notificacion_Tipo] FOREIGN KEY ([IdTipoNotificacion]) REFERENCES [TipoNotificacion] ([IdTipoNotificacion]),
    CONSTRAINT [FK_Notificacion_Usuario] FOREIGN KEY ([IdUsuario]) REFERENCES [Usuario] ([IdUsuario])
);

CREATE TABLE [Certificado] (
    [IdCertificado] int NOT NULL IDENTITY,
    [IdEvaluacion] int NOT NULL,
    [IdEstadoCertificado] int NOT NULL,
    [NumeroCertificado] nvarchar(40) NOT NULL,
    [QrToken] uniqueidentifier NOT NULL CONSTRAINT [DF_Certificado_QrToken] DEFAULT ((newid())),
    [FechaEmision] datetime2(0) NOT NULL CONSTRAINT [DF_Certificado_FechaEmision] DEFAULT ((sysdatetime())),
    [FechaVencimiento] datetime2(0) NULL,
    CONSTRAINT [PK_Certificado] PRIMARY KEY ([IdCertificado]),
    CONSTRAINT [FK_Certificado_Estado] FOREIGN KEY ([IdEstadoCertificado]) REFERENCES [EstadoCertificado] ([IdEstadoCertificado]),
    CONSTRAINT [FK_Certificado_Evaluacion] FOREIGN KEY ([IdEvaluacion]) REFERENCES [Evaluacion] ([IdEvaluacion])
);

CREATE TABLE [ResultadoCriterio] (
    [IdEvaluacion] int NOT NULL,
    [IdCriterio] int NOT NULL,
    [ValorTexto] nvarchar(1000) NULL,
    [ValorNumerico] decimal(12,2) NULL,
    [ValorBooleano] bit NULL,
    [Observacion] nvarchar(1000) NULL,
    CONSTRAINT [PK_ResultadoCriterio] PRIMARY KEY ([IdEvaluacion], [IdCriterio]),
    CONSTRAINT [FK_ResultadoCriterio_Criterio] FOREIGN KEY ([IdCriterio]) REFERENCES [CriterioEvaluacion] ([IdCriterio]),
    CONSTRAINT [FK_ResultadoCriterio_Evaluacion] FOREIGN KEY ([IdEvaluacion]) REFERENCES [Evaluacion] ([IdEvaluacion])
);

CREATE INDEX [IX_Asignacion_Especialista] ON [AsignacionEspecialista] ([IdEspecialista], [Activa]);

CREATE UNIQUE INDEX [UX_Asignacion_SolicitudActiva] ON [AsignacionEspecialista] ([IdSolicitud]) WHERE ([Activa]=(1));

CREATE UNIQUE INDEX [UQ_Categoria_Nombre] ON [Categoria] ([Nombre]);

CREATE INDEX [IX_CategoriaMarca_IdMarca] ON [CategoriaMarca] ([IdMarca]);

CREATE INDEX [IX_Certificado_IdEstadoCertificado] ON [Certificado] ([IdEstadoCertificado]);

CREATE UNIQUE INDEX [UQ_Certificado_IdEvaluacion] ON [Certificado] ([IdEvaluacion]);

CREATE UNIQUE INDEX [UQ_Certificado_Numero] ON [Certificado] ([NumeroCertificado]);

CREATE UNIQUE INDEX [UQ_Certificado_QrToken] ON [Certificado] ([QrToken]);

CREATE INDEX [IX_Criterio_Categoria] ON [CriterioEvaluacion] ([IdCategoria], [Activo]);

CREATE UNIQUE INDEX [UQ_Criterio_CategoriaNombre] ON [CriterioEvaluacion] ([IdCategoria], [Nombre]);

CREATE INDEX [IX_EspecialidadEspecialista_IdCategoria_IdMarca] ON [EspecialidadEspecialista] ([IdCategoria], [IdMarca]);

CREATE UNIQUE INDEX [UX_Especialidad_EspecialistaCategoriaMarca] ON [EspecialidadEspecialista] ([IdEspecialista], [IdCategoria], [IdMarca]) WHERE [IdMarca] IS NOT NULL;

CREATE UNIQUE INDEX [UQ_Especialista_IdUsuario] ON [Especialista] ([IdUsuario]);

CREATE UNIQUE INDEX [UQ_EstadoCertificado_Codigo] ON [EstadoCertificado] ([Codigo]);

CREATE UNIQUE INDEX [UQ_EstadoSolicitud_Codigo] ON [EstadoSolicitud] ([Codigo]);

CREATE UNIQUE INDEX [UQ_EstadoSolicitud_Nombre] ON [EstadoSolicitud] ([Nombre]);

CREATE INDEX [IX_Evaluacion_IdEspecialista] ON [Evaluacion] ([IdEspecialista]);

CREATE UNIQUE INDEX [UQ_Evaluacion_IdSolicitud] ON [Evaluacion] ([IdSolicitud]);

CREATE INDEX [IX_Evidencia_IdTipoEvidencia] ON [Evidencia] ([IdTipoEvidencia]);

CREATE INDEX [IX_Evidencia_Solicitud] ON [Evidencia] ([IdSolicitud]);

CREATE UNIQUE INDEX [UQ_Marca_Nombre] ON [Marca] ([Nombre]);

CREATE INDEX [IX_Notificacion_IdSolicitud] ON [Notificacion] ([IdSolicitud]);

CREATE INDEX [IX_Notificacion_IdTipoNotificacion] ON [Notificacion] ([IdTipoNotificacion]);

CREATE INDEX [IX_Notificacion_UsuarioLeida] ON [Notificacion] ([IdUsuario], [Leida], [FechaCreacion] DESC);

CREATE INDEX [IX_Producto_CategoriaMarca] ON [Producto] ([IdCategoria], [IdMarca]);

CREATE INDEX [IX_Producto_IdMarca] ON [Producto] ([IdMarca]);

CREATE INDEX [IX_Producto_Propietario] ON [Producto] ([IdPropietario]);

CREATE INDEX [IX_ResultadoCriterio_IdCriterio] ON [ResultadoCriterio] ([IdCriterio]);

CREATE UNIQUE INDEX [UQ_Rol_Nombre] ON [Rol] ([Nombre]);

CREATE INDEX [IX_Solicitud_Estado] ON [SolicitudEvaluacion] ([IdEstadoSolicitud]);

CREATE INDEX [IX_Solicitud_Producto] ON [SolicitudEvaluacion] ([IdProducto]);

CREATE INDEX [IX_Solicitud_Solicitante] ON [SolicitudEvaluacion] ([IdSolicitante]);

CREATE UNIQUE INDEX [UQ_SolicitudEvaluacion_Folio] ON [SolicitudEvaluacion] ([Folio]);

CREATE UNIQUE INDEX [UQ_TipoEvidencia_Codigo] ON [TipoEvidencia] ([Codigo]);

CREATE UNIQUE INDEX [UQ_TipoNotificacion_Codigo] ON [TipoNotificacion] ([Codigo]);

CREATE UNIQUE INDEX [UQ_Usuario_Correo] ON [Usuario] ([Correo]);

CREATE INDEX [IX_UsuarioRol_IdRol] ON [UsuarioRol] ([IdRol]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261019003256_InitialSchema', N'10.0.13');

COMMIT;
GO


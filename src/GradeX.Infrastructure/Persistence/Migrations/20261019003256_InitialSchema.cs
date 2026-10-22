using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GradeX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categoria",
                columns: table => new
                {
                    IdCategoria = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Activa = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                        .Annotation("Relational:DefaultConstraintName", "DF_Categoria_Activa")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categoria", x => x.IdCategoria);
                });

            migrationBuilder.CreateTable(
                name: "EstadoCertificado",
                columns: table => new
                {
                    IdEstadoCertificado = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EstadoCertificado", x => x.IdEstadoCertificado);
                });

            migrationBuilder.CreateTable(
                name: "EstadoSolicitud",
                columns: table => new
                {
                    IdEstadoSolicitud = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OrdenVisual = table.Column<short>(type: "smallint", nullable: false),
                    EsFinal = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EstadoSolicitud", x => x.IdEstadoSolicitud);
                });

            migrationBuilder.CreateTable(
                name: "Marca",
                columns: table => new
                {
                    IdMarca = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Activa = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                        .Annotation("Relational:DefaultConstraintName", "DF_Marca_Activa")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Marca", x => x.IdMarca);
                });

            migrationBuilder.CreateTable(
                name: "Rol",
                columns: table => new
                {
                    IdRol = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                        .Annotation("Relational:DefaultConstraintName", "DF_Rol_Activo")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rol", x => x.IdRol);
                });

            migrationBuilder.CreateTable(
                name: "TipoEvidencia",
                columns: table => new
                {
                    IdTipoEvidencia = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TipoEvidencia", x => x.IdTipoEvidencia);
                });

            migrationBuilder.CreateTable(
                name: "TipoNotificacion",
                columns: table => new
                {
                    IdTipoNotificacion = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TipoNotificacion", x => x.IdTipoNotificacion);
                });

            migrationBuilder.CreateTable(
                name: "Usuario",
                columns: table => new
                {
                    IdUsuario = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NombreCompleto = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Correo = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                        .Annotation("Relational:DefaultConstraintName", "DF_Usuario_Activo"),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "(sysdatetime())")
                        .Annotation("Relational:DefaultConstraintName", "DF_Usuario_FechaCreacion"),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuario", x => x.IdUsuario);
                });

            migrationBuilder.CreateTable(
                name: "CriterioEvaluacion",
                columns: table => new
                {
                    IdCriterio = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdCategoria = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TipoDato = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Obligatorio = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                        .Annotation("Relational:DefaultConstraintName", "DF_Criterio_Obligatorio"),
                    Orden = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1)
                        .Annotation("Relational:DefaultConstraintName", "DF_Criterio_Orden"),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                        .Annotation("Relational:DefaultConstraintName", "DF_Criterio_Activo")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CriterioEvaluacion", x => x.IdCriterio);
                    table.ForeignKey(
                        name: "FK_Criterio_Categoria",
                        column: x => x.IdCategoria,
                        principalTable: "Categoria",
                        principalColumn: "IdCategoria");
                });

            migrationBuilder.CreateTable(
                name: "CategoriaMarca",
                columns: table => new
                {
                    IdCategoria = table.Column<int>(type: "int", nullable: false),
                    IdMarca = table.Column<int>(type: "int", nullable: false),
                    Activa = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                        .Annotation("Relational:DefaultConstraintName", "DF_CategoriaMarca_Activa")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoriaMarca", x => new { x.IdCategoria, x.IdMarca });
                    table.ForeignKey(
                        name: "FK_CategoriaMarca_Categoria",
                        column: x => x.IdCategoria,
                        principalTable: "Categoria",
                        principalColumn: "IdCategoria");
                    table.ForeignKey(
                        name: "FK_CategoriaMarca_Marca",
                        column: x => x.IdMarca,
                        principalTable: "Marca",
                        principalColumn: "IdMarca");
                });

            migrationBuilder.CreateTable(
                name: "Especialista",
                columns: table => new
                {
                    IdEspecialista = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdUsuario = table.Column<int>(type: "int", nullable: false),
                    AniosExperiencia = table.Column<short>(type: "smallint", nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "PENDIENTE")
                        .Annotation("Relational:DefaultConstraintName", "DF_Especialista_Estado"),
                    Biografia = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                    FechaAlta = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "(sysdatetime())")
                        .Annotation("Relational:DefaultConstraintName", "DF_Especialista_FechaAlta")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Especialista", x => x.IdEspecialista);
                    table.ForeignKey(
                        name: "FK_Especialista_Usuario",
                        column: x => x.IdUsuario,
                        principalTable: "Usuario",
                        principalColumn: "IdUsuario");
                });

            migrationBuilder.CreateTable(
                name: "UsuarioRol",
                columns: table => new
                {
                    IdUsuario = table.Column<int>(type: "int", nullable: false),
                    IdRol = table.Column<int>(type: "int", nullable: false),
                    FechaAsignacion = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "(sysdatetime())")
                        .Annotation("Relational:DefaultConstraintName", "DF_UsuarioRol_Fecha")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuarioRol", x => new { x.IdUsuario, x.IdRol });
                    table.ForeignKey(
                        name: "FK_UsuarioRol_Rol",
                        column: x => x.IdRol,
                        principalTable: "Rol",
                        principalColumn: "IdRol");
                    table.ForeignKey(
                        name: "FK_UsuarioRol_Usuario",
                        column: x => x.IdUsuario,
                        principalTable: "Usuario",
                        principalColumn: "IdUsuario");
                });

            migrationBuilder.CreateTable(
                name: "Producto",
                columns: table => new
                {
                    IdProducto = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdPropietario = table.Column<int>(type: "int", nullable: false),
                    IdCategoria = table.Column<int>(type: "int", nullable: false),
                    IdMarca = table.Column<int>(type: "int", nullable: true),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Modelo = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    SKU = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    NumeroSerie = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    IMEI = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Accesorios = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Descripcion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "(sysdatetime())")
                        .Annotation("Relational:DefaultConstraintName", "DF_Producto_FechaRegistro"),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                        .Annotation("Relational:DefaultConstraintName", "DF_Producto_Activo")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Producto", x => x.IdProducto);
                    table.ForeignKey(
                        name: "FK_Producto_Categoria",
                        column: x => x.IdCategoria,
                        principalTable: "Categoria",
                        principalColumn: "IdCategoria");
                    table.ForeignKey(
                        name: "FK_Producto_CategoriaMarca",
                        columns: x => new { x.IdCategoria, x.IdMarca },
                        principalTable: "CategoriaMarca",
                        principalColumns: new[] { "IdCategoria", "IdMarca" });
                    table.ForeignKey(
                        name: "FK_Producto_Marca",
                        column: x => x.IdMarca,
                        principalTable: "Marca",
                        principalColumn: "IdMarca");
                    table.ForeignKey(
                        name: "FK_Producto_Propietario",
                        column: x => x.IdPropietario,
                        principalTable: "Usuario",
                        principalColumn: "IdUsuario");
                });

            migrationBuilder.CreateTable(
                name: "EspecialidadEspecialista",
                columns: table => new
                {
                    IdEspecialidad = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdEspecialista = table.Column<int>(type: "int", nullable: false),
                    IdCategoria = table.Column<int>(type: "int", nullable: false),
                    IdMarca = table.Column<int>(type: "int", nullable: true),
                    Nivel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "GENERAL")
                        .Annotation("Relational:DefaultConstraintName", "DF_Especialidad_Nivel"),
                    Activa = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                        .Annotation("Relational:DefaultConstraintName", "DF_Especialidad_Activa"),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "(sysdatetime())")
                        .Annotation("Relational:DefaultConstraintName", "DF_Especialidad_Fecha")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EspecialidadEspecialista", x => x.IdEspecialidad);
                    table.ForeignKey(
                        name: "FK_Especialidad_Categoria",
                        column: x => x.IdCategoria,
                        principalTable: "Categoria",
                        principalColumn: "IdCategoria");
                    table.ForeignKey(
                        name: "FK_Especialidad_CategoriaMarca",
                        columns: x => new { x.IdCategoria, x.IdMarca },
                        principalTable: "CategoriaMarca",
                        principalColumns: new[] { "IdCategoria", "IdMarca" });
                    table.ForeignKey(
                        name: "FK_Especialidad_Especialista",
                        column: x => x.IdEspecialista,
                        principalTable: "Especialista",
                        principalColumn: "IdEspecialista");
                });

            migrationBuilder.CreateTable(
                name: "SolicitudEvaluacion",
                columns: table => new
                {
                    IdSolicitud = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Folio = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IdProducto = table.Column<int>(type: "int", nullable: false),
                    IdSolicitante = table.Column<int>(type: "int", nullable: false),
                    IdEstadoSolicitud = table.Column<int>(type: "int", nullable: false),
                    FechaSolicitud = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "(sysdatetime())")
                        .Annotation("Relational:DefaultConstraintName", "DF_Solicitud_FechaSolicitud"),
                    FechaUltimoCambio = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "(sysdatetime())")
                        .Annotation("Relational:DefaultConstraintName", "DF_Solicitud_FechaCambio"),
                    ObservacionesSolicitante = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolicitudEvaluacion", x => x.IdSolicitud);
                    table.ForeignKey(
                        name: "FK_Solicitud_Estado",
                        column: x => x.IdEstadoSolicitud,
                        principalTable: "EstadoSolicitud",
                        principalColumn: "IdEstadoSolicitud");
                    table.ForeignKey(
                        name: "FK_Solicitud_Producto",
                        column: x => x.IdProducto,
                        principalTable: "Producto",
                        principalColumn: "IdProducto");
                    table.ForeignKey(
                        name: "FK_Solicitud_Solicitante",
                        column: x => x.IdSolicitante,
                        principalTable: "Usuario",
                        principalColumn: "IdUsuario");
                });

            migrationBuilder.CreateTable(
                name: "AsignacionEspecialista",
                columns: table => new
                {
                    IdAsignacion = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdSolicitud = table.Column<int>(type: "int", nullable: false),
                    IdEspecialista = table.Column<int>(type: "int", nullable: false),
                    FechaAsignacion = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "(sysdatetime())")
                        .Annotation("Relational:DefaultConstraintName", "DF_Asignacion_Fecha"),
                    FechaFin = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true),
                    Activa = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                        .Annotation("Relational:DefaultConstraintName", "DF_Asignacion_Activa"),
                    MotivoFin = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AsignacionEspecialista", x => x.IdAsignacion);
                    table.ForeignKey(
                        name: "FK_Asignacion_Especialista",
                        column: x => x.IdEspecialista,
                        principalTable: "Especialista",
                        principalColumn: "IdEspecialista");
                    table.ForeignKey(
                        name: "FK_Asignacion_Solicitud",
                        column: x => x.IdSolicitud,
                        principalTable: "SolicitudEvaluacion",
                        principalColumn: "IdSolicitud");
                });

            migrationBuilder.CreateTable(
                name: "Evaluacion",
                columns: table => new
                {
                    IdEvaluacion = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdSolicitud = table.Column<int>(type: "int", nullable: false),
                    IdEspecialista = table.Column<int>(type: "int", nullable: false),
                    Dictamen = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ConditionGrade = table.Column<decimal>(type: "decimal(4,2)", nullable: true),
                    TechnicalHealth = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    ObservacionesGenerales = table.Column<string>(type: "nvarchar(1500)", maxLength: 1500, nullable: true),
                    FechaInicio = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "(sysdatetime())")
                        .Annotation("Relational:DefaultConstraintName", "DF_Evaluacion_FechaInicio"),
                    FechaFin = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Evaluacion", x => x.IdEvaluacion);
                    table.ForeignKey(
                        name: "FK_Evaluacion_Especialista",
                        column: x => x.IdEspecialista,
                        principalTable: "Especialista",
                        principalColumn: "IdEspecialista");
                    table.ForeignKey(
                        name: "FK_Evaluacion_Solicitud",
                        column: x => x.IdSolicitud,
                        principalTable: "SolicitudEvaluacion",
                        principalColumn: "IdSolicitud");
                });

            migrationBuilder.CreateTable(
                name: "Evidencia",
                columns: table => new
                {
                    IdEvidencia = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdSolicitud = table.Column<int>(type: "int", nullable: false),
                    IdTipoEvidencia = table.Column<int>(type: "int", nullable: false),
                    UrlArchivo = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FechaCarga = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "(sysdatetime())")
                        .Annotation("Relational:DefaultConstraintName", "DF_Evidencia_FechaCarga"),
                    Activa = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                        .Annotation("Relational:DefaultConstraintName", "DF_Evidencia_Activa")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Evidencia", x => x.IdEvidencia);
                    table.ForeignKey(
                        name: "FK_Evidencia_Solicitud",
                        column: x => x.IdSolicitud,
                        principalTable: "SolicitudEvaluacion",
                        principalColumn: "IdSolicitud");
                    table.ForeignKey(
                        name: "FK_Evidencia_Tipo",
                        column: x => x.IdTipoEvidencia,
                        principalTable: "TipoEvidencia",
                        principalColumn: "IdTipoEvidencia");
                });

            migrationBuilder.CreateTable(
                name: "Notificacion",
                columns: table => new
                {
                    IdNotificacion = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdUsuario = table.Column<int>(type: "int", nullable: false),
                    IdSolicitud = table.Column<int>(type: "int", nullable: true),
                    IdTipoNotificacion = table.Column<int>(type: "int", nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Mensaje = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    Leida = table.Column<bool>(type: "bit", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "(sysdatetime())")
                        .Annotation("Relational:DefaultConstraintName", "DF_Notificacion_Fecha"),
                    FechaLectura = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notificacion", x => x.IdNotificacion);
                    table.ForeignKey(
                        name: "FK_Notificacion_Solicitud",
                        column: x => x.IdSolicitud,
                        principalTable: "SolicitudEvaluacion",
                        principalColumn: "IdSolicitud");
                    table.ForeignKey(
                        name: "FK_Notificacion_Tipo",
                        column: x => x.IdTipoNotificacion,
                        principalTable: "TipoNotificacion",
                        principalColumn: "IdTipoNotificacion");
                    table.ForeignKey(
                        name: "FK_Notificacion_Usuario",
                        column: x => x.IdUsuario,
                        principalTable: "Usuario",
                        principalColumn: "IdUsuario");
                });

            migrationBuilder.CreateTable(
                name: "Certificado",
                columns: table => new
                {
                    IdCertificado = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdEvaluacion = table.Column<int>(type: "int", nullable: false),
                    IdEstadoCertificado = table.Column<int>(type: "int", nullable: false),
                    NumeroCertificado = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    QrToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())")
                        .Annotation("Relational:DefaultConstraintName", "DF_Certificado_QrToken"),
                    FechaEmision = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "(sysdatetime())")
                        .Annotation("Relational:DefaultConstraintName", "DF_Certificado_FechaEmision"),
                    FechaVencimiento = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Certificado", x => x.IdCertificado);
                    table.ForeignKey(
                        name: "FK_Certificado_Estado",
                        column: x => x.IdEstadoCertificado,
                        principalTable: "EstadoCertificado",
                        principalColumn: "IdEstadoCertificado");
                    table.ForeignKey(
                        name: "FK_Certificado_Evaluacion",
                        column: x => x.IdEvaluacion,
                        principalTable: "Evaluacion",
                        principalColumn: "IdEvaluacion");
                });

            migrationBuilder.CreateTable(
                name: "ResultadoCriterio",
                columns: table => new
                {
                    IdEvaluacion = table.Column<int>(type: "int", nullable: false),
                    IdCriterio = table.Column<int>(type: "int", nullable: false),
                    ValorTexto = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ValorNumerico = table.Column<decimal>(type: "decimal(12,2)", nullable: true),
                    ValorBooleano = table.Column<bool>(type: "bit", nullable: true),
                    Observacion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResultadoCriterio", x => new { x.IdEvaluacion, x.IdCriterio });
                    table.ForeignKey(
                        name: "FK_ResultadoCriterio_Criterio",
                        column: x => x.IdCriterio,
                        principalTable: "CriterioEvaluacion",
                        principalColumn: "IdCriterio");
                    table.ForeignKey(
                        name: "FK_ResultadoCriterio_Evaluacion",
                        column: x => x.IdEvaluacion,
                        principalTable: "Evaluacion",
                        principalColumn: "IdEvaluacion");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Asignacion_Especialista",
                table: "AsignacionEspecialista",
                columns: new[] { "IdEspecialista", "Activa" });

            migrationBuilder.CreateIndex(
                name: "UX_Asignacion_SolicitudActiva",
                table: "AsignacionEspecialista",
                column: "IdSolicitud",
                unique: true,
                filter: "([Activa]=(1))");

            migrationBuilder.CreateIndex(
                name: "UQ_Categoria_Nombre",
                table: "Categoria",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CategoriaMarca_IdMarca",
                table: "CategoriaMarca",
                column: "IdMarca");

            migrationBuilder.CreateIndex(
                name: "IX_Certificado_IdEstadoCertificado",
                table: "Certificado",
                column: "IdEstadoCertificado");

            migrationBuilder.CreateIndex(
                name: "UQ_Certificado_IdEvaluacion",
                table: "Certificado",
                column: "IdEvaluacion",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Certificado_Numero",
                table: "Certificado",
                column: "NumeroCertificado",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Certificado_QrToken",
                table: "Certificado",
                column: "QrToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Criterio_Categoria",
                table: "CriterioEvaluacion",
                columns: new[] { "IdCategoria", "Activo" });

            migrationBuilder.CreateIndex(
                name: "UQ_Criterio_CategoriaNombre",
                table: "CriterioEvaluacion",
                columns: new[] { "IdCategoria", "Nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EspecialidadEspecialista_IdCategoria_IdMarca",
                table: "EspecialidadEspecialista",
                columns: new[] { "IdCategoria", "IdMarca" });

            migrationBuilder.CreateIndex(
                name: "UX_Especialidad_EspecialistaCategoriaMarca",
                table: "EspecialidadEspecialista",
                columns: new[] { "IdEspecialista", "IdCategoria", "IdMarca" },
                unique: true,
                filter: "[IdMarca] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UQ_Especialista_IdUsuario",
                table: "Especialista",
                column: "IdUsuario",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_EstadoCertificado_Codigo",
                table: "EstadoCertificado",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_EstadoSolicitud_Codigo",
                table: "EstadoSolicitud",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_EstadoSolicitud_Nombre",
                table: "EstadoSolicitud",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Evaluacion_IdEspecialista",
                table: "Evaluacion",
                column: "IdEspecialista");

            migrationBuilder.CreateIndex(
                name: "UQ_Evaluacion_IdSolicitud",
                table: "Evaluacion",
                column: "IdSolicitud",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Evidencia_IdTipoEvidencia",
                table: "Evidencia",
                column: "IdTipoEvidencia");

            migrationBuilder.CreateIndex(
                name: "IX_Evidencia_Solicitud",
                table: "Evidencia",
                column: "IdSolicitud");

            migrationBuilder.CreateIndex(
                name: "UQ_Marca_Nombre",
                table: "Marca",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notificacion_IdSolicitud",
                table: "Notificacion",
                column: "IdSolicitud");

            migrationBuilder.CreateIndex(
                name: "IX_Notificacion_IdTipoNotificacion",
                table: "Notificacion",
                column: "IdTipoNotificacion");

            migrationBuilder.CreateIndex(
                name: "IX_Notificacion_UsuarioLeida",
                table: "Notificacion",
                columns: new[] { "IdUsuario", "Leida", "FechaCreacion" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Producto_CategoriaMarca",
                table: "Producto",
                columns: new[] { "IdCategoria", "IdMarca" });

            migrationBuilder.CreateIndex(
                name: "IX_Producto_IdMarca",
                table: "Producto",
                column: "IdMarca");

            migrationBuilder.CreateIndex(
                name: "IX_Producto_Propietario",
                table: "Producto",
                column: "IdPropietario");

            migrationBuilder.CreateIndex(
                name: "IX_ResultadoCriterio_IdCriterio",
                table: "ResultadoCriterio",
                column: "IdCriterio");

            migrationBuilder.CreateIndex(
                name: "UQ_Rol_Nombre",
                table: "Rol",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Solicitud_Estado",
                table: "SolicitudEvaluacion",
                column: "IdEstadoSolicitud");

            migrationBuilder.CreateIndex(
                name: "IX_Solicitud_Producto",
                table: "SolicitudEvaluacion",
                column: "IdProducto");

            migrationBuilder.CreateIndex(
                name: "IX_Solicitud_Solicitante",
                table: "SolicitudEvaluacion",
                column: "IdSolicitante");

            migrationBuilder.CreateIndex(
                name: "UQ_SolicitudEvaluacion_Folio",
                table: "SolicitudEvaluacion",
                column: "Folio",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_TipoEvidencia_Codigo",
                table: "TipoEvidencia",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_TipoNotificacion_Codigo",
                table: "TipoNotificacion",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Usuario_Correo",
                table: "Usuario",
                column: "Correo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UsuarioRol_IdRol",
                table: "UsuarioRol",
                column: "IdRol");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AsignacionEspecialista");

            migrationBuilder.DropTable(
                name: "Certificado");

            migrationBuilder.DropTable(
                name: "EspecialidadEspecialista");

            migrationBuilder.DropTable(
                name: "Evidencia");

            migrationBuilder.DropTable(
                name: "Notificacion");

            migrationBuilder.DropTable(
                name: "ResultadoCriterio");

            migrationBuilder.DropTable(
                name: "UsuarioRol");

            migrationBuilder.DropTable(
                name: "EstadoCertificado");

            migrationBuilder.DropTable(
                name: "TipoEvidencia");

            migrationBuilder.DropTable(
                name: "TipoNotificacion");

            migrationBuilder.DropTable(
                name: "CriterioEvaluacion");

            migrationBuilder.DropTable(
                name: "Evaluacion");

            migrationBuilder.DropTable(
                name: "Rol");

            migrationBuilder.DropTable(
                name: "Especialista");

            migrationBuilder.DropTable(
                name: "SolicitudEvaluacion");

            migrationBuilder.DropTable(
                name: "EstadoSolicitud");

            migrationBuilder.DropTable(
                name: "Producto");

            migrationBuilder.DropTable(
                name: "CategoriaMarca");

            migrationBuilder.DropTable(
                name: "Usuario");

            migrationBuilder.DropTable(
                name: "Categoria");

            migrationBuilder.DropTable(
                name: "Marca");
        }
    }
}

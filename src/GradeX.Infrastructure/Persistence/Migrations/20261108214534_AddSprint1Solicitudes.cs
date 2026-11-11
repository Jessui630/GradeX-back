using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GradeX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSprint1Solicitudes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClaveIdempotencia",
                table: "SolicitudEvaluacion",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HashSha256",
                table: "Evidencia",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NombreOriginal",
                table: "Evidencia",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "TamanoBytes",
                table: "Evidencia",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TipoContenido",
                table: "Evidencia",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CategoriaCampoRequisito",
                columns: table => new
                {
                    IdCategoria = table.Column<int>(type: "int", nullable: false),
                    Campo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Obligatorio = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    LongitudMaxima = table.Column<int>(type: "int", nullable: true),
                    Orden = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoriaCampoRequisito", x => new { x.IdCategoria, x.Campo });
                    table.ForeignKey(
                        name: "FK_CategoriaCampoRequisito_Categoria",
                        column: x => x.IdCategoria,
                        principalTable: "Categoria",
                        principalColumn: "IdCategoria",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CategoriaEvidenciaRequisito",
                columns: table => new
                {
                    IdCategoria = table.Column<int>(type: "int", nullable: false),
                    IdTipoEvidencia = table.Column<int>(type: "int", nullable: false),
                    Obligatorio = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CantidadMinima = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    CantidadMaxima = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    Orden = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoriaEvidenciaRequisito", x => new { x.IdCategoria, x.IdTipoEvidencia });
                    table.ForeignKey(
                        name: "FK_CategoriaEvidenciaRequisito_Categoria",
                        column: x => x.IdCategoria,
                        principalTable: "Categoria",
                        principalColumn: "IdCategoria",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CategoriaEvidenciaRequisito_Tipo",
                        column: x => x.IdTipoEvidencia,
                        principalTable: "TipoEvidencia",
                        principalColumn: "IdTipoEvidencia");
                });

            migrationBuilder.CreateTable(
                name: "SesionUsuario",
                columns: table => new
                {
                    IdSesion = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdUsuario = table.Column<int>(type: "int", nullable: false),
                    TokenHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "(sysdatetime())")
                        .Annotation("Relational:DefaultConstraintName", "DF_SesionUsuario_FechaCreacion"),
                    FechaExpiracion = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false),
                    FechaRevocacion = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SesionUsuario", x => x.IdSesion);
                    table.ForeignKey(
                        name: "FK_SesionUsuario_Usuario",
                        column: x => x.IdUsuario,
                        principalTable: "Usuario",
                        principalColumn: "IdUsuario",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UQ_Solicitud_Idempotencia",
                table: "SolicitudEvaluacion",
                column: "ClaveIdempotencia",
                unique: true,
                filter: "([ClaveIdempotencia] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_CategoriaEvidenciaRequisito_IdTipoEvidencia",
                table: "CategoriaEvidenciaRequisito",
                column: "IdTipoEvidencia");

            migrationBuilder.CreateIndex(
                name: "IX_SesionUsuario_Expiracion",
                table: "SesionUsuario",
                column: "FechaExpiracion");

            migrationBuilder.CreateIndex(
                name: "IX_SesionUsuario_Usuario",
                table: "SesionUsuario",
                column: "IdUsuario");

            migrationBuilder.CreateIndex(
                name: "UQ_SesionUsuario_TokenHash",
                table: "SesionUsuario",
                column: "TokenHash",
                unique: true);

            migrationBuilder.Sql("""
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
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CategoriaCampoRequisito");

            migrationBuilder.DropTable(
                name: "CategoriaEvidenciaRequisito");

            migrationBuilder.DropTable(
                name: "SesionUsuario");

            migrationBuilder.DropIndex(
                name: "UQ_Solicitud_Idempotencia",
                table: "SolicitudEvaluacion");

            migrationBuilder.DropColumn(
                name: "ClaveIdempotencia",
                table: "SolicitudEvaluacion");

            migrationBuilder.DropColumn(
                name: "HashSha256",
                table: "Evidencia");

            migrationBuilder.DropColumn(
                name: "NombreOriginal",
                table: "Evidencia");

            migrationBuilder.DropColumn(
                name: "TamanoBytes",
                table: "Evidencia");

            migrationBuilder.DropColumn(
                name: "TipoContenido",
                table: "Evidencia");

            migrationBuilder.Sql("""
                DELETE FROM dbo.TipoEvidencia
                WHERE Codigo IN (N'VISTA_GENERAL', N'ETIQUETA', N'SKU', N'NUMERO_SERIE', N'CAJA', N'ACCESORIOS', N'DEFECTOS');
                DELETE FROM dbo.EstadoSolicitud
                WHERE Codigo IN (N'BORRADOR', N'PENDIENTE_ASIGNACION');
                DELETE FROM dbo.Categoria
                WHERE Nombre IN (N'Sneakers', N'Ropa', N'Coleccionables', N'TCG', N'Consolas', N'Teléfonos', N'Laptops', N'Computadoras y componentes');
                """);
        }
    }
}

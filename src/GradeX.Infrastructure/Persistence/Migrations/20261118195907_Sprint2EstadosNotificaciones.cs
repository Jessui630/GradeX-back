using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GradeX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint2EstadosNotificaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RecibirNotificaciones",
                table: "Usuario",
                type: "bit",
                nullable: false,
                defaultValue: true)
                .Annotation("Relational:DefaultConstraintName", "DF_Usuario_RecibirNotificaciones");

            migrationBuilder.Sql("""
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
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecibirNotificaciones",
                table: "Usuario")
                .Annotation("Relational:DefaultConstraintName", "DF_Usuario_RecibirNotificaciones");
        }
    }
}

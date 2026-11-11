using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GradeX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEstadoAsignada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1 FROM dbo.EstadoSolicitud
                    WHERE Codigo = N'ASIGNADA' OR Nombre = N'Asignada')
                    INSERT INTO dbo.EstadoSolicitud (Codigo, Nombre, OrdenVisual, EsFinal)
                    VALUES (N'ASIGNADA', N'Asignada', 3, 0);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1
                    FROM dbo.SolicitudEvaluacion s
                    INNER JOIN dbo.EstadoSolicitud e ON e.IdEstadoSolicitud = s.IdEstadoSolicitud
                    WHERE e.Codigo = N'ASIGNADA')
                    DELETE FROM dbo.EstadoSolicitud WHERE Codigo = N'ASIGNADA';
                """);
        }
    }
}

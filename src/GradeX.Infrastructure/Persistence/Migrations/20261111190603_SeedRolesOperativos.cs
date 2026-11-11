using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GradeX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedRolesOperativos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
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
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE r
                FROM dbo.Rol r
                WHERE r.Nombre IN (N'Solicitante', N'Especialista', N'Administrador')
                  AND NOT EXISTS (
                      SELECT 1 FROM dbo.UsuarioRol ur WHERE ur.IdRol = r.IdRol);
                """);
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GradeX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCertificateSpecialtyReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IdEspecialidad",
                table: "Certificado",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Certificado_IdEspecialidad",
                table: "Certificado",
                column: "IdEspecialidad");

            migrationBuilder.AddForeignKey(
                name: "FK_Certificado_Especialidad",
                table: "Certificado",
                column: "IdEspecialidad",
                principalTable: "EspecialidadEspecialista",
                principalColumn: "IdEspecialidad",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Certificado_Especialidad",
                table: "Certificado");

            migrationBuilder.DropIndex(
                name: "IX_Certificado_IdEspecialidad",
                table: "Certificado");

            migrationBuilder.DropColumn(
                name: "IdEspecialidad",
                table: "Certificado");
        }
    }
}

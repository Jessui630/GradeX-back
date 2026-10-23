using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GradeX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RestoreCheckConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_ResultadoCriterio_AlgunValor",
                table: "ResultadoCriterio",
                sql: "([ValorTexto] IS NOT NULL OR [ValorNumerico] IS NOT NULL OR [ValorBooleano] IS NOT NULL OR [Observacion] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Notificacion_FechaLectura",
                table: "Notificacion",
                sql: "([FechaLectura] IS NULL OR [FechaLectura]>=[FechaCreacion])");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Evaluacion_ConditionGrade",
                table: "Evaluacion",
                sql: "([ConditionGrade] IS NULL OR [ConditionGrade]>=(0) AND [ConditionGrade]<=(10))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Evaluacion_Dictamen",
                table: "Evaluacion",
                sql: "([Dictamen] IS NULL OR ([Dictamen]=N'INCONCLUSO' OR [Dictamen]=N'NO_AUTENTICO' OR [Dictamen]=N'AUTENTICO'))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Evaluacion_Fechas",
                table: "Evaluacion",
                sql: "([FechaFin] IS NULL OR [FechaFin]>=[FechaInicio])");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Evaluacion_TechnicalHealth",
                table: "Evaluacion",
                sql: "([TechnicalHealth] IS NULL OR [TechnicalHealth]>=(0) AND [TechnicalHealth]<=(100))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Especialista_Estado",
                table: "Especialista",
                sql: "([Estado]=N'SUSPENDIDO' OR [Estado]=N'INACTIVO' OR [Estado]=N'ACTIVO' OR [Estado]=N'PENDIENTE')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Especialista_Experiencia",
                table: "Especialista",
                sql: "([AniosExperiencia] IS NULL OR [AniosExperiencia]>=(0))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Especialidad_Nivel",
                table: "EspecialidadEspecialista",
                sql: "([Nivel]=N'EXPERTO' OR [Nivel]=N'AVANZADO' OR [Nivel]=N'INTERMEDIO' OR [Nivel]=N'GENERAL')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Criterio_TipoDato",
                table: "CriterioEvaluacion",
                sql: "([TipoDato]=N'ESCALA' OR [TipoDato]=N'NUMERICO' OR [TipoDato]=N'TEXTO' OR [TipoDato]=N'BOOLEANO')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Certificado_Fechas",
                table: "Certificado",
                sql: "([FechaVencimiento] IS NULL OR [FechaVencimiento]>=[FechaEmision])");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Asignacion_Fechas",
                table: "AsignacionEspecialista",
                sql: "([FechaFin] IS NULL OR [FechaFin]>=[FechaAsignacion])");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ResultadoCriterio_AlgunValor",
                table: "ResultadoCriterio");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Notificacion_FechaLectura",
                table: "Notificacion");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Evaluacion_ConditionGrade",
                table: "Evaluacion");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Evaluacion_Dictamen",
                table: "Evaluacion");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Evaluacion_Fechas",
                table: "Evaluacion");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Evaluacion_TechnicalHealth",
                table: "Evaluacion");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Especialista_Estado",
                table: "Especialista");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Especialista_Experiencia",
                table: "Especialista");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Especialidad_Nivel",
                table: "EspecialidadEspecialista");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Criterio_TipoDato",
                table: "CriterioEvaluacion");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Certificado_Fechas",
                table: "Certificado");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Asignacion_Fechas",
                table: "AsignacionEspecialista");
        }
    }
}

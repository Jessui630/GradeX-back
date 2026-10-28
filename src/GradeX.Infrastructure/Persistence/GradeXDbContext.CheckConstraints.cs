using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;

namespace GradeX.Infrastructure.Persistence;

/// <summary>
/// Restricciones CHECK originales del esquema SQL Server de GradeX.
/// Archivo parcial: conserva el código obtenido mediante scaffold.
/// </summary>
public partial class GradeXDbContext
{
    [SuppressMessage(
        "Performance",
        "CA1822",
        Justification = "La firma del método parcial la declara el scaffold de EF Core; convertir solo esta implementación en static rompería el contrato generado.")]
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        AddCheck(modelBuilder, "AsignacionEspecialista",
            "CK_Asignacion_Fechas",
            "([FechaFin] IS NULL OR [FechaFin]>=[FechaAsignacion])");
        AddCheck(modelBuilder, "Certificado",
            "CK_Certificado_Fechas",
            "([FechaVencimiento] IS NULL OR [FechaVencimiento]>=[FechaEmision])");
        AddCheck(modelBuilder, "CriterioEvaluacion",
            "CK_Criterio_TipoDato",
            "([TipoDato]=N'ESCALA' OR [TipoDato]=N'NUMERICO' OR [TipoDato]=N'TEXTO' OR [TipoDato]=N'BOOLEANO')");
        AddCheck(modelBuilder, "EspecialidadEspecialista",
            "CK_Especialidad_Nivel",
            "([Nivel]=N'EXPERTO' OR [Nivel]=N'AVANZADO' OR [Nivel]=N'INTERMEDIO' OR [Nivel]=N'GENERAL')");
        AddCheck(modelBuilder, "Especialista",
            "CK_Especialista_Estado",
            "([Estado]=N'SUSPENDIDO' OR [Estado]=N'INACTIVO' OR [Estado]=N'ACTIVO' OR [Estado]=N'PENDIENTE')");
        AddCheck(modelBuilder, "Especialista",
            "CK_Especialista_Experiencia",
            "([AniosExperiencia] IS NULL OR [AniosExperiencia]>=(0))");
        AddCheck(modelBuilder, "Evaluacion",
            "CK_Evaluacion_ConditionGrade",
            "([ConditionGrade] IS NULL OR [ConditionGrade]>=(0) AND [ConditionGrade]<=(10))");
        AddCheck(modelBuilder, "Evaluacion",
            "CK_Evaluacion_Dictamen",
            "([Dictamen] IS NULL OR ([Dictamen]=N'INCONCLUSO' OR [Dictamen]=N'NO_AUTENTICO' OR [Dictamen]=N'AUTENTICO'))");
        AddCheck(modelBuilder, "Evaluacion",
            "CK_Evaluacion_Fechas",
            "([FechaFin] IS NULL OR [FechaFin]>=[FechaInicio])");
        AddCheck(modelBuilder, "Evaluacion",
            "CK_Evaluacion_TechnicalHealth",
            "([TechnicalHealth] IS NULL OR [TechnicalHealth]>=(0) AND [TechnicalHealth]<=(100))");
        AddCheck(modelBuilder, "Notificacion",
            "CK_Notificacion_FechaLectura",
            "([FechaLectura] IS NULL OR [FechaLectura]>=[FechaCreacion])");
        AddCheck(modelBuilder, "ResultadoCriterio",
            "CK_ResultadoCriterio_AlgunValor",
            "([ValorTexto] IS NOT NULL OR [ValorNumerico] IS NOT NULL OR [ValorBooleano] IS NOT NULL OR [Observacion] IS NOT NULL)");
    }

    private static void AddCheck(
        ModelBuilder modelBuilder,
        string tableName,
        string constraintName,
        string expression)
    {
        // Busca la entidad por su nombre de tabla, no por el nombre
        // de la clase que pudo generar el scaffold.
        var matches = modelBuilder.Model.GetEntityTypes()
            .Where(entity =>
                string.Equals(entity.GetTableName(), tableName,
                    StringComparison.OrdinalIgnoreCase)
                && (entity.GetSchema() is null
                    || string.Equals(entity.GetSchema(), "dbo",
                        StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                $"Se esperaba una entidad para dbo.{tableName}; " +
                $"se encontraron {matches.Length}.");
        }

        // Conserva el nombre y el esquema existentes en el mapeo.
        modelBuilder.Entity(matches[0].ClrType)
            .ToTable(table => table.HasCheckConstraint(constraintName, expression));
    }
}

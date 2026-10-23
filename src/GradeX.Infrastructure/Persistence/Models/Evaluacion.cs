using System;
using System.Collections.Generic;

namespace GradeX.Infrastructure.Persistence.Models;

public partial class Evaluacion
{
    public int IdEvaluacion { get; set; }

    public int IdSolicitud { get; set; }

    public int IdEspecialista { get; set; }

    public string? Dictamen { get; set; }

    public decimal? ConditionGrade { get; set; }

    public decimal? TechnicalHealth { get; set; }

    public string? ObservacionesGenerales { get; set; }

    public DateTime FechaInicio { get; set; }

    public DateTime? FechaFin { get; set; }

    public virtual Certificado? Certificado { get; set; }

    public virtual Especialistum IdEspecialistaNavigation { get; set; } = null!;

    public virtual SolicitudEvaluacion IdSolicitudNavigation { get; set; } = null!;

    public virtual ICollection<ResultadoCriterio> ResultadoCriterios { get; set; } = new List<ResultadoCriterio>();
}

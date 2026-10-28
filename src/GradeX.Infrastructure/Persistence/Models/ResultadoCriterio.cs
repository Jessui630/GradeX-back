using System;
using System.Collections.Generic;

namespace GradeX.Infrastructure.Persistence.Models;

public partial class ResultadoCriterio
{
    public int IdEvaluacion { get; set; }

    public int IdCriterio { get; set; }

    public string? ValorTexto { get; set; }

    public decimal? ValorNumerico { get; set; }

    public bool? ValorBooleano { get; set; }

    public string? Observacion { get; set; }

    public virtual CriterioEvaluacion IdCriterioNavigation { get; set; } = null!;

    public virtual Evaluacion IdEvaluacionNavigation { get; set; } = null!;
}

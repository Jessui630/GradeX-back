using System;
using System.Collections.Generic;

namespace GradeX.Infrastructure.Persistence.Models;

public partial class CriterioEvaluacion
{
    public int IdCriterio { get; set; }

    public int IdCategoria { get; set; }

    public string Nombre { get; set; } = null!;

    public string? Descripcion { get; set; }

    public string TipoDato { get; set; } = null!;

    public bool Obligatorio { get; set; }

    public short Orden { get; set; }

    public bool Activo { get; set; }

    public virtual Categorium IdCategoriaNavigation { get; set; } = null!;

    public virtual ICollection<ResultadoCriterio> ResultadoCriterios { get; set; } = new List<ResultadoCriterio>();
}

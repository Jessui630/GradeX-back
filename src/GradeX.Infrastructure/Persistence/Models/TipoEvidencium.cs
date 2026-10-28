using System;
using System.Collections.Generic;

namespace GradeX.Infrastructure.Persistence.Models;

public partial class TipoEvidencium
{
    public int IdTipoEvidencia { get; set; }

    public string Codigo { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    public virtual ICollection<Evidencium> Evidencia { get; set; } = new List<Evidencium>();
}

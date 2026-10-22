using System;
using System.Collections.Generic;

namespace GradeX.Infrastructure.Persistence.Models;

public partial class Evidencium
{
    public int IdEvidencia { get; set; }

    public int IdSolicitud { get; set; }

    public int IdTipoEvidencia { get; set; }

    public string UrlArchivo { get; set; } = null!;

    public string? Descripcion { get; set; }

    public DateTime FechaCarga { get; set; }

    public bool Activa { get; set; }

    public virtual SolicitudEvaluacion IdSolicitudNavigation { get; set; } = null!;

    public virtual TipoEvidencium IdTipoEvidenciaNavigation { get; set; } = null!;
}

using System;
using System.Collections.Generic;

namespace GradeX.Infrastructure.Persistence.Models;

public partial class EstadoSolicitud
{
    public int IdEstadoSolicitud { get; set; }

    public string Codigo { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    public short OrdenVisual { get; set; }

    public bool EsFinal { get; set; }

    public virtual ICollection<SolicitudEvaluacion> SolicitudEvaluacions { get; set; } = new List<SolicitudEvaluacion>();
}

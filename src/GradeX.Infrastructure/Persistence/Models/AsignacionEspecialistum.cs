using System;
using System.Collections.Generic;

namespace GradeX.Infrastructure.Persistence.Models;

public partial class AsignacionEspecialistum
{
    public int IdAsignacion { get; set; }

    public int IdSolicitud { get; set; }

    public int IdEspecialista { get; set; }

    public DateTime FechaAsignacion { get; set; }

    public DateTime? FechaFin { get; set; }

    public bool Activa { get; set; }

    public string? MotivoFin { get; set; }

    public virtual Especialistum IdEspecialistaNavigation { get; set; } = null!;

    public virtual SolicitudEvaluacion IdSolicitudNavigation { get; set; } = null!;
}

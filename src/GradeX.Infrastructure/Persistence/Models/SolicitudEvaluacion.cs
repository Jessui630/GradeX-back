using System;
using System.Collections.Generic;

namespace GradeX.Infrastructure.Persistence.Models;

public partial class SolicitudEvaluacion
{
    public int IdSolicitud { get; set; }

    public string Folio { get; set; } = null!;

    public int IdProducto { get; set; }

    public int IdSolicitante { get; set; }

    public int IdEstadoSolicitud { get; set; }

    public DateTime FechaSolicitud { get; set; }

    public DateTime FechaUltimoCambio { get; set; }

    public string? ObservacionesSolicitante { get; set; }

    public virtual AsignacionEspecialistum? AsignacionEspecialistum { get; set; }

    public virtual Evaluacion? Evaluacion { get; set; }

    public virtual ICollection<Evidencium> Evidencia { get; set; } = new List<Evidencium>();

    public virtual EstadoSolicitud IdEstadoSolicitudNavigation { get; set; } = null!;

    public virtual Producto IdProductoNavigation { get; set; } = null!;

    public virtual Usuario IdSolicitanteNavigation { get; set; } = null!;

    public virtual ICollection<Notificacion> Notificacions { get; set; } = new List<Notificacion>();
}

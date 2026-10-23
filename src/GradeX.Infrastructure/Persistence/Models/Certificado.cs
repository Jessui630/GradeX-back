using System;
using System.Collections.Generic;

namespace GradeX.Infrastructure.Persistence.Models;

public partial class Certificado
{
    public int IdCertificado { get; set; }

    public int IdEvaluacion { get; set; }

    public int IdEstadoCertificado { get; set; }

    public int? IdEspecialidad { get; set; }

    public string NumeroCertificado { get; set; } = null!;

    public Guid QrToken { get; set; }

    public DateTime FechaEmision { get; set; }

    public DateTime? FechaVencimiento { get; set; }

    public virtual EstadoCertificado IdEstadoCertificadoNavigation { get; set; } = null!;

    public virtual EspecialidadEspecialistum? IdEspecialidadNavigation { get; set; }

    public virtual Evaluacion IdEvaluacionNavigation { get; set; } = null!;
}

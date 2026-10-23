using System;
using System.Collections.Generic;

namespace GradeX.Infrastructure.Persistence.Models;

public partial class EstadoCertificado
{
    public int IdEstadoCertificado { get; set; }

    public string Codigo { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    public virtual ICollection<Certificado> Certificados { get; set; } = new List<Certificado>();
}

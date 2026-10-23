using System;
using System.Collections.Generic;

namespace GradeX.Infrastructure.Persistence.Models;

public partial class Especialistum
{
    public int IdEspecialista { get; set; }

    public int IdUsuario { get; set; }

    public short? AniosExperiencia { get; set; }

    public string Estado { get; set; } = null!;

    public string? Biografia { get; set; }

    public DateTime FechaAlta { get; set; }

    public virtual ICollection<AsignacionEspecialistum> AsignacionEspecialista { get; set; } = new List<AsignacionEspecialistum>();

    public virtual ICollection<EspecialidadEspecialistum> EspecialidadEspecialista { get; set; } = new List<EspecialidadEspecialistum>();

    public virtual ICollection<Evaluacion> Evaluacions { get; set; } = new List<Evaluacion>();

    public virtual Usuario IdUsuarioNavigation { get; set; } = null!;
}

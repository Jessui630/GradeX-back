using System;
using System.Collections.Generic;

namespace GradeX.Infrastructure.Persistence.Models;

public partial class EspecialidadEspecialistum
{
    public int IdEspecialidad { get; set; }

    public int IdEspecialista { get; set; }

    public int IdCategoria { get; set; }

    public int? IdMarca { get; set; }

    public string Nivel { get; set; } = null!;

    public bool Activa { get; set; }

    public DateTime FechaRegistro { get; set; }

    public virtual CategoriaMarca? CategoriaMarca { get; set; }

    public virtual Categorium IdCategoriaNavigation { get; set; } = null!;

    public virtual Especialistum IdEspecialistaNavigation { get; set; } = null!;

    public virtual ICollection<Certificado> Certificados { get; set; } = new List<Certificado>();
}

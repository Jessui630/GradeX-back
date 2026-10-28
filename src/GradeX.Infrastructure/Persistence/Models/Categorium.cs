using System;
using System.Collections.Generic;

namespace GradeX.Infrastructure.Persistence.Models;

public partial class Categorium
{
    public int IdCategoria { get; set; }

    public string Nombre { get; set; } = null!;

    public string? Descripcion { get; set; }

    public bool Activa { get; set; }

    public virtual ICollection<CategoriaMarca> CategoriaMarcas { get; set; } = new List<CategoriaMarca>();

    public virtual ICollection<CriterioEvaluacion> CriterioEvaluacions { get; set; } = new List<CriterioEvaluacion>();

    public virtual ICollection<EspecialidadEspecialistum> EspecialidadEspecialista { get; set; } = new List<EspecialidadEspecialistum>();

    public virtual ICollection<Producto> Productos { get; set; } = new List<Producto>();
}

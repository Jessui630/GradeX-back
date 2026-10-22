using System;
using System.Collections.Generic;

namespace GradeX.Infrastructure.Persistence.Models;

public partial class CategoriaMarca
{
    public int IdCategoria { get; set; }

    public int IdMarca { get; set; }

    public bool Activa { get; set; }

    public virtual ICollection<EspecialidadEspecialistum> EspecialidadEspecialista { get; set; } = new List<EspecialidadEspecialistum>();

    public virtual Categorium IdCategoriaNavigation { get; set; } = null!;

    public virtual Marca IdMarcaNavigation { get; set; } = null!;

    public virtual ICollection<Producto> Productos { get; set; } = new List<Producto>();
}

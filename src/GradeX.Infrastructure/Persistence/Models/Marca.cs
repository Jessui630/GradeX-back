using System;
using System.Collections.Generic;

namespace GradeX.Infrastructure.Persistence.Models;

public partial class Marca
{
    public int IdMarca { get; set; }

    public string Nombre { get; set; } = null!;

    public bool Activa { get; set; }

    public virtual ICollection<CategoriaMarca> CategoriaMarcas { get; set; } = new List<CategoriaMarca>();

    public virtual ICollection<Producto> Productos { get; set; } = new List<Producto>();
}

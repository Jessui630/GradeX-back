using System;
using System.Collections.Generic;

namespace GradeX.Infrastructure.Persistence.Models;

public partial class Producto
{
    public int IdProducto { get; set; }

    public int IdPropietario { get; set; }

    public int IdCategoria { get; set; }

    public int? IdMarca { get; set; }

    public string Nombre { get; set; } = null!;

    public string? Modelo { get; set; }

    public string? Sku { get; set; }

    public string? NumeroSerie { get; set; }

    public string? Imei { get; set; }

    public string? Accesorios { get; set; }

    public string? Descripcion { get; set; }

    public DateTime FechaRegistro { get; set; }

    public bool Activo { get; set; }

    public virtual CategoriaMarca? CategoriaMarca { get; set; }

    public virtual Categorium IdCategoriaNavigation { get; set; } = null!;

    public virtual Marca? IdMarcaNavigation { get; set; }

    public virtual Usuario IdPropietarioNavigation { get; set; } = null!;

    public virtual ICollection<SolicitudEvaluacion> SolicitudEvaluacions { get; set; } = new List<SolicitudEvaluacion>();
}

namespace GradeX.Infrastructure.Persistence.Models;

public partial class CategoriaEvidenciaRequisito
{
    public int IdCategoria { get; set; }

    public int IdTipoEvidencia { get; set; }

    public bool Obligatorio { get; set; }

    public int CantidadMinima { get; set; }

    public int CantidadMaxima { get; set; }

    public int Orden { get; set; }

    public virtual Categorium IdCategoriaNavigation { get; set; } = null!;

    public virtual TipoEvidencium IdTipoEvidenciaNavigation { get; set; } = null!;
}

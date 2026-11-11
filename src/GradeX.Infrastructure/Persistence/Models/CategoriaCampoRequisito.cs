namespace GradeX.Infrastructure.Persistence.Models;

public partial class CategoriaCampoRequisito
{
    public int IdCategoria { get; set; }

    public string Campo { get; set; } = null!;

    public bool Obligatorio { get; set; }

    public int? LongitudMaxima { get; set; }

    public int Orden { get; set; }

    public virtual Categorium IdCategoriaNavigation { get; set; } = null!;
}

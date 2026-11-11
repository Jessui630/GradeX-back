namespace GradeX.Infrastructure.Persistence.Models;

public partial class SesionUsuario
{
    public Guid IdSesion { get; set; }

    public int IdUsuario { get; set; }

    public string TokenHash { get; set; } = null!;

    public DateTime FechaCreacion { get; set; }

    public DateTime FechaExpiracion { get; set; }

    public DateTime? FechaRevocacion { get; set; }

    public virtual Usuario IdUsuarioNavigation { get; set; } = null!;
}

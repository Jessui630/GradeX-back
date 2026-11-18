namespace GradeX.Application.Notificaciones;

public sealed record NotificacionResumen(
    long IdNotificacion,
    int? IdSolicitud,
    string TipoCodigo,
    string Titulo,
    string Mensaje,
    bool Leida,
    DateTime FechaCreacion,
    DateTime? FechaLectura);

public sealed record ListadoNotificaciones(
    IReadOnlyList<NotificacionResumen> Elementos,
    int Pagina,
    int TamanoPagina,
    int Total,
    int NoLeidas);

public interface INotificacionStore
{
    Task<ListadoNotificaciones> ListarAsync(
        int idUsuario, int pagina, int tamanoPagina, bool soloNoLeidas,
        CancellationToken cancellationToken);

    Task<bool> MarcarLeidaAsync(
        long idNotificacion, int idUsuario, CancellationToken cancellationToken);

    Task<int> MarcarTodasLeidasAsync(int idUsuario, CancellationToken cancellationToken);

    Task<int> ContarNoLeidasAsync(int idUsuario, CancellationToken cancellationToken);
}

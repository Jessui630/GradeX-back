namespace GradeX.Application.Cuentas;

public enum EstadoContrasenaComprometida
{
    Comprometida,
    NoEncontrada,
    NoDisponible
}

public interface IContrasenaComprometida
{
    ValueTask<EstadoContrasenaComprometida> EstaComprometidaAsync(string contrasenaNormalizada, CancellationToken cancellationToken);
}

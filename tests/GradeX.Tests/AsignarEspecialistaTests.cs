using GradeX.Application.Asignaciones;

namespace GradeX.Tests;

public sealed class AsignarEspecialistaTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-1, 2)]
    [InlineData(2, -1)]
    public async Task IdentificadoresInvalidosNoLleganAlStore(int idSolicitud, int idEspecialista)
    {
        var store = new AsignacionStoreFalso();
        var caso = new AsignarEspecialista(store);

        var resultado = await caso.EjecutarAsync(new DatosAsignacion(idSolicitud, idEspecialista));

        Assert.Equal(EstadoAsignarEspecialista.DatosInvalidos, resultado.Estado);
        Assert.Equal(0, store.Llamadas);
    }

    [Fact]
    public async Task ResultadoExitosoSeConserva()
    {
        var creada = new AsignacionCreada(
            10, 20, "SOL-1", 30, "Especialista", DateTime.UtcNow, "ASIGNADA", "Asignada");
        var store = new AsignacionStoreFalso
        {
            Resultado = new ResultadoAsignarEspecialista(
                EstadoAsignarEspecialista.Exito, creada)
        };
        var caso = new AsignarEspecialista(store);

        var resultado = await caso.EjecutarAsync(new DatosAsignacion(20, 30));

        Assert.Equal(EstadoAsignarEspecialista.Exito, resultado.Estado);
        Assert.Same(creada, resultado.Asignacion);
        Assert.Equal(1, store.Llamadas);
    }

    [Fact]
    public async Task ErroresDelStoreSePropagan()
    {
        var store = new AsignacionStoreFalso
        {
            Resultado = new ResultadoAsignarEspecialista(
                EstadoAsignarEspecialista.SinCompatibilidad,
                EstadoSolicitudCodigo: "PENDIENTE_ASIGNACION")
        };
        var caso = new AsignarEspecialista(store);

        var resultado = await caso.EjecutarAsync(new DatosAsignacion(20, 30));

        Assert.Equal(EstadoAsignarEspecialista.SinCompatibilidad, resultado.Estado);
        Assert.Equal("PENDIENTE_ASIGNACION", resultado.EstadoSolicitudCodigo);
        Assert.Equal(1, store.Llamadas);
    }

    private sealed class AsignacionStoreFalso : IAsignacionStore
    {
        public int Llamadas { get; private set; }
        public ResultadoAsignarEspecialista Resultado { get; init; } =
            new(EstadoAsignarEspecialista.SolicitudNoEncontrada);

        public Task<ListadoSolicitudesAsignacion> ListarSolicitudesAsync(
            string? estado, int pagina, int tamanoPagina, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<SolicitudAsignacionDetalle?> ObtenerSolicitudAsync(
            int idSolicitud, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CompatiblesSolicitud?> ObtenerCompatiblesAsync(
            int idSolicitud, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ResultadoAsignarEspecialista> AsignarAsync(
            DatosAsignacion datos, CancellationToken cancellationToken)
        {
            Llamadas++;
            return Task.FromResult(Resultado);
        }
    }
}

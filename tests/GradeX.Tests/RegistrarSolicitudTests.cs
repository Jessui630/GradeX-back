using GradeX.Application.Productos;
using GradeX.Application.Solicitudes;

namespace GradeX.Tests;

public sealed class RegistrarSolicitudTests
{
    [Fact]
    public async Task DatosValidosCreanBorrador()
    {
        var store = new Store();
        var resultado = await new RegistrarSolicitud(Catalogo(), store).EjecutarAsync(
            Datos(nombre: "  Producto  ", modelo: " Modelo "), 7, CancellationToken.None);

        Assert.Equal(EstadoCrearSolicitud.Exito, resultado.Estado);
        Assert.NotNull(store.Datos);
        Assert.Equal("Producto", store.Datos.Nombre);
        Assert.Equal("Modelo", store.Datos.Modelo);
    }

    [Theory]
    [InlineData(null, "Modelo", EstadoCrearSolicitud.DatosInvalidos, "nombre")]
    [InlineData("Producto", null, EstadoCrearSolicitud.DatosInvalidos, "modelo")]
    public async Task CamposObligatoriosSonValidados(
        string? nombre, string? modelo, EstadoCrearSolicitud esperado, string campo)
    {
        var resultado = await new RegistrarSolicitud(Catalogo(), new Store()).EjecutarAsync(
            Datos(nombre: nombre, modelo: modelo), 7, CancellationToken.None);

        Assert.Equal(esperado, resultado.Estado);
        Assert.Equal(campo, resultado.Campo);
    }

    [Fact]
    public async Task CategoriaInexistenteEsRechazada()
    {
        var resultado = await new RegistrarSolicitud(Catalogo(activa: false), new Store())
            .EjecutarAsync(Datos(), 7, CancellationToken.None);

        Assert.Equal(EstadoCrearSolicitud.CategoriaInvalida, resultado.Estado);
    }

    [Fact]
    public async Task RequisitoDesconocidoNoSeIgnora()
    {
        var catalogo = Catalogo(campo: "CAMPO_INVENTADO");
        var resultado = await new RegistrarSolicitud(catalogo, new Store())
            .EjecutarAsync(Datos(), 7, CancellationToken.None);

        Assert.Equal(EstadoCrearSolicitud.RequisitosNoConfigurados, resultado.Estado);
    }

    [Fact]
    public async Task LongitudMaximaEsValidada()
    {
        var resultado = await new RegistrarSolicitud(Catalogo(), new Store())
            .EjecutarAsync(Datos(nombre: new string('a', 151)), 7, CancellationToken.None);

        Assert.Equal(EstadoCrearSolicitud.DatosInvalidos, resultado.Estado);
    }

    [Fact]
    public async Task ResultadoDelAlmacenSePropaga()
    {
        var resultado = await new RegistrarSolicitud(Catalogo(), new Store
        {
            Resultado = new ResultadoCrearSolicitud(EstadoCrearSolicitud.MarcaInvalida)
        }).EjecutarAsync(Datos(), 7, CancellationToken.None);

        Assert.Equal(EstadoCrearSolicitud.MarcaInvalida, resultado.Estado);
    }

    private static DatosNuevaSolicitud Datos(string? nombre = "Producto", string? modelo = "Modelo") =>
        new(1, null, nombre, modelo, "SKU", "SERIE", null, null, null, null, Guid.NewGuid());

    private static CatalogoFalso Catalogo(
        bool activa = true, string campo = "NOMBRE") =>
        new(activa, campo);

    private sealed class CatalogoFalso(bool activa, string campo) : ICatalogoProductoStore
    {
        public Task<IReadOnlyList<CategoriaResumen>> ListarCategoriasAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CategoriaResumen>>([]);

        public Task<IReadOnlyList<MarcaResumen>> ListarMarcasAsync(int idCategoria, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MarcaResumen>>([]);

        public Task<RequisitosCategoria?> ObtenerRequisitosAsync(
            int idCategoria, CancellationToken cancellationToken) =>
            Task.FromResult<RequisitosCategoria?>(activa
                ? new RequisitosCategoria(idCategoria,
                    [
                        new CampoRequisito("NOMBRE", "Nombre del producto", true, null, 1),
                        new CampoRequisito("MODELO", "Modelo", true, null, 2),
                        .. (campo is "NOMBRE" or "MODELO"
                            ? Array.Empty<CampoRequisito>()
                            : [new CampoRequisito(campo, "Campo", true, null, 3)])
                    ],
                    [])
                : null);
    }

    private sealed class Store : ISolicitudStore
    {
        public ResultadoCrearSolicitud Resultado { get; init; } =
            new(EstadoCrearSolicitud.Exito, new SolicitudCreada(1, "SOL-1", "BORRADOR", "Borrador"));
        public DatosNuevaSolicitud? Datos { get; private set; }

        public Task<ResultadoCrearSolicitud> CrearBorradorAsync(
            DatosNuevaSolicitud datos, int idSolicitante, CancellationToken cancellationToken)
        {
            Datos = datos;
            return Task.FromResult(Resultado);
        }

        public Task<ResultadoEnviarSolicitud> EnviarAsync(
            int idSolicitud, int idSolicitante, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<SolicitudListado> ListarAsync(
            int idSolicitante, int pagina, int tamanoPagina, string? estado,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SolicitudDetalle?> ObtenerDetalleAsync(
            int idSolicitud, int idSolicitante, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ResultadoAgregarEvidencia> AgregarEvidenciaAsync(
            int idSolicitud, int idSolicitante, int idTipoEvidencia, string claveAlmacenamiento,
            string nombreOriginal, string tipoContenido, long tamanoBytes, string hashSha256,
            string? descripcion, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ArchivoEvidencia?> ObtenerArchivoAsync(
            int idSolicitud, int idEvidencia, int idSolicitante, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}

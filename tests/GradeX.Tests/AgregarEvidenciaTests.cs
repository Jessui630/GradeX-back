using GradeX.Application.Solicitudes;
using GradeX.Infrastructure.Storage;

namespace GradeX.Tests;

public sealed class AgregarEvidenciaTests
{
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=");

    [Fact]
    public async Task ImagenValidaSeGuardaYSeRegistra()
    {
        var root = CrearRuta();
        try
        {
            var store = new Store();
            var resultado = await new AgregarEvidencia(store, new LocalEvidenciaStorage(root))
                .EjecutarAsync(1, 7, Datos(Png, "foto.png"), CancellationToken.None);

            Assert.Equal(EstadoAgregarEvidencia.Exito, resultado.Estado);
            Assert.Equal("image/png", store.TipoContenido);
            Assert.Equal(Png.Length, store.TamanoBytes);
            Assert.Single(Directory.GetFiles(root, "*", SearchOption.AllDirectories));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task FirmaIncorrectaEsRechazada()
    {
        var root = CrearRuta();
        try
        {
            var resultado = await new AgregarEvidencia(new Store(), new LocalEvidenciaStorage(root))
                .EjecutarAsync(1, 7, Datos("no es una imagen"u8.ToArray(), "foto.png"), CancellationToken.None);

            Assert.Equal(EstadoAgregarEvidencia.ArchivoInvalido, resultado.Estado);
            Assert.Empty(Directory.GetFiles(root, "*", SearchOption.AllDirectories));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ExtensionNoCoincideConFirma()
    {
        var root = CrearRuta();
        try
        {
            var resultado = await new AgregarEvidencia(new Store(), new LocalEvidenciaStorage(root))
                .EjecutarAsync(1, 7, Datos(Png, "foto.jpg"), CancellationToken.None);

            Assert.Equal(EstadoAgregarEvidencia.ArchivoInvalido, resultado.Estado);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ArchivoExcesivoEsRechazadoSinLeer()
    {
        var root = CrearRuta();
        try
        {
            var resultado = await new AgregarEvidencia(new Store(), new LocalEvidenciaStorage(root))
                .EjecutarAsync(1, 7, Datos(Png, "foto.png", 8 * 1024 * 1024 + 1), CancellationToken.None);

            Assert.Equal(EstadoAgregarEvidencia.ArchivoInvalido, resultado.Estado);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task FalloDelAlmacenBorraArchivoNuevo()
    {
        var root = CrearRuta();
        try
        {
            var resultado = await new AgregarEvidencia(
                new Store { Resultado = new(EstadoAgregarEvidencia.NoEncontrada) },
                new LocalEvidenciaStorage(root))
                .EjecutarAsync(1, 7, Datos(Png, "foto.png"), CancellationToken.None);

            Assert.Equal(EstadoAgregarEvidencia.NoEncontrada, resultado.Estado);
            Assert.Empty(Directory.GetFiles(root, "*", SearchOption.AllDirectories));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task FalloDelSistemaDeArchivosEsControlado()
    {
        var resultado = await new AgregarEvidencia(new Store(), new AlmacenamientoFallido())
            .EjecutarAsync(1, 7, Datos(Png, "foto.png"), CancellationToken.None);

        Assert.Equal(EstadoAgregarEvidencia.AlmacenamientoNoDisponible, resultado.Estado);
    }

    [Fact]
    public async Task RechazaRutasFueraDelAlmacenamiento()
    {
        var root = CrearRuta();
        try
        {
            var storage = new LocalEvidenciaStorage(root);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                storage.AbrirAsync("../../archivo.png", CancellationToken.None));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static DatosEvidencia Datos(
        byte[] contenido, string nombre, long? longitud = null) =>
        new(1, nombre, "image/png", longitud ?? contenido.Length, null, new MemoryStream(contenido));

    private static string CrearRuta()
    {
        var ruta = Path.Combine(Path.GetTempPath(), $"gradex-evidencia-{Guid.NewGuid():N}");
        Directory.CreateDirectory(ruta);
        return ruta;
    }

    private sealed class Store : ISolicitudStore
    {
        public ResultadoAgregarEvidencia Resultado { get; init; } = new(
            EstadoAgregarEvidencia.Exito,
            new EvidenciaCreada(3, 1, "VISTA_GENERAL", "Vista general", null, "foto.png",
                "image/png", 68, DateTime.UtcNow));
        public string? TipoContenido { get; private set; }
        public long? TamanoBytes { get; private set; }

        public Task<ResultadoAgregarEvidencia> AgregarEvidenciaAsync(
            int idSolicitud, int idSolicitante, int idTipoEvidencia, string claveAlmacenamiento,
            string nombreOriginal, string tipoContenido, long tamanoBytes, string hashSha256,
            string? descripcion, CancellationToken cancellationToken)
        {
            TipoContenido = tipoContenido;
            TamanoBytes = tamanoBytes;
            return Task.FromResult(Resultado);
        }

        public Task<ResultadoCrearSolicitud> CrearBorradorAsync(
            DatosNuevaSolicitud datos, int idSolicitante, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ResultadoEnviarSolicitud> EnviarAsync(
            int idSolicitud, int idSolicitante, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<SolicitudListado> ListarAsync(
            int idSolicitante, int pagina, int tamanoPagina, string? estado,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SolicitudDetalle?> ObtenerDetalleAsync(
            int idSolicitud, int idSolicitante, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ArchivoEvidencia?> ObtenerArchivoAsync(
            int idSolicitud, int idEvidencia, int idSolicitante, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class AlmacenamientoFallido : IEvidenciaStorage
    {
        public Task<string> GuardarAsync(
            Stream contenido, string extension, CancellationToken cancellationToken) =>
            throw new IOException("Fallo sintético.");

        public Task<Stream?> AbrirAsync(string clave, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task EliminarAsync(string clave, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}

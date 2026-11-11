using System.Security.Cryptography;

namespace GradeX.Application.Solicitudes;

public sealed class AgregarEvidencia(ISolicitudStore solicitudes, IEvidenciaStorage almacenamiento)
{
    private const int MaxBytes = 8 * 1024 * 1024;
    private const int MaxDescripcion = 500;
    private static readonly HashSet<string> Extensiones = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    public async Task<ResultadoAgregarEvidencia> EjecutarAsync(
        int idSolicitud,
        int idSolicitante,
        DatosEvidencia datos,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(datos);
        if (idSolicitante <= 0 || datos.IdTipoEvidencia <= 0)
            return new ResultadoAgregarEvidencia(EstadoAgregarEvidencia.ArchivoInvalido);

        if (datos.LongitudDeclarada > MaxBytes || datos.LongitudDeclarada <= 0)
            return new ResultadoAgregarEvidencia(EstadoAgregarEvidencia.ArchivoInvalido);

        var descripcion = NormalizacionSolicitud.Texto(datos.Descripcion);
        if (NormalizacionSolicitud.Excede(descripcion, MaxDescripcion))
            return new ResultadoAgregarEvidencia(EstadoAgregarEvidencia.ArchivoInvalido);

        using var buffer = new MemoryStream();
        var bloque = new byte[81920];
        var total = 0;
        int leidos;
        while ((leidos = await datos.Contenido.ReadAsync(bloque, cancellationToken)) > 0)
        {
            total += leidos;
            if (total > MaxBytes)
                return new ResultadoAgregarEvidencia(EstadoAgregarEvidencia.ArchivoInvalido);
            buffer.Write(bloque, 0, leidos);
        }

        if (total == 0)
            return new ResultadoAgregarEvidencia(EstadoAgregarEvidencia.ArchivoInvalido);

        var bytes = buffer.ToArray();
        var nombreOriginal = Path.GetFileName(datos.NombreOriginal.Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(nombreOriginal))
            return new ResultadoAgregarEvidencia(EstadoAgregarEvidencia.ArchivoInvalido);

        var formato = DetectarFormato(bytes);
        var extension = Path.GetExtension(nombreOriginal);
        if (formato is null || !Extensiones.Contains(extension) ||
            !string.Equals(extension, formato.Extension, StringComparison.OrdinalIgnoreCase) &&
            !(extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) &&
              formato.Extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)))
            return new ResultadoAgregarEvidencia(EstadoAgregarEvidencia.ArchivoInvalido);

        string? clave = null;
        try
        {
            try
            {
                clave = await almacenamiento.GuardarAsync(
                    new MemoryStream(bytes), formato.Extension, cancellationToken);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return new ResultadoAgregarEvidencia(EstadoAgregarEvidencia.AlmacenamientoNoDisponible);
            }

            var hash = Convert.ToHexString(SHA256.HashData(bytes));
            var resultado = await solicitudes.AgregarEvidenciaAsync(
                idSolicitud,
                idSolicitante,
                datos.IdTipoEvidencia,
                clave,
                nombreOriginal,
                formato.TipoContenido,
                bytes.LongLength,
                hash,
                descripcion,
                cancellationToken);

            if (resultado.Estado != EstadoAgregarEvidencia.Exito)
            {
                await EliminarSilenciosoAsync(clave, cancellationToken);
                return resultado;
            }

            if (resultado.ClaveAnterior is not null)
                await EliminarSilenciosoAsync(resultado.ClaveAnterior, cancellationToken);

            return resultado;
        }
        catch
        {
            if (clave is not null)
                await EliminarSilenciosoAsync(clave, cancellationToken);
            throw;
        }
    }

    private async Task EliminarSilenciosoAsync(string clave, CancellationToken cancellationToken)
    {
        try
        {
            await almacenamiento.EliminarAsync(clave, cancellationToken);
        }
        catch
        {
            // El archivo queda huérfano; se registra el fallo en la limpieza operativa.
        }
    }

    private static FormatoImagen? DetectarFormato(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return new FormatoImagen(".jpg", "image/jpeg");
        if (bytes.Length >= 8 &&
            bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
            bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
            return new FormatoImagen(".png", "image/png");
        if (bytes.Length >= 12 &&
            bytes[0] == 'R' && bytes[1] == 'I' && bytes[2] == 'F' && bytes[3] == 'F' &&
            bytes[8] == 'W' && bytes[9] == 'E' && bytes[10] == 'B' && bytes[11] == 'P')
            return new FormatoImagen(".webp", "image/webp");
        return null;
    }

    private sealed record FormatoImagen(string Extension, string TipoContenido);
}

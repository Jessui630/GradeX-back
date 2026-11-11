using GradeX.Application.Productos;

namespace GradeX.Application.Solicitudes;

internal static class NormalizacionSolicitud
{
    internal static readonly IReadOnlyDictionary<string, (string Etiqueta, int Maximo)> Campos =
        new Dictionary<string, (string, int)>(StringComparer.OrdinalIgnoreCase)
        {
            ["NOMBRE"] = ("Nombre del producto", 150),
            ["MODELO"] = ("Modelo", 150),
            ["SKU"] = ("SKU", 80),
            ["NUMERO_SERIE"] = ("Número de serie", 120),
            ["IMEI"] = ("IMEI", 30),
            ["ACCESORIOS"] = ("Accesorios", 500),
            ["DESCRIPCION"] = ("Descripción", 1000),
            ["MARCA"] = ("Marca", 0)
        };

    public static string? Texto(string? valor)
    {
        var limpio = valor?.Trim();
        return string.IsNullOrWhiteSpace(limpio) ? null : limpio;
    }

    public static bool Excede(string? valor, int maximo) =>
        valor is not null && valor.Length > maximo;
}

public sealed class RegistrarSolicitud(
    ICatalogoProductoStore catalogo,
    ISolicitudStore solicitudes)
{
    public async Task<ResultadoCrearSolicitud> EjecutarAsync(
        DatosNuevaSolicitud datos, int idSolicitante, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(datos);
        if (idSolicitante <= 0)
            return new ResultadoCrearSolicitud(EstadoCrearSolicitud.DatosInvalidos);

        var nombre = NormalizacionSolicitud.Texto(datos.Nombre);
        var modelo = NormalizacionSolicitud.Texto(datos.Modelo);
        var sku = NormalizacionSolicitud.Texto(datos.Sku);
        var numeroSerie = NormalizacionSolicitud.Texto(datos.NumeroSerie);
        var imei = NormalizacionSolicitud.Texto(datos.Imei);
        var accesorios = NormalizacionSolicitud.Texto(datos.Accesorios);
        var descripcion = NormalizacionSolicitud.Texto(datos.Descripcion);
        var observaciones = NormalizacionSolicitud.Texto(datos.Observaciones);

        if (nombre is null)
            return new ResultadoCrearSolicitud(EstadoCrearSolicitud.DatosInvalidos, Campo: "nombre");

        var requisitos = await catalogo.ObtenerRequisitosAsync(datos.IdCategoria, cancellationToken);
        if (requisitos is null)
            return new ResultadoCrearSolicitud(EstadoCrearSolicitud.CategoriaInvalida, Campo: "categoria");

        var valores = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["NOMBRE"] = nombre,
            ["MODELO"] = modelo,
            ["SKU"] = sku,
            ["NUMERO_SERIE"] = numeroSerie,
            ["IMEI"] = imei,
            ["ACCESORIOS"] = accesorios,
            ["DESCRIPCION"] = descripcion,
            ["MARCA"] = datos.IdMarca is > 0 ? "definida" : null
        };

        foreach (var requisito in requisitos.Campos)
        {
            if (!NormalizacionSolicitud.Campos.TryGetValue(requisito.Campo, out var definicion))
                return new ResultadoCrearSolicitud(
                    EstadoCrearSolicitud.RequisitosNoConfigurados, Campo: requisito.Campo);

            if (requisito.Obligatorio && valores.GetValueOrDefault(requisito.Campo) is null)
                return new ResultadoCrearSolicitud(
                    EstadoCrearSolicitud.DatosInvalidos, Campo: requisito.Campo.ToLowerInvariant());

            var maximo = requisito.LongitudMaxima is > 0
                ? Math.Min(requisito.LongitudMaxima.Value, definicion.Maximo)
                : definicion.Maximo;
            if (maximo > 0 && NormalizacionSolicitud.Excede(valores.GetValueOrDefault(requisito.Campo), maximo))
                return new ResultadoCrearSolicitud(
                    EstadoCrearSolicitud.DatosInvalidos, Campo: requisito.Campo.ToLowerInvariant());
        }

        if (NormalizacionSolicitud.Excede(nombre, 150) ||
            NormalizacionSolicitud.Excede(modelo, 150) ||
            NormalizacionSolicitud.Excede(sku, 80) ||
            NormalizacionSolicitud.Excede(numeroSerie, 120) ||
            NormalizacionSolicitud.Excede(imei, 30) ||
            NormalizacionSolicitud.Excede(accesorios, 500) ||
            NormalizacionSolicitud.Excede(descripcion, 1000) ||
            NormalizacionSolicitud.Excede(observaciones, 1000))
            return new ResultadoCrearSolicitud(EstadoCrearSolicitud.DatosInvalidos);

        return await solicitudes.CrearBorradorAsync(
            datos with
            {
                Nombre = nombre,
                Modelo = modelo,
                Sku = sku,
                NumeroSerie = numeroSerie,
                Imei = imei,
                Accesorios = accesorios,
                Descripcion = descripcion,
                Observaciones = observaciones
            },
            idSolicitante,
            cancellationToken);
    }
}

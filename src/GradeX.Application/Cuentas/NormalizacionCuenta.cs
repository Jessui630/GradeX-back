using System.Net.Mail;
using System.Text;

namespace GradeX.Application.Cuentas;

internal static class NormalizacionCuenta
{
    public static string? Nombre(string? valor)
    {
        var nombre = valor?.Trim();
        return string.IsNullOrWhiteSpace(nombre) || nombre.Length > 150 ? null : nombre;
    }

    public static string? Correo(string? valor)
    {
        var correo = valor?.Trim();
        if (string.IsNullOrEmpty(correo) || correo.Length > 254 ||
            correo.Any(c => c > 127 || char.IsWhiteSpace(c) || char.IsControl(c)) ||
            !MailAddress.TryCreate(correo, out var direccion) ||
            !string.Equals(direccion.Address, correo, StringComparison.Ordinal))
            return null;

        return correo.ToLowerInvariant();
    }

    public static string? Contrasena(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        try
        {
            var normalizada = valor.Normalize(NormalizationForm.FormC);
            var longitud = normalizada.EnumerateRunes().Count();
            return longitud is < 15 or > 128 || normalizada.EnumerateRunes().Any(Rune.IsControl)
                ? null
                : normalizada;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    public static string? ContrasenaParaVerificacion(string? valor)
    {
        if (valor is null) return null;
        try
        {
            return valor.Normalize(NormalizationForm.FormC);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}

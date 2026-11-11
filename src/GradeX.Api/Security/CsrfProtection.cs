using System.Security.Cryptography;
using System.Text;

namespace GradeX.Api.Security;

public static class CsrfProtection
{
    public const string CookieName = "GradeX.Csrf";
    public const string HeaderName = "X-CSRF-Token";

    public static string CrearToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    public static bool Validar(HttpRequest request)
    {
        if (!request.Cookies.TryGetValue(CookieName, out var cookie) ||
            !request.Headers.TryGetValue(HeaderName, out var header))
            return false;

        var cookieValue = cookie.ToString();
        var headerValue = header.ToString();
        if (string.IsNullOrEmpty(cookieValue) || string.IsNullOrEmpty(headerValue))
            return false;

        var cookieBytes = Encoding.UTF8.GetBytes(cookieValue);
        var headerBytes = Encoding.UTF8.GetBytes(headerValue);
        return cookieBytes.Length == headerBytes.Length &&
            CryptographicOperations.FixedTimeEquals(cookieBytes, headerBytes);
    }
}

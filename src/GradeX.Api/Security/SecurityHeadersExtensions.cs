namespace GradeX.Api.Security;

public static class SecurityHeadersExtensions
{
    private const string StrictTransportSecurity = "max-age=31536000";

    public static IApplicationBuilder UseGradeXSecurityHeaders(
        this IApplicationBuilder app, bool isDevelopment) =>
        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers["X-Content-Type-Options"] = "nosniff";
                headers["X-Frame-Options"] = "DENY";
                headers["Referrer-Policy"] = "no-referrer";
                headers["Permissions-Policy"] =
                    "camera=(self), microphone=(), geolocation=()";
                headers["X-Permitted-Cross-Domain-Policies"] = "none";
                if (!isDevelopment && context.Request.IsHttps)
                    headers["Strict-Transport-Security"] = StrictTransportSecurity;
                return Task.CompletedTask;
            });

            await next();
        });
}

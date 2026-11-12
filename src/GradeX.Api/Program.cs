using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Threading.RateLimiting;
using GradeX.Api.Security;
using GradeX.Application.Cuentas;
using GradeX.Application.Productos;
using GradeX.Application.Sesiones;
using GradeX.Application.Solicitudes;
using GradeX.Infrastructure.Persistence;
using GradeX.Infrastructure.Persistence.Commands;
using GradeX.Infrastructure.Persistence.Queries;
using GradeX.Infrastructure.Persistence.Sessions;
using GradeX.Infrastructure.Security;
using GradeX.Infrastructure.Storage;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration
    .GetConnectionString("GradeX")
    ?? throw new InvalidOperationException(
        "No se encontró la conexión GradeX."
    );

builder.Services.AddDbContext<GradeXDbContext>(
    options => options.UseSqlServer(connectionString)
);

builder.Services.AddHttpClient<IContrasenaComprometida, PwnedPasswords>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(10);
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GradeX", "0.1"));
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
    .RemoveAllLoggers();

builder.Services.AddScoped<IRegistroCuentaStore, RegistroCuentaStore>();
builder.Services.AddScoped<ICredencialesStore, CredencialesStore>();
builder.Services.AddScoped<ICatalogoProductoStore, CatalogoProductoStore>();
builder.Services.AddScoped<ISolicitudStore, SolicitudStore>();
builder.Services.AddSingleton<IContrasenaHasher, ContrasenaHasher>();
builder.Services.AddScoped<RegistrarCuenta>();
builder.Services.AddScoped<IniciarSesion>();
builder.Services.AddScoped<RegistrarSolicitud>();
builder.Services.AddScoped<AgregarEvidencia>();
builder.Services.AddScoped<ISesionStore, SesionStore>();
builder.Services.AddSingleton<IEvidenciaStorage>(_ =>
{
    var ruta = builder.Configuration.GetValue<string>("Almacenamiento:Evidencias:Ruta")
        ?? Path.Combine("App_Data", "evidencias");
    return new LocalEvidenciaStorage(Path.IsPathRooted(ruta)
        ? ruta
        : Path.Combine(builder.Environment.ContentRootPath, ruta));
});

builder.Services.AddAuthentication(SesionConstantes.Esquema)
    .AddScheme<AuthenticationSchemeOptions, SesionAuthenticationHandler>(
        SesionConstantes.Esquema, _ => { });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Solicitante", policy => policy.RequireRole("Solicitante"));
    options.AddPolicy("Especialista", policy => policy.RequireRole("Especialista"));
    options.AddPolicy("Administrador", policy => policy.RequireRole("Administrador"));
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    foreach (var proxy in builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
    {
        if (IPAddress.TryParse(proxy, out var direccion))
            options.KnownProxies.Add(direccion);
    }
});

var origenes = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173"];
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy => policy
        .WithOrigins(origenes)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

builder.Services.AddRateLimiter(options =>
{
    var registroLimite = builder.Configuration.GetValue("RateLimiting:Registro:PermitLimit", 5);
    var registroMinutos = builder.Configuration.GetValue("RateLimiting:Registro:WindowMinutes", 10);
    var loginLimite = builder.Configuration.GetValue("RateLimiting:Login:PermitLimit", 10);
    var loginMinutos = builder.Configuration.GetValue("RateLimiting:Login:WindowMinutes", 10);
    var solicitudesLimite = builder.Configuration.GetValue("RateLimiting:Solicitudes:PermitLimit", 60);
    var solicitudesMinutos = builder.Configuration.GetValue("RateLimiting:Solicitudes:WindowMinutes", 60);
    var globalLimite = builder.Configuration.GetValue("RateLimiting:Global:PermitLimit", 100);
    var globalMinutos = builder.Configuration.GetValue("RateLimiting:Global:WindowMinutes", 60);

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(_ =>
        RateLimitPartition.GetFixedWindowLimiter("instancia", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = globalLimite,
            Window = TimeSpan.FromMinutes(globalMinutos),
            QueueLimit = 0,
            AutoReplenishment = true
        }));

    options.AddPolicy("registro", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "sin-ip",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = registroLimite,
                Window = TimeSpan.FromMinutes(registroMinutos),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "sin-ip",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = loginLimite,
                Window = TimeSpan.FromMinutes(loginMinutos),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("solicitudes", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "sin-ip",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = solicitudesLimite,
                Window = TimeSpan.FromMinutes(solicitudesMinutos),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }

        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(
            new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Demasiadas solicitudes",
                Detail = "Espera un momento antes de volver a intentarlo."
            },
            options: null,
            contentType: "application/problem+json",
            cancellationToken: cancellationToken);
    };
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.UnmappedMemberHandling =
            System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow;
    });
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler(errorApp => errorApp.Run(ApiExceptionHandler.HandleAsync));
if (app.Configuration.GetValue("ForwardedHeaders:Enabled", false))
    app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }

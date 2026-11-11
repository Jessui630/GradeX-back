using GradeX.Application.Cuentas;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace GradeX.Infrastructure.Security;

public sealed class ContrasenaHasher : IContrasenaHasher
{
    private readonly PasswordHasher<object> hasher = new(Options.Create(new PasswordHasherOptions
    {
        CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3,
        IterationCount = 220_000
    }));

    private readonly string dummyHash;

    public ContrasenaHasher()
    {
        dummyHash = hasher.HashPassword(null!, "GradeX verification dummy");
    }

    public string Hash(string contrasena)
    {
        ArgumentNullException.ThrowIfNull(contrasena);
        return hasher.HashPassword(null!, contrasena);
    }

    public bool Verificar(string? passwordHash, string contrasena)
    {
        ArgumentNullException.ThrowIfNull(contrasena);
        var hash = string.IsNullOrEmpty(passwordHash) ? dummyHash : passwordHash;
        try
        {
            return hasher.VerifyHashedPassword(null!, hash, contrasena) != PasswordVerificationResult.Failed;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

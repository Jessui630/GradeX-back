using System.Buffers.Binary;
using GradeX.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace GradeX.Tests;

public class ContrasenaHasherTests
{
    [Fact]
    public void HashDebeUsarIdentityV3ConSalUnicaYSerVerificable()
    {
        const string contrasena = "Frase larga de prueba 456!";
        var hasher = new ContrasenaHasher();
        var primero = hasher.Hash(contrasena);
        var segundo = hasher.Hash(contrasena);
        var bytes = Convert.FromBase64String(primero);
        var verifier = new PasswordHasher<object>(Options.Create(new PasswordHasherOptions
        {
            CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3,
            IterationCount = 220_000
        }));

        Assert.NotEqual(primero, segundo);
        Assert.NotEqual(contrasena, primero);
        Assert.True(primero.Length <= 255);
        Assert.Equal(1, bytes[0]);
        Assert.Equal(220_000u, BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(5, 4)));
        Assert.Equal(PasswordVerificationResult.Success, verifier.VerifyHashedPassword(null!, primero, contrasena));
        Assert.Equal(PasswordVerificationResult.Failed, verifier.VerifyHashedPassword(null!, primero, "contraseña equivocada"));
    }

    [Fact]
    public void VerificarAceptaHashCorrectoYRechazaInvalido()
    {
        const string contrasena = "Frase larga de prueba 456!";
        var hasher = new ContrasenaHasher();
        var hash = hasher.Hash(contrasena);

        Assert.True(hasher.Verificar(hash, contrasena));
        Assert.False(hasher.Verificar(hash, "otra contraseña incorrecta"));
        Assert.False(hasher.Verificar("no-es-un-hash", contrasena));
        Assert.False(hasher.Verificar(null, contrasena));
    }
}

using System.Data;
using GradeX.Application.Cuentas;
using GradeX.Infrastructure.Persistence.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GradeX.Infrastructure.Persistence.Commands;

public sealed class RegistroCuentaStore(GradeXDbContext db, string nombreRol = "Solicitante") : IRegistroCuentaStore
{
    public Task<bool> ExisteCorreoAsync(string correo, CancellationToken cancellationToken) =>
        db.Usuarios.AsNoTracking().AnyAsync(u => u.Correo == correo, cancellationToken);

    public async Task<ResultadoRegistroCuenta> RegistrarAsync(
        string nombreCompleto, string correo, string passwordHash, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var rol = await db.Rols.SingleOrDefaultAsync(r => r.Nombre == nombreRol, cancellationToken);
        if (rol is null || !rol.Activo)
            return new ResultadoRegistroCuenta(EstadoRegistroCuenta.RolNoDisponible);

        var usuario = new Usuario
        {
            NombreCompleto = nombreCompleto,
            Correo = correo,
            PasswordHash = passwordHash,
            Activo = true
        };
        usuario.UsuarioRols.Add(new UsuarioRol { IdRol = rol.IdRol });
        db.Usuarios.Add(usuario);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ResultadoRegistroCuenta(EstadoRegistroCuenta.Exito, usuario.IdUsuario);
        }
        catch (DbUpdateException ex) when (EsCorreoDuplicado(ex))
        {
            db.Entry(usuario.UsuarioRols.Single()).State = EntityState.Detached;
            db.Entry(usuario).State = EntityState.Detached;
            return new ResultadoRegistroCuenta(EstadoRegistroCuenta.CorreoDuplicado);
        }
    }

    private static bool EsCorreoDuplicado(DbUpdateException exception) =>
        exception.InnerException is SqlException sql &&
        sql.Errors.Cast<SqlError>().Any(error =>
            error.Number is 2601 or 2627 &&
            error.Message.Contains("UQ_Usuario_Correo", StringComparison.Ordinal));
}

using System.Data;
using GradeX.Application.Asignaciones;
using GradeX.Infrastructure.Persistence.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GradeX.Infrastructure.Persistence.Commands;

public sealed class AsignacionStore(GradeXDbContext db) : IAsignacionStore
{
    private const string EstadoPendiente = "PENDIENTE_ASIGNACION";
    private const string EstadoAsignada = "ASIGNADA";
    private const string EstadoEspecialistaActivo = "ACTIVO";

    public async Task<ListadoSolicitudesAsignacion> ListarSolicitudesAsync(
        string? estado, int pagina, int tamanoPagina, CancellationToken cancellationToken)
    {
        pagina = Math.Max(1, pagina);
        tamanoPagina = Math.Clamp(tamanoPagina, 1, 50);

        var consulta = db.SolicitudEvaluacions.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(estado))
        {
            var codigo = estado.Trim().ToUpperInvariant();
            consulta = consulta.Where(s => s.IdEstadoSolicitudNavigation.Codigo == codigo);
        }

        var total = await consulta.CountAsync(cancellationToken);
        var filas = await consulta
            .OrderBy(s => s.IdEstadoSolicitudNavigation.OrdenVisual)
            .ThenBy(s => s.FechaSolicitud)
            .ThenBy(s => s.IdSolicitud)
            .Skip((pagina - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .Select(ProyeccionSolicitud())
            .ToListAsync(cancellationToken);

        var conteos = await ContarCompatiblesAsync(filas, cancellationToken);
        return new ListadoSolicitudesAsignacion(
            filas.Select(fila => new SolicitudAsignacionResumen(
                fila.IdSolicitud,
                fila.Folio,
                fila.NombreProducto,
                fila.Categoria,
                fila.Marca,
                fila.EstadoCodigo,
                fila.EstadoNombre,
                fila.FechaSolicitud,
                conteos.GetValueOrDefault(fila.IdSolicitud),
                Asignacion(fila))).ToList(),
            pagina,
            tamanoPagina,
            total);
    }

    public async Task<SolicitudAsignacionDetalle?> ObtenerSolicitudAsync(
        int idSolicitud, CancellationToken cancellationToken)
    {
        var fila = await db.SolicitudEvaluacions
            .AsNoTracking()
            .Where(s => s.IdSolicitud == idSolicitud)
            .Select(ProyeccionSolicitud())
            .SingleOrDefaultAsync(cancellationToken);

        if (fila is null)
            return null;

        var puedeAsignarse = fila.EstadoCodigo == EstadoPendiente && fila.AsignacionId is null;
        IReadOnlyList<EspecialistaCompatible> compatibles = puedeAsignarse
            ? await CargarCompatiblesAsync(
                fila.IdCategoria, fila.IdMarca, null, cancellationToken)
            : Array.Empty<EspecialistaCompatible>();

        return new SolicitudAsignacionDetalle(
            fila.IdSolicitud,
            fila.Folio,
            fila.NombreProducto,
            fila.Modelo,
            fila.IdCategoria,
            fila.Categoria,
            fila.IdMarca,
            fila.Marca,
            fila.EstadoCodigo,
            fila.EstadoNombre,
            fila.FechaSolicitud,
            fila.FechaUltimoCambio,
            puedeAsignarse,
            Asignacion(fila),
            compatibles);
    }

    public async Task<CompatiblesSolicitud?> ObtenerCompatiblesAsync(
        int idSolicitud, CancellationToken cancellationToken)
    {
        var fila = await db.SolicitudEvaluacions
            .AsNoTracking()
            .Where(s => s.IdSolicitud == idSolicitud)
            .Select(ProyeccionSolicitud())
            .SingleOrDefaultAsync(cancellationToken);

        if (fila is null)
            return null;

        var puedeAsignarse = fila.EstadoCodigo == EstadoPendiente && fila.AsignacionId is null;
        IReadOnlyList<EspecialistaCompatible> compatibles = puedeAsignarse
            ? await CargarCompatiblesAsync(
                fila.IdCategoria, fila.IdMarca, null, cancellationToken)
            : Array.Empty<EspecialistaCompatible>();

        return new CompatiblesSolicitud(
            fila.IdSolicitud,
            fila.Folio,
            fila.Categoria,
            fila.Marca,
            fila.EstadoCodigo,
            puedeAsignarse,
            Asignacion(fila),
            compatibles);
    }

    public async Task<ResultadoAsignarEspecialista> AsignarAsync(
        DatosAsignacion datos, CancellationToken cancellationToken)
    {
        var solicitud = await db.SolicitudEvaluacions
            .AsNoTracking()
            .Where(s => s.IdSolicitud == datos.IdSolicitud)
            .Select(s => new
            {
                s.IdSolicitud,
                s.Folio,
                s.IdEstadoSolicitud,
                EstadoCodigo = s.IdEstadoSolicitudNavigation.Codigo,
                EstadoNombre = s.IdEstadoSolicitudNavigation.Nombre,
                IdCategoria = s.IdProductoNavigation.IdCategoria,
                IdMarca = s.IdProductoNavigation.IdMarca,
                AsignacionId = s.AsignacionEspecialistum == null
                    ? (int?)null
                    : s.AsignacionEspecialistum.IdAsignacion
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (solicitud is null)
            return new ResultadoAsignarEspecialista(EstadoAsignarEspecialista.SolicitudNoEncontrada);

        if (solicitud.AsignacionId is not null)
            return await ResultadoYaAsignadaAsync(datos.IdSolicitud, cancellationToken);

        if (solicitud.EstadoCodigo != EstadoPendiente)
            return new ResultadoAsignarEspecialista(
                EstadoAsignarEspecialista.SolicitudNoAsignable,
                EstadoSolicitudCodigo: solicitud.EstadoCodigo,
                EstadoSolicitudNombre: solicitud.EstadoNombre);

        var estadoAsignada = await db.EstadoSolicituds
            .AsNoTracking()
            .SingleOrDefaultAsync(e => e.Codigo == EstadoAsignada, cancellationToken);
        if (estadoAsignada is null)
            return new ResultadoAsignarEspecialista(EstadoAsignarEspecialista.EstadoNoConfigurado);

        await using var transaccion = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        try
        {
            var ahora = DateTime.UtcNow;
            var actualizadas = await db.SolicitudEvaluacions
                .Where(s => s.IdSolicitud == datos.IdSolicitud &&
                    s.IdEstadoSolicitud == solicitud.IdEstadoSolicitud)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(s => s.IdEstadoSolicitud, estadoAsignada.IdEstadoSolicitud)
                    .SetProperty(s => s.FechaUltimoCambio, ahora), cancellationToken);

            if (actualizadas == 0)
            {
                await transaccion.RollbackAsync(cancellationToken);
                return await ResultadoConflictoAsync(datos.IdSolicitud, cancellationToken);
            }

            var compatibles = await CargarCompatiblesAsync(
                solicitud.IdCategoria, solicitud.IdMarca, datos.IdEspecialista, cancellationToken);
            var especialista = compatibles.SingleOrDefault();
            if (especialista is null)
            {
                await transaccion.RollbackAsync(cancellationToken);
                var existe = await db.Especialista
                    .AsNoTracking()
                    .AnyAsync(e => e.IdEspecialista == datos.IdEspecialista, cancellationToken);
                return new ResultadoAsignarEspecialista(
                    existe
                        ? EstadoAsignarEspecialista.SinCompatibilidad
                        : EstadoAsignarEspecialista.EspecialistaNoEncontrado,
                    EstadoSolicitudCodigo: solicitud.EstadoCodigo,
                    EstadoSolicitudNombre: solicitud.EstadoNombre);
            }

            var asignacion = new AsignacionEspecialistum
            {
                IdSolicitud = datos.IdSolicitud,
                IdEspecialista = datos.IdEspecialista,
                FechaAsignacion = ahora,
                Activa = true
            };
            db.AsignacionEspecialista.Add(asignacion);
            await db.SaveChangesAsync(cancellationToken);
            await transaccion.CommitAsync(cancellationToken);

            return new ResultadoAsignarEspecialista(
                EstadoAsignarEspecialista.Exito,
                new AsignacionCreada(
                    asignacion.IdAsignacion,
                    solicitud.IdSolicitud,
                    solicitud.Folio,
                    asignacion.IdEspecialista,
                    especialista.Nombre,
                    asignacion.FechaAsignacion,
                    estadoAsignada.Codigo,
                    estadoAsignada.Nombre));
        }
        catch (DbUpdateException ex) when (EsDuplicado(ex))
        {
            await RevertirSilenciosoAsync(transaccion);
            db.ChangeTracker.Clear();
            return await ResultadoYaAsignadaAsync(datos.IdSolicitud, cancellationToken);
        }
        catch (Exception ex) when (EsConflictoConcurrente(ex))
        {
            await RevertirSilenciosoAsync(transaccion);
            db.ChangeTracker.Clear();
            return new ResultadoAsignarEspecialista(EstadoAsignarEspecialista.ConflictoConcurrente);
        }
    }

    private async Task<IReadOnlyList<EspecialistaCompatible>> CargarCompatiblesAsync(
        int idCategoria, int? idMarca, int? idEspecialista, CancellationToken cancellationToken)
    {
        var consulta = db.EspecialidadEspecialista
            .AsNoTracking()
            .Where(e => e.Activa &&
                e.IdCategoria == idCategoria &&
                e.IdCategoriaNavigation.Activa &&
                e.IdEspecialistaNavigation.Estado == EstadoEspecialistaActivo &&
                e.IdEspecialistaNavigation.IdUsuarioNavigation.Activo &&
                (e.IdMarca == null ||
                    (e.CategoriaMarca != null &&
                     e.CategoriaMarca.Activa &&
                     e.CategoriaMarca.IdMarcaNavigation.Activa)) &&
                (idMarca == null
                    ? e.IdMarca == null
                    : e.IdMarca == null || e.IdMarca == idMarca));

        if (idEspecialista is not null)
            consulta = consulta.Where(e => e.IdEspecialista == idEspecialista);

        var filas = await consulta
            .Select(e => new
            {
                e.IdEspecialista,
                Nombre = e.IdEspecialistaNavigation.IdUsuarioNavigation.NombreCompleto,
                e.IdEspecialistaNavigation.AniosExperiencia,
                e.Nivel,
                e.IdCategoria,
                Categoria = e.IdCategoriaNavigation.Nombre,
                e.IdMarca,
                Marca = e.CategoriaMarca != null ? e.CategoriaMarca.IdMarcaNavigation.Nombre : null,
                CargaActiva = e.IdEspecialistaNavigation.AsignacionEspecialista.Count(a => a.Activa)
            })
            .ToListAsync(cancellationToken);

        return filas
            .GroupBy(fila => fila.IdEspecialista)
            .Select(grupo =>
            {
                var preferida = grupo
                    .OrderByDescending(fila => fila.IdMarca is not null)
                    .ThenByDescending(fila => fila.AniosExperiencia ?? 0)
                    .First();
                return new EspecialistaCompatible(
                    preferida.IdEspecialista,
                    preferida.Nombre,
                    preferida.AniosExperiencia,
                    preferida.Nivel,
                    preferida.IdCategoria,
                    preferida.Categoria,
                    preferida.IdMarca,
                    preferida.Marca,
                    preferida.CargaActiva,
                    preferida.IdMarca is not null);
            })
            .OrderByDescending(especialista => especialista.CoincideMarca)
            .ThenBy(especialista => especialista.CargaActiva)
            .ThenByDescending(especialista => especialista.AniosExperiencia ?? 0)
            .ThenBy(especialista => especialista.Nombre, StringComparer.OrdinalIgnoreCase)
            .ThenBy(especialista => especialista.IdEspecialista)
            .ToList();
    }

    private async Task<Dictionary<int, int>> ContarCompatiblesAsync(
        IReadOnlyList<SolicitudFila> filas, CancellationToken cancellationToken)
    {
        if (filas.Count == 0)
            return new Dictionary<int, int>();

        var categorias = filas.Select(fila => fila.IdCategoria).Distinct().ToList();
        var areas = await db.EspecialidadEspecialista
            .AsNoTracking()
            .Where(e => e.Activa &&
                categorias.Contains(e.IdCategoria) &&
                e.IdCategoriaNavigation.Activa &&
                e.IdEspecialistaNavigation.Estado == EstadoEspecialistaActivo &&
                e.IdEspecialistaNavigation.IdUsuarioNavigation.Activo &&
                (e.IdMarca == null ||
                    (e.CategoriaMarca != null &&
                     e.CategoriaMarca.Activa &&
                     e.CategoriaMarca.IdMarcaNavigation.Activa)))
            .Select(e => new { e.IdEspecialista, e.IdCategoria, e.IdMarca })
            .ToListAsync(cancellationToken);

        return filas.ToDictionary(
            fila => fila.IdSolicitud,
            fila => areas
                .Where(area => CompatibilidadEspecialista.EsCompatible(
                    fila.IdCategoria, fila.IdMarca, area.IdCategoria, area.IdMarca))
                .Select(area => area.IdEspecialista)
                .Distinct()
                .Count());
    }

    private async Task<ResultadoAsignarEspecialista> ResultadoYaAsignadaAsync(
        int idSolicitud, CancellationToken cancellationToken)
    {
        var actual = await db.AsignacionEspecialista
            .AsNoTracking()
            .Where(a => a.IdSolicitud == idSolicitud && a.Activa)
            .Select(a => new AsignacionActual(
                a.IdAsignacion,
                a.IdEspecialista,
                a.IdEspecialistaNavigation.IdUsuarioNavigation.NombreCompleto,
                a.FechaAsignacion,
                a.Activa))
            .SingleOrDefaultAsync(cancellationToken);

        return new ResultadoAsignarEspecialista(
            EstadoAsignarEspecialista.YaAsignada,
            AsignacionExistente: actual);
    }

    private async Task<ResultadoAsignarEspecialista> ResultadoConflictoAsync(
        int idSolicitud, CancellationToken cancellationToken)
    {
        var estado = await db.SolicitudEvaluacions
            .AsNoTracking()
            .Where(s => s.IdSolicitud == idSolicitud)
            .Select(s => new
            {
                Codigo = s.IdEstadoSolicitudNavigation.Codigo,
                Nombre = s.IdEstadoSolicitudNavigation.Nombre,
                AsignacionId = s.AsignacionEspecialistum == null
                    ? (int?)null
                    : s.AsignacionEspecialistum.IdAsignacion
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (estado?.AsignacionId is not null)
            return await ResultadoYaAsignadaAsync(idSolicitud, cancellationToken);

        return estado is null
            ? new ResultadoAsignarEspecialista(EstadoAsignarEspecialista.SolicitudNoEncontrada)
            : new ResultadoAsignarEspecialista(
                EstadoAsignarEspecialista.SolicitudNoAsignable,
                EstadoSolicitudCodigo: estado.Codigo,
                EstadoSolicitudNombre: estado.Nombre);
    }

    private static System.Linq.Expressions.Expression<Func<SolicitudEvaluacion, SolicitudFila>>
        ProyeccionSolicitud() =>
        s => new SolicitudFila
        {
            IdSolicitud = s.IdSolicitud,
            Folio = s.Folio,
            NombreProducto = s.IdProductoNavigation.Nombre,
            Modelo = s.IdProductoNavigation.Modelo,
            IdCategoria = s.IdProductoNavigation.IdCategoria,
            Categoria = s.IdProductoNavigation.IdCategoriaNavigation.Nombre,
            IdMarca = s.IdProductoNavigation.IdMarca,
            Marca = s.IdProductoNavigation.IdMarcaNavigation != null
                ? s.IdProductoNavigation.IdMarcaNavigation.Nombre
                : null,
            EstadoCodigo = s.IdEstadoSolicitudNavigation.Codigo,
            EstadoNombre = s.IdEstadoSolicitudNavigation.Nombre,
            FechaSolicitud = s.FechaSolicitud,
            FechaUltimoCambio = s.FechaUltimoCambio,
            AsignacionId = s.AsignacionEspecialistum == null
                ? null
                : s.AsignacionEspecialistum.IdAsignacion,
            AsignacionEspecialistaId = s.AsignacionEspecialistum == null
                ? null
                : s.AsignacionEspecialistum.IdEspecialista,
            AsignacionEspecialista = s.AsignacionEspecialistum == null
                ? null
                : s.AsignacionEspecialistum.IdEspecialistaNavigation.IdUsuarioNavigation.NombreCompleto,
            AsignacionFecha = s.AsignacionEspecialistum == null
                ? null
                : s.AsignacionEspecialistum.FechaAsignacion,
            AsignacionActiva = s.AsignacionEspecialistum != null &&
                s.AsignacionEspecialistum.Activa
        };

    private static AsignacionActual? Asignacion(SolicitudFila fila) =>
        fila.AsignacionId is null || fila.AsignacionEspecialistaId is null ||
        fila.AsignacionEspecialista is null || fila.AsignacionFecha is null
            ? null
            : new AsignacionActual(
                fila.AsignacionId.Value,
                fila.AsignacionEspecialistaId.Value,
                fila.AsignacionEspecialista,
                fila.AsignacionFecha.Value,
                fila.AsignacionActiva);

    private static async Task RevertirSilenciosoAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaccion)
    {
        try
        {
            await transaccion.RollbackAsync(CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            // La transacción ya fue revertida o cerrada por el proveedor.
        }
        catch (SqlException)
        {
            // La conexión ya no permite revertir; el contexto se descarta por el llamador.
        }
    }

    private static bool EsDuplicado(DbUpdateException exception) =>
        exception.InnerException is SqlException sql &&
        sql.Errors.Cast<SqlError>().Any(error => error.Number is 2601 or 2627);

    private static bool EsConflictoConcurrente(Exception exception) =>
        (exception is SqlException sql && sql.Errors.Cast<SqlError>().Any(EsDeadlock)) ||
        (exception is DbUpdateException { InnerException: SqlException inner } &&
         inner.Errors.Cast<SqlError>().Any(EsDeadlock));

    private static bool EsDeadlock(SqlError error) => error.Number == 1205;

    private sealed class SolicitudFila
    {
        public int IdSolicitud { get; init; }
        public string Folio { get; init; } = null!;
        public string NombreProducto { get; init; } = null!;
        public string? Modelo { get; init; }
        public int IdCategoria { get; init; }
        public string Categoria { get; init; } = null!;
        public int? IdMarca { get; init; }
        public string? Marca { get; init; }
        public string EstadoCodigo { get; init; } = null!;
        public string EstadoNombre { get; init; } = null!;
        public DateTime FechaSolicitud { get; init; }
        public DateTime FechaUltimoCambio { get; init; }
        public int? AsignacionId { get; init; }
        public int? AsignacionEspecialistaId { get; init; }
        public string? AsignacionEspecialista { get; init; }
        public DateTime? AsignacionFecha { get; init; }
        public bool AsignacionActiva { get; init; }
    }
}

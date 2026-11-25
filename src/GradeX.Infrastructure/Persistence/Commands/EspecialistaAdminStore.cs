using GradeX.Application.Administracion;
using GradeX.Application.Workflow;
using GradeX.Infrastructure.Persistence;
using GradeX.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace GradeX.Infrastructure.Persistence.Commands;

public sealed class EspecialistaAdminStore(
    GradeXDbContext db,
    NotificacionWriter notificaciones) : IEspecialistaAdminStore
{
    public async Task<IReadOnlyList<EspecialistaAdmin>> ListarAsync(
        CancellationToken cancellationToken)
    {
        var especialistas = await db.Especialista
            .AsNoTracking()
            .Include(e => e.IdUsuarioNavigation)
            .Include(e => e.EspecialidadEspecialista)
                .ThenInclude(e => e.IdCategoriaNavigation)
            .Include(e => e.EspecialidadEspecialista)
                .ThenInclude(e => e.CategoriaMarca)
                .ThenInclude(cm => cm!.IdMarcaNavigation)
            .OrderBy(e => e.IdUsuarioNavigation.NombreCompleto)
            .ToListAsync(cancellationToken);

        var cargas = await db.AsignacionEspecialista
            .AsNoTracking()
            .Where(a => a.Activa &&
                a.IdSolicitudNavigation.IdEstadoSolicitudNavigation.Codigo !=
                    FlujoGradeX.EstadosSolicitud.Finalizada)
            .GroupBy(a => a.IdEspecialista)
            .Select(g => new { g.Key, Total = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Total, cancellationToken);

        return especialistas.Select(e => Mapear(e, cargas.GetValueOrDefault(e.IdEspecialista)))
            .ToList();
    }

    public async Task<IReadOnlyList<UsuarioCandidatoEspecialista>> BuscarUsuariosAsync(
        string? buscar, CancellationToken cancellationToken)
    {
        var consulta = db.Usuarios.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var filtro = buscar.Trim();
            consulta = consulta.Where(u =>
                u.NombreCompleto.Contains(filtro) || u.Correo.Contains(filtro));
        }

        return await consulta
            .OrderBy(u => u.NombreCompleto)
            .Take(20)
            .Select(u => new UsuarioCandidatoEspecialista(
                u.IdUsuario,
                u.NombreCompleto,
                u.Correo,
                u.Activo,
                u.Especialistum != null))
            .ToListAsync(cancellationToken);
    }

    public async Task<ResultadoAdmin<EspecialistaAdmin>> CrearAsync(
        DatosEspecialistaAdmin datos, CancellationToken cancellationToken)
    {
        var validacion = await ValidarAsync(datos, null, cancellationToken);
        if (validacion is not null)
            return validacion;

        var usuario = await db.Usuarios
            .SingleOrDefaultAsync(u => u.IdUsuario == datos.IdUsuario, cancellationToken);
        if (usuario is null)
            return new ResultadoAdmin<EspecialistaAdmin>(
                EstadoOperacionAdmin.NoEncontrado, Campo: "idUsuario",
                Detalle: "El usuario indicado no existe.");
        if (!usuario.Activo)
            return new ResultadoAdmin<EspecialistaAdmin>(
                EstadoOperacionAdmin.DatosInvalidos, Campo: "idUsuario",
                Detalle: "El usuario debe estar activo para acreditarse como especialista.");

        var existente = await db.Especialista
            .AsNoTracking()
            .AnyAsync(e => e.IdUsuario == datos.IdUsuario, cancellationToken);
        if (existente)
            return new ResultadoAdmin<EspecialistaAdmin>(
                EstadoOperacionAdmin.Conflicto, Campo: "idUsuario",
                Detalle: "El usuario ya tiene un perfil de especialista.");

        var rol = await db.Rols
            .SingleOrDefaultAsync(r => r.Nombre == "Especialista" && r.Activo,
                cancellationToken);
        if (rol is null)
            return new ResultadoAdmin<EspecialistaAdmin>(
                EstadoOperacionAdmin.EstadoNoConfigurado,
                Detalle: "El rol Especialista no está configurado.");

        await using var transaccion = await db.Database.BeginTransactionAsync(cancellationToken);
        var especialista = new Especialistum
        {
            IdUsuario = datos.IdUsuario,
            Estado = string.IsNullOrWhiteSpace(datos.Estado)
                ? FlujoGradeX.EstadosEspecialista.Pendiente
                : datos.Estado,
            AniosExperiencia = datos.AniosExperiencia,
            Biografia = Limpiar(datos.Biografia),
            FechaAlta = DateTime.UtcNow
        };
        db.Especialista.Add(especialista);

        var tieneRol = await db.UsuarioRols
            .AnyAsync(ur => ur.IdUsuario == datos.IdUsuario && ur.IdRol == rol.IdRol,
                cancellationToken);
        if (!tieneRol)
            db.UsuarioRols.Add(new UsuarioRol
            {
                IdUsuario = datos.IdUsuario,
                IdRol = rol.IdRol,
                FechaAsignacion = DateTime.UtcNow
            });

        await db.SaveChangesAsync(cancellationToken);
        await AgregarEspecialidadesAsync(
            especialista.IdEspecialista, datos.Especialidades, cancellationToken);
        await notificaciones.CrearAsync(
            datos.IdUsuario,
            null,
            FlujoGradeX.TiposNotificacion.CambioEstado,
            "Perfil de especialista",
            "Tu perfil de especialista fue creado. El administrador revisará su estado y áreas.",
            cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaccion.CommitAsync(cancellationToken);

        var creado = await ObtenerAsync(especialista.IdEspecialista, cancellationToken);
        return new ResultadoAdmin<EspecialistaAdmin>(
            EstadoOperacionAdmin.Exito, creado);
    }

    public async Task<ResultadoAdmin<EspecialistaAdmin>> ActualizarAsync(
        int idEspecialista, DatosEspecialistaAdmin datos,
        CancellationToken cancellationToken)
    {
        var especialista = await db.Especialista
            .Include(e => e.IdUsuarioNavigation)
            .Include(e => e.EspecialidadEspecialista)
            .SingleOrDefaultAsync(e => e.IdEspecialista == idEspecialista, cancellationToken);
        if (especialista is null)
            return new ResultadoAdmin<EspecialistaAdmin>(EstadoOperacionAdmin.NoEncontrado);
        if (datos.IdUsuario != especialista.IdUsuario)
            return new ResultadoAdmin<EspecialistaAdmin>(
                EstadoOperacionAdmin.DatosInvalidos, Campo: "idUsuario",
                Detalle: "El usuario del perfil de especialista no se puede reasignar.");

        var validacion = await ValidarAsync(datos, idEspecialista, cancellationToken);
        if (validacion is not null)
            return validacion;

        especialista.Estado = string.IsNullOrWhiteSpace(datos.Estado)
            ? especialista.Estado
            : datos.Estado;
        especialista.AniosExperiencia = datos.AniosExperiencia;
        especialista.Biografia = Limpiar(datos.Biografia);

        var deseadas = datos.Especialidades
            .Select(e => (e.IdCategoria, e.IdMarca, e.Nivel ?? FlujoGradeX.NivelesEspecialista.General))
            .ToList();
        foreach (var actual in especialista.EspecialidadEspecialista)
        {
            var coincide = deseadas.Any(d =>
                d.IdCategoria == actual.IdCategoria && d.IdMarca == actual.IdMarca);
            actual.Activa = coincide;
        }
        foreach (var deseada in deseadas)
        {
            var actual = especialista.EspecialidadEspecialista.FirstOrDefault(e =>
                e.IdCategoria == deseada.IdCategoria && e.IdMarca == deseada.IdMarca);
            if (actual is null)
            {
                db.EspecialidadEspecialista.Add(new EspecialidadEspecialistum
                {
                    IdEspecialista = idEspecialista,
                    IdCategoria = deseada.IdCategoria,
                    IdMarca = deseada.IdMarca,
                    Nivel = deseada.Item3,
                    Activa = true,
                    FechaRegistro = DateTime.UtcNow
                });
            }
            else
            {
                actual.Nivel = deseada.Item3;
                actual.Activa = true;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        var actualizado = await ObtenerAsync(idEspecialista, cancellationToken);
        return new ResultadoAdmin<EspecialistaAdmin>(
            EstadoOperacionAdmin.Exito, actualizado);
    }

    private async Task<EspecialistaAdmin?> ObtenerAsync(
        int idEspecialista, CancellationToken cancellationToken)
    {
        var especialista = await db.Especialista
            .AsNoTracking()
            .Include(e => e.IdUsuarioNavigation)
            .Include(e => e.EspecialidadEspecialista)
                .ThenInclude(e => e.IdCategoriaNavigation)
            .Include(e => e.EspecialidadEspecialista)
                .ThenInclude(e => e.CategoriaMarca)
                .ThenInclude(cm => cm!.IdMarcaNavigation)
            .SingleOrDefaultAsync(e => e.IdEspecialista == idEspecialista, cancellationToken);
        if (especialista is null)
            return null;
        var carga = await db.AsignacionEspecialista
            .AsNoTracking()
            .CountAsync(a => a.IdEspecialista == idEspecialista && a.Activa &&
                a.IdSolicitudNavigation.IdEstadoSolicitudNavigation.Codigo !=
                    FlujoGradeX.EstadosSolicitud.Finalizada, cancellationToken);
        return Mapear(especialista, carga);
    }

    private async Task<ResultadoAdmin<EspecialistaAdmin>?> ValidarAsync(
        DatosEspecialistaAdmin datos, int? idEspecialista,
        CancellationToken cancellationToken)
    {
        if (datos.IdUsuario <= 0)
            return new ResultadoAdmin<EspecialistaAdmin>(
                EstadoOperacionAdmin.DatosInvalidos, Campo: "idUsuario",
                Detalle: "Selecciona un usuario válido.");
        if (datos.AniosExperiencia is < 0)
            return new ResultadoAdmin<EspecialistaAdmin>(
                EstadoOperacionAdmin.DatosInvalidos, Campo: "aniosExperiencia",
                Detalle: "Los años de experiencia no pueden ser negativos.");
        if (datos.Biografia?.Trim().Length > 600)
            return new ResultadoAdmin<EspecialistaAdmin>(
                EstadoOperacionAdmin.DatosInvalidos, Campo: "biografia",
                Detalle: "La biografía admite hasta 600 caracteres.");
        if (!string.IsNullOrWhiteSpace(datos.Estado) &&
            !FlujoGradeX.EstadosEspecialista.Todos.Contains(datos.Estado))
            return new ResultadoAdmin<EspecialistaAdmin>(
                EstadoOperacionAdmin.DatosInvalidos, Campo: "estado",
                Detalle: "El estado del especialista no es válido.");
        if (datos.Especialidades.Count == 0)
            return new ResultadoAdmin<EspecialistaAdmin>(
                EstadoOperacionAdmin.DatosInvalidos, Campo: "especialidades",
                Detalle: "Configura al menos un área de especialidad.");

        var claves = new HashSet<(int, int?)>();
        foreach (var especialidad in datos.Especialidades)
        {
            if (!claves.Add((especialidad.IdCategoria, especialidad.IdMarca)))
                return new ResultadoAdmin<EspecialistaAdmin>(
                    EstadoOperacionAdmin.DatosInvalidos, Campo: "especialidades",
                    Detalle: "No se permiten áreas duplicadas.");
            if (!string.IsNullOrWhiteSpace(especialidad.Nivel) &&
                !FlujoGradeX.NivelesEspecialista.Todos.Contains(especialidad.Nivel))
                return new ResultadoAdmin<EspecialistaAdmin>(
                    EstadoOperacionAdmin.DatosInvalidos, Campo: "especialidades",
                    Detalle: "El nivel de especialidad no es válido.");
        }

        var categorias = datos.Especialidades
            .Select(e => e.IdCategoria)
            .Distinct()
            .ToList();
        var existentes = await db.Categoria
            .AsNoTracking()
            .Where(c => categorias.Contains(c.IdCategoria) && c.Activa)
            .Select(c => c.IdCategoria)
            .ToListAsync(cancellationToken);
        if (existentes.Count != categorias.Count)
            return new ResultadoAdmin<EspecialistaAdmin>(
                EstadoOperacionAdmin.DatosInvalidos, Campo: "especialidades",
                Detalle: "Una de las categorías no existe o está inactiva.");

        var marcas = datos.Especialidades
            .Where(e => e.IdMarca is not null)
            .Select(e => new { e.IdCategoria, IdMarca = e.IdMarca!.Value })
            .ToList();
        foreach (var marca in marcas)
        {
            var valida = await db.CategoriaMarcas
                .AsNoTracking()
                .AnyAsync(cm => cm.IdCategoria == marca.IdCategoria &&
                    cm.IdMarca == marca.IdMarca && cm.Activa &&
                    cm.IdMarcaNavigation.Activa, cancellationToken);
            if (!valida)
                return new ResultadoAdmin<EspecialistaAdmin>(
                    EstadoOperacionAdmin.DatosInvalidos, Campo: "especialidades",
                    Detalle: "Una de las marcas no pertenece a la categoría o está inactiva.");
        }

        if (idEspecialista is not null)
        {
            var existe = await db.Especialista
                .AsNoTracking()
                .AnyAsync(e => e.IdEspecialista == idEspecialista, cancellationToken);
            if (!existe)
                return new ResultadoAdmin<EspecialistaAdmin>(
                    EstadoOperacionAdmin.NoEncontrado);
        }
        return null;
    }

    private async Task AgregarEspecialidadesAsync(
        int idEspecialista, IReadOnlyList<DatosEspecialidadAdmin> especialidades,
        CancellationToken cancellationToken)
    {
        db.EspecialidadEspecialista.AddRange(especialidades.Select(e =>
            new EspecialidadEspecialistum
            {
                IdEspecialista = idEspecialista,
                IdCategoria = e.IdCategoria,
                IdMarca = e.IdMarca,
                Nivel = string.IsNullOrWhiteSpace(e.Nivel)
                    ? FlujoGradeX.NivelesEspecialista.General
                    : e.Nivel,
                Activa = true,
                FechaRegistro = DateTime.UtcNow
            }));
        await db.SaveChangesAsync(cancellationToken);
    }

    private static EspecialistaAdmin Mapear(Especialistum especialista, int carga) =>
        new(
            especialista.IdEspecialista,
            especialista.IdUsuario,
            especialista.IdUsuarioNavigation.NombreCompleto,
            especialista.IdUsuarioNavigation.Correo,
            especialista.Estado,
            especialista.AniosExperiencia,
            especialista.Biografia,
            especialista.FechaAlta,
            carga,
            especialista.EspecialidadEspecialista
                .OrderBy(e => e.IdCategoriaNavigation.Nombre)
                .ThenBy(e => e.CategoriaMarca?.IdMarcaNavigation.Nombre)
                .Select(e => new EspecialidadAdmin(
                    e.IdCategoria,
                    e.IdCategoriaNavigation.Nombre,
                    e.IdMarca,
                    e.CategoriaMarca?.IdMarcaNavigation.Nombre,
                    e.Nivel,
                    e.Activa))
                .ToList());

    private static string? Limpiar(string? valor)
    {
        var limpio = valor?.Trim();
        return string.IsNullOrWhiteSpace(limpio) ? null : limpio;
    }
}

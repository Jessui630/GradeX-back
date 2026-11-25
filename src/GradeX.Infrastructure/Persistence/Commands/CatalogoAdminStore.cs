using GradeX.Application.Administracion;
using GradeX.Application.Workflow;
using GradeX.Infrastructure.Persistence;
using GradeX.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace GradeX.Infrastructure.Persistence.Commands;

public sealed class CatalogoAdminStore(GradeXDbContext db) : ICatalogoAdminStore
{
    public async Task<IReadOnlyList<CategoriaAdmin>> ListarCategoriasAsync(
        CancellationToken cancellationToken) =>
        await db.Categoria
            .AsNoTracking()
            .OrderBy(c => c.Nombre)
            .Select(c => new CategoriaAdmin(
                c.IdCategoria,
                c.Nombre,
                c.Descripcion,
                c.Activa,
                c.Productos.Count,
                c.CategoriaMarcas.Count))
            .ToListAsync(cancellationToken);

    public async Task<ResultadoAdmin<CategoriaAdmin>> CrearCategoriaAsync(
        DatosCategoriaAdmin datos, CancellationToken cancellationToken)
    {
        var validacion = ValidarCategoria(datos);
        if (validacion is not null)
            return validacion;

        var categoria = new Categorium
        {
            Nombre = datos.Nombre!.Trim(),
            Descripcion = Limpiar(datos.Descripcion),
            Activa = datos.Activa
        };
        db.Categoria.Add(categoria);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return new ResultadoAdmin<CategoriaAdmin>(
                EstadoOperacionAdmin.Conflicto, Campo: "nombre",
                Detalle: "Ya existe una categoría con ese nombre.");
        }

        return new ResultadoAdmin<CategoriaAdmin>(
            EstadoOperacionAdmin.Exito,
            new CategoriaAdmin(categoria.IdCategoria, categoria.Nombre,
                categoria.Descripcion, categoria.Activa, 0, 0));
    }

    public async Task<ResultadoAdmin<CategoriaAdmin>> ActualizarCategoriaAsync(
        int idCategoria, DatosCategoriaAdmin datos, CancellationToken cancellationToken)
    {
        var validacion = ValidarCategoria(datos);
        if (validacion is not null)
            return validacion;

        var categoria = await db.Categoria
            .SingleOrDefaultAsync(c => c.IdCategoria == idCategoria, cancellationToken);
        if (categoria is null)
            return new ResultadoAdmin<CategoriaAdmin>(EstadoOperacionAdmin.NoEncontrado);

        categoria.Nombre = datos.Nombre!.Trim();
        categoria.Descripcion = Limpiar(datos.Descripcion);
        categoria.Activa = datos.Activa;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return new ResultadoAdmin<CategoriaAdmin>(
                EstadoOperacionAdmin.Conflicto, Campo: "nombre",
                Detalle: "Ya existe una categoría con ese nombre.");
        }

        var productos = await db.Productos.CountAsync(
            p => p.IdCategoria == idCategoria, cancellationToken);
        var marcas = await db.CategoriaMarcas.CountAsync(
            cm => cm.IdCategoria == idCategoria, cancellationToken);
        return new ResultadoAdmin<CategoriaAdmin>(
            EstadoOperacionAdmin.Exito,
            new CategoriaAdmin(categoria.IdCategoria, categoria.Nombre,
                categoria.Descripcion, categoria.Activa, productos, marcas));
    }

    public async Task<IReadOnlyList<TipoEvidenciaAdmin>> ListarTiposEvidenciaAsync(
        CancellationToken cancellationToken) =>
        await db.TipoEvidencia
            .AsNoTracking()
            .OrderBy(t => t.Nombre)
            .Select(t => new TipoEvidenciaAdmin(t.IdTipoEvidencia, t.Codigo, t.Nombre))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MarcaAdmin>> ListarMarcasAsync(
        CancellationToken cancellationToken) =>
        await db.Marcas
            .AsNoTracking()
            .OrderBy(m => m.Nombre)
            .Select(m => new MarcaAdmin(
                m.IdMarca, m.Nombre, m.Activa, m.Productos.Count))
            .ToListAsync(cancellationToken);

    public async Task<ResultadoAdmin<MarcaAdmin>> CrearMarcaAsync(
        DatosMarcaAdmin datos, CancellationToken cancellationToken)
    {
        var validacion = ValidarMarca(datos);
        if (validacion is not null)
            return validacion;

        var marca = new Marca
        {
            Nombre = datos.Nombre!.Trim(),
            Activa = datos.Activa
        };
        db.Marcas.Add(marca);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return new ResultadoAdmin<MarcaAdmin>(
                EstadoOperacionAdmin.Conflicto, Campo: "nombre",
                Detalle: "Ya existe una marca con ese nombre.");
        }

        return new ResultadoAdmin<MarcaAdmin>(
            EstadoOperacionAdmin.Exito,
            new MarcaAdmin(marca.IdMarca, marca.Nombre, marca.Activa, 0));
    }

    public async Task<ResultadoAdmin<MarcaAdmin>> ActualizarMarcaAsync(
        int idMarca, DatosMarcaAdmin datos, CancellationToken cancellationToken)
    {
        var validacion = ValidarMarca(datos);
        if (validacion is not null)
            return validacion;

        var marca = await db.Marcas
            .SingleOrDefaultAsync(m => m.IdMarca == idMarca, cancellationToken);
        if (marca is null)
            return new ResultadoAdmin<MarcaAdmin>(EstadoOperacionAdmin.NoEncontrado);

        marca.Nombre = datos.Nombre!.Trim();
        marca.Activa = datos.Activa;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return new ResultadoAdmin<MarcaAdmin>(
                EstadoOperacionAdmin.Conflicto, Campo: "nombre",
                Detalle: "Ya existe una marca con ese nombre.");
        }

        var productos = await db.Productos.CountAsync(
            p => p.IdMarca == idMarca, cancellationToken);
        return new ResultadoAdmin<MarcaAdmin>(
            EstadoOperacionAdmin.Exito,
            new MarcaAdmin(marca.IdMarca, marca.Nombre, marca.Activa, productos));
    }

    public async Task<IReadOnlyList<CategoriaMarcaAdmin>> ListarCategoriaMarcasAsync(
        int idCategoria, CancellationToken cancellationToken) =>
        await db.CategoriaMarcas
            .AsNoTracking()
            .Where(cm => cm.IdCategoria == idCategoria)
            .OrderBy(cm => cm.IdMarcaNavigation.Nombre)
            .Select(cm => new CategoriaMarcaAdmin(
                cm.IdMarca, cm.IdMarcaNavigation.Nombre, cm.Activa))
            .ToListAsync(cancellationToken);

    public async Task<ResultadoAdmin<CategoriaMarcaAdmin>> VincularMarcaAsync(
        int idCategoria, int idMarca, bool activa, CancellationToken cancellationToken)
    {
        var categoria = await db.Categoria
            .AsNoTracking()
            .AnyAsync(c => c.IdCategoria == idCategoria, cancellationToken);
        var marca = await db.Marcas
            .AsNoTracking()
            .SingleOrDefaultAsync(m => m.IdMarca == idMarca, cancellationToken);
        if (!categoria || marca is null)
            return new ResultadoAdmin<CategoriaMarcaAdmin>(
                EstadoOperacionAdmin.NoEncontrado);

        var vinculo = await db.CategoriaMarcas
            .SingleOrDefaultAsync(cm => cm.IdCategoria == idCategoria &&
                cm.IdMarca == idMarca, cancellationToken);
        if (vinculo is null)
        {
            vinculo = new CategoriaMarca
            {
                IdCategoria = idCategoria,
                IdMarca = idMarca,
                Activa = activa
            };
            db.CategoriaMarcas.Add(vinculo);
        }
        else
        {
            vinculo.Activa = activa;
        }

        await db.SaveChangesAsync(cancellationToken);
        return new ResultadoAdmin<CategoriaMarcaAdmin>(
            EstadoOperacionAdmin.Exito,
            new CategoriaMarcaAdmin(marca.IdMarca, marca.Nombre, vinculo.Activa));
    }

    public async Task<RequisitosCategoriaAdmin?> ObtenerRequisitosAsync(
        int idCategoria, CancellationToken cancellationToken)
    {
        var existe = await db.Categoria
            .AsNoTracking()
            .AnyAsync(c => c.IdCategoria == idCategoria, cancellationToken);
        if (!existe)
            return null;

        var campos = await db.CategoriaCampoRequisitos
            .AsNoTracking()
            .Where(r => r.IdCategoria == idCategoria)
            .OrderBy(r => r.Orden)
            .Select(r => new RequisitoCampoAdmin(
                r.Campo, r.Obligatorio, r.LongitudMaxima, r.Orden))
            .ToListAsync(cancellationToken);
        var evidencias = await db.CategoriaEvidenciaRequisitos
            .AsNoTracking()
            .Where(r => r.IdCategoria == idCategoria)
            .OrderBy(r => r.Orden)
            .Select(r => new RequisitoEvidenciaAdmin(
                r.IdTipoEvidencia,
                r.IdTipoEvidenciaNavigation.Codigo,
                r.IdTipoEvidenciaNavigation.Nombre,
                r.Obligatorio,
                r.CantidadMinima,
                r.CantidadMaxima,
                r.Orden))
            .ToListAsync(cancellationToken);
        return new RequisitosCategoriaAdmin(idCategoria, campos, evidencias);
    }

    public async Task<ResultadoAdmin<RequisitosCategoriaAdmin>> ActualizarRequisitosAsync(
        int idCategoria, DatosRequisitosCategoria datos,
        CancellationToken cancellationToken)
    {
        var categoria = await db.Categoria
            .AsNoTracking()
            .AnyAsync(c => c.IdCategoria == idCategoria, cancellationToken);
        if (!categoria)
            return new ResultadoAdmin<RequisitosCategoriaAdmin>(
                EstadoOperacionAdmin.NoEncontrado);

        var campos = datos.Campos ?? [];
        var evidencias = datos.Evidencias ?? [];
        if (campos.Count == 0)
            return new ResultadoAdmin<RequisitosCategoriaAdmin>(
                EstadoOperacionAdmin.DatosInvalidos, Campo: "campos",
                Detalle: "Configura al menos el campo NOMBRE.");
        if (campos.Select(c => c.Campo).Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
            campos.Count)
            return new ResultadoAdmin<RequisitosCategoriaAdmin>(
                EstadoOperacionAdmin.DatosInvalidos, Campo: "campos",
                Detalle: "No se permiten campos duplicados.");
        var nombre = campos.FirstOrDefault(c =>
            string.Equals(c.Campo, "NOMBRE", StringComparison.OrdinalIgnoreCase));
        if (nombre is null || !nombre.Obligatorio)
            return new ResultadoAdmin<RequisitosCategoriaAdmin>(
                EstadoOperacionAdmin.DatosInvalidos, Campo: "campos",
                Detalle: "NOMBRE debe estar presente y ser obligatorio.");
        foreach (var campo in campos)
        {
            if (!FlujoGradeX.CamposProducto.Todos.Contains(
                    campo.Campo, StringComparer.OrdinalIgnoreCase))
                return new ResultadoAdmin<RequisitosCategoriaAdmin>(
                    EstadoOperacionAdmin.DatosInvalidos, Campo: "campos",
                    Detalle: $"El campo {campo.Campo} no está permitido.");
            if (campo.Orden < 1 || campo.LongitudMaxima is <= 0 or > 1000)
                return new ResultadoAdmin<RequisitosCategoriaAdmin>(
                    EstadoOperacionAdmin.DatosInvalidos, Campo: "campos",
                    Detalle: "Orden debe ser positivo y la longitud máxima debe estar entre 1 y 1000.");
        }

        if (evidencias.Count == 0)
            return new ResultadoAdmin<RequisitosCategoriaAdmin>(
                EstadoOperacionAdmin.DatosInvalidos, Campo: "evidencias",
                Detalle: "Configura al menos un tipo de evidencia.");
        if (evidencias.Select(e => e.IdTipoEvidencia).Distinct().Count() != evidencias.Count)
            return new ResultadoAdmin<RequisitosCategoriaAdmin>(
                EstadoOperacionAdmin.DatosInvalidos, Campo: "evidencias",
                Detalle: "No se permiten tipos de evidencia duplicados.");
        var tipos = await db.TipoEvidencia
            .AsNoTracking()
            .Where(t => evidencias.Select(e => e.IdTipoEvidencia).Contains(t.IdTipoEvidencia))
            .ToDictionaryAsync(t => t.IdTipoEvidencia, cancellationToken);
        if (tipos.Count != evidencias.Count)
            return new ResultadoAdmin<RequisitosCategoriaAdmin>(
                EstadoOperacionAdmin.DatosInvalidos, Campo: "evidencias",
                Detalle: "Uno de los tipos de evidencia no existe.");
        foreach (var evidencia in evidencias)
        {
            if (evidencia.CantidadMinima < 0 || evidencia.CantidadMaxima < 1 ||
                evidencia.CantidadMaxima < evidencia.CantidadMinima || evidencia.Orden < 1)
                return new ResultadoAdmin<RequisitosCategoriaAdmin>(
                    EstadoOperacionAdmin.DatosInvalidos, Campo: "evidencias",
                    Detalle: "Las cantidades y el orden de evidencia no son válidos.");
        }
        if (!evidencias.Any(e => e.Obligatorio && e.CantidadMinima > 0))
            return new ResultadoAdmin<RequisitosCategoriaAdmin>(
                EstadoOperacionAdmin.DatosInvalidos, Campo: "evidencias",
                Detalle: "Debe existir al menos una evidencia obligatoria para poder enviar solicitudes.");

        await using var transaccion = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await db.CategoriaCampoRequisitos
                .Where(r => r.IdCategoria == idCategoria)
                .ExecuteDeleteAsync(cancellationToken);
            await db.CategoriaEvidenciaRequisitos
                .Where(r => r.IdCategoria == idCategoria)
                .ExecuteDeleteAsync(cancellationToken);
            db.CategoriaCampoRequisitos.AddRange(campos.Select(c => new CategoriaCampoRequisito
            {
                IdCategoria = idCategoria,
                Campo = c.Campo.Trim().ToUpperInvariant(),
                Obligatorio = c.Obligatorio,
                LongitudMaxima = c.LongitudMaxima,
                Orden = c.Orden
            }));
            db.CategoriaEvidenciaRequisitos.AddRange(evidencias.Select(e => new CategoriaEvidenciaRequisito
            {
                IdCategoria = idCategoria,
                IdTipoEvidencia = e.IdTipoEvidencia,
                Obligatorio = e.Obligatorio,
                CantidadMinima = e.CantidadMinima,
                CantidadMaxima = e.CantidadMaxima,
                Orden = e.Orden
            }));
            await db.SaveChangesAsync(cancellationToken);
            await transaccion.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaccion.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            return new ResultadoAdmin<RequisitosCategoriaAdmin>(
                EstadoOperacionAdmin.Conflicto, Campo: "requisitos",
                Detalle: "No fue posible guardar los requisitos. Revisa los datos.");
        }

        return new ResultadoAdmin<RequisitosCategoriaAdmin>(
            EstadoOperacionAdmin.Exito,
            (await ObtenerRequisitosAsync(idCategoria, cancellationToken))!);
    }

    public async Task<IReadOnlyList<CriterioAdmin>> ListarCriteriosAsync(
        int idCategoria, CancellationToken cancellationToken) =>
        await db.CriterioEvaluacions
            .AsNoTracking()
            .Where(c => c.IdCategoria == idCategoria)
            .OrderBy(c => c.Orden)
            .ThenBy(c => c.Nombre)
            .Select(c => new CriterioAdmin(
                c.IdCriterio, c.Nombre, c.Descripcion, c.TipoDato,
                c.Obligatorio, c.Orden, c.Activo, c.ResultadoCriterios.Count))
            .ToListAsync(cancellationToken);

    public async Task<ResultadoAdmin<CriterioAdmin>> CrearCriterioAsync(
        int idCategoria, DatosCriterioAdmin datos, CancellationToken cancellationToken)
    {
        var validacion = ValidarCriterio(datos);
        if (validacion is not null)
            return validacion;
        var categoria = await db.Categoria
            .AsNoTracking()
            .AnyAsync(c => c.IdCategoria == idCategoria, cancellationToken);
        if (!categoria)
            return new ResultadoAdmin<CriterioAdmin>(EstadoOperacionAdmin.NoEncontrado);

        var criterio = new CriterioEvaluacion
        {
            IdCategoria = idCategoria,
            Nombre = datos.Nombre!.Trim(),
            Descripcion = Limpiar(datos.Descripcion),
            TipoDato = datos.TipoDato!,
            Obligatorio = datos.Obligatorio,
            Orden = datos.Orden,
            Activo = datos.Activo
        };
        db.CriterioEvaluacions.Add(criterio);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return new ResultadoAdmin<CriterioAdmin>(
                EstadoOperacionAdmin.Conflicto, Campo: "nombre",
                Detalle: "Ya existe un criterio con ese nombre en la categoría.");
        }

        return new ResultadoAdmin<CriterioAdmin>(
            EstadoOperacionAdmin.Exito,
            new CriterioAdmin(criterio.IdCriterio, criterio.Nombre,
                criterio.Descripcion, criterio.TipoDato, criterio.Obligatorio,
                criterio.Orden, criterio.Activo, 0));
    }

    public async Task<ResultadoAdmin<CriterioAdmin>> ActualizarCriterioAsync(
        int idCriterio, DatosCriterioAdmin datos, CancellationToken cancellationToken)
    {
        var validacion = ValidarCriterio(datos);
        if (validacion is not null)
            return validacion;
        var criterio = await db.CriterioEvaluacions
            .SingleOrDefaultAsync(c => c.IdCriterio == idCriterio, cancellationToken);
        if (criterio is null)
            return new ResultadoAdmin<CriterioAdmin>(EstadoOperacionAdmin.NoEncontrado);

        var resultados = await db.ResultadoCriterios
            .AsNoTracking()
            .CountAsync(r => r.IdCriterio == idCriterio, cancellationToken);
        if (resultados > 0 &&
            !string.Equals(criterio.TipoDato, datos.TipoDato, StringComparison.Ordinal))
            return new ResultadoAdmin<CriterioAdmin>(
                EstadoOperacionAdmin.Conflicto, Campo: "tipoDato",
                Detalle: "No se puede cambiar el tipo de un criterio que ya tiene resultados. " +
                    "Crea un criterio nuevo para futuras evaluaciones.");

        criterio.Nombre = datos.Nombre!.Trim();
        criterio.Descripcion = Limpiar(datos.Descripcion);
        criterio.TipoDato = datos.TipoDato!;
        criterio.Obligatorio = datos.Obligatorio;
        criterio.Orden = datos.Orden;
        criterio.Activo = datos.Activo;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return new ResultadoAdmin<CriterioAdmin>(
                EstadoOperacionAdmin.Conflicto, Campo: "nombre",
                Detalle: "Ya existe un criterio con ese nombre en la categoría.");
        }

        return new ResultadoAdmin<CriterioAdmin>(
            EstadoOperacionAdmin.Exito,
            new CriterioAdmin(criterio.IdCriterio, criterio.Nombre,
                criterio.Descripcion, criterio.TipoDato, criterio.Obligatorio,
                criterio.Orden, criterio.Activo, resultados));
    }

    private static ResultadoAdmin<CategoriaAdmin>? ValidarCategoria(
        DatosCategoriaAdmin datos)
    {
        var nombre = datos.Nombre?.Trim();
        if (string.IsNullOrWhiteSpace(nombre))
            return new ResultadoAdmin<CategoriaAdmin>(
                EstadoOperacionAdmin.DatosInvalidos, Campo: "nombre",
                Detalle: "El nombre de la categoría es obligatorio.");
        if (nombre.Length > 100)
            return new ResultadoAdmin<CategoriaAdmin>(
                EstadoOperacionAdmin.DatosInvalidos, Campo: "nombre",
                Detalle: "El nombre admite hasta 100 caracteres.");
        if (datos.Descripcion?.Trim().Length > 300)
            return new ResultadoAdmin<CategoriaAdmin>(
                EstadoOperacionAdmin.DatosInvalidos, Campo: "descripcion",
                Detalle: "La descripción admite hasta 300 caracteres.");
        return null;
    }

    private static ResultadoAdmin<MarcaAdmin>? ValidarMarca(DatosMarcaAdmin datos)
    {
        var nombre = datos.Nombre?.Trim();
        if (string.IsNullOrWhiteSpace(nombre))
            return new ResultadoAdmin<MarcaAdmin>(
                EstadoOperacionAdmin.DatosInvalidos, Campo: "nombre",
                Detalle: "El nombre de la marca es obligatorio.");
        if (nombre.Length > 100)
            return new ResultadoAdmin<MarcaAdmin>(
                EstadoOperacionAdmin.DatosInvalidos, Campo: "nombre",
                Detalle: "El nombre admite hasta 100 caracteres.");
        return null;
    }

    private static ResultadoAdmin<CriterioAdmin>? ValidarCriterio(
        DatosCriterioAdmin datos)
    {
        var nombre = datos.Nombre?.Trim();
        if (string.IsNullOrWhiteSpace(nombre))
            return new ResultadoAdmin<CriterioAdmin>(
                EstadoOperacionAdmin.DatosInvalidos, Campo: "nombre",
                Detalle: "El nombre del criterio es obligatorio.");
        if (nombre.Length > 150)
            return new ResultadoAdmin<CriterioAdmin>(
                EstadoOperacionAdmin.DatosInvalidos, Campo: "nombre",
                Detalle: "El nombre admite hasta 150 caracteres.");
        if (datos.Descripcion?.Trim().Length > 500)
            return new ResultadoAdmin<CriterioAdmin>(
                EstadoOperacionAdmin.DatosInvalidos, Campo: "descripcion",
                Detalle: "La descripción admite hasta 500 caracteres.");
        if (datos.TipoDato is null ||
            !FlujoGradeX.TiposCriterio.Todos.Contains(datos.TipoDato))
            return new ResultadoAdmin<CriterioAdmin>(
                EstadoOperacionAdmin.DatosInvalidos, Campo: "tipoDato",
                Detalle: "El tipo de dato no es válido.");
        if (datos.Orden < 1)
            return new ResultadoAdmin<CriterioAdmin>(
                EstadoOperacionAdmin.DatosInvalidos, Campo: "orden",
                Detalle: "El orden debe ser positivo.");
        return null;
    }

    private static string? Limpiar(string? valor)
    {
        var limpio = valor?.Trim();
        return string.IsNullOrWhiteSpace(limpio) ? null : limpio;
    }
}

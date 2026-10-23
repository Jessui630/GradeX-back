using System;
using System.Collections.Generic;
using GradeX.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace GradeX.Infrastructure.Persistence;

public partial class GradeXDbContext : DbContext
{
    public GradeXDbContext(DbContextOptions<GradeXDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AsignacionEspecialistum> AsignacionEspecialista { get; set; }

    public virtual DbSet<CategoriaMarca> CategoriaMarcas { get; set; }

    public virtual DbSet<Categorium> Categoria { get; set; }

    public virtual DbSet<Certificado> Certificados { get; set; }

    public virtual DbSet<CriterioEvaluacion> CriterioEvaluacions { get; set; }

    public virtual DbSet<EspecialidadEspecialistum> EspecialidadEspecialista { get; set; }

    public virtual DbSet<Especialistum> Especialista { get; set; }

    public virtual DbSet<EstadoCertificado> EstadoCertificados { get; set; }

    public virtual DbSet<EstadoSolicitud> EstadoSolicituds { get; set; }

    public virtual DbSet<Evaluacion> Evaluacions { get; set; }

    public virtual DbSet<Evidencium> Evidencia { get; set; }

    public virtual DbSet<Marca> Marcas { get; set; }

    public virtual DbSet<Notificacion> Notificacions { get; set; }

    public virtual DbSet<Producto> Productos { get; set; }

    public virtual DbSet<ResultadoCriterio> ResultadoCriterios { get; set; }

    public virtual DbSet<Rol> Rols { get; set; }

    public virtual DbSet<SolicitudEvaluacion> SolicitudEvaluacions { get; set; }

    public virtual DbSet<TipoEvidencium> TipoEvidencia { get; set; }

    public virtual DbSet<TipoNotificacion> TipoNotificacions { get; set; }

    public virtual DbSet<Usuario> Usuarios { get; set; }

    public virtual DbSet<UsuarioRol> UsuarioRols { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AsignacionEspecialistum>(entity =>
        {
            entity.HasKey(e => e.IdAsignacion);

            entity.HasIndex(e => new { e.IdEspecialista, e.Activa }, "IX_Asignacion_Especialista");

            entity.HasIndex(e => e.IdSolicitud, "UX_Asignacion_SolicitudActiva")
                .IsUnique()
                .HasFilter("([Activa]=(1))");

            entity.Property(e => e.Activa).HasDefaultValue(true, "DF_Asignacion_Activa");
            entity.Property(e => e.FechaAsignacion)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysdatetime())", "DF_Asignacion_Fecha");
            entity.Property(e => e.FechaFin).HasPrecision(0);
            entity.Property(e => e.MotivoFin).HasMaxLength(300);

            entity.HasOne(d => d.IdEspecialistaNavigation).WithMany(p => p.AsignacionEspecialista)
                .HasForeignKey(d => d.IdEspecialista)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Asignacion_Especialista");

            entity.HasOne(d => d.IdSolicitudNavigation).WithOne(p => p.AsignacionEspecialistum)
                .HasForeignKey<AsignacionEspecialistum>(d => d.IdSolicitud)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Asignacion_Solicitud");
        });

        modelBuilder.Entity<CategoriaMarca>(entity =>
        {
            entity.HasKey(e => new { e.IdCategoria, e.IdMarca });

            entity.ToTable("CategoriaMarca");

            entity.Property(e => e.Activa).HasDefaultValue(true, "DF_CategoriaMarca_Activa");

            entity.HasOne(d => d.IdCategoriaNavigation).WithMany(p => p.CategoriaMarcas)
                .HasForeignKey(d => d.IdCategoria)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CategoriaMarca_Categoria");

            entity.HasOne(d => d.IdMarcaNavigation).WithMany(p => p.CategoriaMarcas)
                .HasForeignKey(d => d.IdMarca)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CategoriaMarca_Marca");
        });

        modelBuilder.Entity<Categorium>(entity =>
        {
            entity.HasKey(e => e.IdCategoria);

            entity.HasIndex(e => e.Nombre, "UQ_Categoria_Nombre").IsUnique();

            entity.Property(e => e.Activa).HasDefaultValue(true, "DF_Categoria_Activa");
            entity.Property(e => e.Descripcion).HasMaxLength(300);
            entity.Property(e => e.Nombre).HasMaxLength(100);
        });

        modelBuilder.Entity<Certificado>(entity =>
        {
            entity.HasKey(e => e.IdCertificado);

            entity.ToTable("Certificado");

            entity.HasIndex(e => e.IdEvaluacion, "UQ_Certificado_IdEvaluacion").IsUnique();

            entity.HasIndex(e => e.NumeroCertificado, "UQ_Certificado_Numero").IsUnique();

            entity.HasIndex(e => e.QrToken, "UQ_Certificado_QrToken").IsUnique();

            entity.HasIndex(e => e.IdEspecialidad, "IX_Certificado_IdEspecialidad");

            entity.Property(e => e.FechaEmision)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysdatetime())", "DF_Certificado_FechaEmision");
            entity.Property(e => e.FechaVencimiento).HasPrecision(0);
            entity.Property(e => e.NumeroCertificado).HasMaxLength(40);
            entity.Property(e => e.QrToken).HasDefaultValueSql("(newid())", "DF_Certificado_QrToken");

            entity.HasOne(d => d.IdEstadoCertificadoNavigation).WithMany(p => p.Certificados)
                .HasForeignKey(d => d.IdEstadoCertificado)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Certificado_Estado");

            entity.HasOne(d => d.IdEvaluacionNavigation).WithOne(p => p.Certificado)
                .HasForeignKey<Certificado>(d => d.IdEvaluacion)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Certificado_Evaluacion");

            entity.HasOne(d => d.IdEspecialidadNavigation).WithMany(p => p.Certificados)
                .HasForeignKey(d => d.IdEspecialidad)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_Certificado_Especialidad");
        });

        modelBuilder.Entity<CriterioEvaluacion>(entity =>
        {
            entity.HasKey(e => e.IdCriterio);

            entity.ToTable("CriterioEvaluacion");

            entity.HasIndex(e => new { e.IdCategoria, e.Activo }, "IX_Criterio_Categoria");

            entity.HasIndex(e => new { e.IdCategoria, e.Nombre }, "UQ_Criterio_CategoriaNombre").IsUnique();

            entity.Property(e => e.Activo).HasDefaultValue(true, "DF_Criterio_Activo");
            entity.Property(e => e.Descripcion).HasMaxLength(500);
            entity.Property(e => e.Nombre).HasMaxLength(150);
            entity.Property(e => e.Obligatorio).HasDefaultValue(true, "DF_Criterio_Obligatorio");
            entity.Property(e => e.Orden).HasDefaultValue((short)1, "DF_Criterio_Orden");
            entity.Property(e => e.TipoDato).HasMaxLength(20);

            entity.HasOne(d => d.IdCategoriaNavigation).WithMany(p => p.CriterioEvaluacions)
                .HasForeignKey(d => d.IdCategoria)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Criterio_Categoria");
        });

        modelBuilder.Entity<EspecialidadEspecialistum>(entity =>
        {
            entity.HasKey(e => e.IdEspecialidad);

            entity.HasIndex(e => new { e.IdEspecialista, e.IdCategoria, e.IdMarca }, "UX_Especialidad_EspecialistaCategoriaMarca").IsUnique();

            entity.Property(e => e.Activa).HasDefaultValue(true, "DF_Especialidad_Activa");
            entity.Property(e => e.FechaRegistro)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysdatetime())", "DF_Especialidad_Fecha");
            entity.Property(e => e.Nivel)
                .HasMaxLength(20)
                .HasDefaultValue("GENERAL", "DF_Especialidad_Nivel");

            entity.HasOne(d => d.IdCategoriaNavigation).WithMany(p => p.EspecialidadEspecialista)
                .HasForeignKey(d => d.IdCategoria)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Especialidad_Categoria");

            entity.HasOne(d => d.IdEspecialistaNavigation).WithMany(p => p.EspecialidadEspecialista)
                .HasForeignKey(d => d.IdEspecialista)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Especialidad_Especialista");

            entity.HasOne(d => d.CategoriaMarca).WithMany(p => p.EspecialidadEspecialista)
                .HasForeignKey(d => new { d.IdCategoria, d.IdMarca })
                .HasConstraintName("FK_Especialidad_CategoriaMarca");
        });

        modelBuilder.Entity<Especialistum>(entity =>
        {
            entity.HasKey(e => e.IdEspecialista);

            entity.HasIndex(e => e.IdUsuario, "UQ_Especialista_IdUsuario").IsUnique();

            entity.Property(e => e.Biografia).HasMaxLength(600);
            entity.Property(e => e.Estado)
                .HasMaxLength(20)
                .HasDefaultValue("PENDIENTE", "DF_Especialista_Estado");
            entity.Property(e => e.FechaAlta)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysdatetime())", "DF_Especialista_FechaAlta");

            entity.HasOne(d => d.IdUsuarioNavigation).WithOne(p => p.Especialistum)
                .HasForeignKey<Especialistum>(d => d.IdUsuario)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Especialista_Usuario");
        });

        modelBuilder.Entity<EstadoCertificado>(entity =>
        {
            entity.HasKey(e => e.IdEstadoCertificado);

            entity.ToTable("EstadoCertificado");

            entity.HasIndex(e => e.Codigo, "UQ_EstadoCertificado_Codigo").IsUnique();

            entity.Property(e => e.Codigo).HasMaxLength(30);
            entity.Property(e => e.Nombre).HasMaxLength(80);
        });

        modelBuilder.Entity<EstadoSolicitud>(entity =>
        {
            entity.HasKey(e => e.IdEstadoSolicitud);

            entity.ToTable("EstadoSolicitud");

            entity.HasIndex(e => e.Codigo, "UQ_EstadoSolicitud_Codigo").IsUnique();

            entity.HasIndex(e => e.Nombre, "UQ_EstadoSolicitud_Nombre").IsUnique();

            entity.Property(e => e.Codigo).HasMaxLength(40);
            entity.Property(e => e.Nombre).HasMaxLength(100);
        });

        modelBuilder.Entity<Evaluacion>(entity =>
        {
            entity.HasKey(e => e.IdEvaluacion);

            entity.ToTable("Evaluacion");

            entity.HasIndex(e => e.IdSolicitud, "UQ_Evaluacion_IdSolicitud").IsUnique();

            entity.Property(e => e.ConditionGrade).HasColumnType("decimal(4, 2)");
            entity.Property(e => e.Dictamen).HasMaxLength(20);
            entity.Property(e => e.FechaFin).HasPrecision(0);
            entity.Property(e => e.FechaInicio)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysdatetime())", "DF_Evaluacion_FechaInicio");
            entity.Property(e => e.ObservacionesGenerales).HasMaxLength(1500);
            entity.Property(e => e.TechnicalHealth).HasColumnType("decimal(5, 2)");

            entity.HasOne(d => d.IdEspecialistaNavigation).WithMany(p => p.Evaluacions)
                .HasForeignKey(d => d.IdEspecialista)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Evaluacion_Especialista");

            entity.HasOne(d => d.IdSolicitudNavigation).WithOne(p => p.Evaluacion)
                .HasForeignKey<Evaluacion>(d => d.IdSolicitud)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Evaluacion_Solicitud");
        });

        modelBuilder.Entity<Evidencium>(entity =>
        {
            entity.HasKey(e => e.IdEvidencia);

            entity.HasIndex(e => e.IdSolicitud, "IX_Evidencia_Solicitud");

            entity.Property(e => e.Activa).HasDefaultValue(true, "DF_Evidencia_Activa");
            entity.Property(e => e.Descripcion).HasMaxLength(500);
            entity.Property(e => e.FechaCarga)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysdatetime())", "DF_Evidencia_FechaCarga");
            entity.Property(e => e.UrlArchivo).HasMaxLength(1000);

            entity.HasOne(d => d.IdSolicitudNavigation).WithMany(p => p.Evidencia)
                .HasForeignKey(d => d.IdSolicitud)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Evidencia_Solicitud");

            entity.HasOne(d => d.IdTipoEvidenciaNavigation).WithMany(p => p.Evidencia)
                .HasForeignKey(d => d.IdTipoEvidencia)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Evidencia_Tipo");
        });

        modelBuilder.Entity<Marca>(entity =>
        {
            entity.HasKey(e => e.IdMarca);

            entity.ToTable("Marca");

            entity.HasIndex(e => e.Nombre, "UQ_Marca_Nombre").IsUnique();

            entity.Property(e => e.Activa).HasDefaultValue(true, "DF_Marca_Activa");
            entity.Property(e => e.Nombre).HasMaxLength(100);
        });

        modelBuilder.Entity<Notificacion>(entity =>
        {
            entity.HasKey(e => e.IdNotificacion);

            entity.ToTable("Notificacion");

            entity.HasIndex(e => new { e.IdUsuario, e.Leida, e.FechaCreacion }, "IX_Notificacion_UsuarioLeida").IsDescending(false, false, true);

            entity.Property(e => e.FechaCreacion)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysdatetime())", "DF_Notificacion_Fecha");
            entity.Property(e => e.FechaLectura).HasPrecision(0);
            entity.Property(e => e.Mensaje).HasMaxLength(600);
            entity.Property(e => e.Titulo).HasMaxLength(150);

            entity.HasOne(d => d.IdSolicitudNavigation).WithMany(p => p.Notificacions)
                .HasForeignKey(d => d.IdSolicitud)
                .HasConstraintName("FK_Notificacion_Solicitud");

            entity.HasOne(d => d.IdTipoNotificacionNavigation).WithMany(p => p.Notificacions)
                .HasForeignKey(d => d.IdTipoNotificacion)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Notificacion_Tipo");

            entity.HasOne(d => d.IdUsuarioNavigation).WithMany(p => p.Notificacions)
                .HasForeignKey(d => d.IdUsuario)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Notificacion_Usuario");
        });

        modelBuilder.Entity<Producto>(entity =>
        {
            entity.HasKey(e => e.IdProducto);

            entity.ToTable("Producto");

            entity.HasIndex(e => new { e.IdCategoria, e.IdMarca }, "IX_Producto_CategoriaMarca");

            entity.HasIndex(e => e.IdPropietario, "IX_Producto_Propietario");

            entity.Property(e => e.Accesorios).HasMaxLength(500);
            entity.Property(e => e.Activo).HasDefaultValue(true, "DF_Producto_Activo");
            entity.Property(e => e.Descripcion).HasMaxLength(1000);
            entity.Property(e => e.FechaRegistro)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysdatetime())", "DF_Producto_FechaRegistro");
            entity.Property(e => e.Imei)
                .HasMaxLength(30)
                .HasColumnName("IMEI");
            entity.Property(e => e.Modelo).HasMaxLength(150);
            entity.Property(e => e.Nombre).HasMaxLength(150);
            entity.Property(e => e.NumeroSerie).HasMaxLength(120);
            entity.Property(e => e.Sku)
                .HasMaxLength(80)
                .HasColumnName("SKU");

            entity.HasOne(d => d.IdCategoriaNavigation).WithMany(p => p.Productos)
                .HasForeignKey(d => d.IdCategoria)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Producto_Categoria");

            entity.HasOne(d => d.IdMarcaNavigation).WithMany(p => p.Productos)
                .HasForeignKey(d => d.IdMarca)
                .HasConstraintName("FK_Producto_Marca");

            entity.HasOne(d => d.IdPropietarioNavigation).WithMany(p => p.Productos)
                .HasForeignKey(d => d.IdPropietario)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Producto_Propietario");

            entity.HasOne(d => d.CategoriaMarca).WithMany(p => p.Productos)
                .HasForeignKey(d => new { d.IdCategoria, d.IdMarca })
                .HasConstraintName("FK_Producto_CategoriaMarca");
        });

        modelBuilder.Entity<ResultadoCriterio>(entity =>
        {
            entity.HasKey(e => new { e.IdEvaluacion, e.IdCriterio });

            entity.ToTable("ResultadoCriterio");

            entity.Property(e => e.Observacion).HasMaxLength(1000);
            entity.Property(e => e.ValorNumerico).HasColumnType("decimal(12, 2)");
            entity.Property(e => e.ValorTexto).HasMaxLength(1000);

            entity.HasOne(d => d.IdCriterioNavigation).WithMany(p => p.ResultadoCriterios)
                .HasForeignKey(d => d.IdCriterio)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ResultadoCriterio_Criterio");

            entity.HasOne(d => d.IdEvaluacionNavigation).WithMany(p => p.ResultadoCriterios)
                .HasForeignKey(d => d.IdEvaluacion)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ResultadoCriterio_Evaluacion");
        });

        modelBuilder.Entity<Rol>(entity =>
        {
            entity.HasKey(e => e.IdRol);

            entity.ToTable("Rol");

            entity.HasIndex(e => e.Nombre, "UQ_Rol_Nombre").IsUnique();

            entity.Property(e => e.Activo).HasDefaultValue(true, "DF_Rol_Activo");
            entity.Property(e => e.Descripcion).HasMaxLength(200);
            entity.Property(e => e.Nombre).HasMaxLength(50);
        });

        modelBuilder.Entity<SolicitudEvaluacion>(entity =>
        {
            entity.HasKey(e => e.IdSolicitud);

            entity.ToTable("SolicitudEvaluacion");

            entity.HasIndex(e => e.IdEstadoSolicitud, "IX_Solicitud_Estado");

            entity.HasIndex(e => e.IdProducto, "IX_Solicitud_Producto");

            entity.HasIndex(e => e.IdSolicitante, "IX_Solicitud_Solicitante");

            entity.HasIndex(e => e.Folio, "UQ_SolicitudEvaluacion_Folio").IsUnique();

            entity.Property(e => e.FechaSolicitud)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysdatetime())", "DF_Solicitud_FechaSolicitud");
            entity.Property(e => e.FechaUltimoCambio)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysdatetime())", "DF_Solicitud_FechaCambio");
            entity.Property(e => e.Folio).HasMaxLength(30);
            entity.Property(e => e.ObservacionesSolicitante).HasMaxLength(1000);

            entity.HasOne(d => d.IdEstadoSolicitudNavigation).WithMany(p => p.SolicitudEvaluacions)
                .HasForeignKey(d => d.IdEstadoSolicitud)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Solicitud_Estado");

            entity.HasOne(d => d.IdProductoNavigation).WithMany(p => p.SolicitudEvaluacions)
                .HasForeignKey(d => d.IdProducto)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Solicitud_Producto");

            entity.HasOne(d => d.IdSolicitanteNavigation).WithMany(p => p.SolicitudEvaluacions)
                .HasForeignKey(d => d.IdSolicitante)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Solicitud_Solicitante");
        });

        modelBuilder.Entity<TipoEvidencium>(entity =>
        {
            entity.HasKey(e => e.IdTipoEvidencia);

            entity.HasIndex(e => e.Codigo, "UQ_TipoEvidencia_Codigo").IsUnique();

            entity.Property(e => e.Codigo).HasMaxLength(40);
            entity.Property(e => e.Nombre).HasMaxLength(100);
        });

        modelBuilder.Entity<TipoNotificacion>(entity =>
        {
            entity.HasKey(e => e.IdTipoNotificacion);

            entity.ToTable("TipoNotificacion");

            entity.HasIndex(e => e.Codigo, "UQ_TipoNotificacion_Codigo").IsUnique();

            entity.Property(e => e.Codigo).HasMaxLength(50);
            entity.Property(e => e.Nombre).HasMaxLength(120);
        });

        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.HasKey(e => e.IdUsuario);

            entity.ToTable("Usuario");

            entity.HasIndex(e => e.Correo, "UQ_Usuario_Correo").IsUnique();

            entity.Property(e => e.Activo).HasDefaultValue(true, "DF_Usuario_Activo");
            entity.Property(e => e.Correo).HasMaxLength(254);
            entity.Property(e => e.FechaActualizacion).HasPrecision(0);
            entity.Property(e => e.FechaCreacion)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysdatetime())", "DF_Usuario_FechaCreacion");
            entity.Property(e => e.NombreCompleto).HasMaxLength(150);
            entity.Property(e => e.PasswordHash).HasMaxLength(255);
        });

        modelBuilder.Entity<UsuarioRol>(entity =>
        {
            entity.HasKey(e => new { e.IdUsuario, e.IdRol });

            entity.ToTable("UsuarioRol");

            entity.Property(e => e.FechaAsignacion)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysdatetime())", "DF_UsuarioRol_Fecha");

            entity.HasOne(d => d.IdRolNavigation).WithMany(p => p.UsuarioRols)
                .HasForeignKey(d => d.IdRol)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UsuarioRol_Rol");

            entity.HasOne(d => d.IdUsuarioNavigation).WithMany(p => p.UsuarioRols)
                .HasForeignKey(d => d.IdUsuario)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UsuarioRol_Usuario");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

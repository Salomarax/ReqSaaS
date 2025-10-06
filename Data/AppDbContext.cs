using Microsoft.EntityFrameworkCore;
using ReqSaaS_1.Data.Entities;
using ReqSaaS_1.Models;

namespace ReqSaaS_1.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
        
        public DbSet<Tipo> Tipos => Set<Tipo>();

        public DbSet<Credencial> Credenciales => Set<Credencial>();
        public DbSet<Requisito> Requisitos => Set<Requisito>();
        public DbSet<DetalleEvaluacion> DetalleEvaluaciones => Set<DetalleEvaluacion>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // comenta si no usas ScalarInt
            modelBuilder.Entity<ScalarInt>().HasNoKey();

            modelBuilder.HasDefaultSchema("public");

            // ----- Credencial -----
            modelBuilder.Entity<Credencial>(entity =>
            {
                entity.ToTable("Credencial");
                entity.HasKey(e => e.IdCredencial);

                entity.Property(e => e.IdCredencial).HasColumnName("ID_credencial");
                entity.Property(e => e.IdOrganismo)
                      .HasColumnName("ID_organismo")
                      .HasColumnType("varchar(12)")
                      .HasMaxLength(12)
                      .IsRequired();
                entity.Property(e => e.ClaveHash).HasColumnName("Clave_hash");
                entity.Property(e => e.Nombre).HasColumnName("Nombre");
                entity.Property(e => e.IdNivel).HasColumnName("ID_nivel");
            });

            // ----- Requisito -----
            modelBuilder.Entity<Requisito>(e =>
            {
                e.ToTable("Requisitos", "public");
                e.HasKey(x => x.IdReq);

                e.Property(x => x.IdReq).HasColumnName("ID_requisito");
                e.Property(x => x.IdOrganismo).HasColumnName("ID_organismo");
                e.Property(x => x.Titulo).HasColumnName("Titulo");
                e.Property(x => x.Descripcion).HasColumnName("Descripcion");
                e.Property(x => x.Entidad).HasColumnName("Entidad");
                e.Property(x => x.PorcentajeCumplimiento).HasColumnName("PorcentajeCumplimiento");
                e.Property(x => x.IdTipo).HasColumnName("ID_tipo");
               e.Property(x => x.NormaIDBCN).HasColumnName("normaID_BCN");
            });

            // ----- Lista de Tipos -----
            modelBuilder.Entity<Tipo>(e =>
            {
                e.ToTable("Tipo", "public");
                e.HasKey(t => t.IdTipo);
                e.Property(t => t.IdTipo).HasColumnName("ID_tipo");
                e.Property(t => t.Nombre).HasColumnName("Nombre");
            });

            // ----- DetalleEvaluacion -----
            modelBuilder.Entity<DetalleEvaluacion>(e =>
            {
                e.ToTable("DetalleEvaluacion", "public");
                e.HasKey(x => x.IdItem);

                e.Property(x => x.IdItem).HasColumnName("ID_item");
                e.Property(x => x.IdRequisito).HasColumnName("ID_requisito");
                e.Property(x => x.Justificacion).HasColumnName("Justificacion");
                e.Property(x => x.Cumplimiento).HasColumnName("Cumplimiento");
                e.Property(x => x.ArchivoUrl).HasColumnName("Archivo_url");
                e.Property(x => x.Detalle).HasColumnName("Detalle");

                e.HasOne(x => x.Requisito)
                 .WithMany(r => r.DetalleEvaluaciones)
                 .HasForeignKey(x => x.IdRequisito)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            base.OnModelCreating(modelBuilder);
        }
    }
}

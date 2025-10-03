using Microsoft.EntityFrameworkCore;
using ReqSaaS_1.Data.Entities;

namespace ReqSaaS_1.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Credencial> Credenciales => Set<Credencial>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Si tu esquema es "public" en Postgres:
            modelBuilder.HasDefaultSchema("public");

            modelBuilder.Entity<Credencial>(entity =>
            {
                entity.ToTable("Credencial");                 // public."Credencial"
                entity.HasKey(e => e.IdCredencial);           // <-- ¡NO e => e.Id!

                entity.Property(e => e.IdCredencial).HasColumnName("ID_credencial");
                entity.Property(e => e.IdOrganismo).HasColumnName("ID_organismo");
                entity.Property(e => e.ClaveHash).HasColumnName("Clave_hash");
                entity.Property(e => e.Nombre).HasColumnName("Nombre");
                entity.Property(e => e.IdNivel).HasColumnName("ID_nivel");
            });

            base.OnModelCreating(modelBuilder);
        }
    }
}

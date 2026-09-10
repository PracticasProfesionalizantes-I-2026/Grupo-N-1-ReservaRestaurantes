using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Context;

public class RestaurantDbContext : DbContext
{
    public RestaurantDbContext(DbContextOptions<RestaurantDbContext> options) : base(options)
    {
    }

    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Mesa> Mesas => Set<Mesa>();
    public DbSet<Reserva> Reservas => Set<Reserva>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configuración de Cliente
        modelBuilder.Entity<Cliente>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Nombre).IsRequired().HasMaxLength(100);
            entity.Property(c => c.Apellido).IsRequired().HasMaxLength(100);
            entity.Property(c => c.Email).IsRequired().HasMaxLength(150);
            entity.Property(c => c.Telefono).IsRequired().HasMaxLength(30);
            entity.Property(c => c.FechaRegistro).IsRequired();

            entity.HasIndex(c => c.Email).IsUnique();
        });

        // Configuración de Mesa
        modelBuilder.Entity<Mesa>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Numero).IsRequired();
            entity.Property(m => m.Capacidad).IsRequired();
            entity.Property(m => m.Ubicacion).IsRequired().HasMaxLength(80);
            entity.Property(m => m.Estado).IsRequired().HasConversion<int>();
            entity.Property(m => m.Activa).IsRequired().HasDefaultValue(true);

            entity.HasIndex(m => m.Numero).IsUnique();
        });

        // Configuración de Reserva y relaciones con DeleteBehavior.Restrict
        modelBuilder.Entity<Reserva>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.FechaHora).IsRequired();
            entity.Property(r => r.CantidadComensales).IsRequired();
            entity.Property(r => r.DuracionEstimadaMinutos).IsRequired().HasDefaultValue(120);
            entity.Property(r => r.Estado).IsRequired().HasConversion<int>();
            entity.Property(r => r.Observaciones).HasMaxLength(500);
            entity.Property(r => r.FechaCreacion).IsRequired();

            // Relación con Cliente (DeleteBehavior.Restrict)
            entity.HasOne(r => r.Cliente)
                  .WithMany(c => c.Reservas)
                  .HasForeignKey(r => r.ClienteId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Relación con Mesa (DeleteBehavior.Restrict)
            entity.HasOne(r => r.Mesa)
                  .WithMany(m => m.Reservas)
                  .HasForeignKey(r => r.MesaId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }
}

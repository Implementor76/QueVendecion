using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QueVendecion.Domain.Entities;

namespace QueVendecion.Infrastructure.Configurations;

public class PedidoConfiguration : IEntityTypeConfiguration<Pedido>
{
    public void Configure(EntityTypeBuilder<Pedido> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.ClienteNombre).HasMaxLength(100);
        builder.Property(p => p.Total).HasColumnType("decimal(18,2)");
        builder.Property(p => p.Observaciones).HasMaxLength(500);
        builder.Property(p => p.VentanaSolicitada).HasMaxLength(11);
        builder.Property(p => p.HorarioConfirmado).HasMaxLength(100);
        builder.Property(p => p.DireccionEntrega).HasMaxLength(300);
        builder.Property(p => p.HistorialEstadosJson).HasColumnType("text");
        builder.Property(p => p.ClaveIdempotencia).HasMaxLength(36);
        builder.Property(p => p.HuellaSolicitud).HasMaxLength(64);
        builder.HasIndex(p => new { p.TiendaId, p.ClaveIdempotencia }).IsUnique();
        builder.HasIndex(p => new { p.TiendaId, p.EntregadoEnUtc });
        builder.HasIndex(p => new { p.TiendaId, p.FechaServicio, p.Estado });
        builder.HasOne(p => p.Tienda)
            .WithMany(t => t.Pedidos)
            .HasForeignKey(p => p.TiendaId);
        builder.HasOne(p => p.Cliente)
            .WithMany(c => c.Pedidos)
            .HasForeignKey(p => p.ClienteId)
            .IsRequired(false);
    }
}

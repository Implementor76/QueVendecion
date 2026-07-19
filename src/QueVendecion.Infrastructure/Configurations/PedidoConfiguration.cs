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
        builder.HasOne(p => p.Tienda)
            .WithMany(t => t.Pedidos)
            .HasForeignKey(p => p.TiendaId);
        builder.HasOne(p => p.Cliente)
            .WithMany(c => c.Pedidos)
            .HasForeignKey(p => p.ClienteId)
            .IsRequired(false);
    }
}

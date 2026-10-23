using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QueVendecion.Domain.Entities;

namespace QueVendecion.Infrastructure.Configurations;

public class ItemPedidoConfiguration : IEntityTypeConfiguration<ItemPedido>
{
    public void Configure(EntityTypeBuilder<ItemPedido> builder)
    {
        builder.HasKey(i => i.Id);
        builder.Property(i => i.PrecioUnitario).HasColumnType("decimal(18,2)");
        builder.Property(i => i.CostoUnitarioHistorico).HasColumnType("decimal(18,4)");
        builder.HasOne(i => i.Pedido)
            .WithMany(p => p.Items)
            .HasForeignKey(i => i.PedidoId);
        builder.HasOne(i => i.Producto)
            .WithMany()
            .HasForeignKey(i => i.ProductoId);
    }
}

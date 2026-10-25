using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QueVendecion.Domain.Entities;

namespace QueVendecion.Infrastructure.Configurations;

public class ProductoConfiguration : IEntityTypeConfiguration<Producto>
{
    public void Configure(EntityTypeBuilder<Producto> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Nombre).IsRequired().HasMaxLength(100);
        builder.Property(p => p.Descripcion).HasMaxLength(500);
        builder.Property(p => p.Precio).HasColumnType("decimal(18,2)");
        builder.Property(p => p.CostoUnitario).HasColumnType("decimal(18,4)");
        builder.Property(p => p.ImagenUrl).HasMaxLength(500);
        builder.Property(p => p.TieneInventario).HasDefaultValue(false);
        builder.Property(p => p.Stock).HasDefaultValue(null);
        builder.Property(p => p.FechasServicioJson).HasColumnType("text");
        builder.Property(p => p.EntregaPermitida).IsRequired(false);
        builder.Property(p => p.AdmiteDobleCara).HasDefaultValue(false);
        builder.Property(p => p.RequierePdf).HasDefaultValue(false);
        builder.Property(p => p.RevisionImpresion).HasMaxLength(36);
        builder.HasOne(p => p.PapelInsumo).WithMany().HasForeignKey(p => p.PapelInsumoId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.Tienda)
            .WithMany(t => t.Productos)
            .HasForeignKey(p => p.TiendaId);
    }
}

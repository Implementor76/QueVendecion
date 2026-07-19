using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QueVendecion.Domain.Entities;

namespace QueVendecion.Infrastructure.Configurations;

public class TiendaConfiguration : IEntityTypeConfiguration<Tienda>
{
    public void Configure(EntityTypeBuilder<Tienda> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Nombre).IsRequired().HasMaxLength(100);
        builder.Property(t => t.Slug).IsRequired().HasMaxLength(50);
        builder.HasIndex(t => t.Slug).IsUnique();
        builder.Property(t => t.Direccion).HasMaxLength(200);
        builder.Property(t => t.Telefono).HasMaxLength(20);
    }
}

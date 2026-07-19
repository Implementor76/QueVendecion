using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QueVendecion.Domain.Entities;

namespace QueVendecion.Infrastructure.Configurations;

public class UsuarioTiendaConfiguration : IEntityTypeConfiguration<UsuarioTienda>
{
    public void Configure(EntityTypeBuilder<UsuarioTienda> builder)
    {
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Nombre).IsRequired().HasMaxLength(100);
        builder.Property(u => u.Email).IsRequired().HasMaxLength(150);
        builder.Property(u => u.PasswordHash).IsRequired();
        builder.HasIndex(u => u.Email).IsUnique();
        builder.HasOne(u => u.Tienda)
            .WithMany(t => t.Usuarios)
            .HasForeignKey(u => u.TiendaId);
    }
}

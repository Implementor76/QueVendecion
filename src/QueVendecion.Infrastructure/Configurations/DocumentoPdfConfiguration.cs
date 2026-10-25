using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QueVendecion.Domain.Entities;

namespace QueVendecion.Infrastructure.Configurations;

public class DocumentoPdfConfiguration : IEntityTypeConfiguration<DocumentoPdf>
{
    public void Configure(EntityTypeBuilder<DocumentoPdf> b)
    {
        b.HasKey(d => d.Id);
        b.Property(d => d.NombreOriginal).HasMaxLength(255);
        b.Property(d => d.NombreInterno).HasMaxLength(64);
        b.Property(d => d.Tipo).HasMaxLength(80);
        b.Property(d => d.Sha256).HasMaxLength(64);
        b.Property(d => d.SesionHash).HasMaxLength(64);
        b.Property(d => d.Estado).HasMaxLength(20);
        b.Property(d => d.PaginasSeleccionadas).HasMaxLength(2000);
        b.HasIndex(d => new { d.TiendaId, d.ClienteId, d.Estado });
        b.HasIndex(d => d.ItemPedidoId).IsUnique();
        b.HasOne(d => d.ItemPedido).WithOne(i => i.DocumentoPdf).HasForeignKey<DocumentoPdf>(d => d.ItemPedidoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class SesionDocumentoConfiguration : IEntityTypeConfiguration<SesionDocumento>
{
    public void Configure(EntityTypeBuilder<SesionDocumento> b)
    {
        b.HasKey(s => s.Id);
        b.Property(s => s.TokenHash).HasMaxLength(64);
        b.HasIndex(s => s.TokenHash).IsUnique();
    }
}

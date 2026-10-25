using Microsoft.EntityFrameworkCore;
using QueVendecion.Domain.Entities;

namespace QueVendecion.Infrastructure.Data;

public class QueVendecionDbContext : DbContext
{
    public QueVendecionDbContext(DbContextOptions<QueVendecionDbContext> options)
        : base(options)
    {
    }

    public DbSet<Tienda> Tiendas => Set<Tienda>();
    public DbSet<UsuarioTienda> UsuariosTienda => Set<UsuarioTienda>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<ItemPedido> ItemsPedido => Set<ItemPedido>();
    public DbSet<MovimientoInventario> MovimientosInventario => Set<MovimientoInventario>();
    public DbSet<DocumentoPdf> DocumentosPdf => Set<DocumentoPdf>();
    public DbSet<SesionDocumento> SesionesDocumento => Set<SesionDocumento>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(QueVendecionDbContext).Assembly);
    }
}

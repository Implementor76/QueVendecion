using Microsoft.EntityFrameworkCore;
using QueVendecion.Application.DTOs;
using QueVendecion.Application.Exceptions;
using QueVendecion.Domain.Entities;
using QueVendecion.Domain.Enums;
using QueVendecion.Infrastructure.Data;
using QueVendecion.Infrastructure.Services;
using QueVendecion.Tests.Helpers;

namespace QueVendecion.Tests.Services;

public class InventarioTests
{
    private static async Task<int> Tienda(QueVendecionDbContext db)
    {
        var tienda = new Tienda { Nombre = "Inventario", Slug = Guid.NewGuid().ToString() };
        db.Tiendas.Add(tienda);
        await db.SaveChangesAsync();
        return tienda.Id;
    }

    [Theory]
    [InlineData(false, null, null)]
    [InlineData(false, 10, null)]
    [InlineData(true, 10, 10)]
    [InlineData(true, 0, 0)]
    public async Task CrearProductoNormalizaInventario(bool controla, int? stock, int? esperado)
    {
        using var db = TestDbContextFactory.Create();
        var p = await new ProductoService(db).CrearAsync(await Tienda(db),
            new() { Nombre = "Hielo", TieneInventario = controla, Stock = stock });
        Assert.Equal(esperado, p.Stock);
        Assert.Equal(controla, p.TieneInventario);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(null)]
    public async Task RechazaStockInvalidoEnAltaYEdicion(int? stock)
    {
        using var db = TestDbContextFactory.Create();
        var service = new ProductoService(db);
        var tienda = await Tienda(db);
        var dto = new CrearProductoDto { Nombre = "Hielo", TieneInventario = true, Stock = stock };
        await Assert.ThrowsAsync<ReglaNegocioException>(() => service.CrearAsync(tienda, dto));
        var p = await service.CrearAsync(tienda, new() { Nombre = "Hielo" });
        await Assert.ThrowsAsync<ReglaNegocioException>(() => service.ActualizarAsync(p.Id, dto));
        Assert.Null((await service.ObtenerPorIdAsync(p.Id))!.Stock);
    }

    [Fact]
    public async Task ActivaYDesactivaInventario()
    {
        using var db = TestDbContextFactory.Create();
        var service = new ProductoService(db);
        var p = await service.CrearAsync(await Tienda(db), new() { Nombre = "Hielo" });
        var dto = new CrearProductoDto { Nombre = "Hielo", TieneInventario = true, Stock = 10 };
        Assert.Equal(10, (await service.ActualizarAsync(p.Id, dto))!.Stock);
        dto.TieneInventario = false;
        Assert.Null((await service.ActualizarAsync(p.Id, dto))!.Stock);
    }

    private static CrearPedidoDto Pedido(int tienda, int producto, params int[] cantidades) => new()
    {
        TiendaId = tienda,
        Items = cantidades.Select(c => new CrearItemPedidoDto { ProductoId = producto, Cantidad = c }).ToList()
    };

    [Fact]
    public async Task DescuentaRechazaSobreventaYReintegraUnaVez()
    {
        using var db = TestDbContextFactory.Create();
        var tienda = await Tienda(db);
        var p = await new ProductoService(db).CrearAsync(tienda, new() { Nombre = "Hielo", TieneInventario = true, Stock = 10 });
        var service = new PedidoService(db);
        var pedido = await service.CrearAsync(Pedido(tienda, p.Id, 1, 2));
        Assert.Single(pedido.Items);
        Assert.Equal(7, (await db.Productos.FindAsync(p.Id))!.Stock);
        var salida = await db.MovimientosInventario.SingleAsync();
        Assert.Equal(TipoMovimiento.Salida, salida.Tipo);
        Assert.Equal(3, salida.Cantidad);
        await Assert.ThrowsAsync<ReglaNegocioException>(() => service.CrearAsync(Pedido(tienda, p.Id, 4, 4)));
        Assert.Equal(7, (await db.Productos.FindAsync(p.Id))!.Stock);
        Assert.Equal(1, await db.Pedidos.CountAsync());
        await service.ActualizarEstadoAsync(pedido.Id, EstadoPedido.Cancelado);
        await service.ActualizarEstadoAsync(pedido.Id, EstadoPedido.Cancelado);
        Assert.Equal(10, (await db.Productos.FindAsync(p.Id))!.Stock);
        Assert.Equal(1, await db.MovimientosInventario.CountAsync(m => m.Tipo == TipoMovimiento.Entrada));
        await Assert.ThrowsAsync<ReglaNegocioException>(() => service.ActualizarEstadoAsync(pedido.Id, EstadoPedido.Recibido));
    }

    [Fact]
    public async Task PedidoDeCatalogoSeConfirmaYEntregaSinPreparacionNiDescuentoDuplicado()
    {
        using var db = TestDbContextFactory.Create();
        var tienda = await Tienda(db);
        var producto = await new ProductoService(db).CrearAsync(tienda,
            new() { Nombre = "Hielo", Precio = 25, TieneInventario = true, Stock = 4 });
        var service = new PedidoService(db);
        var pedido = await service.CrearAsync(Pedido(tienda, producto.Id, 1));
        await service.ActualizarEstadoAsync(pedido.Id, EstadoPedido.Confirmado, 7);
        var entregado = await service.ActualizarEstadoAsync(pedido.Id, EstadoPedido.Entregado, 7);
        Assert.Equal(new[] { EstadoPedido.Recibido, EstadoPedido.Confirmado, EstadoPedido.Entregado },
            entregado!.HistorialEstados.Select(h => h.Nuevo));
        Assert.Equal(2, entregado.HistorialEstados.Count(h => h.UsuarioId == 7));
        await service.ActualizarEstadoAsync(pedido.Id, EstadoPedido.Entregado, 7);
        Assert.Equal(3, (await db.Productos.FindAsync(producto.Id))!.Stock);
        Assert.Single(await db.MovimientosInventario.ToListAsync());
        await Assert.ThrowsAsync<ReglaNegocioException>(() => service.ActualizarEstadoAsync(pedido.Id, EstadoPedido.Cancelado));
    }

    [Fact]
    public async Task FallaMovimientoReviertePedidoYStock()
    {
        using var db = TestDbContextFactory.Create();
        var tienda = await Tienda(db);
        var p = await new ProductoService(db).CrearAsync(tienda, new() { Nombre = "Hielo", TieneInventario = true, Stock = 10 });
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER fallo_movimiento BEFORE INSERT ON MovimientosInventario BEGIN SELECT RAISE(ABORT, 'fallo simulado'); END;");
        await Assert.ThrowsAsync<DbUpdateException>(() => new PedidoService(db).CrearAsync(Pedido(tienda, p.Id, 3)));
        Assert.Equal(10, (await db.Productos.FindAsync(p.Id))!.Stock);
        Assert.Empty(await db.Pedidos.ToListAsync());
        Assert.Empty(await db.ItemsPedido.ToListAsync());
    }

    [Fact]
    public async Task PedidosYCancelacionesConcurrentesNoDuplicanStock()
    {
        var path = Path.Combine(Path.GetTempPath(), $"qvd-inventario-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<QueVendecionDbContext>()
            .UseSqlite($"Data Source={path};Pooling=False;Default Timeout=10").Options;
        try
        {
            int tienda, producto;
            await using (var seed = new QueVendecionDbContext(options))
            {
                await seed.Database.EnsureCreatedAsync();
                tienda = await Tienda(seed);
                producto = (await new ProductoService(seed).CrearAsync(tienda,
                    new() { Nombre = "Hielo", TieneInventario = true, Stock = 10 })).Id;
            }
            using var start = new Barrier(2);
            var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Task.Run(async () =>
            {
                await using var db = new QueVendecionDbContext(options);
                start.SignalAndWait(TimeSpan.FromSeconds(10));
                try { return (await new PedidoService(db).CrearAsync(Pedido(tienda, producto, 7))).Id; }
                catch (ReglaNegocioException) { return 0; }
            })));
            var pedido = Assert.Single(results, id => id != 0);
            await using (var check = new QueVendecionDbContext(options))
            {
                Assert.Equal(3, (await check.Productos.SingleAsync()).Stock);
                Assert.Equal(1, await check.Pedidos.CountAsync());
                Assert.Equal(1, await check.MovimientosInventario.CountAsync());
            }
            using var cancelStart = new Barrier(2);
            await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Task.Run(async () =>
            {
                await using var db = new QueVendecionDbContext(options);
                cancelStart.SignalAndWait(TimeSpan.FromSeconds(10));
                await new PedidoService(db).ActualizarEstadoAsync(pedido, EstadoPedido.Cancelado);
            })));
            await using var final = new QueVendecionDbContext(options);
            Assert.Equal(10, (await final.Productos.SingleAsync()).Stock);
            Assert.Equal(1, await final.MovimientosInventario.CountAsync(m => m.Tipo == TipoMovimiento.Entrada));
        }
        finally
        {
            File.Delete(path); // Only this test's unique temporary database.
        }
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(false, false, false)]
    public async Task UpgradeBaseAnteriorEsIdempotenteYConservaDatos(bool faltaControl, bool faltaStock, bool faltaMovimientos)
    {
        using var db = TestDbContextFactory.Create();
        var tienda = await Tienda(db);
        await new ProductoService(db).CrearAsync(tienda, new() { Nombre = "Existente" });
        if (faltaMovimientos) await db.Database.ExecuteSqlRawAsync("DROP TABLE MovimientosInventario;");
        if (faltaControl) await db.Database.ExecuteSqlRawAsync("ALTER TABLE Productos DROP COLUMN TieneInventario;");
        if (faltaStock) await db.Database.ExecuteSqlRawAsync("ALTER TABLE Productos DROP COLUMN Stock;");
        db.ChangeTracker.Clear();
        if (faltaControl || faltaStock || faltaMovimientos)
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => InventarioSchemaUpgrade.VerificarAsync(db));
            Assert.Contains("Esquema de inventario pendiente", error.Message);
        }
        await InventarioSchemaUpgrade.AplicarAsync(db);
        await InventarioSchemaUpgrade.AplicarAsync(db);
        var p = await db.Productos.SingleAsync();
        Assert.Equal("Existente", p.Nombre);
        Assert.False(p.TieneInventario);
        Assert.Null(p.Stock);
        Assert.Empty(await db.MovimientosInventario.ToListAsync());
    }

    [Fact]
    public async Task UpgradeConservaInventarioYMovimientosExistentes()
    {
        using var db = TestDbContextFactory.Create();
        var tienda = await Tienda(db);
        var producto = await new ProductoService(db).CrearAsync(tienda,
            new() { Nombre = "Hielo", TieneInventario = true, Stock = 10 });
        var pedido = await new PedidoService(db).CrearAsync(Pedido(tienda, producto.Id, 3));
        await InventarioSchemaUpgrade.AplicarAsync(db);
        await InventarioSchemaUpgrade.AplicarAsync(db);
        db.ChangeTracker.Clear();
        Assert.Equal(7, (await db.Productos.SingleAsync()).Stock);
        var movimiento = await db.MovimientosInventario.SingleAsync();
        Assert.Equal(pedido.Id, movimiento.PedidoId);
        Assert.Equal(3, movimiento.Cantidad);
        Assert.Equal(10, movimiento.StockAnterior);
        Assert.Equal(7, movimiento.StockNuevo);
    }
}

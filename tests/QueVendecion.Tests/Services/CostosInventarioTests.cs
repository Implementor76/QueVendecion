using Microsoft.EntityFrameworkCore;
using QueVendecion.Application.DTOs;
using QueVendecion.Application.Exceptions;
using QueVendecion.Domain.Entities;
using QueVendecion.Domain.Enums;
using QueVendecion.Infrastructure.Data;
using QueVendecion.Infrastructure.Services;
using QueVendecion.Tests.Helpers;

namespace QueVendecion.Tests.Services;

public class CostosInventarioTests
{
    private static async Task<Tienda> Tienda(QueVendecionDbContext db)
    {
        var tienda = new Tienda { Nombre = "Tienda", Slug = Guid.NewGuid().ToString() };
        db.Tiendas.Add(tienda);
        await db.SaveChangesAsync();
        return tienda;
    }

    [Fact]
    public async Task CompraAjusteEdicionYLimitesConservanTrazabilidad()
    {
        using var db = TestDbContextFactory.Create();
        var tienda = await Tienda(db);
        var otra = await Tienda(db);
        var usuario = new UsuarioTienda { TiendaId = tienda.Id, Nombre = "Encargada", Email = "encargada@prueba.local", PasswordHash = "prueba" };
        db.UsuariosTienda.Add(usuario);
        await db.SaveChangesAsync();
        var svc = new ProductoService(db);
        var producto = await svc.CrearAsync(tienda.Id, new() { Nombre = "Pluma", Precio = 10, TieneInventario = true, Stock = 12, ExistenciaMinima = 4 });
        await svc.CompraAsync(tienda.Id, producto.Id, new() { Cantidad = 12, CostoUnitario = 47m / 12m }, usuario.Id);
        await svc.AjusteAsync(tienda.Id, producto.Id, new() { Cantidad = -2, Motivo = "Merma" }, null);
        Assert.Equal(22, (await svc.ObtenerAdminPorIdAsync(tienda.Id, producto.Id))!.Stock);
        Assert.Equal(3.9167m, (await svc.ObtenerAdminPorIdAsync(tienda.Id, producto.Id))!.CostoUnitario);
        var movimientos = await svc.MovimientosAsync(tienda.Id, producto.Id);
        Assert.Equal(3, movimientos!.Count);
        Assert.Equal(new[] { TipoMovimiento.Inicial, TipoMovimiento.Compra, TipoMovimiento.Ajuste }, movimientos.OrderBy(x => x.Id).Select(x => x.Tipo));
        Assert.Equal(new[] { 12, 24, 22 }, movimientos.OrderBy(x => x.Id).Select(x => x.StockNuevo));
        Assert.Equal("Encargada", movimientos.Single(x => x.Tipo == TipoMovimiento.Compra).UsuarioNombre);
        await Assert.ThrowsAsync<ReglaNegocioException>(() => svc.ActualizarAsync(producto.Id, new() { Nombre = "Pluma editada", Precio = 11, TieneInventario = true, Stock = 50 }));
        await svc.ActualizarAsync(producto.Id, new() { Nombre = "Pluma editada", Precio = 11, TieneInventario = true, Stock = 22, CostoUnitario = 3.9167m });
        Assert.Equal(22, (await svc.ObtenerAdminPorIdAsync(tienda.Id, producto.Id))!.Stock);
        await Assert.ThrowsAsync<ReglaNegocioException>(() => svc.AjusteAsync(tienda.Id, producto.Id, new() { Cantidad = -23, Motivo = "Merma" }, null));
        Assert.Null(await svc.CompraAsync(otra.Id, producto.Id, new() { Cantidad = 1, CostoUnitario = 1 }, null));
        Assert.Null(await svc.MovimientosAsync(otra.Id, producto.Id));
        Assert.Null(typeof(ProductoDto).GetProperty("CostoUnitario"));
        Assert.Null(typeof(PedidoDto).GetProperty("CostoUnitarioHistorico"));
    }

    [Fact]
    public async Task CostoHistoricoCeroYDesconocidoResumenSoloEntregados()
    {
        using var db = TestDbContextFactory.Create();
        var tienda = await Tienda(db);
        var productos = new ProductoService(db);
        var conocido = await productos.CrearAsync(tienda.Id, new() { Nombre = "Pluma", Precio = 10, CostoUnitario = 0 });
        var sinCosto = await productos.CrearAsync(tienda.Id, new() { Nombre = "Servicio", Precio = 20 });
        var pedidos = new PedidoService(db);
        CrearPedidoDto Crear(int id) => new() { TiendaId = tienda.Id, Items = new() { new() { ProductoId = id, Cantidad = 1 } } };
        var vendido = await pedidos.CrearAsync(Crear(conocido.Id));
        var desconocido = await pedidos.CrearAsync(Crear(sinCosto.Id));
        var cancelado = await pedidos.CrearAsync(Crear(conocido.Id));
        await pedidos.ActualizarEstadoAsync(vendido.Id, EstadoPedido.Confirmado);
        await pedidos.ActualizarEstadoAsync(vendido.Id, EstadoPedido.Entregado);
        await pedidos.ActualizarEstadoPagoAsync(vendido.Id, EstadoPago.Pagado);
        await pedidos.ActualizarEstadoAsync(desconocido.Id, EstadoPedido.Confirmado);
        await pedidos.ActualizarEstadoAsync(desconocido.Id, EstadoPedido.Entregado);
        await pedidos.ActualizarEstadoAsync(cancelado.Id, EstadoPedido.Cancelado);
        await productos.ActualizarAsync(conocido.Id, new() { Nombre = "Pluma", Precio = 10, CostoUnitario = 8 });
        db.ChangeTracker.Clear();
        Assert.Equal(0, (await db.ItemsPedido.SingleAsync(x => x.PedidoId == vendido.Id)).CostoUnitarioHistorico);
        Assert.Null((await db.ItemsPedido.SingleAsync(x => x.PedidoId == desconocido.Id)).CostoUnitarioHistorico);
        var fecha = DateOnly.FromDateTime(DateTime.UtcNow);
        var resumen = await pedidos.ResumenVentasAsync(tienda.Id, fecha.AddDays(-1), fecha.AddDays(1));
        Assert.Equal(30, resumen.VentasEntregadas);
        Assert.Equal(10, resumen.UtilidadBrutaEstimada);
        Assert.Equal(1, resumen.LineasSinCosto);
        Assert.Equal(20, resumen.ImporteSinCosto);
        Assert.Equal(10, resumen.EntregadoPagado);
        Assert.Equal(20, resumen.EntregadoPendienteCobro);
        Assert.Equal(0, resumen.PedidosPorAtender);
    }

    [Fact]
    public async Task EsquemaCostosRepetibleConservaDatos()
    {
        using var db = TestDbContextFactory.Create();
        var tienda = await Tienda(db);
        var producto = await new ProductoService(db).CrearAsync(tienda.Id, new() { Nombre = "Heredado", Precio = 25, TieneInventario = true, Stock = 2 });
        await db.Database.ExecuteSqlRawAsync("DROP INDEX IF EXISTS IX_Pedidos_TiendaId_EntregadoEnUtc");
        foreach (var sql in new[] {
            "ALTER TABLE Productos DROP COLUMN CostoUnitario", "ALTER TABLE Productos DROP COLUMN ExistenciaMinima",
            "ALTER TABLE Pedidos DROP COLUMN EntregadoEnUtc", "ALTER TABLE ItemsPedido DROP COLUMN CostoUnitarioHistorico",
            "ALTER TABLE MovimientosInventario DROP COLUMN UsuarioId", "ALTER TABLE MovimientosInventario DROP COLUMN CostoUnitario" })
            await db.Database.ExecuteSqlRawAsync(sql);
        db.ChangeTracker.Clear();
        await CostosSchemaUpgrade.AplicarAsync(db);
        await CostosSchemaUpgrade.AplicarAsync(db);
        await CostosSchemaUpgrade.VerificarAsync(db);
        db.ChangeTracker.Clear();
        Assert.Equal(25, (await db.Productos.SingleAsync()).Precio);
        Assert.Equal(2, (await db.Productos.SingleAsync()).Stock);
        Assert.Single(await db.MovimientosInventario.ToListAsync());
        await new ProductoService(db).CompraAsync(tienda.Id, producto.Id, new() { Cantidad = 1, CostoUnitario = 3.9167m }, null);
        db.ChangeTracker.Clear();
        Assert.Equal(3.9167m, (await db.Productos.SingleAsync()).CostoUnitario);
        Assert.Equal(3, (await db.Productos.SingleAsync()).Stock);
    }

    [Fact]
    public async Task ResumenUsaDiaLocalDeEntregaYNoInventaInstanteHistorico()
    {
        using var db = TestDbContextFactory.Create();
        var tienda = await Tienda(db);
        var producto = await new ProductoService(db).CrearAsync(tienda.Id, new() { Nombre = "Pluma", Precio = 10, CostoUnitario = 3 });
        var svc = new PedidoService(db);
        var pedido = await svc.CrearAsync(new() { TiendaId = tienda.Id, Items = new() { new() { ProductoId = producto.Id, Cantidad = 1 } } });
        await svc.ActualizarEstadoAsync(pedido.Id, EstadoPedido.Confirmado);
        await svc.ActualizarEstadoAsync(pedido.Id, EstadoPedido.Entregado);
        var fila = await db.Pedidos.FindAsync(pedido.Id);
        fila!.CreadoEn = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
        fila.EntregadoEnUtc = new DateTime(2026, 10, 23, 5, 11, 0, DateTimeKind.Utc);
        await db.SaveChangesAsync();
        Assert.Equal(10, (await svc.ResumenVentasAsync(tienda.Id, new(2026, 10, 22), new(2026, 10, 22))).VentasEntregadas);
        Assert.Equal(0, (await svc.ResumenVentasAsync(tienda.Id, new(2026, 10, 23), new(2026, 10, 23))).VentasEntregadas);
        fila.EntregadoEnUtc = null;
        fila.HistorialEstadosJson = null;
        await db.SaveChangesAsync();
        var sinEvidencia = await svc.ResumenVentasAsync(tienda.Id, new(2026, 10, 22), new(2026, 10, 22));
        Assert.Equal(0, sinEvidencia.VentasEntregadas);
        Assert.Equal(1, sinEvidencia.EntregadosSinInstante);
    }
}

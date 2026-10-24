using Microsoft.EntityFrameworkCore;
using QueVendecion.Application.DTOs;
using QueVendecion.Application.Exceptions;
using QueVendecion.Domain.Entities;
using QueVendecion.Domain.Enums;
using QueVendecion.Infrastructure.Services;
using QueVendecion.Tests.Helpers;

namespace QueVendecion.Tests.Services;

public class VentaMostradorTests
{
    [Fact]
    public async Task VentaEntregadaPagadaMantieneCostosHistoricosYNoDuplicaCobro()
    {
        await using var db = TestDbContextFactory.Create();
        var tienda = new Tienda { Nombre = "Prueba", Slug = "prueba" };
        db.Tiendas.Add(tienda);
        await db.SaveChangesAsync();
        var producto = new Producto { TiendaId = tienda.Id, Nombre = "Copias", Precio = 3m, CostoUnitario = .70m };
        db.Productos.Add(producto);
        await db.SaveChangesAsync();
        var service = new PedidoService(db);
        var dto = new RegistrarVentaMostradorDto { ClaveIdempotencia = Guid.NewGuid(), ProductoId = producto.Id,
            Cantidad = 20, PrecioEsperado = 3m, FormaPago = FormaPago.Efectivo };
        var venta = await service.RegistrarMostradorAsync(tienda.Id, 17, dto);
        Assert.Equal(60m, venta.Total);
        Assert.Equal(OrigenPedido.Mostrador, venta.Origen);
        Assert.Equal(EstadoPedido.Entregado, venta.Estado);
        Assert.Equal(EstadoPago.Pagado, venta.EstadoPago);
        Assert.Null(venta.ClienteId);
        Assert.Equal(17, venta.CobradoPorUsuarioId);
        Assert.Equal(DateTimeKind.Utc, venta.PagadoEnUtc!.Value.Kind);
        Assert.Equal(17, Assert.Single(venta.HistorialEstados).UsuarioId);
        producto.CostoUnitario = 1m;
        producto.Precio = 4m;
        await db.SaveChangesAsync();
        var repetida = await service.RegistrarMostradorAsync(tienda.Id, 17, dto);
        Assert.Equal(venta.Id, repetida.Id);
        Assert.Equal(1, await db.Pedidos.CountAsync());
        Assert.Equal(.70m, (await db.ItemsPedido.SingleAsync()).CostoUnitarioHistorico);
        Assert.Equal(3m, (await db.ItemsPedido.SingleAsync()).PrecioUnitario);
        Assert.Equal("precio_actualizado", (await Assert.ThrowsAsync<ReglaNegocioException>(() => service.RegistrarMostradorAsync(tienda.Id, 17,
            new RegistrarVentaMostradorDto { ClaveIdempotencia = Guid.NewGuid(), ProductoId = producto.Id, Cantidad = 1, PrecioEsperado = 3m }))).Codigo);
        var zona = TimeZoneInfo.FindSystemTimeZoneById("America/Hermosillo");
        var dia = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(venta.PagadoEnUtc.Value, zona));
        var resumen = await service.ResumenVentasAsync(tienda.Id, dia, dia);
        Assert.Equal(1, resumen.PedidosEntregados);
        Assert.Equal(60m, resumen.VentasEntregadas);
        Assert.Equal(14m, resumen.CostoRegistrado);
        Assert.Equal(46m, resumen.UtilidadBrutaEstimada);
        Assert.Equal(60m, resumen.EntregadoPagado);
        Assert.Equal(0m, resumen.EntregadoPendienteCobro);
        Assert.Empty(await service.ObtenerPorClienteAsync(tienda.Id, 1));
        await Assert.ThrowsAsync<ReglaNegocioException>(() => service.ActualizarEstadoPagoAsync(venta.Id, EstadoPago.Pendiente));
    }

    [Fact]
    public async Task RechazaContenidoDistintoYProductosAjenosNoDisponiblesOConInventario()
    {
        await using var db = TestDbContextFactory.Create();
        var a = new Tienda { Nombre = "A", Slug = "a" };
        var b = new Tienda { Nombre = "B", Slug = "b" };
        db.Tiendas.AddRange(a, b);
        await db.SaveChangesAsync();
        var producto = new Producto { TiendaId = a.Id, Nombre = "Copias", Precio = 3m };
        var ajeno = new Producto { TiendaId = b.Id, Nombre = "Ajeno", Precio = 3m };
        var inventario = new Producto { TiendaId = a.Id, Nombre = "Papel", Precio = 3m, TieneInventario = true, Stock = 10 };
        var inactivo = new Producto { TiendaId = a.Id, Nombre = "Inactivo", Precio = 3m, Disponible = false };
        var negativo = new Producto { TiendaId = a.Id, Nombre = "Negativo heredado", Precio = -3m };
        var enorme = new Producto { TiendaId = a.Id, Nombre = "Enorme", Precio = 10000000000000m };
        db.Productos.AddRange(producto, ajeno, inventario, inactivo, negativo, enorme);
        await db.SaveChangesAsync();
        var service = new PedidoService(db);
        var dto = new RegistrarVentaMostradorDto { ClaveIdempotencia = Guid.NewGuid(), ProductoId = producto.Id, Cantidad = 1, PrecioEsperado = 3m };
        await service.RegistrarMostradorAsync(a.Id, 1, dto);
        dto.Cantidad = 2;
        Assert.Equal("idempotencia_conflicto", (await Assert.ThrowsAsync<ReglaNegocioException>(() => service.RegistrarMostradorAsync(a.Id, 1, dto))).Codigo);
        foreach (var id in new[] { ajeno.Id, inactivo.Id, inventario.Id })
            await Assert.ThrowsAsync<ReglaNegocioException>(() => service.RegistrarMostradorAsync(a.Id, 1,
                new RegistrarVentaMostradorDto { ClaveIdempotencia = Guid.NewGuid(), ProductoId = id, Cantidad = 1, PrecioEsperado = 3m }));
        await Assert.ThrowsAsync<ReglaNegocioException>(() => service.RegistrarMostradorAsync(a.Id, 1,
            new RegistrarVentaMostradorDto { ClaveIdempotencia = Guid.NewGuid(), ProductoId = producto.Id, Cantidad = 0, PrecioEsperado = 3m }));
        Assert.Equal("precio_invalido", (await Assert.ThrowsAsync<ReglaNegocioException>(() => service.RegistrarMostradorAsync(a.Id, 1,
            new RegistrarVentaMostradorDto { ClaveIdempotencia = Guid.NewGuid(), ProductoId = negativo.Id, Cantidad = 1, PrecioEsperado = -3m }))).Codigo);
        Assert.Equal("importe_invalido", (await Assert.ThrowsAsync<ReglaNegocioException>(() => service.RegistrarMostradorAsync(a.Id, 1,
            new RegistrarVentaMostradorDto { ClaveIdempotencia = Guid.NewGuid(), ProductoId = enorme.Id, Cantidad = 1000, PrecioEsperado = enorme.Precio }))).Codigo);
        Assert.Equal(1, await db.Pedidos.CountAsync());
    }

    [Theory]
    [InlineData(false, 1, 3, 0)]
    [InlineData(true, 0, 0, 3)]
    public async Task CostoDesconocidoYCostoCeroSonDistintos(bool costoCero, int sinCosto, int importeSinCosto, int utilidad)
    {
        await using var db = TestDbContextFactory.Create();
        var tienda = new Tienda { Nombre = "A", Slug = "a" };
        db.Tiendas.Add(tienda);
        await db.SaveChangesAsync();
        var producto = new Producto { TiendaId = tienda.Id, Nombre = "Copias", Precio = 3, CostoUnitario = costoCero ? 0m : null };
        db.Productos.Add(producto);
        await db.SaveChangesAsync();
        var service = new PedidoService(db);
        var venta = await service.RegistrarMostradorAsync(tienda.Id, 1, new RegistrarVentaMostradorDto
            { ClaveIdempotencia = Guid.NewGuid(), ProductoId = producto.Id, Cantidad = 1, PrecioEsperado = 3m });
        var dia = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(venta.PagadoEnUtc!.Value,
            TimeZoneInfo.FindSystemTimeZoneById("America/Hermosillo")));
        var resumen = await service.ResumenVentasAsync(tienda.Id, dia, dia);
        Assert.Equal(sinCosto, resumen.LineasSinCosto);
        Assert.Equal(importeSinCosto, resumen.ImporteSinCosto);
        Assert.Equal(utilidad, resumen.UtilidadBrutaEstimada);
    }
}

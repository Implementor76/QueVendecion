using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using QueVendecion.Application.DTOs;
using QueVendecion.Application.Exceptions;
using QueVendecion.Domain.Entities;
using QueVendecion.Domain.Enums;
using QueVendecion.Infrastructure.Services;
using QueVendecion.Tests.Helpers;

namespace QueVendecion.Tests.Services;

public class ConsumoPapelTests
{
    [Fact]
    public async Task DosVentasSimultaneasNoSobrevendenMismasHojas()
    {
        var ruta = Path.Combine(Path.GetTempPath(), $"qvd-papel-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<QueVendecion.Infrastructure.Data.QueVendecionDbContext>()
            .UseSqlite($"Data Source={ruta};Default Timeout=2").Options;
        try
        {
            await using (var inicial = new QueVendecion.Infrastructure.Data.QueVendecionDbContext(options))
            {
                await inicial.Database.EnsureCreatedAsync();
                var (tienda, papel, _) = await Preparar(inicial);
                papel.Stock = 10;
                await inicial.SaveChangesAsync();
            }
            async Task<bool> VenderAsync()
            {
                await using var db = new QueVendecion.Infrastructure.Data.QueVendecionDbContext(options);
                try
                {
                    await new PedidoService(db).RegistrarMostradorAsync(1, 1, new RegistrarVentaMostradorDto
                        { ClaveIdempotencia = Guid.NewGuid(), ProductoId = 1, Cantidad = 8, PrecioEsperado = 1m });
                    return true;
                }
                catch (ReglaNegocioException e) when (e.Codigo is "stock_insuficiente" or "registro_ocupado") { return false; }
            }
            var resultados = await Task.WhenAll(VenderAsync(), VenderAsync());
            Assert.Single(resultados, x => x);
            await using var verificacion = new QueVendecion.Infrastructure.Data.QueVendecionDbContext(options);
            Assert.Equal(2, (await verificacion.Productos.SingleAsync(p => p.Id == 1)).Stock);
            Assert.Equal(1, await verificacion.Pedidos.CountAsync());
            Assert.Equal(1, await verificacion.MovimientosInventario.CountAsync());
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(ruta)) File.Delete(ruta);
        }
    }
    [Theory]
    [InlineData(10, 1, false, 10, 10)]
    [InlineData(10, 1, true, 10, 5)]
    [InlineData(3, 2, true, 6, 4)]
    public async Task MostradorCalculaCarasHojasYCostosSinDuplicar(int caras, int juegos, bool doble, int cobradas, int hojas)
    {
        await using var db = TestDbContextFactory.Create();
        var (tienda, papel, servicio) = await Preparar(db);
        var pedidoService = new PedidoService(db);
        var clave = Guid.NewGuid();
        var dto = new RegistrarVentaMostradorDto { ClaveIdempotencia = clave, ProductoId = servicio.Id,
            Cantidad = cobradas, CarasPorJuego = caras, Juegos = juegos, DobleCara = doble,
            PapelInsumoIdEsperado = papel.Id, AdmiteDobleCaraEsperado = true,
            PrecioEsperado = 3m, FormaPago = FormaPago.Efectivo };
        var venta = await pedidoService.RegistrarMostradorAsync(tienda.Id, 1, dto);
        Assert.Equal(cobradas * 3m, venta.Total);
        Assert.Equal(100 - hojas, papel.Stock);
        var partida = await db.ItemsPedido.SingleAsync();
        Assert.Equal(caras, partida.CarasPorJuego);
        Assert.Equal(juegos, partida.Juegos);
        Assert.Equal(doble, partida.DobleCara);
        Assert.Equal(hojas, partida.HojasConsumidas);
        Assert.Equal(papel.Id, partida.PapelInsumoIdHistorico);
        Assert.Equal(.70m, partida.CostoUnitarioHistorico);
        var movimiento = await db.MovimientosInventario.SingleAsync();
        Assert.Equal(hojas, movimiento.Cantidad);
        Assert.Equal(100, movimiento.StockAnterior);
        Assert.Equal(100 - hojas, movimiento.StockNuevo);
        Assert.Equal(venta.Id, movimiento.PedidoId);
        Assert.Equal(1, movimiento.UsuarioId);
        Assert.Contains("Papel para servicio", movimiento.Observaciones);
        var repetida = await pedidoService.RegistrarMostradorAsync(tienda.Id, 1, dto);
        Assert.Equal(venta.Id, repetida.Id);
        Assert.Equal(1, await db.MovimientosInventario.CountAsync());
        dto.Juegos++;
        Assert.Equal("idempotencia_conflicto", (await Assert.ThrowsAsync<ReglaNegocioException>(() => pedidoService.RegistrarMostradorAsync(tienda.Id, 1, dto))).Codigo);
        var dia = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(venta.PagadoEnUtc!.Value,
            TimeZoneInfo.FindSystemTimeZoneById("America/Hermosillo")));
        var resumen = await pedidoService.ResumenVentasAsync(tienda.Id, dia, dia);
        Assert.Equal(venta.Total, resumen.VentasEntregadas);
        Assert.Equal(Math.Round(.70m * cobradas, 2), resumen.CostoRegistrado);
        Assert.Equal(Math.Round((3m - .70m) * cobradas, 2), resumen.UtilidadBrutaEstimada);
        Assert.Equal(0m, resumen.EntregadoPendienteCobro);
    }

    [Fact]
    public async Task HojasSueltasDescuentanStockYConservanCostoHistorico()
    {
        await using var db = TestDbContextFactory.Create();
        var (tienda, papel, _) = await Preparar(db);
        var venta = await new PedidoService(db).RegistrarMostradorAsync(tienda.Id, 1,
            new RegistrarVentaMostradorDto { ClaveIdempotencia = Guid.NewGuid(), ProductoId = papel.Id,
                Cantidad = 10, PrecioEsperado = 1m });
        Assert.Equal(90, papel.Stock);
        Assert.Equal(10m, venta.Total);
        Assert.Equal(.198m, (await db.ItemsPedido.SingleAsync()).CostoUnitarioHistorico);
        papel.CostoUnitario = .25m;
        await db.SaveChangesAsync();
        Assert.Equal(.198m, (await db.ItemsPedido.SingleAsync()).CostoUnitarioHistorico);
        Assert.Equal("Venta de mostrador", (await db.MovimientosInventario.SingleAsync()).Observaciones);
    }

    [Fact]
    public async Task StockInsuficienteNoDejaVentaNiCobroNiMovimiento()
    {
        await using var db = TestDbContextFactory.Create();
        var (tienda, papel, servicio) = await Preparar(db);
        papel.Stock = 3;
        await db.SaveChangesAsync();
        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => new PedidoService(db).RegistrarMostradorAsync(tienda.Id, 1,
            new RegistrarVentaMostradorDto { ClaveIdempotencia = Guid.NewGuid(), ProductoId = servicio.Id,
                Cantidad = 10, CarasPorJuego = 10, Juegos = 1, DobleCara = false,
                PapelInsumoIdEsperado = papel.Id, AdmiteDobleCaraEsperado = true, PrecioEsperado = 3m }));
        Assert.Equal("stock_insuficiente", ex.Codigo);
        Assert.Empty(await db.Pedidos.ToListAsync());
        Assert.Empty(await db.ItemsPedido.ToListAsync());
        Assert.Empty(await db.MovimientosInventario.ToListAsync());
        Assert.Equal(3, (await db.Productos.FindAsync(papel.Id))!.Stock);
    }

    [Fact]
    public async Task WebReintegraPapelOriginalUnaSolaVezAunqueCambieVinculo()
    {
        await using var db = TestDbContextFactory.Create();
        var (tienda, papel, servicio) = await Preparar(db);
        var service = new PedidoService(db);
        var web = await service.CrearAsync(new CrearPedidoDto { TiendaId = tienda.Id, ClienteNombre = "Ficticio",
            Items = new() { new() { ProductoId = servicio.Id, Cantidad = 6, CarasPorJuego = 3, Juegos = 2, DobleCara = true } } });
        Assert.Equal(96, papel.Stock);
        Assert.Equal(18m, web.Total);
        Assert.Equal(4, (await db.ItemsPedido.SingleAsync()).HojasConsumidas);
        var otro = new Producto { TiendaId = tienda.Id, Nombre = "Otro papel", Precio = 1m, TieneInventario = true, Stock = 50 };
        db.Productos.Add(otro); await db.SaveChangesAsync();
        servicio.PapelInsumoId = otro.Id;
        servicio.CostoUnitario = 1m;
        await db.SaveChangesAsync();
        await service.ActualizarEstadoAsync(web.Id, EstadoPedido.Confirmado, 1);
        await service.ActualizarEstadoAsync(web.Id, EstadoPedido.Cancelado, 1);
        await service.ActualizarEstadoAsync(web.Id, EstadoPedido.Cancelado, 1);
        Assert.Equal(100, papel.Stock);
        Assert.Equal(50, otro.Stock);
        Assert.Equal(.70m, (await db.ItemsPedido.SingleAsync()).CostoUnitarioHistorico);
        Assert.Equal(2, await db.MovimientosInventario.CountAsync(m => m.ProductoId == papel.Id));
        Assert.Equal(0, await db.MovimientosInventario.CountAsync(m => m.ProductoId == otro.Id));
    }

    [Fact]
    public async Task WebVendeHojasYServicioSinDescontarAlConfirmarOEntregar()
    {
        await using var db = TestDbContextFactory.Create();
        var (tienda, papel, servicio) = await Preparar(db);
        var service = new PedidoService(db);
        var web = await service.CrearAsync(new CrearPedidoDto { TiendaId = tienda.Id,
            Items = new() { new() { ProductoId = papel.Id, Cantidad = 10 },
                new() { ProductoId = servicio.Id, Cantidad = 10, CarasPorJuego = 10, Juegos = 1, DobleCara = true } } });
        Assert.Equal(85, papel.Stock);
        Assert.Equal(40m, web.Total);
        Assert.Equal(2, await db.MovimientosInventario.CountAsync());
        await service.ActualizarEstadoAsync(web.Id, EstadoPedido.Confirmado, 1);
        await service.ActualizarEstadoAsync(web.Id, EstadoPedido.Entregado, 1);
        Assert.Equal(85, papel.Stock);
        Assert.Equal(2, await db.MovimientosInventario.CountAsync());
    }

    [Fact]
    public async Task ConfiguracionValidaTiendaYProtegeInsumoReferenciado()
    {
        await using var db = TestDbContextFactory.Create();
        var (tienda, papel, servicio) = await Preparar(db);
        var ajena = new Tienda { Nombre = "Otra", Slug = "otra" };
        db.Tiendas.Add(ajena); await db.SaveChangesAsync();
        var papelAjeno = new Producto { TiendaId = ajena.Id, Nombre = "Papel ajeno", Precio = 1m, TieneInventario = true, Stock = 10 };
        db.Productos.Add(papelAjeno); await db.SaveChangesAsync();
        var productos = new ProductoService(db);
        var dto = new CrearProductoDto { Nombre = servicio.Nombre, Precio = 3m, CostoUnitario = .70m,
            Stock = null, TieneInventario = false, PapelInsumoId = papelAjeno.Id, AdmiteDobleCara = true };
        Assert.Equal("papel_invalido", (await Assert.ThrowsAsync<ReglaNegocioException>(() => productos.ActualizarAsync(servicio.Id, dto))).Codigo);
        Assert.Equal("insumo_referenciado", (await Assert.ThrowsAsync<ReglaNegocioException>(() => productos.EliminarAsync(papel.Id))).Codigo);
        Assert.Equal("producto_no_disponible", (await Assert.ThrowsAsync<ReglaNegocioException>(() => new PedidoService(db).RegistrarMostradorAsync(ajena.Id, 1,
            new RegistrarVentaMostradorDto { ClaveIdempotencia = Guid.NewGuid(), ProductoId = papel.Id, Cantidad = 1, PrecioEsperado = 1m }))).Codigo);
    }

    [Fact]
    public async Task CatalogoPublicoNoExponeCostoNiIdentidadDelInsumo()
    {
        await using var db = TestDbContextFactory.Create();
        var (tienda, papel, servicio) = await Preparar(db);
        var catalogo = await new ProductoService(db).ObtenerPorTiendaAsync(tienda.Id);
        var dto = Assert.Single(catalogo, p => p.Id == servicio.Id);
        Assert.True(dto.ConsumePapel);
        Assert.True(dto.AdmiteDobleCara);
        var json = JsonSerializer.Serialize(dto);
        Assert.DoesNotContain("CostoUnitario", json);
        Assert.DoesNotContain("PapelInsumoId", json);
        Assert.DoesNotContain(papel.CostoUnitario!.Value.ToString(), json);
    }

    private static async Task<(Tienda, Producto, Producto)> Preparar(QueVendecion.Infrastructure.Data.QueVendecionDbContext db)
    {
        var tienda = new Tienda { Nombre = "Prueba", Slug = "prueba" };
        db.Tiendas.Add(tienda); await db.SaveChangesAsync();
        var papel = new Producto { TiendaId = tienda.Id, Nombre = "Hoja", Precio = 1m, CostoUnitario = .198m,
            TieneInventario = true, Stock = 100, EntregaPermitida = EntregaProducto.Ambas };
        db.Productos.Add(papel); await db.SaveChangesAsync();
        var servicio = new Producto { TiendaId = tienda.Id, Nombre = "Copias", Precio = 3m,
            CostoUnitario = .70m, PapelInsumoId = papel.Id, AdmiteDobleCara = true, EntregaPermitida = EntregaProducto.Ambas };
        db.Productos.Add(servicio); await db.SaveChangesAsync();
        return (tienda, papel, servicio);
    }
}

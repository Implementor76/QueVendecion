using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using QueVendecion.Application.DTOs;
using QueVendecion.Domain.Entities;
using QueVendecion.Domain.Enums;
using QueVendecion.Infrastructure.Data;
using QueVendecion.Infrastructure.Services;
using QueVendecion.Tests.Helpers;

namespace QueVendecion.Tests.Services;

public class PedidoServiceTests : IDisposable
{
    private readonly QueVendecionDbContext _context;
    private readonly PedidoService _service;

    public PedidoServiceTests()
    {
        _context = TestDbContextFactory.Create();
        _service = new PedidoService(_context);
    }

    public void Dispose() => _context.Dispose();

    private async Task<Tienda> SeedTienda(string slug = "test-tienda")
    {
        var tienda = new Tienda { Nombre = "Test Tienda", Slug = slug };
        _context.Tiendas.Add(tienda);
        await _context.SaveChangesAsync();
        return tienda;
    }

    private async Task<Producto> SeedProducto(int tiendaId, string nombre = "Hamburguesa", decimal precio = 85m)
    {
        var producto = new Producto { TiendaId = tiendaId, Nombre = nombre, Precio = precio, EntregaPermitida = EntregaProducto.Ambas };
        _context.Productos.Add(producto);
        await _context.SaveChangesAsync();
        return producto;
    }

    private async Task<Cliente> SeedCliente(string nombre = "María", string telefono = "555-1234")
    {
        var cliente = new Cliente { Nombre = nombre, Telefono = telefono };
        _context.Clientes.Add(cliente);
        await _context.SaveChangesAsync();
        return cliente;
    }

    [Fact]
    public async Task CrearAsync_DeberiaCrearPedidoConItems()
    {
        var tienda = await SeedTienda();
        var prod1 = await SeedProducto(tienda.Id, "Hamburguesa", 85m);
        var prod2 = await SeedProducto(tienda.Id, "Papas", 35m);

        var dto = new CrearPedidoDto
        {
            TiendaId = tienda.Id,
            ClienteNombre = "Juan",
            FormaPago = FormaPago.Efectivo,
            Items = new List<CrearItemPedidoDto>
            {
                new() { ProductoId = prod1.Id, Cantidad = 2 },
                new() { ProductoId = prod2.Id, Cantidad = 1 }
            }
        };

        var resultado = await _service.CrearAsync(dto);

        Assert.NotNull(resultado);
        Assert.Equal(tienda.Id, resultado.TiendaId);
        Assert.Equal("Juan", resultado.ClienteNombre);
        Assert.Equal(FormaPago.Efectivo, resultado.FormaPago);
        Assert.Equal(EstadoPedido.Recibido, resultado.Estado);
        Assert.Equal(205m, resultado.Total); // 85*2 + 35*1
        Assert.Equal(2, resultado.Items.Count);
    }

    [Fact]
    public async Task CrearAsync_RechazaPrecioCambiadoSinGuardarPedido()
    {
        var tienda = await SeedTienda();
        var producto = await SeedProducto(tienda.Id, precio: 10m);
        producto.Precio = 12m;
        await _context.SaveChangesAsync();
        var dto = new CrearPedidoDto
        {
            TiendaId = tienda.Id,
            ClienteNombre = "Cliente de prueba",
            ClaveIdempotencia = Guid.NewGuid(),
            TotalEsperado = 10m,
            Items = new() { new() { ProductoId = producto.Id, Cantidad = 1 } }
        };

        var error = await Assert.ThrowsAsync<QueVendecion.Application.Exceptions.ReglaNegocioException>(
            () => _service.CrearAsync(dto));
        Assert.Equal("precio_actualizado", error.Codigo);
        Assert.Empty(await _context.Pedidos.ToListAsync());
    }

    [Fact]
    public async Task CrearAsync_DeberiaLanzarExcepcionSiProductoNoExiste()
    {
        var tienda = await SeedTienda();

        var dto = new CrearPedidoDto
        {
            TiendaId = tienda.Id,
            ClienteNombre = "Juan",
            FormaPago = FormaPago.Efectivo,
            Items = new List<CrearItemPedidoDto>
            {
                new() { ProductoId = 999, Cantidad = 1 }
            }
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CrearAsync(dto));
    }

    [Fact]
    public async Task CrearAsync_DeberiaGuardarClienteId()
    {
        var tienda = await SeedTienda();
        var prod = await SeedProducto(tienda.Id);
        var cliente = await SeedCliente();

        var dto = new CrearPedidoDto
        {
            TiendaId = tienda.Id,
            ClienteId = cliente.Id,
            ClienteNombre = cliente.Nombre,
            FormaPago = FormaPago.Transferencia,
            Items = new List<CrearItemPedidoDto>
            {
                new() { ProductoId = prod.Id, Cantidad = 1 }
            }
        };

        var resultado = await _service.CrearAsync(dto);

        Assert.Equal(cliente.Id, resultado.ClienteId);
    }

    [Fact]
    public async Task ObtenerPorTiendaAsync_DeberiaFiltrarPorEstado()
    {
        var tienda = await SeedTienda();
        var prod = await SeedProducto(tienda.Id);

        await _service.CrearAsync(new CrearPedidoDto
        {
            TiendaId = tienda.Id,
            ClienteNombre = "A",
            FormaPago = FormaPago.Efectivo,
            Items = new() { new() { ProductoId = prod.Id, Cantidad = 1 } }
        });

        await _service.CrearAsync(new CrearPedidoDto
        {
            TiendaId = tienda.Id,
            ClienteNombre = "B",
            FormaPago = FormaPago.Fiado,
            Items = new() { new() { ProductoId = prod.Id, Cantidad = 1 } }
        });

        var todos = await _service.ObtenerPorTiendaAsync(tienda.Id);
        Assert.Equal(2, todos.Count);

        var filtrados = await _service.ObtenerPorTiendaAsync(tienda.Id, EstadoPedido.Recibido);
        Assert.Equal(2, filtrados.Count);

        await _service.ActualizarEstadoAsync(todos[0].Id, EstadoPedido.Confirmado);
        await _service.ActualizarEstadoAsync(todos[0].Id, EstadoPedido.EnPreparacion);
        await _service.ActualizarEstadoAsync(todos[0].Id, EstadoPedido.Listo);
        var listos = await _service.ObtenerPorTiendaAsync(tienda.Id, EstadoPedido.Listo);
        Assert.Single(listos);
    }

    [Fact]
    public async Task ObtenerPorClienteAsync_DeberiaDevolverSoloPedidosDelCliente()
    {
        var tienda = await SeedTienda();
        var prod = await SeedProducto(tienda.Id);
        var cliente = await SeedCliente();

        await _service.CrearAsync(new CrearPedidoDto
        {
            TiendaId = tienda.Id,
            ClienteId = cliente.Id,
            ClienteNombre = cliente.Nombre,
            FormaPago = FormaPago.Efectivo,
            Items = new() { new() { ProductoId = prod.Id, Cantidad = 1 } }
        });

        await _service.CrearAsync(new CrearPedidoDto
        {
            TiendaId = tienda.Id,
            ClienteNombre = "Otra persona",
            FormaPago = FormaPago.Fiado,
            Items = new() { new() { ProductoId = prod.Id, Cantidad = 1 } }
        });

        var pedidosCliente = await _service.ObtenerPorClienteAsync(tienda.Id, cliente.Id);
        Assert.Single(pedidosCliente);
        Assert.Equal(cliente.Id, pedidosCliente[0].ClienteId);
    }

    [Fact]
    public async Task ActualizarEstadoAsync_DeberiaActualizarEstadoYFecha()
    {
        var tienda = await SeedTienda();
        var prod = await SeedProducto(tienda.Id);

        var pedido = await _service.CrearAsync(new CrearPedidoDto
        {
            TiendaId = tienda.Id,
            ClienteNombre = "Test",
            FormaPago = FormaPago.Efectivo,
            Items = new() { new() { ProductoId = prod.Id, Cantidad = 1 } }
        });

        await _service.ActualizarEstadoAsync(pedido.Id, EstadoPedido.Confirmado);
        await _service.ActualizarEstadoAsync(pedido.Id, EstadoPedido.EnPreparacion);
        var actualizado = await _service.ActualizarEstadoAsync(pedido.Id, EstadoPedido.Listo);

        Assert.NotNull(actualizado);
        Assert.Equal(EstadoPedido.Listo, actualizado.Estado);
        Assert.NotNull(actualizado.ActualizadoEn);
    }

    [Fact]
    public async Task ActualizarEstadoAsync_DeberiaDevolverNullSiNoExiste()
    {
        var resultado = await _service.ActualizarEstadoAsync(999, EstadoPedido.Listo);
        Assert.Null(resultado);
    }

    [Fact]
    public async Task ActualizarEstadoPagoAsync_DeberiaActualizarPago()
    {
        var tienda = await SeedTienda();
        var prod = await SeedProducto(tienda.Id);

        var pedido = await _service.CrearAsync(new CrearPedidoDto
        {
            TiendaId = tienda.Id,
            ClienteNombre = "Test",
            FormaPago = FormaPago.Fiado,
            Items = new() { new() { ProductoId = prod.Id, Cantidad = 1 } }
        });

        Assert.Equal(EstadoPago.Pendiente, pedido.EstadoPago);

        var actualizado = await _service.ActualizarEstadoPagoAsync(pedido.Id, EstadoPago.Pagado);

        Assert.NotNull(actualizado);
        Assert.Equal(EstadoPago.Pagado, actualizado.EstadoPago);
    }

    [Fact]
    public async Task EliminarAsync_DeberiaEliminarPedido()
    {
        var tienda = await SeedTienda();
        var prod = await SeedProducto(tienda.Id);

        var pedido = await _service.CrearAsync(new CrearPedidoDto
        {
            TiendaId = tienda.Id,
            ClienteNombre = "Test",
            FormaPago = FormaPago.Efectivo,
            Items = new() { new() { ProductoId = prod.Id, Cantidad = 1 } }
        });

        var eliminado = await _service.EliminarAsync(pedido.Id);
        Assert.True(eliminado);

        var noExiste = await _service.EliminarAsync(pedido.Id);
        Assert.False(noExiste);
    }

    [Fact]
    public async Task ObtenerPorIdAsync_DeberiaDevolverNullSiNoExiste()
    {
        var resultado = await _service.ObtenerPorIdAsync(999);
        Assert.Null(resultado);
    }

    [Fact]
    public async Task SQLite_SerializaInstantesDePedidoYHistorialComoUtcTrasReleer()
    {
        var tienda = await SeedTienda();
        var producto = await SeedProducto(tienda.Id);
        var creado = await _service.CrearAsync(new CrearPedidoDto
        {
            TiendaId = tienda.Id,
            Items = new() { new() { ProductoId = producto.Id, Cantidad = 1 } }
        });
        var instante = new DateTime(2026, 10, 22, 5, 11, 0, DateTimeKind.Utc);
        var pedido = await _context.Pedidos.FindAsync(creado.Id);
        Assert.NotNull(pedido);
        pedido.CreadoEn = instante;
        pedido.ActualizadoEn = instante.AddMinutes(10);
        pedido.HistorialEstadosJson = JsonSerializer.Serialize(new[]
        {
            new CambioEstadoPedidoDto { Nuevo = EstadoPedido.Recibido, FechaUtc = instante }
        });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var releido = await _service.ObtenerPorIdAsync(creado.Id);
        Assert.NotNull(releido);
        Assert.Equal(DateTimeKind.Utc, releido.CreadoEn.Kind);
        Assert.Equal(DateTimeKind.Utc, releido.ActualizadoEn!.Value.Kind);
        Assert.Equal(DateTimeKind.Utc, releido.HistorialEstados[0].FechaUtc.Kind);
        Assert.Equal("2026-10-22T05:11:00Z", JsonSerializer.Serialize(releido.CreadoEn).Trim('"'));
        Assert.Equal("2026-10-22T05:11:00Z", JsonSerializer.Serialize(releido.HistorialEstados[0].FechaUtc).Trim('"'));
    }
}

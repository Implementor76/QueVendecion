using QueVendecion.Application.DTOs;
using QueVendecion.Domain.Entities;
using QueVendecion.Infrastructure.Data;
using QueVendecion.Infrastructure.Services;
using QueVendecion.Tests.Helpers;

namespace QueVendecion.Tests.Services;

public class ProductoServiceTests : IDisposable
{
    private readonly QueVendecionDbContext _context;
    private readonly ProductoService _service;

    public ProductoServiceTests()
    {
        _context = TestDbContextFactory.Create();
        _service = new ProductoService(_context);
    }

    public void Dispose() => _context.Dispose();

    private async Task<Tienda> SeedTienda()
    {
        var tienda = new Tienda { Nombre = "Test", Slug = Guid.NewGuid().ToString() };
        _context.Tiendas.Add(tienda);
        await _context.SaveChangesAsync();
        return tienda;
    }

    [Fact]
    public async Task CrearAsync_DeberiaCrearProducto()
    {
        var tienda = await SeedTienda();

        var dto = new CrearProductoDto
        {
            Nombre = "Hamburguesa",
            Descripcion = "Doble carne",
            Precio = 85m,
            Disponible = true
        };

        var resultado = await _service.CrearAsync(tienda.Id, dto);

        Assert.NotNull(resultado);
        Assert.Equal("Hamburguesa", resultado.Nombre);
        Assert.Equal("Doble carne", resultado.Descripcion);
        Assert.Equal(85m, resultado.Precio);
        Assert.True(resultado.Disponible);
        Assert.Equal(tienda.Id, resultado.TiendaId);
    }

    [Fact]
    public async Task ObtenerPorTiendaAsync_DeberiaDevolverSoloProductosDeLaTienda()
    {
        var tienda1 = await SeedTienda();
        var tienda2 = await SeedTienda();

        await _service.CrearAsync(tienda1.Id, new CrearProductoDto { Nombre = "Prod 1", Precio = 10m });
        await _service.CrearAsync(tienda1.Id, new CrearProductoDto { Nombre = "Prod 2", Precio = 20m });
        await _service.CrearAsync(tienda2.Id, new CrearProductoDto { Nombre = "Prod 3", Precio = 30m });

        var productos = await _service.ObtenerPorTiendaAsync(tienda1.Id);
        Assert.Equal(2, productos.Count);
    }

    [Fact]
    public async Task ObtenerPorIdAsync_DeberiaEncontrarProducto()
    {
        var tienda = await SeedTienda();
        var producto = await _service.CrearAsync(tienda.Id, new CrearProductoDto
        {
            Nombre = "Tacos",
            Precio = 45m
        });

        var resultado = await _service.ObtenerPorIdAsync(producto.Id);

        Assert.NotNull(resultado);
        Assert.Equal("Tacos", resultado.Nombre);
    }

    [Fact]
    public async Task ObtenerPorIdAsync_DeberiaDevolverNullSiNoExiste()
    {
        var resultado = await _service.ObtenerPorIdAsync(999);
        Assert.Null(resultado);
    }

    [Fact]
    public async Task ActualizarAsync_DeberiaActualizarDatos()
    {
        var tienda = await SeedTienda();
        var producto = await _service.CrearAsync(tienda.Id, new CrearProductoDto
        {
            Nombre = "Original",
            Precio = 50m
        });

        var actualizado = await _service.ActualizarAsync(producto.Id, new CrearProductoDto
        {
            Nombre = "Actualizado",
            Precio = 75m,
            Disponible = false
        });

        Assert.NotNull(actualizado);
        Assert.Equal("Actualizado", actualizado.Nombre);
        Assert.Equal(75m, actualizado.Precio);
        Assert.False(actualizado.Disponible);
    }

    [Fact]
    public async Task ActualizarAsync_DeberiaDevolverNullSiNoExiste()
    {
        var resultado = await _service.ActualizarAsync(999, new CrearProductoDto
        {
            Nombre = "Test",
            Precio = 10m
        });
        Assert.Null(resultado);
    }

    [Fact]
    public async Task EliminarAsync_DeberiaEliminarProducto()
    {
        var tienda = await SeedTienda();
        var producto = await _service.CrearAsync(tienda.Id, new CrearProductoDto
        {
            Nombre = "Para eliminar",
            Precio = 10m
        });

        var eliminado = await _service.EliminarAsync(producto.Id);
        Assert.True(eliminado);

        var noExiste = await _service.EliminarAsync(producto.Id);
        Assert.False(noExiste);
    }
}

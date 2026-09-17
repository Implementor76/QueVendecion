using QueVendecion.Application.DTOs;
using QueVendecion.Domain.Entities;
using QueVendecion.Infrastructure.Data;
using QueVendecion.Infrastructure.Services;
using QueVendecion.Tests.Helpers;

namespace QueVendecion.Tests.Services;

public class TiendaServiceTests : IDisposable
{
    private readonly QueVendecionDbContext _context;
    private readonly TiendaService _service;

    public TiendaServiceTests()
    {
        _context = TestDbContextFactory.Create();
        _service = new TiendaService(_context);
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task CrearAsync_DeberiaCrearTiendaConSlugNormalizado()
    {
        var dto = new CrearTiendaDto
        {
            Nombre = "Mi Tienda",
            Slug = "Mi Tienda",
            Direccion = "Calle 123",
            Telefono = "555-0000"
        };

        var resultado = await _service.CrearAsync(dto);

        Assert.NotNull(resultado);
        Assert.Equal("Mi Tienda", resultado.Nombre);
        Assert.Equal("mi-tienda", resultado.Slug);
        Assert.Equal("Calle 123", resultado.Direccion);
        Assert.Equal("555-0000", resultado.Telefono);
    }

    [Fact]
    public async Task ObtenerPorSlugAsync_DeberiaEncontrarTienda()
    {
        await _service.CrearAsync(new CrearTiendaDto
        {
            Nombre = "Test",
            Slug = "test-slug"
        });

        var resultado = await _service.ObtenerPorSlugAsync("test-slug");

        Assert.NotNull(resultado);
        Assert.Equal("test-slug", resultado.Slug);
    }

    [Fact]
    public async Task ObtenerPorSlugAsync_DeberiaDevolverNullSiNoExiste()
    {
        var resultado = await _service.ObtenerPorSlugAsync("no-existe");
        Assert.Null(resultado);
    }

    [Fact]
    public async Task ObtenerPorIdAsync_DeberiaEncontrarTienda()
    {
        var creada = await _service.CrearAsync(new CrearTiendaDto
        {
            Nombre = "Test",
            Slug = "test-id"
        });

        var resultado = await _service.ObtenerPorIdAsync(creada.Id);

        Assert.NotNull(resultado);
        Assert.Equal("Test", resultado.Nombre);
    }

    [Fact]
    public async Task ObtenerPorIdAsync_DeberiaDevolverNullSiNoExiste()
    {
        var resultado = await _service.ObtenerPorIdAsync(999);
        Assert.Null(resultado);
    }

    [Fact]
    public async Task ObtenerTodasAsync_DeberiaDevolverTodas()
    {
        await _service.CrearAsync(new CrearTiendaDto { Nombre = "A", Slug = "a" });
        await _service.CrearAsync(new CrearTiendaDto { Nombre = "B", Slug = "b" });

        var todas = await _service.ObtenerTodasAsync();
        Assert.Equal(2, todas.Count);
    }

    [Fact]
    public async Task ActualizarAsync_DeberiaActualizarDatos()
    {
        var tienda = await _service.CrearAsync(new CrearTiendaDto
        {
            Nombre = "Original",
            Slug = "original"
        });

        var actualizada = await _service.ActualizarAsync(tienda.Id, new CrearTiendaDto
        {
            Nombre = "Actualizada",
            Slug = "actualizada",
            Direccion = "Nueva dirección"
        });

        Assert.NotNull(actualizada);
        Assert.Equal("Actualizada", actualizada.Nombre);
        Assert.Equal("actualizada", actualizada.Slug);
        Assert.Equal("Nueva dirección", actualizada.Direccion);
    }

    [Fact]
    public async Task ActualizarAsync_DeberiaDevolverNullSiNoExiste()
    {
        var resultado = await _service.ActualizarAsync(999, new CrearTiendaDto
        {
            Nombre = "Test",
            Slug = "test"
        });
        Assert.Null(resultado);
    }

    [Fact]
    public async Task EliminarAsync_DeberiaEliminarTienda()
    {
        var tienda = await _service.CrearAsync(new CrearTiendaDto
        {
            Nombre = "Para eliminar",
            Slug = "eliminar"
        });

        var eliminada = await _service.EliminarAsync(tienda.Id);
        Assert.True(eliminada);

        var noExiste = await _service.EliminarAsync(tienda.Id);
        Assert.False(noExiste);
    }

    [Fact]
    public async Task CrearAsync_DeberiaNormalizarSlugConEspacios()
    {
        var resultado = await _service.CrearAsync(new CrearTiendaDto
        {
            Nombre = "Taquería Los Amigos",
            Slug = "Taquería Los Amigos"
        });

        Assert.Equal("taquería-los-amigos", resultado.Slug);
    }
}

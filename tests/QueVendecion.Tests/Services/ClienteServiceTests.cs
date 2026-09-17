using QueVendecion.Application.DTOs;
using QueVendecion.Infrastructure.Data;
using QueVendecion.Infrastructure.Services;
using QueVendecion.Tests.Helpers;

namespace QueVendecion.Tests.Services;

public class ClienteServiceTests : IDisposable
{
    private readonly QueVendecionDbContext _context;
    private readonly ClienteService _service;

    public ClienteServiceTests()
    {
        _context = TestDbContextFactory.Create();
        _service = new ClienteService(_context);
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task CrearOCrearAsync_DeberiaCrearClienteNuevo()
    {
        var dto = new CrearClienteDto { Nombre = "María", Telefono = "555-1234" };

        var resultado = await _service.CrearOCrearAsync(dto);

        Assert.NotNull(resultado);
        Assert.Equal("María", resultado.Nombre);
        Assert.Equal("555-1234", resultado.Telefono);
        Assert.True(resultado.Id > 0);
    }

    [Fact]
    public async Task CrearOCrearAsync_DeberiaDevolverClienteExistente()
    {
        await _service.CrearOCrearAsync(new CrearClienteDto { Nombre = "María", Telefono = "555-1234" });

        var segundo = await _service.CrearOCrearAsync(new CrearClienteDto { Nombre = "María García", Telefono = "555-1234" });

        Assert.Equal("María", segundo.Nombre);
        Assert.Single(_context.Clientes);
    }

    [Fact]
    public async Task ObtenerPorTelefonoAsync_DeberiaEncontrarCliente()
    {
        await _service.CrearOCrearAsync(new CrearClienteDto { Nombre = "Juan", Telefono = "555-5678" });

        var resultado = await _service.ObtenerPorTelefonoAsync("555-5678");

        Assert.NotNull(resultado);
        Assert.Equal("Juan", resultado.Nombre);
    }

    [Fact]
    public async Task ObtenerPorTelefonoAsync_DeberiaDevolverNullSiNoExiste()
    {
        var resultado = await _service.ObtenerPorTelefonoAsync("000-0000");
        Assert.Null(resultado);
    }

    [Fact]
    public async Task ActualizarDeviceTokenAsync_DeberiaGuardarToken()
    {
        await _service.CrearOCrearAsync(new CrearClienteDto { Nombre = "Test", Telefono = "555-9999" });

        await _service.ActualizarDeviceTokenAsync("555-9999", "token-abc-123");

        var cliente = await _service.ObtenerPorTelefonoAsync("555-9999");
        Assert.NotNull(cliente);

        var entity = _context.Clientes.First(c => c.Telefono == "555-9999");
        Assert.Equal("token-abc-123", entity.DeviceToken);
    }

    [Fact]
    public async Task ActualizarDeviceTokenAsync_NoDeberiaFallarSiClienteNoExiste()
    {
        await _service.ActualizarDeviceTokenAsync("no-existe", "token");

        Assert.Empty(_context.Clientes);
    }
}

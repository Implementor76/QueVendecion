using Microsoft.AspNetCore.Mvc;
using QueVendecion.Application.DTOs;
using QueVendecion.Application.Interfaces;
using QueVendecion.Domain.Enums;

namespace QueVendecion.Api.Controllers;

[ApiController]
[Route("api/public")]
public class PublicController : ControllerBase
{
    private readonly ITiendaService _tiendaService;
    private readonly IProductoService _productoService;
    private readonly IPedidoService _pedidoService;
    private readonly IClienteService _clienteService;

    public PublicController(
        ITiendaService tiendaService,
        IProductoService productoService,
        IPedidoService pedidoService,
        IClienteService clienteService)
    {
        _tiendaService = tiendaService;
        _productoService = productoService;
        _pedidoService = pedidoService;
        _clienteService = clienteService;
    }

    [HttpGet("tienda/{slug}")]
    public async Task<ActionResult<TiendaDto>> ObtenerTienda(string slug)
    {
        var tienda = await _tiendaService.ObtenerPorSlugAsync(slug);
        if (tienda == null) return NotFound();
        return tienda;
    }

    [HttpGet("tienda/{slug}/menu")]
    public async Task<ActionResult<List<ProductoDto>>> ObtenerMenu(string slug)
    {
        var tienda = await _tiendaService.ObtenerPorSlugAsync(slug);
        if (tienda == null) return NotFound();

        var productos = await _productoService.ObtenerPorTiendaAsync(tienda.Id);
        return productos.Where(p => p.Disponible).ToList();
    }

    [HttpPost("tienda/{slug}/cliente")]
    public async Task<ActionResult<ClienteDto>> RegistrarCliente(string slug, [FromBody] CrearClienteDto dto)
    {
        var cliente = await _clienteService.CrearOCrearAsync(dto);
        return Ok(cliente);
    }

    [HttpPut("tienda/{slug}/cliente/{telefono}/device-token")]
    public async Task<IActionResult> GuardarDeviceToken(string slug, string telefono, [FromBody] DeviceTokenDto dto)
    {
        await _clienteService.ActualizarDeviceTokenAsync(telefono, dto.Token);
        return Ok();
    }

    [HttpGet("tienda/{slug}/cliente/{telefono}/pedidos")]
    public async Task<ActionResult<List<PedidoDto>>> ObtenerPedidosCliente(string slug, string telefono)
    {
        var tienda = await _tiendaService.ObtenerPorSlugAsync(slug);
        if (tienda == null) return NotFound();

        var cliente = await _clienteService.ObtenerPorTelefonoAsync(telefono);
        if (cliente == null) return Ok(new List<PedidoDto>());

        var pedidos = await _pedidoService.ObtenerPorClienteAsync(tienda.Id, cliente.Id);
        return Ok(pedidos);
    }

    [HttpPost("tienda/{slug}/pedido")]
    public async Task<ActionResult<PedidoDto>> CrearPedido(string slug, [FromBody] CrearPedidoDto dto)
    {
        var tienda = await _tiendaService.ObtenerPorSlugAsync(slug);
        if (tienda == null) return NotFound();

        dto.TiendaId = tienda.Id;
        var pedido = await _pedidoService.CrearAsync(dto);
        return CreatedAtAction(nameof(CrearPedido), new { slug }, pedido);
    }
}

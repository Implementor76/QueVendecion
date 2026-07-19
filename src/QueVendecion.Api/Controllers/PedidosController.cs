using Microsoft.AspNetCore.Mvc;
using QueVendecion.Application.DTOs;
using QueVendecion.Application.Interfaces;
using QueVendecion.Domain.Enums;

namespace QueVendecion.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PedidosController : ControllerBase
{
    private readonly IPedidoService _pedidoService;

    public PedidosController(IPedidoService pedidoService)
    {
        _pedidoService = pedidoService;
    }

    [HttpGet("tienda/{tiendaId}")]
    public async Task<ActionResult<List<PedidoDto>>> ObtenerPorTienda(int tiendaId, [FromQuery] EstadoPedido? estado)
    {
        return await _pedidoService.ObtenerPorTiendaAsync(tiendaId, estado);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<PedidoDto>> ObtenerPorId(int id)
    {
        var pedido = await _pedidoService.ObtenerPorIdAsync(id);
        if (pedido == null) return NotFound();
        return pedido;
    }

    [HttpPost]
    public async Task<ActionResult<PedidoDto>> Crear([FromBody] CrearPedidoDto dto)
    {
        var pedido = await _pedidoService.CrearAsync(dto);
        return CreatedAtAction(nameof(ObtenerPorId), new { id = pedido.Id }, pedido);
    }

    [HttpPatch("{id}/estado")]
    public async Task<ActionResult<PedidoDto>> ActualizarEstado(int id, [FromBody] ActualizarEstadoPedidoDto dto)
    {
        var pedido = await _pedidoService.ActualizarEstadoAsync(id, dto.Estado);
        if (pedido == null) return NotFound();
        return pedido;
    }

    [HttpPatch("{id}/pago")]
    public async Task<ActionResult<PedidoDto>> ActualizarEstadoPago(int id, [FromBody] ActualizarEstadoPagoDto dto)
    {
        var pedido = await _pedidoService.ActualizarEstadoPagoAsync(id, dto.EstadoPago);
        if (pedido == null) return NotFound();
        return pedido;
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Eliminar(int id)
    {
        var eliminado = await _pedidoService.EliminarAsync(id);
        if (!eliminado) return NotFound();
        return NoContent();
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QueVendecion.Application.DTOs;
using QueVendecion.Application.Interfaces;
using QueVendecion.Domain.Enums;

namespace QueVendecion.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PedidosController : ControllerBase
{
    private readonly IPedidoService _pedidoService;

    public PedidosController(IPedidoService pedidoService)
    {
        _pedidoService = pedidoService;
    }

    private int GetTiendaId() => int.Parse(User.FindFirst("TiendaId")?.Value ?? "0");

    [HttpGet("mi-tienda")]
    public async Task<ActionResult<List<PedidoDto>>> ObtenerMisPedidos([FromQuery] EstadoPedido? estado)
    {
        var tiendaId = GetTiendaId();
        return await _pedidoService.ObtenerPorTiendaAsync(tiendaId, estado);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<PedidoDto>> ObtenerPorId(int id)
    {
        var pedido = await _pedidoService.ObtenerPorIdAsync(id);
        if (pedido == null) return NotFound();
        return pedido;
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
}

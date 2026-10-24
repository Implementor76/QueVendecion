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

    [HttpPost("mi-tienda/mostrador")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<PedidoDto>> RegistrarMostrador([FromBody] RegistrarVentaMostradorDto dto)
    {
        var usuarioId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var venta = await _pedidoService.RegistrarMostradorAsync(GetTiendaId(), usuarioId, dto);
        return Ok(venta);
    }

    [HttpGet("mi-tienda/resumen")]
    public async Task<ActionResult<ResumenVentasDto>> Resumen([FromQuery] DateOnly desde, [FromQuery] DateOnly hasta) =>
        await _pedidoService.ResumenVentasAsync(GetTiendaId(), desde, hasta);

    [HttpGet("mi-tienda")]
    public async Task<ActionResult<List<PedidoDto>>> ObtenerMisPedidos([FromQuery] EstadoPedido? estado, [FromQuery] DateOnly? fechaServicio)
    {
        var tiendaId = GetTiendaId();
        return await _pedidoService.ObtenerPorTiendaAsync(tiendaId, estado, fechaServicio);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<PedidoDto>> ObtenerPorId(int id)
    {
        var pedido = await _pedidoService.ObtenerPorIdAsync(id);
        if (pedido == null) return NotFound();
        if (pedido.TiendaId != GetTiendaId()) return Forbid();
        return pedido;
    }

    [HttpPatch("{id}/estado")]
    public async Task<ActionResult<PedidoDto>> ActualizarEstado(int id, [FromBody] ActualizarEstadoPedidoDto dto)
    {
        var pedido = await _pedidoService.ObtenerPorIdAsync(id);
        if (pedido == null) return NotFound();
        if (pedido.TiendaId != GetTiendaId()) return Forbid();

        var usuarioId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var parsed) ? parsed : (int?)null;
        var actualizado = await _pedidoService.ActualizarEstadoAsync(id, dto.Estado, usuarioId);
        if (actualizado == null) return NotFound();
        return actualizado;
    }

    [HttpPatch("{id}/pago")]
    public async Task<ActionResult<PedidoDto>> ActualizarEstadoPago(int id, [FromBody] ActualizarEstadoPagoDto dto)
    {
        var pedido = await _pedidoService.ObtenerPorIdAsync(id);
        if (pedido == null) return NotFound();
        if (pedido.TiendaId != GetTiendaId()) return Forbid();

        var actualizado = await _pedidoService.ActualizarEstadoPagoAsync(id, dto.EstadoPago);
        if (actualizado == null) return NotFound();
        return actualizado;
    }

    [HttpPatch("{id}/horario")]
    public async Task<ActionResult<PedidoDto>> ConfirmarHorario(int id, [FromBody] ConfirmarHorarioPedidoDto dto)
    {
        var pedido = await _pedidoService.ObtenerPorIdAsync(id);
        if (pedido == null) return NotFound();
        if (pedido.TiendaId != GetTiendaId()) return Forbid();
        var actualizado = await _pedidoService.ConfirmarHorarioAsync(id, dto.HorarioConfirmado);
        return actualizado == null ? NotFound() : actualizado;
    }
}

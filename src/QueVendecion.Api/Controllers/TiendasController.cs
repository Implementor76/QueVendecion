using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QueVendecion.Application.DTOs;
using QueVendecion.Application.Interfaces;

namespace QueVendecion.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TiendasController : ControllerBase
{
    private readonly ITiendaService _tiendaService;
    private readonly IProductoService _productoService;

    public TiendasController(ITiendaService tiendaService, IProductoService productoService)
    {
        _tiendaService = tiendaService;
        _productoService = productoService;
    }

    private int GetTiendaId() => int.Parse(User.FindFirst("TiendaId")?.Value ?? "0");

    [HttpGet("mi-tienda")]
    public async Task<ActionResult<TiendaDto>> ObtenerMiTienda()
    {
        var tiendaId = GetTiendaId();
        var tienda = await _tiendaService.ObtenerPorIdAsync(tiendaId);
        if (tienda == null) return NotFound();
        return tienda;
    }

    [HttpPut("mi-tienda")]
    public async Task<ActionResult<TiendaDto>> ActualizarMiTienda([FromBody] CrearTiendaDto dto)
    {
        var tiendaId = GetTiendaId();
        var tienda = await _tiendaService.ActualizarAsync(tiendaId, dto);
        if (tienda == null) return NotFound();
        return tienda;
    }

    [HttpGet("mi-tienda/productos")]
    public async Task<ActionResult<List<ProductoDto>>> ObtenerMisProductos()
    {
        var tiendaId = GetTiendaId();
        return await _productoService.ObtenerPorTiendaAsync(tiendaId);
    }

    [HttpPost("mi-tienda/productos")]
    public async Task<ActionResult<ProductoDto>> CrearProducto([FromBody] CrearProductoDto dto)
    {
        var tiendaId = GetTiendaId();
        var producto = await _productoService.CrearAsync(tiendaId, dto);
        return CreatedAtAction(nameof(CrearProducto), new { id = producto.Id }, producto);
    }

    [HttpPut("mi-tienda/productos/{id}")]
    public async Task<ActionResult<ProductoDto>> ActualizarProducto(int id, [FromBody] CrearProductoDto dto)
    {
        var producto = await _productoService.ActualizarAsync(id, dto);
        if (producto == null) return NotFound();
        return producto;
    }

    [HttpDelete("mi-tienda/productos/{id}")]
    public async Task<IActionResult> EliminarProducto(int id)
    {
        var eliminado = await _productoService.EliminarAsync(id);
        if (!eliminado) return NotFound();
        return NoContent();
    }
}

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

    [HttpPut("mi-tienda/operacion")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<TiendaDto>> ActualizarOperacion([FromBody] OperacionTiendaDto dto)
    {
        var tienda = await _tiendaService.ActualizarOperacionAsync(GetTiendaId(), dto);
        return tienda == null ? NotFound() : tienda;
    }

    [HttpGet("mi-tienda/productos")]
    public async Task<ActionResult<List<ProductoDto>>> ObtenerMisProductos()
    {
        var tiendaId = GetTiendaId();
        return await _productoService.ObtenerPorTiendaAsync(tiendaId);
    }

    [HttpGet("mi-tienda/productos/{id}")]
    public async Task<ActionResult<ProductoDto>> ObtenerMiProducto(int id)
    {
        var producto = await _productoService.ObtenerPorIdAsync(id);
        if (producto == null || producto.TiendaId != GetTiendaId()) return NotFound();
        return producto;
    }

    [HttpPost("mi-tienda/productos")]
    public async Task<ActionResult<ProductoDto>> CrearProducto([FromBody] CrearProductoDto dto)
    {
        var tiendaId = GetTiendaId();
        var producto = await _productoService.CrearAsync(tiendaId, dto);
        return CreatedAtAction(nameof(ObtenerMiProducto), new { id = producto.Id }, producto);
    }

    [HttpPut("mi-tienda/productos/{id}")]
    public async Task<ActionResult<ProductoDto>> ActualizarProducto(int id, [FromBody] CrearProductoDto dto)
    {
        var tiendaId = GetTiendaId();
        var existente = await _productoService.ObtenerPorIdAsync(id);
        if (existente == null) return NotFound();
        if (existente.TiendaId != tiendaId) return Forbid();

        var producto = await _productoService.ActualizarAsync(id, dto);
        if (producto == null) return NotFound();
        return producto;
    }

    [HttpDelete("mi-tienda/productos/{id}")]
    public async Task<IActionResult> EliminarProducto(int id)
    {
        var tiendaId = GetTiendaId();
        var existente = await _productoService.ObtenerPorIdAsync(id);
        if (existente == null) return NotFound();
        if (existente.TiendaId != tiendaId) return Forbid();

        var eliminado = await _productoService.EliminarAsync(id);
        if (!eliminado) return NotFound();
        return NoContent();
    }
}

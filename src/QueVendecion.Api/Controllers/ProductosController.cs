using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using QueVendecion.Application.DTOs;
using QueVendecion.Application.Interfaces;

namespace QueVendecion.Api.Controllers;

[ApiController]
[Route("api/tiendas/{tiendaId}/[controller]")]
public class ProductosController : ControllerBase
{
    private readonly IProductoService _productoService;

    public ProductosController(IProductoService productoService)
    {
        _productoService = productoService;
    }

    [HttpGet]
    public async Task<ActionResult<List<ProductoDto>>> ObtenerPorTienda(int tiendaId)
    {
        return await _productoService.ObtenerPorTiendaAsync(tiendaId);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProductoDto>> ObtenerPorId(int id)
    {
        var producto = await _productoService.ObtenerPorIdAsync(id);
        if (producto == null) return NotFound();
        return producto;
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<ProductoDto>> Crear(int tiendaId, [FromBody] CrearProductoDto dto)
    {
        if (User.FindFirst("TiendaId")?.Value != tiendaId.ToString()) return Forbid();
        var producto = await _productoService.CrearAsync(tiendaId, dto);
        return CreatedAtAction(nameof(ObtenerPorId), new { tiendaId, id = producto.Id }, producto);
    }

    [HttpPut("{id}")]
    [Authorize]
    public async Task<ActionResult<ProductoDto>> Actualizar(int tiendaId, int id, [FromBody] CrearProductoDto dto)
    {
        if (User.FindFirst("TiendaId")?.Value != tiendaId.ToString()) return Forbid();
        var existente = await _productoService.ObtenerPorIdAsync(id);
        if (existente == null || existente.TiendaId != tiendaId) return NotFound();
        var producto = await _productoService.ActualizarAsync(id, dto);
        if (producto == null) return NotFound();
        return producto;
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> Eliminar(int tiendaId, int id)
    {
        if (User.FindFirst("TiendaId")?.Value != tiendaId.ToString()) return Forbid();
        var existente = await _productoService.ObtenerPorIdAsync(id);
        if (existente == null || existente.TiendaId != tiendaId) return NotFound();
        var eliminado = await _productoService.EliminarAsync(id);
        if (!eliminado) return NotFound();
        return NoContent();
    }
}

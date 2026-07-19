using Microsoft.AspNetCore.Mvc;
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
    public async Task<ActionResult<ProductoDto>> Crear(int tiendaId, [FromBody] CrearProductoDto dto)
    {
        var producto = await _productoService.CrearAsync(tiendaId, dto);
        return CreatedAtAction(nameof(ObtenerPorId), new { tiendaId, id = producto.Id }, producto);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ProductoDto>> Actualizar(int id, [FromBody] CrearProductoDto dto)
    {
        var producto = await _productoService.ActualizarAsync(id, dto);
        if (producto == null) return NotFound();
        return producto;
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Eliminar(int id)
    {
        var eliminado = await _productoService.EliminarAsync(id);
        if (!eliminado) return NotFound();
        return NoContent();
    }
}

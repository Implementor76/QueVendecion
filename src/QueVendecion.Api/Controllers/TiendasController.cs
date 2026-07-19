using Microsoft.AspNetCore.Mvc;
using QueVendecion.Application.DTOs;
using QueVendecion.Application.Interfaces;

namespace QueVendecion.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TiendasController : ControllerBase
{
    private readonly ITiendaService _tiendaService;

    public TiendasController(ITiendaService tiendaService)
    {
        _tiendaService = tiendaService;
    }

    [HttpGet]
    public async Task<ActionResult<List<TiendaDto>>> ObtenerTodas()
    {
        return await _tiendaService.ObtenerTodasAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<TiendaDto>> ObtenerPorId(int id)
    {
        var tienda = await _tiendaService.ObtenerPorIdAsync(id);
        if (tienda == null) return NotFound();
        return tienda;
    }

    [HttpPost]
    public async Task<ActionResult<TiendaDto>> Crear([FromBody] CrearTiendaDto dto)
    {
        var tienda = await _tiendaService.CrearAsync(dto);
        return CreatedAtAction(nameof(ObtenerPorId), new { id = tienda.Id }, tienda);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<TiendaDto>> Actualizar(int id, [FromBody] CrearTiendaDto dto)
    {
        var tienda = await _tiendaService.ActualizarAsync(id, dto);
        if (tienda == null) return NotFound();
        return tienda;
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Eliminar(int id)
    {
        var eliminado = await _tiendaService.EliminarAsync(id);
        if (!eliminado) return NotFound();
        return NoContent();
    }
}

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
    private int? GetUsuarioId() => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

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
    public async Task<ActionResult<List<ProductoAdminDto>>> ObtenerMisProductos()
    {
        var tiendaId = GetTiendaId();
        return await _productoService.ObtenerAdminPorTiendaAsync(tiendaId);
    }

    [HttpGet("mi-tienda/productos/{id}")]
    public async Task<ActionResult<ProductoAdminDto>> ObtenerMiProducto(int id)
    {
        var producto = await _productoService.ObtenerAdminPorIdAsync(GetTiendaId(), id);
        if (producto == null) return NotFound();
        return producto;
    }

    [HttpPost("mi-tienda/productos")]
    public async Task<ActionResult<ProductoAdminDto>> CrearProducto([FromBody] CrearProductoDto dto)
    {
        var tiendaId = GetTiendaId();
        var producto = await _productoService.CrearAsync(tiendaId, dto, GetUsuarioId());
        return CreatedAtAction(nameof(ObtenerMiProducto), new { id = producto.Id }, await _productoService.ObtenerAdminPorIdAsync(tiendaId, producto.Id));
    }

    [HttpPut("mi-tienda/productos/{id}")]
    public async Task<ActionResult<ProductoAdminDto>> ActualizarProducto(int id, [FromBody] CrearProductoDto dto)
    {
        var tiendaId = GetTiendaId();
        var existente = await _productoService.ObtenerPorIdAsync(id);
        if (existente == null) return NotFound();
        if (existente.TiendaId != tiendaId) return Forbid();

        var producto = await _productoService.ActualizarAsync(id, dto, GetUsuarioId());
        if (producto == null) return NotFound();
        return (await _productoService.ObtenerAdminPorIdAsync(tiendaId, id))!;
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

    [HttpPost("mi-tienda/productos/{id}/compras")]
    public async Task<ActionResult<ProductoAdminDto>> Compra(int id, [FromBody] CompraInventarioDto dto) =>
        await _productoService.CompraAsync(GetTiendaId(), id, dto, GetUsuarioId()) is { } producto ? producto : NotFound();

    [HttpPost("mi-tienda/productos/{id}/ajustes")]
    public async Task<ActionResult<ProductoAdminDto>> Ajuste(int id, [FromBody] AjusteInventarioDto dto) =>
        await _productoService.AjusteAsync(GetTiendaId(), id, dto, GetUsuarioId()) is { } producto ? producto : NotFound();

    [HttpGet("mi-tienda/productos/{id}/movimientos")]
    public async Task<ActionResult<List<MovimientoInventarioDto>>> Movimientos(int id) =>
        await _productoService.MovimientosAsync(GetTiendaId(), id) is { } movimientos ? movimientos : NotFound();
}

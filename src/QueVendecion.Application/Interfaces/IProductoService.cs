using QueVendecion.Application.DTOs;

namespace QueVendecion.Application.Interfaces;

public interface IProductoService
{
    Task<List<ProductoDto>> ObtenerPorTiendaAsync(int tiendaId);
    Task<List<ProductoAdminDto>> ObtenerAdminPorTiendaAsync(int tiendaId);
    Task<ProductoAdminDto?> ObtenerAdminPorIdAsync(int tiendaId, int id);
    Task<ProductoDto?> ObtenerPorIdAsync(int id);
    Task<ProductoDto> CrearAsync(int tiendaId, CrearProductoDto dto, int? usuarioId = null);
    Task<ProductoDto?> ActualizarAsync(int id, CrearProductoDto dto, int? usuarioId = null);
    Task<bool> EliminarAsync(int id);
    Task<ProductoAdminDto?> CompraAsync(int tiendaId, int id, CompraInventarioDto dto, int? usuarioId);
    Task<ProductoAdminDto?> AjusteAsync(int tiendaId, int id, AjusteInventarioDto dto, int? usuarioId);
    Task<List<MovimientoInventarioDto>?> MovimientosAsync(int tiendaId, int id);
}

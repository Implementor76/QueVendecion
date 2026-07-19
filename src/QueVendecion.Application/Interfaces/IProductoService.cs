using QueVendecion.Application.DTOs;

namespace QueVendecion.Application.Interfaces;

public interface IProductoService
{
    Task<List<ProductoDto>> ObtenerPorTiendaAsync(int tiendaId);
    Task<ProductoDto?> ObtenerPorIdAsync(int id);
    Task<ProductoDto> CrearAsync(int tiendaId, CrearProductoDto dto);
    Task<ProductoDto?> ActualizarAsync(int id, CrearProductoDto dto);
    Task<bool> EliminarAsync(int id);
}

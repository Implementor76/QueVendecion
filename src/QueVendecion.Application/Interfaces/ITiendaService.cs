using QueVendecion.Application.DTOs;

namespace QueVendecion.Application.Interfaces;

public interface ITiendaService
{
    Task<TiendaDto?> ObtenerPorIdAsync(int id);
    Task<TiendaDto?> ObtenerPorSlugAsync(string slug);
    Task<List<TiendaDto>> ObtenerTodasAsync();
    Task<TiendaDto> CrearAsync(CrearTiendaDto dto);
    Task<TiendaDto?> ActualizarAsync(int id, CrearTiendaDto dto);
    Task<bool> EliminarAsync(int id);
}

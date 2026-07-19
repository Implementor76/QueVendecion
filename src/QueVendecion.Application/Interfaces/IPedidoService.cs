using QueVendecion.Application.DTOs;
using QueVendecion.Domain.Enums;

namespace QueVendecion.Application.Interfaces;

public interface IPedidoService
{
    Task<List<PedidoDto>> ObtenerPorTiendaAsync(int tiendaId, EstadoPedido? estado = null);
    Task<List<PedidoDto>> ObtenerPorClienteAsync(int tiendaId, int clienteId);
    Task<PedidoDto?> ObtenerPorIdAsync(int id);
    Task<PedidoDto> CrearAsync(CrearPedidoDto dto);
    Task<PedidoDto?> ActualizarEstadoAsync(int id, EstadoPedido estado);
    Task<PedidoDto?> ActualizarEstadoPagoAsync(int id, EstadoPago estadoPago);
    Task<bool> EliminarAsync(int id);
}

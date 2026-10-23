using QueVendecion.Application.DTOs;
using QueVendecion.Domain.Enums;

namespace QueVendecion.Application.Interfaces;

public interface IPedidoService
{
    Task<ResumenVentasDto> ResumenVentasAsync(int tiendaId, DateOnly desde, DateOnly hasta);
    Task<List<PedidoDto>> ObtenerPorTiendaAsync(int tiendaId, EstadoPedido? estado = null, DateOnly? fechaServicio = null);
    Task<List<PedidoDto>> ObtenerPorClienteAsync(int tiendaId, int clienteId);
    Task<PedidoDto?> ObtenerPorIdAsync(int id);
    Task<PedidoDto> CrearAsync(CrearPedidoDto dto);
    Task<PedidoDto?> ActualizarEstadoAsync(int id, EstadoPedido estado, int? usuarioId = null);
    Task<PedidoDto?> ActualizarEstadoPagoAsync(int id, EstadoPago estadoPago);
    Task<PedidoDto?> ConfirmarHorarioAsync(int id, string? horarioConfirmado);
    Task<bool> EliminarAsync(int id);
}

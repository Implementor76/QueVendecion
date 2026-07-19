using Microsoft.EntityFrameworkCore;
using QueVendecion.Application.DTOs;
using QueVendecion.Application.Interfaces;
using QueVendecion.Domain.Entities;
using QueVendecion.Domain.Enums;
using QueVendecion.Infrastructure.Data;

namespace QueVendecion.Infrastructure.Services;

public class PedidoService : IPedidoService
{
    private readonly QueVendecionDbContext _context;

    public PedidoService(QueVendecionDbContext context)
    {
        _context = context;
    }

    public async Task<List<PedidoDto>> ObtenerPorTiendaAsync(int tiendaId, EstadoPedido? estado = null)
    {
        var query = _context.Pedidos
            .Include(p => p.Items)
            .ThenInclude(i => i.Producto)
            .Where(p => p.TiendaId == tiendaId)
            .AsQueryable();

        if (estado.HasValue)
            query = query.Where(p => p.Estado == estado.Value);

        var pedidos = await query
            .OrderByDescending(p => p.CreadoEn)
            .ToListAsync();

        return pedidos.Select(MapToDto).ToList();
    }

    public async Task<PedidoDto?> ObtenerPorIdAsync(int id)
    {
        var pedido = await _context.Pedidos
            .Include(p => p.Items)
            .ThenInclude(i => i.Producto)
            .FirstOrDefaultAsync(p => p.Id == id);

        return pedido == null ? null : MapToDto(pedido);
    }

    public async Task<PedidoDto> CrearAsync(CrearPedidoDto dto)
    {
        var pedido = new Pedido
        {
            TiendaId = dto.TiendaId,
            ClienteNombre = dto.ClienteNombre,
            FormaPago = dto.FormaPago,
            Observaciones = dto.Observaciones
        };

        foreach (var itemDto in dto.Items)
        {
            var producto = await _context.Productos.FindAsync(itemDto.ProductoId);
            if (producto == null)
                throw new InvalidOperationException($"Producto {itemDto.ProductoId} no encontrado");

            var item = new ItemPedido
            {
                ProductoId = itemDto.ProductoId,
                Cantidad = itemDto.Cantidad,
                PrecioUnitario = producto.Precio
            };

            pedido.Items.Add(item);
        }

        pedido.Total = pedido.Items.Sum(i => i.Subtotal);

        _context.Pedidos.Add(pedido);
        await _context.SaveChangesAsync();

        return await ObtenerPorIdAsync(pedido.Id) ?? MapToDto(pedido);
    }

    public async Task<PedidoDto?> ActualizarEstadoAsync(int id, EstadoPedido estado)
    {
        var pedido = await _context.Pedidos.FindAsync(id);
        if (pedido == null) return null;

        pedido.Estado = estado;
        pedido.ActualizadoEn = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return await ObtenerPorIdAsync(id);
    }

    public async Task<PedidoDto?> ActualizarEstadoPagoAsync(int id, EstadoPago estadoPago)
    {
        var pedido = await _context.Pedidos.FindAsync(id);
        if (pedido == null) return null;

        pedido.EstadoPago = estadoPago;
        pedido.ActualizadoEn = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return await ObtenerPorIdAsync(id);
    }

    public async Task<bool> EliminarAsync(int id)
    {
        var pedido = await _context.Pedidos.FindAsync(id);
        if (pedido == null) return false;

        _context.Pedidos.Remove(pedido);
        await _context.SaveChangesAsync();
        return true;
    }

    private static PedidoDto MapToDto(Pedido p) => new()
    {
        Id = p.Id,
        TiendaId = p.TiendaId,
        ClienteNombre = p.ClienteNombre,
        Estado = p.Estado,
        FormaPago = p.FormaPago,
        EstadoPago = p.EstadoPago,
        Total = p.Total,
        Observaciones = p.Observaciones,
        CreadoEn = p.CreadoEn,
        Items = p.Items.Select(i => new ItemPedidoDto
        {
            Id = i.Id,
            ProductoId = i.ProductoId,
            ProductoNombre = i.Producto?.Nombre ?? "",
            Cantidad = i.Cantidad,
            PrecioUnitario = i.PrecioUnitario,
            Subtotal = i.Subtotal
        }).ToList()
    };
}

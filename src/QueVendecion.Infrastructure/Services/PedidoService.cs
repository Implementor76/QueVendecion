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

    public async Task<List<PedidoDto>> ObtenerPorClienteAsync(int tiendaId, int clienteId)
    {
        var pedidos = await _context.Pedidos
            .Include(p => p.Items)
            .ThenInclude(i => i.Producto)
            .Where(p => p.TiendaId == tiendaId && p.ClienteId == clienteId)
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
            ClienteId = dto.ClienteId,
            ClienteNombre = dto.ClienteNombre,
            FormaPago = dto.FormaPago,
            Observaciones = dto.Observaciones
        };

        var requisiciones = new Dictionary<Producto, int>();

        foreach (var itemDto in dto.Items)
        {
            var producto = await _context.Productos.FindAsync(itemDto.ProductoId);
            if (producto == null)
                throw new InvalidOperationException($"Producto {itemDto.ProductoId} no encontrado");

            if (producto.TieneInventario && (producto.Stock ?? 0) < itemDto.Cantidad)
                throw new InvalidOperationException($"Stock insuficiente para {producto.Nombre}");

            requisiciones[producto] = itemDto.Cantidad;

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

        foreach (var (producto, cantidad) in requisiciones)
        {
            if (!producto.TieneInventario) continue;

            var stockAnterior = producto.Stock ?? 0;
            producto.Stock = stockAnterior - cantidad;

            _context.MovimientosInventario.Add(new MovimientoInventario
            {
                ProductoId = producto.Id,
                Tipo = TipoMovimiento.Salida,
                Cantidad = cantidad,
                StockAnterior = stockAnterior,
                StockNuevo = producto.Stock.Value,
                PedidoId = pedido.Id,
                Observaciones = $"Venta pedido {pedido.Id}"
            });
        }

        await _context.SaveChangesAsync();

        return await ObtenerPorIdAsync(pedido.Id) ?? MapToDto(pedido);
    }

    public async Task<PedidoDto?> ActualizarEstadoAsync(int id, EstadoPedido estado)
    {
        var pedido = await _context.Pedidos
            .Include(p => p.Items)
            .ThenInclude(i => i.Producto)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (pedido == null) return null;

        var estadoAnterior = pedido.Estado;
        pedido.Estado = estado;
        pedido.ActualizadoEn = DateTime.UtcNow;

        if (estado == EstadoPedido.Cancelado && estadoAnterior != EstadoPedido.Cancelado)
        {
            foreach (var item in pedido.Items)
            {
                var producto = item.Producto;
                if (producto == null || !producto.TieneInventario) continue;

                var stockAnterior = producto.Stock ?? 0;
                producto.Stock = stockAnterior + item.Cantidad;

                _context.MovimientosInventario.Add(new MovimientoInventario
                {
                    ProductoId = producto.Id,
                    Tipo = TipoMovimiento.Entrada,
                    Cantidad = item.Cantidad,
                    StockAnterior = stockAnterior,
                    StockNuevo = producto.Stock.Value,
                    PedidoId = pedido.Id,
                    Observaciones = "Reintegro por cancelación de pedido"
                });
            }
        }

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
        ClienteId = p.ClienteId,
        ClienteNombre = p.ClienteNombre,
        Estado = p.Estado,
        FormaPago = p.FormaPago,
        EstadoPago = p.EstadoPago,
        Total = p.Total,
        Observaciones = p.Observaciones,
        CreadoEn = p.CreadoEn,
        ActualizadoEn = p.ActualizadoEn,
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

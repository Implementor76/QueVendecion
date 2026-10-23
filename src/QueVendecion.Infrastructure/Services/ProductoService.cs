using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using QueVendecion.Application.DTOs;
using QueVendecion.Application.Interfaces;
using QueVendecion.Domain.Entities;
using QueVendecion.Infrastructure.Data;

namespace QueVendecion.Infrastructure.Services;

public class ProductoService : IProductoService
{
    private readonly QueVendecionDbContext _context;

    public ProductoService(QueVendecionDbContext context)
    {
        _context = context;
    }

    public async Task<List<ProductoDto>> ObtenerPorTiendaAsync(int tiendaId)
    {
        return await _context.Productos
            .Where(p => p.TiendaId == tiendaId)
            .Select(p => MapToDto(p))
            .ToListAsync();
    }

    public async Task<ProductoDto?> ObtenerPorIdAsync(int id)
    {
        var producto = await _context.Productos.FindAsync(id);
        return producto == null ? null : MapToDto(producto);
    }

    public async Task<ProductoDto> CrearAsync(int tiendaId, CrearProductoDto dto)
    {
        ValidarInventario(dto);
        ValidarFechas(dto);
        ValidarEntrega(dto);
        var producto = new Producto
        {
            TiendaId = tiendaId,
            Nombre = dto.Nombre,
            Descripcion = dto.Descripcion,
            Precio = dto.Precio,
            Disponible = dto.Disponible,
            ImagenUrl = dto.ImagenUrl,
            TieneInventario = dto.TieneInventario,
            Stock = dto.TieneInventario ? dto.Stock : null
            ,FechasServicioJson = dto.FechasServicio.Count == 0 ? null : JsonSerializer.Serialize(dto.FechasServicio.Distinct().Order()),
            EntregaPermitida = dto.EntregaPermitida
        };

        _context.Productos.Add(producto);
        await _context.SaveChangesAsync();

        return MapToDto(producto);
    }

    public async Task<ProductoDto?> ActualizarAsync(int id, CrearProductoDto dto)
    {
        ValidarInventario(dto);
        ValidarFechas(dto);
        ValidarEntrega(dto);
        var producto = await _context.Productos.FindAsync(id);
        if (producto == null) return null;

        producto.Nombre = dto.Nombre;
        producto.Descripcion = dto.Descripcion;
        producto.Precio = dto.Precio;
        producto.Disponible = dto.Disponible;
        producto.ImagenUrl = dto.ImagenUrl;
        producto.TieneInventario = dto.TieneInventario;
        producto.Stock = dto.TieneInventario ? dto.Stock : null;
        producto.FechasServicioJson = dto.FechasServicio.Count == 0 ? null : JsonSerializer.Serialize(dto.FechasServicio.Distinct().Order());
        producto.EntregaPermitida = dto.EntregaPermitida;

        await _context.SaveChangesAsync();
        return MapToDto(producto);
    }

    public async Task<bool> EliminarAsync(int id)
    {
        var producto = await _context.Productos.FindAsync(id);
        if (producto == null) return false;

        _context.Productos.Remove(producto);
        await _context.SaveChangesAsync();
        return true;
    }

    private static void ValidarInventario(CrearProductoDto dto)
    {
        if (dto.TieneInventario && (dto.Stock == null || dto.Stock < 0))
            throw new QueVendecion.Application.Exceptions.ReglaNegocioException("stock_invalido", "Captura un stock entero mayor o igual a cero.");
    }

    private static void ValidarFechas(CrearProductoDto dto)
    {
        if (dto.FechasServicio == null || dto.FechasServicio.Count > 366)
            throw new QueVendecion.Application.Exceptions.ReglaNegocioException("fechas_invalidas", "Configura un máximo de 366 fechas por producto.");
    }

    private static void ValidarEntrega(CrearProductoDto dto)
    {
        if (dto.EntregaPermitida == null || !Enum.IsDefined(dto.EntregaPermitida.Value))
            throw new QueVendecion.Application.Exceptions.ReglaNegocioException("entrega_producto_invalida", "Elige si el producto es para recoger, domicilio o ambos.");
    }

    private static ProductoDto MapToDto(Producto p) => new()
    {
        Id = p.Id,
        TiendaId = p.TiendaId,
        Nombre = p.Nombre,
        Descripcion = p.Descripcion,
        Precio = p.Precio,
        Disponible = p.Disponible,
        ImagenUrl = p.ImagenUrl,
        TieneInventario = p.TieneInventario,
        Stock = p.Stock,
        FechasServicio = OperacionReglas.LeerFechas(p)
        ,EntregaPermitida = p.EntregaPermitida
    };
}

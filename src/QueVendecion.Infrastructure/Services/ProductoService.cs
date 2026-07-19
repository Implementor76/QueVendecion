using Microsoft.EntityFrameworkCore;
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
        var producto = new Producto
        {
            TiendaId = tiendaId,
            Nombre = dto.Nombre,
            Descripcion = dto.Descripcion,
            Precio = dto.Precio,
            Disponible = dto.Disponible,
            ImagenUrl = dto.ImagenUrl
        };

        _context.Productos.Add(producto);
        await _context.SaveChangesAsync();

        return MapToDto(producto);
    }

    public async Task<ProductoDto?> ActualizarAsync(int id, CrearProductoDto dto)
    {
        var producto = await _context.Productos.FindAsync(id);
        if (producto == null) return null;

        producto.Nombre = dto.Nombre;
        producto.Descripcion = dto.Descripcion;
        producto.Precio = dto.Precio;
        producto.Disponible = dto.Disponible;
        producto.ImagenUrl = dto.ImagenUrl;

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

    private static ProductoDto MapToDto(Producto p) => new()
    {
        Id = p.Id,
        TiendaId = p.TiendaId,
        Nombre = p.Nombre,
        Descripcion = p.Descripcion,
        Precio = p.Precio,
        Disponible = p.Disponible,
        ImagenUrl = p.ImagenUrl
    };
}

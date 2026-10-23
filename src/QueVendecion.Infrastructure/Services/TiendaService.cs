using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using QueVendecion.Application.DTOs;
using QueVendecion.Application.Interfaces;
using QueVendecion.Domain.Entities;
using QueVendecion.Infrastructure.Data;

namespace QueVendecion.Infrastructure.Services;

public class TiendaService : ITiendaService
{
    private readonly QueVendecionDbContext _context;

    public TiendaService(QueVendecionDbContext context)
    {
        _context = context;
    }

    public async Task<TiendaDto?> ObtenerPorIdAsync(int id)
    {
        var tienda = await _context.Tiendas.FindAsync(id);
        return tienda == null ? null : MapToDto(tienda);
    }

    public async Task<TiendaDto?> ObtenerPorSlugAsync(string slug)
    {
        var tienda = await _context.Tiendas.FirstOrDefaultAsync(t => t.Slug == slug);
        return tienda == null ? null : MapToDto(tienda);
    }

    public async Task<List<TiendaDto>> ObtenerTodasAsync()
    {
        return await _context.Tiendas
            .Select(t => MapToDto(t))
            .ToListAsync();
    }

    public async Task<TiendaDto> CrearAsync(CrearTiendaDto dto)
    {
        var tienda = new Tienda
        {
            Nombre = dto.Nombre,
            Slug = dto.Slug.ToLower().Replace(" ", "-"),
            Direccion = dto.Direccion,
            Telefono = dto.Telefono
        };

        _context.Tiendas.Add(tienda);
        await _context.SaveChangesAsync();

        return MapToDto(tienda);
    }

    public async Task<TiendaDto?> ActualizarAsync(int id, CrearTiendaDto dto)
    {
        var tienda = await _context.Tiendas.FindAsync(id);
        if (tienda == null) return null;

        tienda.Nombre = dto.Nombre;
        tienda.Slug = dto.Slug.ToLower().Replace(" ", "-");
        tienda.Direccion = dto.Direccion;
        tienda.Telefono = dto.Telefono;

        await _context.SaveChangesAsync();
        return MapToDto(tienda);
    }

    public async Task<bool> EliminarAsync(int id)
    {
        var tienda = await _context.Tiendas.FindAsync(id);
        if (tienda == null) return false;

        _context.Tiendas.Remove(tienda);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<TiendaDto?> ActualizarOperacionAsync(int id, OperacionTiendaDto dto)
    {
        OperacionReglas.ValidarConfiguracion(dto);
        var tienda = await _context.Tiendas.FindAsync(id);
        if (tienda == null) return null;
        tienda.OperacionJson = JsonSerializer.Serialize(dto);
        await _context.SaveChangesAsync();
        return MapToDto(tienda);
    }

    private static TiendaDto MapToDto(Tienda t) => new()
    {
        Id = t.Id,
        Nombre = t.Nombre,
        Slug = t.Slug,
        Direccion = t.Direccion,
        Telefono = t.Telefono,
        CreadaEn = t.CreadaEn
        ,Operacion = OperacionReglas.Leer(t)
    };
}

using Microsoft.EntityFrameworkCore;
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

    private static TiendaDto MapToDto(Tienda t) => new()
    {
        Id = t.Id,
        Nombre = t.Nombre,
        Direccion = t.Direccion,
        Telefono = t.Telefono,
        CreadaEn = t.CreadaEn
    };
}

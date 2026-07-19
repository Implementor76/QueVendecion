using Microsoft.EntityFrameworkCore;
using QueVendecion.Application.DTOs;
using QueVendecion.Application.Interfaces;
using QueVendecion.Domain.Entities;
using QueVendecion.Infrastructure.Data;

namespace QueVendecion.Infrastructure.Services;

public class ClienteService : IClienteService
{
    private readonly QueVendecionDbContext _context;

    public ClienteService(QueVendecionDbContext context)
    {
        _context = context;
    }

    public async Task<ClienteDto?> ObtenerPorTelefonoAsync(string telefono)
    {
        var cliente = await _context.Clientes
            .FirstOrDefaultAsync(c => c.Telefono == telefono);

        return cliente == null ? null : MapToDto(cliente);
    }

    public async Task<ClienteDto> CrearOCrearAsync(CrearClienteDto dto)
    {
        var cliente = await _context.Clientes
            .FirstOrDefaultAsync(c => c.Telefono == dto.Telefono);

        if (cliente != null)
            return MapToDto(cliente);

        cliente = new Cliente
        {
            Nombre = dto.Nombre,
            Telefono = dto.Telefono
        };

        _context.Clientes.Add(cliente);
        await _context.SaveChangesAsync();

        return MapToDto(cliente);
    }

    public async Task ActualizarDeviceTokenAsync(string telefono, string deviceToken)
    {
        var cliente = await _context.Clientes
            .FirstOrDefaultAsync(c => c.Telefono == telefono);

        if (cliente != null)
        {
            cliente.DeviceToken = deviceToken;
            await _context.SaveChangesAsync();
        }
    }

    private static ClienteDto MapToDto(Cliente c) => new()
    {
        Id = c.Id,
        Nombre = c.Nombre,
        Telefono = c.Telefono
    };
}

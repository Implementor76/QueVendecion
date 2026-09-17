using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QueVendecion.Application.DTOs;
using QueVendecion.Domain.Entities;
using QueVendecion.Domain.Enums;
using QueVendecion.Infrastructure.Data;

namespace QueVendecion.Api.Controllers;

[ApiController]
[Route("api/superadmin")]
[Authorize(Roles = "SuperAdmin")]
public class SuperAdminController : ControllerBase
{
    private readonly QueVendecionDbContext _context;

    public SuperAdminController(QueVendecionDbContext context)
    {
        _context = context;
    }

    [HttpPost("tienda")]
    public async Task<ActionResult> CrearTiendaConAdmin([FromBody] CrearTiendaConAdminDto dto)
    {
        var tienda = new Tienda
        {
            Nombre = dto.NombreTienda,
            Slug = dto.Slug.ToLower().Replace(" ", "-"),
            Direccion = dto.Direccion,
            Telefono = dto.Telefono
        };

        _context.Tiendas.Add(tienda);
        await _context.SaveChangesAsync();

        var usuario = new UsuarioTienda
        {
            TiendaId = tienda.Id,
            Nombre = dto.AdminNombre,
            Email = dto.AdminEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.AdminPassword),
            Rol = RolUsuario.Admin
        };

        _context.UsuariosTienda.Add(usuario);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            tienda.Id,
            tienda.Nombre,
            tienda.Slug,
            admin = new { usuario.Id, usuario.Email }
        });
    }

    [HttpGet("tiendas")]
    public async Task<ActionResult> ObtenerTodas()
    {
        var tiendas = await _context.Tiendas
            .Select(t => new
            {
                t.Id,
                t.Nombre,
                t.Slug,
                t.Direccion,
                t.Telefono,
                t.CreadaEn,
                Usuarios = t.Usuarios.Count,
                Pedidos = t.Pedidos.Count
            })
            .ToListAsync();

        return Ok(tiendas);
    }

    [HttpGet("tiendas/{id}")]
    public async Task<ActionResult> ObtenerPorId(int id)
    {
        var tienda = await _context.Tiendas
            .Where(t => t.Id == id)
            .Select(t => new
            {
                t.Id,
                t.Nombre,
                t.Slug,
                t.Direccion,
                t.Telefono,
                t.CreadaEn,
                Usuarios = t.Usuarios.Select(u => new { u.Id, u.Nombre, u.Email, u.Rol }),
                Pedidos = t.Pedidos.Count
            })
            .FirstOrDefaultAsync();

        if (tienda == null) return NotFound();
        return Ok(tienda);
    }

    [HttpDelete("tiendas/{id}/pedidos")]
    public async Task<ActionResult> EliminarPedidos(int id)
    {
        var tienda = await _context.Tiendas.FindAsync(id);
        if (tienda == null) return NotFound();

        var items = await _context.ItemsPedido
            .Where(i => i.Pedido.TiendaId == id)
            .ToListAsync();
        _context.ItemsPedido.RemoveRange(items);

        var pedidos = await _context.Pedidos
            .Where(p => p.TiendaId == id)
            .ToListAsync();
        _context.Pedidos.RemoveRange(pedidos);

        await _context.SaveChangesAsync();

        return Ok(new { eliminados = pedidos.Count });
    }
}

public class CrearTiendaConAdminDto
{
    public string NombreTienda { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Direccion { get; set; }
    public string? Telefono { get; set; }
    public string AdminNombre { get; set; } = string.Empty;
    public string AdminEmail { get; set; } = string.Empty;
    public string AdminPassword { get; set; } = string.Empty;
}

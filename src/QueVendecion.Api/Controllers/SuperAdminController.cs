using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QueVendecion.Application.DTOs;
using QueVendecion.Domain.Entities;
using QueVendecion.Domain.Enums;
using QueVendecion.Infrastructure.Data;

namespace QueVendecion.Api.Controllers;

[ApiController]
[Route("api/superadmin")]
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
                Usuarios = t.Usuarios.Count
            })
            .ToListAsync();

        return Ok(tiendas);
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

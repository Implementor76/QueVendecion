using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using QueVendecion.Application.DTOs;
using QueVendecion.Application.Interfaces;
using QueVendecion.Domain.Entities;
using QueVendecion.Domain.Enums;
using QueVendecion.Infrastructure.Data;

namespace QueVendecion.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly QueVendecionDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthService(QueVendecionDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<AuthResponseDto?> LoginAsync(LoginDto dto)
    {
        var usuario = await _context.UsuariosTienda
            .FirstOrDefaultAsync(u => u.Email == dto.Email && u.Activo);

        if (usuario == null || !BCrypt.Net.BCrypt.Verify(dto.Password, usuario.PasswordHash))
            return null;

        var token = GenerateToken(usuario);

        return new AuthResponseDto
        {
            Token = token,
            Nombre = usuario.Nombre,
            Email = usuario.Email,
            Rol = usuario.Rol.ToString(),
            TiendaId = usuario.TiendaId
        };
    }

    public async Task<AuthResponseDto?> RegistrarAsync(RegistrarUsuarioDto dto)
    {
        var existe = await _context.UsuariosTienda
            .AnyAsync(u => u.Email == dto.Email && u.TiendaId == dto.TiendaId);

        if (existe) return null;

        var usuario = new UsuarioTienda
        {
            TiendaId = dto.TiendaId,
            Nombre = dto.Nombre,
            Email = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Rol = dto.Rol == "Admin" ? RolUsuario.Admin : RolUsuario.Staff
        };

        _context.UsuariosTienda.Add(usuario);
        await _context.SaveChangesAsync();

        var token = GenerateToken(usuario);

        return new AuthResponseDto
        {
            Token = token,
            Nombre = usuario.Nombre,
            Email = usuario.Email,
            Rol = usuario.Rol.ToString(),
            TiendaId = usuario.TiendaId
        };
    }

    private string GenerateToken(UsuarioTienda usuario)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? "QueVendecion_SecretKey_2024_MVP_!"));

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new Claim(ClaimTypes.Email, usuario.Email),
            new Claim(ClaimTypes.Role, usuario.Rol.ToString()),
            new Claim("TiendaId", usuario.TiendaId.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"] ?? "QueVendecion",
            audience: _configuration["Jwt:Audience"] ?? "QueVendecion",
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

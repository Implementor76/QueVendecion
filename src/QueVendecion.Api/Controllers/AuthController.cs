using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QueVendecion.Application.DTOs;
using QueVendecion.Application.Interfaces;
using QueVendecion.Infrastructure.Data;

namespace QueVendecion.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly QueVendecionDbContext _context;

    public AuthController(IAuthService authService, QueVendecionDbContext context)
    {
        _authService = authService;
        _context = context;
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginDto dto)
    {
        var result = await _authService.LoginAsync(dto);
        if (result == null) return Unauthorized(new { message = "Credenciales inválidas" });
        return result;
    }

    [Authorize]
    [HttpPost("cambiar-password")]
    public async Task<IActionResult> CambiarPassword([FromBody] CambiarPasswordDto dto)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var usuario = await _context.UsuariosTienda.FindAsync(userId);
        if (usuario == null) return NotFound();

        if (!BCrypt.Net.BCrypt.Verify(dto.PasswordActual, usuario.PasswordHash))
            return BadRequest(new { message = "La contraseña actual es incorrecta" });

        usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.PasswordNueva);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Contraseña actualizada" });
    }
}

public class CambiarPasswordDto
{
    public string PasswordActual { get; set; } = string.Empty;
    public string PasswordNueva { get; set; } = string.Empty;
}

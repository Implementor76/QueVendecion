using Microsoft.AspNetCore.Mvc;
using QueVendecion.Application.DTOs;
using QueVendecion.Application.Interfaces;

namespace QueVendecion.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginDto dto)
    {
        var result = await _authService.LoginAsync(dto);
        if (result == null) return Unauthorized(new { message = "Credenciales inválidas" });
        return result;
    }

    [HttpPost("registrar")]
    public async Task<ActionResult<AuthResponseDto>> Registrar([FromBody] RegistrarUsuarioDto dto)
    {
        var result = await _authService.RegistrarAsync(dto);
        if (result == null) return BadRequest(new { message = "El email ya existe en esta tienda" });
        return result;
    }
}

using QueVendecion.Application.DTOs;

namespace QueVendecion.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDto?> LoginAsync(LoginDto dto);
    Task<AuthResponseDto?> RegistrarAsync(RegistrarUsuarioDto dto);
}

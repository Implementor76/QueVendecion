using QueVendecion.Application.DTOs;

namespace QueVendecion.Application.Interfaces;

public interface IClienteService
{
    Task<ClienteDto?> ObtenerPorTelefonoAsync(string telefono);
    Task<ClienteDto> CrearOCrearAsync(CrearClienteDto dto);
    Task ActualizarDeviceTokenAsync(string telefono, string deviceToken);
}

using Microsoft.AspNetCore.Mvc;
using QueVendecion.Application.DTOs;
using QueVendecion.Application.Interfaces;

namespace QueVendecion.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClientesController : ControllerBase
{
    private readonly IClienteService _clienteService;

    public ClientesController(IClienteService clienteService)
    {
        _clienteService = clienteService;
    }

    [HttpGet("telefono/{telefono}")]
    public async Task<ActionResult<ClienteDto>> ObtenerPorTelefono(string telefono)
    {
        var cliente = await _clienteService.ObtenerPorTelefonoAsync(telefono);
        if (cliente == null) return NotFound();
        return cliente;
    }

    [HttpPost]
    public async Task<ActionResult<ClienteDto>> CrearOCrear([FromBody] CrearClienteDto dto)
    {
        var cliente = await _clienteService.CrearOCrearAsync(dto);
        return Ok(cliente);
    }
}

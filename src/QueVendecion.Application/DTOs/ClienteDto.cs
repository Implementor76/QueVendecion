namespace QueVendecion.Application.DTOs;

public class ClienteDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Telefono { get; set; }
}

public class CrearClienteDto
{
    public string Nombre { get; set; } = string.Empty;
    public string? Telefono { get; set; }
}

public class DeviceTokenDto
{
    public string Token { get; set; } = string.Empty;
}

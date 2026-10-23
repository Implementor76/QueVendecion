using QueVendecion.Domain.Enums;

namespace QueVendecion.Application.DTOs;

public class TiendaDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Direccion { get; set; }
    public string? Telefono { get; set; }
    public DateTime CreadaEn { get; set; }
    public OperacionTiendaDto Operacion { get; set; } = new();
}

public class CrearTiendaDto
{
    public string Nombre { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Direccion { get; set; }
    public string? Telefono { get; set; }
}

using QueVendecion.Domain.Enums;

namespace QueVendecion.Application.DTOs;

public class ProductoDto
{
    public int Id { get; set; }
    public int TiendaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal Precio { get; set; }
    public bool Disponible { get; set; }
    public string? ImagenUrl { get; set; }
    public bool TieneInventario { get; set; }
    public int? Stock { get; set; }
}

public class CrearProductoDto
{
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal Precio { get; set; }
    public bool Disponible { get; set; } = true;
    public string? ImagenUrl { get; set; }
    public bool TieneInventario { get; set; }
    public int? Stock { get; set; }
}

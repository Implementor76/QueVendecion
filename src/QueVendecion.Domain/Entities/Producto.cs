namespace QueVendecion.Domain.Entities;

public class Producto
{
    public int Id { get; set; }
    public int TiendaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal Precio { get; set; }
    public bool Disponible { get; set; } = true;
    public string? ImagenUrl { get; set; }
    public bool TieneInventario { get; set; }
    public int? Stock { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

    public Tienda Tienda { get; set; } = null!;
    public ICollection<MovimientoInventario> Movimientos { get; set; } = new List<MovimientoInventario>();
}

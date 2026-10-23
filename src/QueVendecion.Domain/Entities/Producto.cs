using QueVendecion.Domain.Enums;

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
    public decimal? CostoUnitario { get; set; }
    public int? ExistenciaMinima { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public string? FechasServicioJson { get; set; }
    // Null indicates an existing product awaiting explicit delivery mapping.
    public EntregaProducto? EntregaPermitida { get; set; }

    public Tienda Tienda { get; set; } = null!;
    public ICollection<MovimientoInventario> Movimientos { get; set; } = new List<MovimientoInventario>();
}

using QueVendecion.Domain.Enums;

namespace QueVendecion.Domain.Entities;

public class MovimientoInventario
{
    public int Id { get; set; }
    public int ProductoId { get; set; }
    public TipoMovimiento Tipo { get; set; }
    public int Cantidad { get; set; }
    public int StockAnterior { get; set; }
    public int StockNuevo { get; set; }
    public int? PedidoId { get; set; }
    public string? Observaciones { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;

    public Producto Producto { get; set; } = null!;
    public Pedido? Pedido { get; set; }
}

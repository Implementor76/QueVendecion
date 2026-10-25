namespace QueVendecion.Domain.Entities;

public class ItemPedido
{
    public int Id { get; set; }
    public int PedidoId { get; set; }
    public int ProductoId { get; set; }
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal? CostoUnitarioHistorico { get; set; }
    public int? CarasPorJuego { get; set; }
    public int? Juegos { get; set; }
    public bool? DobleCara { get; set; }
    public int? PapelInsumoIdHistorico { get; set; }
    public int? HojasConsumidas { get; set; }
    public DocumentoPdf? DocumentoPdf { get; set; }
    public decimal Subtotal => Cantidad * PrecioUnitario;

    public Pedido Pedido { get; set; } = null!;
    public Producto Producto { get; set; } = null!;
}

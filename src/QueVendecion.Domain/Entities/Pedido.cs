using QueVendecion.Domain.Enums;

namespace QueVendecion.Domain.Entities;

public class Pedido
{
    public int Id { get; set; }
    public int TiendaId { get; set; }
    public int? ClienteId { get; set; }
    public string? ClienteNombre { get; set; }
    public EstadoPedido Estado { get; set; } = EstadoPedido.Recibido;
    public FormaPago FormaPago { get; set; }
    public EstadoPago EstadoPago { get; set; } = EstadoPago.Pendiente;
    public decimal Total { get; set; }
    public string? Observaciones { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public DateTime? ActualizadoEn { get; set; }
    public ModalidadPedido? Modalidad { get; set; }
    public FormaEntrega? Entrega { get; set; }
    public string? DireccionEntrega { get; set; }
    public DateOnly? FechaServicio { get; set; }
    public string? VentanaSolicitada { get; set; }
    public string? HorarioConfirmado { get; set; }
    public string? HistorialEstadosJson { get; set; }

    public Tienda Tienda { get; set; } = null!;
    public Cliente? Cliente { get; set; }
    public ICollection<ItemPedido> Items { get; set; } = new List<ItemPedido>();
}

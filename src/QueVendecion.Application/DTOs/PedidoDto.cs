using QueVendecion.Domain.Enums;

namespace QueVendecion.Application.DTOs;

public class PedidoDto
{
    public int Id { get; set; }
    public int TiendaId { get; set; }
    public int? ClienteId { get; set; }
    public string? ClienteNombre { get; set; }
    public EstadoPedido Estado { get; set; }
    public FormaPago FormaPago { get; set; }
    public EstadoPago EstadoPago { get; set; }
    public decimal Total { get; set; }
    public string? Observaciones { get; set; }
    public DateTime CreadoEn { get; set; }
    public DateTime? ActualizadoEn { get; set; }
    public List<ItemPedidoDto> Items { get; set; } = new();
}

public class CrearPedidoDto
{
    public int TiendaId { get; set; }
    public int? ClienteId { get; set; }
    public string? ClienteNombre { get; set; }
    public string? TelefonoCliente { get; set; }
    public FormaPago FormaPago { get; set; }
    public string? Observaciones { get; set; }
    public List<CrearItemPedidoDto> Items { get; set; } = new();
}

public class ItemPedidoDto
{
    public int Id { get; set; }
    public int ProductoId { get; set; }
    public string ProductoNombre { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Subtotal { get; set; }
}

public class CrearItemPedidoDto
{
    public int ProductoId { get; set; }
    public int Cantidad { get; set; }
}

public class ActualizarEstadoPedidoDto
{
    public EstadoPedido Estado { get; set; }
}

public class ActualizarEstadoPagoDto
{
    public EstadoPago EstadoPago { get; set; }
}

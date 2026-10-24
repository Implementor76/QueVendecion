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
    public OrigenPedido Origen { get; set; }
    public DateTime? PagadoEnUtc { get; set; }
    public int? CobradoPorUsuarioId { get; set; }
    public decimal Total { get; set; }
    public string? Observaciones { get; set; }
    public DateTime CreadoEn { get; set; }
    public DateTime? ActualizadoEn { get; set; }
    public ModalidadPedido? Modalidad { get; set; }
    public FormaEntrega? Entrega { get; set; }
    public string? DireccionEntrega { get; set; }
    public DateOnly? FechaServicio { get; set; }
    public string? VentanaSolicitada { get; set; }
    public string? HorarioConfirmado { get; set; }
    public List<CambioEstadoPedidoDto> HistorialEstados { get; set; } = new();
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
    public ModalidadPedido Modalidad { get; set; } = ModalidadPedido.AlMomento;
    public FormaEntrega Entrega { get; set; } = FormaEntrega.Recoleccion;
    public string? DireccionEntrega { get; set; }
    public DateOnly? FechaServicio { get; set; }
    public string? VentanaSolicitada { get; set; }
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
    public int? CarasPorJuego { get; set; }
    public int? Juegos { get; set; }
    public bool? DobleCara { get; set; }
    public int? HojasConsumidas { get; set; }
}

public class CrearItemPedidoDto
{
    public int ProductoId { get; set; }
    public int Cantidad { get; set; }
    public int? CarasPorJuego { get; set; }
    public int? Juegos { get; set; }
    public bool? DobleCara { get; set; }
}

public class RegistrarVentaMostradorDto
{
    public Guid ClaveIdempotencia { get; set; }
    public int ProductoId { get; set; }
    public int Cantidad { get; set; }
    public int? CarasPorJuego { get; set; }
    public int? Juegos { get; set; }
    public bool? DobleCara { get; set; }
    public int? PapelInsumoIdEsperado { get; set; }
    public bool AdmiteDobleCaraEsperado { get; set; }
    public decimal PrecioEsperado { get; set; }
    public FormaPago FormaPago { get; set; }
    public string? Nota { get; set; }
}

public class ActualizarEstadoPedidoDto
{
    public EstadoPedido Estado { get; set; }
}

public class ActualizarEstadoPagoDto
{
    public EstadoPago EstadoPago { get; set; }
}

public class ConfirmarHorarioPedidoDto
{
    public string? HorarioConfirmado { get; set; }
}

public class CambioEstadoPedidoDto
{
    public EstadoPedido? Anterior { get; set; }
    public EstadoPedido Nuevo { get; set; }
    public DateTime FechaUtc { get; set; }
    public int? UsuarioId { get; set; }
}

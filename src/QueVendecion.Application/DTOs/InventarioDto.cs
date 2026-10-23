using QueVendecion.Domain.Enums;

namespace QueVendecion.Application.DTOs;

public class CompraInventarioDto
{
    public int Cantidad { get; set; }
    public decimal CostoUnitario { get; set; }
    public string? Observaciones { get; set; }
}

public class AjusteInventarioDto
{
    public int Cantidad { get; set; } // Signed delta.
    public string Motivo { get; set; } = string.Empty;
}

public class MovimientoInventarioDto
{
    public int Id { get; set; }
    public TipoMovimiento Tipo { get; set; }
    public int Cantidad { get; set; }
    public int StockAnterior { get; set; }
    public int StockNuevo { get; set; }
    public int? PedidoId { get; set; }
    public int? UsuarioId { get; set; }
    public string? UsuarioNombre { get; set; }
    public decimal? CostoUnitario { get; set; }
    public string? Observaciones { get; set; }
    public DateTime FechaUtc { get; set; }
}

public class ResumenVentasDto
{
    public decimal VentasEntregadas { get; set; }
    public decimal CostoRegistrado { get; set; }
    public decimal UtilidadBrutaEstimada { get; set; }
    public int LineasSinCosto { get; set; }
    public decimal ImporteSinCosto { get; set; }
    public int PedidosEntregados { get; set; }
    public int EntregadosSinInstante { get; set; }
    public int PedidosPorAtender { get; set; }
    public decimal EntregadoPagado { get; set; }
    public decimal EntregadoPendienteCobro { get; set; }
}

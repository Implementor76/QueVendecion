namespace QueVendecion.Domain.Entities;

public class DocumentoPdf
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int TiendaId { get; set; }
    public int ClienteId { get; set; }
    public string SesionHash { get; set; } = string.Empty;
    public int? ItemPedidoId { get; set; }
    public string NombreOriginal { get; set; } = string.Empty;
    public string NombreInterno { get; set; } = string.Empty;
    public long TamanoBytes { get; set; }
    public string Tipo { get; set; } = "application/pdf";
    public int PaginasTotales { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public DateTime CreadoEnUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiraEnUtc { get; set; }
    // Temporal, Asociado, Eliminado.
    public string Estado { get; set; } = "Temporal";
    public string? PaginasSeleccionadas { get; set; }
    public ItemPedido? ItemPedido { get; set; }
}

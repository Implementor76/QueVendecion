namespace QueVendecion.Domain.Entities;

public class SesionDocumento
{
    public int Id { get; set; }
    public int TiendaId { get; set; }
    public int ClienteId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiraEnUtc { get; set; }
}

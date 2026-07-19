namespace QueVendecion.Domain.Entities;

public class Tienda
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Direccion { get; set; }
    public string? Telefono { get; set; }
    public DateTime CreadaEn { get; set; } = DateTime.UtcNow;

    public ICollection<UsuarioTienda> Usuarios { get; set; } = new List<UsuarioTienda>();
    public ICollection<Producto> Productos { get; set; } = new List<Producto>();
    public ICollection<Pedido> Pedidos { get; set; } = new List<Pedido>();
}

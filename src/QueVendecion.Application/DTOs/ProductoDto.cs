using QueVendecion.Domain.Enums;

namespace QueVendecion.Application.DTOs;

public class ProductoDto
{
    public int Id { get; set; }
    public int TiendaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal Precio { get; set; }
    public bool Disponible { get; set; }
    public string? ImagenUrl { get; set; }
    public bool TieneInventario { get; set; }
    public int? Stock { get; set; }
    public bool ConsumePapel { get; set; }
    public bool AdmiteDobleCara { get; set; }
    public List<DateOnly> FechasServicio { get; set; } = new();
    public EntregaProducto? EntregaPermitida { get; set; }
}

public class CrearProductoDto
{
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal Precio { get; set; }
    public bool Disponible { get; set; } = true;
    public string? ImagenUrl { get; set; }
    public bool TieneInventario { get; set; }
    public int? Stock { get; set; }
    public decimal? CostoUnitario { get; set; }
    public int? ExistenciaMinima { get; set; }
    public int? PapelInsumoId { get; set; }
    public bool AdmiteDobleCara { get; set; }
    public List<DateOnly> FechasServicio { get; set; } = new();
    public EntregaProducto? EntregaPermitida { get; set; } = EntregaProducto.Ambas;
}

public class ProductoAdminDto : ProductoDto
{
    public decimal? CostoUnitario { get; set; }
    public int? ExistenciaMinima { get; set; }
    public int? PapelInsumoId { get; set; }
}

using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using QueVendecion.Application.DTOs;
using QueVendecion.Application.Exceptions;
using QueVendecion.Application.Interfaces;
using QueVendecion.Domain.Entities;
using QueVendecion.Domain.Enums;
using System.Data;
using QueVendecion.Infrastructure.Data;

namespace QueVendecion.Infrastructure.Services;

public class ProductoService : IProductoService
{
    private readonly QueVendecionDbContext _context;

    public ProductoService(QueVendecionDbContext context)
    {
        _context = context;
    }

    public async Task<List<ProductoDto>> ObtenerPorTiendaAsync(int tiendaId)
    {
        return await _context.Productos
            .Where(p => p.TiendaId == tiendaId)
            .Select(p => MapToDto(p))
            .ToListAsync();
    }

    public async Task<List<ProductoAdminDto>> ObtenerAdminPorTiendaAsync(int tiendaId) =>
        (await _context.Productos.AsNoTracking().Where(p => p.TiendaId == tiendaId).OrderBy(p => p.Nombre).ToListAsync()).Select(MapAdmin).ToList();

    public async Task<ProductoAdminDto?> ObtenerAdminPorIdAsync(int tiendaId, int id)
    {
        var p = await _context.Productos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.TiendaId == tiendaId);
        return p == null ? null : MapAdmin(p);
    }

    public async Task<ProductoDto?> ObtenerPorIdAsync(int id)
    {
        var producto = await _context.Productos.FindAsync(id);
        return producto == null ? null : MapToDto(producto);
    }

    public async Task<ProductoDto> CrearAsync(int tiendaId, CrearProductoDto dto, int? usuarioId = null)
    {
        ValidarInventario(dto);
        ValidarCostoYMinimo(dto);
        ValidarFechas(dto);
        ValidarEntrega(dto);
        await ValidarUsuarioAsync(tiendaId, usuarioId);
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await ValidarPapelAsync(tiendaId, null, dto);
        var producto = new Producto
        {
            TiendaId = tiendaId,
            Nombre = dto.Nombre,
            Descripcion = dto.Descripcion,
            Precio = dto.Precio,
            Disponible = dto.Disponible,
            ImagenUrl = dto.ImagenUrl,
            TieneInventario = dto.TieneInventario,
            Stock = dto.TieneInventario ? dto.Stock : null,
            CostoUnitario = dto.CostoUnitario,
            ExistenciaMinima = dto.TieneInventario ? dto.ExistenciaMinima : null,
            PapelInsumoId = dto.PapelInsumoId,
            AdmiteDobleCara = dto.AdmiteDobleCara,
            RequierePdf = dto.RequierePdf,
            RevisionImpresion = dto.RequierePdf ? Guid.NewGuid().ToString("N") : null,
            FechasServicioJson = dto.FechasServicio.Count == 0 ? null : JsonSerializer.Serialize(dto.FechasServicio.Distinct().Order()),
            EntregaPermitida = dto.EntregaPermitida
        };

        _context.Productos.Add(producto);
        await _context.SaveChangesAsync();
        if (producto.TieneInventario && producto.Stock > 0)
        {
            _context.MovimientosInventario.Add(new MovimientoInventario { ProductoId = producto.Id,
                Tipo = TipoMovimiento.Inicial, Cantidad = producto.Stock.Value, StockAnterior = 0,
                StockNuevo = producto.Stock.Value, UsuarioId = usuarioId,
                CostoUnitario = producto.CostoUnitario, Observaciones = "Existencia inicial" });
            await _context.SaveChangesAsync();
        }
        await tx.CommitAsync();

        return MapToDto(producto);
    }

    public async Task<ProductoDto?> ActualizarAsync(int id, CrearProductoDto dto, int? usuarioId = null)
    {
        ValidarInventario(dto);
        ValidarCostoYMinimo(dto);
        ValidarFechas(dto);
        ValidarEntrega(dto);
        var producto = await _context.Productos.FindAsync(id);
        if (producto == null) return null;
        await ValidarUsuarioAsync(producto.TiendaId, usuarioId);
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await _context.Entry(producto).ReloadAsync();
        await ValidarPapelAsync(producto.TiendaId, id, dto);
        if (producto.TieneInventario && !dto.TieneInventario)
            throw new ReglaNegocioException("inventario_activo", "No se puede desactivar el inventario de un producto existente.");
        if (producto.TieneInventario && dto.Stock != producto.Stock)
            throw new ReglaNegocioException("stock_por_movimiento", "Registra entradas o ajustes para cambiar existencias.");
        var activar = !producto.TieneInventario && dto.TieneInventario;

        producto.Nombre = dto.Nombre;
        producto.Descripcion = dto.Descripcion;
        producto.Precio = dto.Precio;
        producto.Disponible = dto.Disponible;
        producto.ImagenUrl = dto.ImagenUrl;
        producto.TieneInventario = dto.TieneInventario;
        if (activar) producto.Stock = dto.Stock;
        producto.CostoUnitario = dto.CostoUnitario;
        producto.ExistenciaMinima = dto.TieneInventario ? dto.ExistenciaMinima : null;
        producto.PapelInsumoId = dto.PapelInsumoId;
        producto.AdmiteDobleCara = dto.AdmiteDobleCara;
        producto.RequierePdf = dto.RequierePdf;
        producto.RevisionImpresion = dto.RequierePdf ? Guid.NewGuid().ToString("N") : null;
        producto.FechasServicioJson = dto.FechasServicio.Count == 0 ? null : JsonSerializer.Serialize(dto.FechasServicio.Distinct().Order());
        producto.EntregaPermitida = dto.EntregaPermitida;

        await _context.SaveChangesAsync();
        if (activar && producto.Stock > 0)
        {
            _context.MovimientosInventario.Add(new MovimientoInventario { ProductoId = id,
                Tipo = TipoMovimiento.Inicial, Cantidad = producto.Stock.Value, StockAnterior = 0,
                StockNuevo = producto.Stock.Value, UsuarioId = usuarioId,
                CostoUnitario = producto.CostoUnitario, Observaciones = "Activación de inventario" });
            await _context.SaveChangesAsync();
        }
        await tx.CommitAsync();
        return MapToDto(producto);
    }

    public async Task<bool> EliminarAsync(int id)
    {
        var producto = await _context.Productos.FindAsync(id);
        if (producto == null) return false;
        if (await _context.Productos.AnyAsync(p => p.PapelInsumoId == id))
            throw new ReglaNegocioException("insumo_referenciado", "Desvincula este papel de los servicios antes de borrarlo.");
        if (await _context.ItemsPedido.AnyAsync(i => i.ProductoId == id) || await _context.MovimientosInventario.AnyAsync(m => m.ProductoId == id))
            throw new ReglaNegocioException("producto_con_historial", "No se puede borrar un producto con pedidos o movimientos. Márcalo como no disponible.");

        _context.Productos.Remove(producto);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<ProductoAdminDto?> CompraAsync(int tiendaId, int id, CompraInventarioDto dto, int? usuarioId)
    {
        if (dto.Cantidad <= 0 || dto.Observaciones?.Length > 200 || dto.CostoUnitario < 0 || dto.CostoUnitario > 99999999999999m)
            throw new ReglaNegocioException("movimiento_invalido", "Captura cantidad positiva, costo no negativo y observación breve.");
        await ValidarUsuarioAsync(tiendaId, usuarioId);
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var p = await _context.Productos.FirstOrDefaultAsync(x => x.Id == id && x.TiendaId == tiendaId);
        if (p == null) return null;
        await _context.Entry(p).ReloadAsync();
        if (!p.TieneInventario) throw new ReglaNegocioException("inventario_inactivo", "Activa el control de inventario primero.");
        var anterior = p.Stock ?? throw new ReglaNegocioException("stock_invalido", "Inventario sin existencia registrada.");
        if (dto.Cantidad > int.MaxValue - anterior)
            throw new ReglaNegocioException("stock_invalido", "La cantidad excede el máximo permitido.");
        p.Stock = checked(anterior + dto.Cantidad);
        p.CostoUnitario = Math.Round(dto.CostoUnitario, 4, MidpointRounding.AwayFromZero);
        _context.MovimientosInventario.Add(new MovimientoInventario { ProductoId = id, Tipo = TipoMovimiento.Compra,
            Cantidad = dto.Cantidad, StockAnterior = anterior, StockNuevo = p.Stock.Value,
            CostoUnitario = p.CostoUnitario, UsuarioId = usuarioId, Observaciones = dto.Observaciones?.Trim() });
        await _context.SaveChangesAsync();
        await tx.CommitAsync();
        return MapAdmin(p);
    }

    public async Task<ProductoAdminDto?> AjusteAsync(int tiendaId, int id, AjusteInventarioDto dto, int? usuarioId)
    {
        if (dto.Cantidad == 0 || string.IsNullOrWhiteSpace(dto.Motivo) || dto.Motivo.Length > 200)
            throw new ReglaNegocioException("movimiento_invalido", "Captura una variación y un motivo breve.");
        await ValidarUsuarioAsync(tiendaId, usuarioId);
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var p = await _context.Productos.FirstOrDefaultAsync(x => x.Id == id && x.TiendaId == tiendaId);
        if (p == null) return null;
        await _context.Entry(p).ReloadAsync();
        if (!p.TieneInventario) throw new ReglaNegocioException("inventario_inactivo", "Activa el control de inventario primero.");
        var anterior = p.Stock ?? throw new ReglaNegocioException("stock_invalido", "Inventario sin existencia registrada.");
        var resultado = (long)anterior + dto.Cantidad;
        if (resultado > int.MaxValue || resultado < 0)
            throw new ReglaNegocioException("stock_insuficiente", "El ajuste deja existencias fuera del rango permitido.");
        var nuevo = (int)resultado;
        p.Stock = nuevo;
        _context.MovimientosInventario.Add(new MovimientoInventario { ProductoId = id, Tipo = TipoMovimiento.Ajuste,
            Cantidad = dto.Cantidad, StockAnterior = anterior, StockNuevo = nuevo,
            UsuarioId = usuarioId, Observaciones = dto.Motivo.Trim() });
        await _context.SaveChangesAsync();
        await tx.CommitAsync();
        return MapAdmin(p);
    }

    public async Task<List<MovimientoInventarioDto>?> MovimientosAsync(int tiendaId, int id)
    {
        if (!await _context.Productos.AnyAsync(p => p.Id == id && p.TiendaId == tiendaId)) return null;
        var movimientos = await _context.MovimientosInventario.AsNoTracking().Where(m => m.ProductoId == id)
            .OrderByDescending(m => m.Fecha).ThenByDescending(m => m.Id).ToListAsync();
        var usuarios = await _context.UsuariosTienda.AsNoTracking().Where(u => u.TiendaId == tiendaId)
            .ToDictionaryAsync(u => u.Id, u => u.Nombre);
        return movimientos
            .Select(m => new MovimientoInventarioDto { Id = m.Id, Tipo = m.Tipo, Cantidad = m.Cantidad,
                StockAnterior = m.StockAnterior, StockNuevo = m.StockNuevo, PedidoId = m.PedidoId,
                UsuarioId = m.UsuarioId, UsuarioNombre = m.UsuarioId.HasValue ? usuarios.GetValueOrDefault(m.UsuarioId.Value) : null,
                CostoUnitario = m.CostoUnitario, Observaciones = m.Observaciones,
                FechaUtc = DateTime.SpecifyKind(m.Fecha, DateTimeKind.Utc) }).ToList();
    }

    private async Task ValidarUsuarioAsync(int tiendaId, int? usuarioId)
    {
        if (usuarioId.HasValue && !await _context.UsuariosTienda.AnyAsync(u => u.Id == usuarioId && u.TiendaId == tiendaId))
            throw new ReglaNegocioException("usuario_invalido", "El usuario no pertenece a esta tienda.");
    }

    private async Task ValidarPapelAsync(int tiendaId, int? servicioId, CrearProductoDto dto)
    {
        if (dto.RequierePdf && (dto.TieneInventario || dto.PapelInsumoId == null ||
            dto.EntregaPermitida is not (QueVendecion.Domain.Enums.EntregaProducto.Recoleccion or QueVendecion.Domain.Enums.EntregaProducto.Ambas)))
            throw new ReglaNegocioException("pdf_configuracion_invalida", "La impresión PDF requiere papel vinculado, sin inventario propio y recolección disponible.");
        if (dto.RequierePdf && !OperacionReglas.Leer(await _context.Tiendas.FindAsync(tiendaId)
            ?? throw new ReglaNegocioException("tienda_no_disponible", "Tienda no encontrada.")).Recoleccion)
            throw new ReglaNegocioException("pdf_configuracion_invalida", "Habilita la recolección de la tienda antes de ofrecer impresiones PDF.");
        if (dto.PapelInsumoId == null)
        {
            if (dto.AdmiteDobleCara)
                throw new ReglaNegocioException("papel_invalido", "Selecciona el papel antes de habilitar ambos lados.");
            return;
        }
        if (dto.TieneInventario || dto.PapelInsumoId == servicioId)
            throw new ReglaNegocioException("papel_invalido", "El servicio debe estar sin inventario y no puede usarse como su propio insumo.");
        var insumo = await _context.Productos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == dto.PapelInsumoId && p.TiendaId == tiendaId);
        if (insumo == null || !insumo.TieneInventario || insumo.PapelInsumoId != null)
            throw new ReglaNegocioException("papel_invalido", "Elige un producto de esta tienda con inventario activo como papel.");
    }

    private static void ValidarCostoYMinimo(CrearProductoDto dto)
    {
        if (dto.CostoUnitario < 0 || dto.CostoUnitario > 99999999999999m)
            throw new ReglaNegocioException("costo_invalido", "El costo debe ser mayor o igual a cero.");
        if (dto.ExistenciaMinima < 0 || !dto.TieneInventario && dto.ExistenciaMinima.HasValue)
            throw new ReglaNegocioException("minimo_invalido", "La existencia mínima requiere inventario y no puede ser negativa.");
    }

    private static void ValidarInventario(CrearProductoDto dto)
    {
        if (dto.TieneInventario && (dto.Stock == null || dto.Stock < 0))
            throw new QueVendecion.Application.Exceptions.ReglaNegocioException("stock_invalido", "Captura un stock entero mayor o igual a cero.");
    }

    private static void ValidarFechas(CrearProductoDto dto)
    {
        if (dto.FechasServicio == null || dto.FechasServicio.Count > 366)
            throw new QueVendecion.Application.Exceptions.ReglaNegocioException("fechas_invalidas", "Configura un máximo de 366 fechas por producto.");
    }

    private static void ValidarEntrega(CrearProductoDto dto)
    {
        if (dto.EntregaPermitida == null || !Enum.IsDefined(dto.EntregaPermitida.Value))
            throw new QueVendecion.Application.Exceptions.ReglaNegocioException("entrega_producto_invalida", "Elige si el producto es para recoger, domicilio o ambos.");
    }

    private static ProductoDto MapToDto(Producto p) => new()
    {
        Id = p.Id,
        TiendaId = p.TiendaId,
        Nombre = p.Nombre,
        Descripcion = p.Descripcion,
        Precio = p.Precio,
        Disponible = p.Disponible,
        ImagenUrl = p.ImagenUrl,
        TieneInventario = p.TieneInventario,
        Stock = p.Stock,
        ConsumePapel = p.PapelInsumoId.HasValue,
        AdmiteDobleCara = p.AdmiteDobleCara,
        RequierePdf = p.RequierePdf,
        RevisionImpresion = p.RequierePdf ? p.RevisionImpresion : null,
        FechasServicio = OperacionReglas.LeerFechas(p),
        EntregaPermitida = p.EntregaPermitida
    };

    private static ProductoAdminDto MapAdmin(Producto p) => new()
    {
        Id = p.Id, TiendaId = p.TiendaId, Nombre = p.Nombre, Descripcion = p.Descripcion,
        Precio = p.Precio, Disponible = p.Disponible, ImagenUrl = p.ImagenUrl,
        TieneInventario = p.TieneInventario, Stock = p.Stock,
        ConsumePapel = p.PapelInsumoId.HasValue, AdmiteDobleCara = p.AdmiteDobleCara, RequierePdf = p.RequierePdf,
        RevisionImpresion = p.RevisionImpresion,
        FechasServicio = OperacionReglas.LeerFechas(p), EntregaPermitida = p.EntregaPermitida,
        CostoUnitario = p.CostoUnitario, ExistenciaMinima = p.ExistenciaMinima, PapelInsumoId = p.PapelInsumoId
    };
}

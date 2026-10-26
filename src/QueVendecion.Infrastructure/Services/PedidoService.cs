using System.Data;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using QueVendecion.Application.Exceptions;
using Microsoft.EntityFrameworkCore;
using QueVendecion.Application.DTOs;
using QueVendecion.Application.Interfaces;
using QueVendecion.Domain.Entities;
using QueVendecion.Domain.Enums;
using QueVendecion.Infrastructure.Data;

namespace QueVendecion.Infrastructure.Services;

public class PedidoService : IPedidoService
{
    private readonly QueVendecionDbContext _context;
    private readonly DocumentoService? _documentos;

    public PedidoService(QueVendecionDbContext context, DocumentoService? documentos = null)
    {
        _context = context;
        _documentos = documentos;
    }

    public async Task<ResumenVentasDto> ResumenVentasAsync(int tiendaId, DateOnly desde, DateOnly hasta)
    {
        if (desde == default || hasta == default || hasta < desde || hasta.DayNumber - desde.DayNumber > 366)
            throw new ReglaNegocioException("rango_invalido", "Elige un rango de hasta 366 días.");
        var tienda = await _context.Tiendas.FindAsync(tiendaId)
            ?? throw new ReglaNegocioException("tienda_no_disponible", "Tienda no encontrada.");
        var zona = TimeZoneInfo.FindSystemTimeZoneById(OperacionReglas.Leer(tienda).ZonaHoraria);
        var pedidos = await _context.Pedidos.AsNoTracking().Include(p => p.Items)
            .Where(p => p.TiendaId == tiendaId).ToListAsync();
        var resumen = new ResumenVentasDto { PedidosPorAtender = pedidos.Count(p => p.Estado is not (EstadoPedido.Entregado or EstadoPedido.Cancelado)) };
        foreach (var pedido in pedidos.Where(p => p.Estado == EstadoPedido.Entregado))
        {
            var instante = pedido.EntregadoEnUtc;
            if (!instante.HasValue && !string.IsNullOrWhiteSpace(pedido.HistorialEstadosJson))
                instante = JsonSerializer.Deserialize<List<CambioEstadoPedidoDto>>(pedido.HistorialEstadosJson)?
                    .LastOrDefault(c => c.Nuevo == EstadoPedido.Entregado)?.FechaUtc;
            if (!instante.HasValue) { resumen.EntregadosSinInstante++; continue; }
            var dia = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(ComoUtc(instante.Value), zona));
            if (dia < desde || dia > hasta) continue;
            resumen.PedidosEntregados++;
            resumen.VentasEntregadas += pedido.Total;
            if (pedido.EstadoPago == EstadoPago.Pagado) resumen.EntregadoPagado += pedido.Total;
            else resumen.EntregadoPendienteCobro += pedido.Total;
            foreach (var item in pedido.Items)
            {
                if (item.CostoUnitarioHistorico.HasValue)
                {
                    resumen.CostoRegistrado += item.CostoUnitarioHistorico.Value * item.Cantidad;
                    resumen.UtilidadBrutaEstimada += (item.PrecioUnitario - item.CostoUnitarioHistorico.Value) * item.Cantidad;
                }
                else { resumen.LineasSinCosto++; resumen.ImporteSinCosto += item.Subtotal; }
            }
        }
        resumen.CostoRegistrado = Math.Round(resumen.CostoRegistrado, 2);
        resumen.UtilidadBrutaEstimada = Math.Round(resumen.UtilidadBrutaEstimada, 2);
        return resumen;
    }

    public async Task<List<PedidoDto>> ObtenerPorTiendaAsync(int tiendaId, EstadoPedido? estado = null, DateOnly? fechaServicio = null)
    {
        var query = _context.Pedidos
            .Include(p => p.Items)
            .ThenInclude(i => i.Producto)
            .Include(p => p.Items).ThenInclude(i => i.DocumentoPdf)
            .Where(p => p.TiendaId == tiendaId)
            .AsQueryable();

        if (estado.HasValue)
            query = query.Where(p => p.Estado == estado.Value);
        if (fechaServicio.HasValue)
            query = query.Where(p => p.FechaServicio == fechaServicio.Value);

        var pedidos = await query
            .OrderByDescending(p => p.CreadoEn)
            .ToListAsync();

        return pedidos.Select(MapToDto).ToList();
    }

    public async Task<List<PedidoDto>> ObtenerPorClienteAsync(int tiendaId, int clienteId, string? sesionDocumentoHash = null)
    {
        var pedidos = await _context.Pedidos
            .Include(p => p.Items)
            .ThenInclude(i => i.Producto)
            .Include(p => p.Items).ThenInclude(i => i.DocumentoPdf)
            .Where(p => p.TiendaId == tiendaId && p.ClienteId == clienteId && p.Origen == OrigenPedido.Web &&
                p.Items.All(i => i.DocumentoPdf == null || i.DocumentoPdf.SesionHash == sesionDocumentoHash))
            .OrderByDescending(p => p.CreadoEn)
            .ToListAsync();

        return pedidos.Select(MapToDto).ToList();
    }

    public async Task<PedidoDto?> ObtenerPorIdAsync(int id)
    {
        var pedido = await _context.Pedidos
            .Include(p => p.Items)
            .ThenInclude(i => i.Producto)
            .Include(p => p.Items).ThenInclude(i => i.DocumentoPdf)
            .FirstOrDefaultAsync(p => p.Id == id);

        return pedido == null ? null : MapToDto(pedido);
    }

    public async Task<PedidoDto> CrearAsync(CrearPedidoDto dto)
    {
        if (dto.ClaveIdempotencia == Guid.Empty)
            throw new ReglaNegocioException("clave_invalida", "La clave del pedido no es válida.");
        var huella = dto.ClaveIdempotencia.HasValue ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { dto.TiendaId, dto.ClienteId, dto.SesionDocumentoHash, dto.Items,
                dto.FormaPago, dto.Observaciones, dto.Modalidad, dto.Entrega, dto.DireccionEntrega,
                dto.FechaServicio, dto.VentanaSolicitada, dto.TotalEsperado })))) : null;
        for (var intento = 0; intento < 5; intento++)
        {
            try
            {
                return await EnTransaccionAsync(async () =>
                {
                    if (dto.ClaveIdempotencia.HasValue)
                    {
                        var previo = await _context.Pedidos.Include(p => p.Items).ThenInclude(i => i.Producto)
                            .Include(p => p.Items).ThenInclude(i => i.DocumentoPdf)
                            .FirstOrDefaultAsync(p => p.TiendaId == dto.TiendaId && p.ClaveIdempotencia == dto.ClaveIdempotencia.Value.ToString("D"));
                        if (previo != null)
                        {
                            if (previo.HuellaSolicitud != huella || previo.ClienteId != dto.ClienteId)
                                throw new ReglaNegocioException("idempotencia_conflicto", "Esta clave corresponde a otro pedido.");
                            return MapToDto(previo);
                        }
                    }
                    return await CrearInternoAsync(dto, huella);
                });
            }
            catch (Exception ex) when (dto.ClaveIdempotencia.HasValue && intento < 4 && EsConflictoConcurrente(ex))
            {
                _context.ChangeTracker.Clear();
                await Task.Delay(30 * (intento + 1));
            }
        }
        throw new ReglaNegocioException("registro_ocupado", "No se pudo confirmar el pedido. Reintenta con la misma clave.");
    }

    public async Task<PedidoDto> RegistrarMostradorAsync(int tiendaId, int usuarioId, RegistrarVentaMostradorDto dto)
    {
        if (tiendaId <= 0 || usuarioId <= 0)
            throw new ReglaNegocioException("acceso_denegado", "No se identificó la tienda o el administrador.");
        if (dto.ClaveIdempotencia == Guid.Empty)
            throw new ReglaNegocioException("clave_invalida", "La clave de registro no es válida.");
        if (dto.Cantidad <= 0 || dto.ProductoId <= 0)
            throw new ReglaNegocioException("cantidad_invalida", "Selecciona un servicio y una cantidad entera mayor a cero.");
        if (dto.PrecioEsperado < 0)
            throw new ReglaNegocioException("precio_invalido", "El servicio tiene un precio inválido.");
        if (dto.FormaPago is not (FormaPago.Efectivo or FormaPago.Transferencia))
            throw new ReglaNegocioException("pago_invalido", "Selecciona efectivo o transferencia.");
        var nota = string.IsNullOrWhiteSpace(dto.Nota) ? null : dto.Nota.Trim();
        if (nota?.Length > 500)
            throw new ReglaNegocioException("nota_invalida", "La nota debe tener 500 caracteres o menos.");
        var clave = dto.ClaveIdempotencia.ToString("D");
        var huella = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { dto.ProductoId, dto.Cantidad, dto.CarasPorJuego, dto.Juegos, dto.DobleCara,
                dto.PapelInsumoIdEsperado, dto.AdmiteDobleCaraEsperado, dto.PrecioEsperado, dto.FormaPago, Nota = nota }))));
        // Las ventas previas a la configuracion de papel usaron esta huella. Sus reintentos
        // conservan el mismo resultado sin registrar otra venta ni modificar inventario.
        var huellaAnterior = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { dto.ProductoId, dto.Cantidad, dto.PrecioEsperado, dto.FormaPago, Nota = nota }))));
        var solicitudAnterior = dto.CarasPorJuego == null && dto.Juegos == null && dto.DobleCara == null &&
            dto.PapelInsumoIdEsperado == null && !dto.AdmiteDobleCaraEsperado;

        // The unique index and the transaction commit together decide the winning request.
        // After a concurrent insert or transient SQLite lock, read the committed winner.
        for (var intento = 0; intento < 5; intento++)
        {
            try
            {
                return await EnTransaccionAsync(async () =>
                {
                    var anterior = await _context.Pedidos.Include(p => p.Items).ThenInclude(i => i.Producto)
                        .FirstOrDefaultAsync(p => p.TiendaId == tiendaId && p.ClaveIdempotencia == clave);
                    if (anterior != null)
                    {
                        if (anterior.HuellaSolicitud != huella &&
                            !(solicitudAnterior && anterior.HuellaSolicitud == huellaAnterior))
                            throw new ReglaNegocioException("idempotencia_conflicto", "Esta clave ya registró una venta distinta.");
                        return MapToDto(anterior);
                    }
                    var producto = await _context.Productos.AsNoTracking()
                        .FirstOrDefaultAsync(p => p.Id == dto.ProductoId && p.TiendaId == tiendaId);
                    if (producto == null || !producto.Disponible)
                        throw new ReglaNegocioException("producto_no_disponible", "El servicio no está disponible en tu tienda.");
                    if (producto.RequierePdf)
                        throw new ReglaNegocioException("pdf_requerido", "Este servicio requiere un PDF y se solicita desde el catálogo del cliente.");
                    if (producto.PapelInsumoId != dto.PapelInsumoIdEsperado || producto.AdmiteDobleCara != dto.AdmiteDobleCaraEsperado)
                        throw new ReglaNegocioException("papel_configuracion_cambio", "Cambió la configuración del servicio. Revisa el consumo antes de cobrar.");
                    PapelCalculo? papel = null;
                    if (producto.PapelInsumoId.HasValue)
                    {
                        papel = PapelReglas.Calcular(dto.CarasPorJuego, dto.Juegos, dto.DobleCara, producto.AdmiteDobleCara);
                        if (dto.Cantidad != papel.Value.CarasCobradas)
                            throw new ReglaNegocioException("papel_cantidad_invalida", "El total de caras no coincide con los juegos.");
                    }
                    else if (dto.CarasPorJuego.HasValue || dto.Juegos.HasValue || dto.DobleCara.HasValue)
                        throw new ReglaNegocioException("papel_configuracion_cambio", "Este producto no tiene consumo de papel configurado.");
                    if (producto.Precio < 0)
                        throw new ReglaNegocioException("precio_invalido", "El servicio tiene un precio inválido.");
                    if (producto.Precio != dto.PrecioEsperado)
                        throw new ReglaNegocioException("precio_actualizado", "El precio cambió. Revisa el importe actualizado antes de cobrar.");
                    decimal total;
                    try { total = checked(producto.Precio * dto.Cantidad); }
                    catch (OverflowException) { throw new ReglaNegocioException("importe_invalido", "El importe excede el máximo permitido."); }
                    if (total > 9999999999999999.99m)
                        throw new ReglaNegocioException("importe_invalido", "El importe excede el máximo permitido.");
                    var ahora = DateTime.UtcNow;
                    var pedido = new Pedido
                    {
                        TiendaId = tiendaId,
                        Origen = OrigenPedido.Mostrador,
                        ClaveIdempotencia = clave,
                        HuellaSolicitud = huella,
                        Estado = EstadoPedido.Entregado,
                        EstadoPago = EstadoPago.Pagado,
                        FormaPago = dto.FormaPago,
                        Observaciones = nota,
                        CreadoEn = ahora,
                        EntregadoEnUtc = ahora,
                        PagadoEnUtc = ahora,
                        CobradoPorUsuarioId = usuarioId,
                        Total = total,
                        HistorialEstadosJson = JsonSerializer.Serialize(new[] { new CambioEstadoPedidoDto
                            { Anterior = null, Nuevo = EstadoPedido.Entregado, FechaUtc = ahora, UsuarioId = usuarioId } }),
                        Items = new List<ItemPedido> { new()
                            { ProductoId = producto.Id, Cantidad = dto.Cantidad, PrecioUnitario = producto.Precio,
                                CostoUnitarioHistorico = producto.CostoUnitario,
                                CarasPorJuego = dto.CarasPorJuego, Juegos = dto.Juegos, DobleCara = dto.DobleCara,
                                PapelInsumoIdHistorico = producto.PapelInsumoId, HojasConsumidas = papel?.HojasConsumidas } }
                    };
                    _context.Pedidos.Add(pedido);
                    await _context.SaveChangesAsync();
                    var salidas = new List<(int ProductoId, int Cantidad, string Motivo)>();
                    if (producto.TieneInventario) salidas.Add((producto.Id, dto.Cantidad, "Venta de mostrador"));
                    if (papel.HasValue) salidas.Add((producto.PapelInsumoId!.Value, papel.Value.HojasConsumidas, "Papel para servicio de copias"));
                    await ConsumirAsync(pedido.Id, tiendaId, salidas, usuarioId);
                    await _context.SaveChangesAsync();
                    return MapToDto(pedido);
                });
            }
            catch (Exception ex) when (intento < 4 && EsConflictoConcurrente(ex))
            {
                _context.ChangeTracker.Clear();
                await Task.Delay(30 * (intento + 1));
            }
        }
        throw new ReglaNegocioException("registro_ocupado", "No se pudo confirmar la venta. Reintenta con la misma clave.");
    }

    private static bool EsConflictoConcurrente(Exception ex) =>
        ex is Microsoft.Data.Sqlite.SqliteException sqlite && sqlite.SqliteErrorCode is 5 or 6 or 19 ||
        ex is Npgsql.PostgresException pg && pg.SqlState is "23505" or "40001" ||
        ex.InnerException != null && EsConflictoConcurrente(ex.InnerException);

    private async Task<PedidoDto> CrearInternoAsync(CrearPedidoDto dto, string? huella)
    {
        if (dto.Items == null || dto.Items.Count == 0 || dto.Items.Any(i => i.Cantidad < 0))
            throw new ReglaNegocioException("cantidad_invalida", "Agrega productos con cantidades mayores a cero.");
        var tienda = await _context.Tiendas.FindAsync(dto.TiendaId)
            ?? throw new ReglaNegocioException("tienda_no_disponible", "La tienda no está disponible.");
        var operacion = OperacionReglas.Leer(tienda);
        var pedido = new Pedido
        {
            TiendaId = dto.TiendaId,
            ClienteId = dto.ClienteId,
            ClienteNombre = dto.ClienteNombre,
            FormaPago = dto.FormaPago,
            Observaciones = dto.Observaciones,
            Modalidad = dto.Modalidad,
            Entrega = dto.Entrega,
            DireccionEntrega = dto.Entrega == FormaEntrega.Domicilio ? dto.DireccionEntrega?.Trim() : null,
            VentanaSolicitada = dto.VentanaSolicitada
        };
        if (dto.ClaveIdempotencia.HasValue)
        {
            pedido.ClaveIdempotencia = dto.ClaveIdempotencia.Value.ToString("D");
            pedido.HuellaSolicitud = huella;
        }
        pedido.HistorialEstadosJson = JsonSerializer.Serialize(new[] { new CambioEstadoPedidoDto
            { Anterior = null, Nuevo = EstadoPedido.Recibido, FechaUtc = pedido.CreadoEn } });

        var salidas = new List<(int ProductoId, int Cantidad, string Motivo)>();
        var productos = new List<Producto>();
        var pdfs = new List<(DocumentoPdf Documento, ItemPedido Item)>();

        foreach (var grupo in dto.Items.GroupBy(i => (i.ProductoId, i.DocumentoPdfId)).OrderBy(g => g.Key.ProductoId))
        {
            var producto = await _context.Productos.FindAsync(grupo.Key.ProductoId);
            if (producto == null)
                throw new InvalidOperationException($"Producto {grupo.Key.ProductoId} no encontrado");

            await _context.Entry(producto).ReloadAsync();
            if (producto.TiendaId != dto.TiendaId || !producto.Disponible)
                throw new ReglaNegocioException("producto_no_disponible", "Un producto ya no está disponible en esta tienda.");

            PapelCalculo? papel = null;
            var itemDto = grupo.First();
            int cantidad;
            DocumentoPdf? documento = null;
            string? paginas = null;
            if (producto.RequierePdf)
            {
                if (_documentos == null || dto.Entrega != FormaEntrega.Recoleccion || dto.ClienteId == null ||
                    string.IsNullOrWhiteSpace(dto.SesionDocumentoHash) || dto.ClaveIdempotencia == null ||
                    dto.Items.Count(i => i.DocumentoPdfId.HasValue) > _documentos.MaxTrabajos ||
                    grupo.Count() != 1 || itemDto.DocumentoPdfId == null || itemDto.CarasPorJuego != null ||
                    itemDto.RevisionImpresion != producto.RevisionImpresion || itemDto.PrecioEsperado != producto.Precio)
                    throw new ReglaNegocioException("pdf_configuracion_cambio", "Revisa el PDF, precio, recolección y opciones antes de enviar el pedido.");
                // PostgreSQL bloquea la fila antes de mover el archivo; otra solicitud del
                // mismo PDF espera y después observa su estado definitivo.
                if (_context.Database.IsNpgsql())
                    await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"DocumentosPdf\" WHERE \"Id\" = {itemDto.DocumentoPdfId.Value} FOR UPDATE");
                documento = await _context.DocumentosPdf.FirstOrDefaultAsync(d => d.Id == itemDto.DocumentoPdfId);
                if (documento == null || documento.TiendaId != dto.TiendaId || documento.ClienteId != dto.ClienteId ||
                    documento.SesionHash != dto.SesionDocumentoHash || documento.Estado != "Temporal" ||
                    documento.ExpiraEnUtc <= DateTime.UtcNow || !producto.PapelInsumoId.HasValue)
                    throw new ReglaNegocioException("pdf_no_disponible", "El PDF no está disponible para este pedido. Vuelve a subirlo.");
                await _documentos.ValidarParaPedidoAsync(documento);
                var seleccion = PaginasPdf.Normalizar(itemDto.Paginas, documento.PaginasTotales);
                paginas = seleccion.Normalizada;
                papel = PapelReglas.Calcular(seleccion.Cantidad, itemDto.Juegos, itemDto.DobleCara, producto.AdmiteDobleCara);
                cantidad = papel.Value.CarasCobradas;
                if (itemDto.Cantidad != 0 && itemDto.Cantidad != cantidad)
                    throw new ReglaNegocioException("pdf_paginas_invalidas", "Las caras enviadas no coinciden con las páginas seleccionadas.");
            }
            else if (producto.PapelInsumoId.HasValue)
            {
                if (itemDto.DocumentoPdfId.HasValue || itemDto.Paginas != null)
                    throw new ReglaNegocioException("pdf_configuracion_cambio", "Este servicio no recibe archivos.");
                if (grupo.Count() != 1)
                    throw new ReglaNegocioException("papel_cantidad_invalida", "Incluye una sola configuración por servicio.");
                papel = PapelReglas.Calcular(itemDto.CarasPorJuego, itemDto.Juegos, itemDto.DobleCara, producto.AdmiteDobleCara);
                if (itemDto.Cantidad != papel.Value.CarasCobradas)
                    throw new ReglaNegocioException("papel_cantidad_invalida", "El total de caras no coincide con los juegos.");
                cantidad = papel.Value.CarasCobradas;
            }
            else
            {
                if (itemDto.DocumentoPdfId.HasValue || itemDto.Paginas != null)
                    throw new ReglaNegocioException("pdf_configuracion_cambio", "Este producto no recibe archivos.");
                if (grupo.Any(i => i.CarasPorJuego.HasValue || i.Juegos.HasValue || i.DobleCara.HasValue))
                    throw new ReglaNegocioException("papel_configuracion_cambio", "Este producto no tiene consumo de papel configurado.");
                var suma = grupo.Sum(i => (long)i.Cantidad);
                if (suma <= 0 || suma > int.MaxValue) throw new ReglaNegocioException("cantidad_invalida", "Cantidad fuera de rango.");
                cantidad = (int)suma;
            }
            productos.Add(producto);

            var item = new ItemPedido
            {
                ProductoId = producto.Id,
                Cantidad = cantidad,
                PrecioUnitario = producto.Precio,
                CostoUnitarioHistorico = producto.CostoUnitario,
                CarasPorJuego = producto.RequierePdf ? papel?.CarasCobradas / itemDto.Juegos : itemDto.CarasPorJuego,
                Juegos = itemDto.Juegos, DobleCara = itemDto.DobleCara,
                PapelInsumoIdHistorico = producto.PapelInsumoId, HojasConsumidas = papel?.HojasConsumidas
            };

            pedido.Items.Add(item);
            if (documento != null)
            {
                if (pdfs.Any(p => p.Documento.Id == documento.Id))
                    throw new ReglaNegocioException("pdf_repetido", "Un PDF solo puede incluirse una vez por pedido.");
                documento.PaginasSeleccionadas = paginas;
                pdfs.Add((documento, item));
            }
            if (producto.TieneInventario) salidas.Add((producto.Id, cantidad, "Venta pedido"));
            if (papel.HasValue) salidas.Add((producto.PapelInsumoId!.Value, papel.Value.HojasConsumidas, "Papel para servicio de copias"));
        }

        OperacionReglas.ValidarPedido(operacion, dto, productos, DateTime.UtcNow);
        pedido.FechaServicio = dto.Modalidad == ModalidadPedido.AlMomento
            ? DateOnly.FromDateTime(OperacionReglas.AhoraLocal(operacion, DateTime.UtcNow))
            : dto.FechaServicio;

        try { pedido.Total = checked(pedido.Items.Sum(i => i.Subtotal)); }
        catch (OverflowException) { throw new ReglaNegocioException("importe_invalido", "El pedido supera el importe permitido."); }
        if ((pdfs.Count > 0 || dto.TotalEsperado.HasValue) && dto.TotalEsperado != pedido.Total)
            throw new ReglaNegocioException("precio_actualizado", "Cambió el total. Revisa el pedido antes de enviarlo.");

        _context.Pedidos.Add(pedido);
        await _context.SaveChangesAsync();

        await ConsumirAsync(pedido.Id, dto.TiendaId, salidas, null);

        var finalizados = new List<DocumentoPdf>();
        try
        {
            foreach (var (documento, item) in pdfs)
            {
                if (_documentos!.Finalizar(documento)) finalizados.Add(documento);
                documento.ItemPedidoId = item.Id;
                documento.Estado = "Asociado";
                documento.ExpiraEnUtc = null;
            }
            await _context.SaveChangesAsync();
        }
        catch
        {
            foreach (var documento in finalizados) _documentos!.DeshacerFinalizacion(documento);
            throw;
        }

        return await ObtenerPorIdAsync(pedido.Id) ?? MapToDto(pedido);
    }

    private async Task ConsumirAsync(int pedidoId, int tiendaId,
        List<(int ProductoId, int Cantidad, string Motivo)> salidas, int? usuarioId)
    {
        foreach (var grupo in salidas.GroupBy(s => s.ProductoId).OrderBy(g => g.Key))
        {
            var producto = await _context.Productos.FirstOrDefaultAsync(p => p.Id == grupo.Key && p.TiendaId == tiendaId);
            if (producto == null) throw new ReglaNegocioException("papel_invalido", "El insumo ya no pertenece a esta tienda.");
            await _context.Entry(producto).ReloadAsync();
            if (!producto.TieneInventario || producto.Stock == null)
                throw new ReglaNegocioException("inventario_inactivo", "El insumo no tiene inventario activo.");
            var total = grupo.Sum(s => (long)s.Cantidad);
            if (total > producto.Stock || total <= 0)
                throw new ReglaNegocioException("stock_insuficiente", $"Stock insuficiente para {producto.Nombre}.");
            foreach (var salida in grupo)
            {
                var anterior = producto.Stock.Value;
                producto.Stock = anterior - salida.Cantidad;
                _context.MovimientosInventario.Add(new MovimientoInventario
                {
                    ProductoId = producto.Id, Tipo = TipoMovimiento.Salida, Cantidad = salida.Cantidad,
                    StockAnterior = anterior, StockNuevo = producto.Stock.Value, PedidoId = pedidoId,
                    UsuarioId = usuarioId, Observaciones = salida.Motivo == "Venta pedido"
                        ? $"Venta pedido {pedidoId}" : salida.Motivo
                });
            }
        }
    }

    public Task<PedidoDto?> ActualizarEstadoAsync(int id, EstadoPedido estado, int? usuarioId = null) =>
        EnTransaccionAsync(() => ActualizarEstadoInternoAsync(id, estado, usuarioId));

    private async Task<PedidoDto?> ActualizarEstadoInternoAsync(int id, EstadoPedido estado, int? usuarioId)
    {
        var pedido = await _context.Pedidos
            .Include(p => p.Items)
            .ThenInclude(i => i.Producto)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (pedido == null) return null;

        await _context.Entry(pedido).ReloadAsync();
        if (pedido.Estado == EstadoPedido.Cancelado)
        {
            if (estado != EstadoPedido.Cancelado)
                throw new ReglaNegocioException("pedido_cancelado", "Un pedido cancelado no puede reabrirse. Crea un nuevo pedido.");
            return MapToDto(pedido);
        }
        if (estado == pedido.Estado) return MapToDto(pedido);
        var permitida = estado == EstadoPedido.Cancelado && pedido.Estado != EstadoPedido.Entregado ||
            pedido.Estado switch
            {
                EstadoPedido.Recibido => estado == EstadoPedido.Confirmado,
                EstadoPedido.Confirmado => estado is EstadoPedido.EnPreparacion or EstadoPedido.Listo or EstadoPedido.Entregado,
                EstadoPedido.EnPreparacion => estado is EstadoPedido.Listo or EstadoPedido.Entregado,
                EstadoPedido.Listo => estado == EstadoPedido.Entregado,
                _ => false
            };
        if (!Enum.IsDefined(estado) || !permitida)
            throw new ReglaNegocioException("transicion_invalida", "Confirma el pedido primero; después prepara, marca listo o entrega según corresponda.");
        var estadoAnterior = pedido.Estado;
        pedido.Estado = estado;
        pedido.ActualizadoEn = DateTime.UtcNow;
        if (estado == EstadoPedido.Entregado) pedido.EntregadoEnUtc = pedido.ActualizadoEn;
        var historial = string.IsNullOrWhiteSpace(pedido.HistorialEstadosJson)
            ? new List<CambioEstadoPedidoDto>()
            : JsonSerializer.Deserialize<List<CambioEstadoPedidoDto>>(pedido.HistorialEstadosJson) ?? new();
        historial.Add(new CambioEstadoPedidoDto { Anterior = estadoAnterior, Nuevo = estado,
            FechaUtc = pedido.ActualizadoEn.Value, UsuarioId = usuarioId });
        pedido.HistorialEstadosJson = JsonSerializer.Serialize(historial);

        if (estado == EstadoPedido.Cancelado && estadoAnterior != EstadoPedido.Cancelado)
        {
            var salidas = await _context.MovimientosInventario
                .Where(m => m.PedidoId == id && m.Tipo == TipoMovimiento.Salida)
                .GroupBy(m => m.ProductoId)
                .Select(g => new { ProductoId = g.Key, Cantidad = g.Sum(m => m.Cantidad) })
                .OrderBy(m => m.ProductoId).ToListAsync();
            foreach (var item in salidas)
            {
                var producto = await _context.Productos.FindAsync(item.ProductoId);
                if (producto == null) continue;
                await _context.Entry(producto).ReloadAsync();
                if (!producto.TieneInventario) continue;

                var stockAnterior = producto.Stock ?? 0;
                producto.Stock = checked(stockAnterior + item.Cantidad);

                _context.MovimientosInventario.Add(new MovimientoInventario
                {
                    ProductoId = producto.Id,
                    Tipo = TipoMovimiento.Entrada,
                    Cantidad = item.Cantidad,
                    StockAnterior = stockAnterior,
                    StockNuevo = producto.Stock.Value,
                    PedidoId = pedido.Id,
                    UsuarioId = usuarioId,
                    Observaciones = "Reintegro por cancelación de pedido"
                });
            }
        }

        await _context.SaveChangesAsync();

        return await ObtenerPorIdAsync(id);
    }

    public async Task<PedidoDto?> ActualizarEstadoPagoAsync(int id, EstadoPago estadoPago)
    {
        var pedido = await _context.Pedidos.FindAsync(id);
        if (pedido == null) return null;
        if (pedido.Origen == OrigenPedido.Mostrador && estadoPago != EstadoPago.Pagado)
            throw new ReglaNegocioException("pago_invalido", "Una venta de mostrador cobrada no puede quedar pendiente.");

        pedido.EstadoPago = estadoPago;
        pedido.ActualizadoEn = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return await ObtenerPorIdAsync(id);
    }

    public async Task<PedidoDto?> ConfirmarHorarioAsync(int id, string? horarioConfirmado)
    {
        var pedido = await _context.Pedidos.FindAsync(id);
        if (pedido == null) return null;
        if (pedido.Estado is EstadoPedido.Cancelado or EstadoPedido.Entregado)
            throw new ReglaNegocioException("transicion_invalida", "Este pedido ya está cerrado.");
        if (horarioConfirmado?.Length > 100)
            throw new ReglaNegocioException("horario_invalido", "El horario confirmado debe tener menos de 100 caracteres.");
        pedido.HorarioConfirmado = string.IsNullOrWhiteSpace(horarioConfirmado) ? null : horarioConfirmado.Trim();
        pedido.ActualizadoEn = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return await ObtenerPorIdAsync(id);
    }

    public async Task<bool> EliminarAsync(int id)
    {
        var pedido = await _context.Pedidos.FindAsync(id);
        if (pedido == null) return false;

        _context.Pedidos.Remove(pedido);
        await _context.SaveChangesAsync();
        return true;
    }

    private async Task<T> EnTransaccionAsync<T>(Func<Task<T>> action)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var result = await action();
            await transaction.CommitAsync();
            return result;
        }
        catch
        {
            await transaction.RollbackAsync();
            _context.ChangeTracker.Clear();
            throw;
        }
    }

    private static PedidoDto MapToDto(Pedido p) => new()
    {
        Id = p.Id,
        TiendaId = p.TiendaId,
        ClienteId = p.ClienteId,
        ClienteNombre = p.ClienteNombre,
        Estado = p.Estado,
        FormaPago = p.FormaPago,
        EstadoPago = p.EstadoPago,
        Origen = p.Origen,
        PagadoEnUtc = p.PagadoEnUtc.HasValue ? ComoUtc(p.PagadoEnUtc.Value) : null,
        CobradoPorUsuarioId = p.CobradoPorUsuarioId,
        Total = p.Total,
        Observaciones = p.Observaciones,
        CreadoEn = ComoUtc(p.CreadoEn),
        ActualizadoEn = p.ActualizadoEn.HasValue ? ComoUtc(p.ActualizadoEn.Value) : null,
        Modalidad = p.Modalidad,
        Entrega = p.Entrega,
        DireccionEntrega = p.DireccionEntrega,
        FechaServicio = p.FechaServicio,
        VentanaSolicitada = p.VentanaSolicitada,
        HorarioConfirmado = p.HorarioConfirmado,
        HistorialEstados = LeerHistorial(p.HistorialEstadosJson),
        Items = p.Items.Select(i => new ItemPedidoDto
        {
            Id = i.Id,
            ProductoId = i.ProductoId,
            ProductoNombre = i.Producto?.Nombre ?? "",
            Cantidad = i.Cantidad,
            PrecioUnitario = i.PrecioUnitario,
            Subtotal = i.Subtotal,
            CarasPorJuego = i.CarasPorJuego, Juegos = i.Juegos, DobleCara = i.DobleCara,
            HojasConsumidas = i.HojasConsumidas
            ,DocumentoPdfId = i.DocumentoPdf?.Id,
            NombrePdf = i.DocumentoPdf?.NombreOriginal,
            PaginasTotalesPdf = i.DocumentoPdf?.PaginasTotales,
            PaginasSeleccionadas = i.DocumentoPdf?.PaginasSeleccionadas,
            ArchivoPdfDisponible = i.DocumentoPdf?.Estado == "Asociado"
        }).ToList()
    };

    // SQLite conserva el valor del instante, pero EF lo lee con Kind=Unspecified.
    // Estas columnas e historial se escriben con DateTime.UtcNow; no se altera la base.
    private static DateTime ComoUtc(DateTime valor) => valor.Kind switch
    {
        DateTimeKind.Utc => valor,
        DateTimeKind.Local => valor.ToUniversalTime(),
        _ => DateTime.SpecifyKind(valor, DateTimeKind.Utc)
    };

    private static List<CambioEstadoPedidoDto> LeerHistorial(string? json)
    {
        var cambios = string.IsNullOrWhiteSpace(json)
            ? new List<CambioEstadoPedidoDto>()
            : JsonSerializer.Deserialize<List<CambioEstadoPedidoDto>>(json) ?? new();
        foreach (var cambio in cambios) cambio.FechaUtc = ComoUtc(cambio.FechaUtc);
        return cambios;
    }
}

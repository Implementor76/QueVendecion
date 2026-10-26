using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using QueVendecion.Application.DTOs;
using QueVendecion.Application.Exceptions;
using QueVendecion.Domain.Entities;
using QueVendecion.Domain.Enums;
using QueVendecion.Infrastructure.Services;
using QueVendecion.Tests.Helpers;

namespace QueVendecion.Tests.Services;

public class PdfPedidoTests
{
    [Fact]
    public void RangosSeOrdenanYEliminanDuplicados()
    {
        Assert.Equal(new SeleccionPdf("1,2,3,5", 4), PaginasPdf.Normalizar("5,1-3,2,3", 5));
        Assert.Equal(5, PaginasPdf.Normalizar("todas", 5).Cantidad);
        foreach (var invalido in new[] { "", "0", "1-6", "5-3", "1,x", "1-", "," })
        {
            if (invalido == "") continue; // vacío equivale a todas las páginas.
            Assert.Throws<ReglaNegocioException>(() => PaginasPdf.Normalizar(invalido, 5));
        }
    }

    [Fact]
    public async Task PdfRealProducePedidoHistoricoYReservaPapelUnaVez()
    {
        var raiz = Path.Combine(Path.GetTempPath(), "qvd-pdf-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using var db = TestDbContextFactory.Create();
            var (tienda, cliente, papel, servicio) = await Sembrar(db);
            var docs = Servicio(db, raiz);
            var token = await docs.CrearSesionAsync(tienda.Id, cliente.Id);
            var sesion = await docs.ValidarSesionAsync(tienda.Id, token);
            var bytes = Pdf(5);
            await using var stream = new MemoryStream(bytes);
            var documento = await docs.SubirAsync(tienda.Id, sesion, "prueba.pdf", "application/pdf", bytes.Length, stream, default);
            Assert.Equal(5, documento.PaginasTotales);
            Assert.Equal("Temporal", documento.Estado);
            Assert.Equal(100, papel.Stock);
            var dto = new CrearPedidoDto { TiendaId = tienda.Id, ClienteId = cliente.Id,
                SesionDocumentoHash = sesion.TokenHash, ClaveIdempotencia = Guid.NewGuid(), TotalEsperado = 24m,
                Items = new() { new() { ProductoId = servicio.Id, DocumentoPdfId = documento.Id,
                    Paginas = "1-3,5", Juegos = 2, DobleCara = true, Cantidad = 8,
                    RevisionImpresion = servicio.RevisionImpresion, PrecioEsperado = 3m } } };
            var pedidos = new PedidoService(db, docs);
            var pedido = await pedidos.CrearAsync(dto);
            Assert.Equal(24m, pedido.Total);
            Assert.Equal(96, papel.Stock);
            var item = Assert.Single(pedido.Items);
            Assert.Equal("1,2,3,5", item.PaginasSeleccionadas);
            Assert.Equal(4, item.HojasConsumidas);
            Assert.True(item.ArchivoPdfDisponible);
            Assert.False(docs.Finalizar(documento)); // el reintento no vuelve a mover el archivo.
            Assert.Equal(1, await db.MovimientosInventario.CountAsync());
            Assert.Equal(pedido.Id, (await pedidos.CrearAsync(dto)).Id);
            Assert.Equal(1, await db.Pedidos.CountAsync());
            Assert.Equal(1, await db.MovimientosInventario.CountAsync());
            dto.Items[0].Paginas = "1-2";
            Assert.Equal("idempotencia_conflicto", (await Assert.ThrowsAsync<ReglaNegocioException>(() =>
                pedidos.CrearAsync(dto))).Codigo);
            dto.Items[0].Paginas = "1-3,5";
            servicio = (await db.Productos.FindAsync(servicio.Id))!;
            servicio.Precio = 4m; servicio.CostoUnitario = 1m;
            await db.SaveChangesAsync();
            Assert.Equal(4m, (await db.Productos.FindAsync(servicio.Id))!.Precio);
            Assert.Equal(3m, (await db.ItemsPedido.SingleAsync()).PrecioUnitario);
            Assert.Equal(.70m, (await db.ItemsPedido.SingleAsync()).CostoUnitarioHistorico);
            await pedidos.ActualizarEstadoAsync(pedido.Id, EstadoPedido.Cancelado, 1);
            await pedidos.ActualizarEstadoAsync(pedido.Id, EstadoPedido.Cancelado, 1);
            Assert.Equal(100, (await db.Productos.FindAsync(papel.Id))!.Stock);
            Assert.Equal(2, await db.MovimientosInventario.CountAsync());
            Assert.Empty(await pedidos.ObtenerPorClienteAsync(tienda.Id, cliente.Id));
            Assert.Single(await pedidos.ObtenerPorClienteAsync(tienda.Id, cliente.Id, sesion.TokenHash));
            var cerrado = (await db.Pedidos.FindAsync(pedido.Id))!;
            var historial = JsonSerializer.Deserialize<List<CambioEstadoPedidoDto>>(cerrado.HistorialEstadosJson!)!;
            historial[^1].FechaUtc = DateTime.UtcNow.AddDays(-31);
            cerrado.HistorialEstadosJson = JsonSerializer.Serialize(historial);
            cerrado.ActualizadoEn = DateTime.UtcNow; // editar después del cierre no prolonga retención.
            await db.SaveChangesAsync();
            await docs.LimpiarAsync(default);
            await docs.LimpiarAsync(default);
            Assert.Equal("Eliminado", (await db.DocumentosPdf.FindAsync(documento.Id))!.Estado);
            Assert.Equal("1,2,3,5", (await db.DocumentosPdf.FindAsync(documento.Id))!.PaginasSeleccionadas);
            Assert.Throws<ReglaNegocioException>(() => docs.RutaDescarga(documento));
        }
        finally { if (Directory.Exists(raiz)) Directory.Delete(raiz, true); }
    }

    [Fact]
    public async Task DosArchivosMismoServicioValidanStockTotalSinPedidoParcial()
    {
        var raiz = Path.Combine(Path.GetTempPath(), "qvd-pdf-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using var db = TestDbContextFactory.Create();
            var (tienda, cliente, papel, servicio) = await Sembrar(db);
            papel.Stock = 5; await db.SaveChangesAsync();
            var docs = Servicio(db, raiz);
            var token = await docs.CrearSesionAsync(tienda.Id, cliente.Id);
            var sesion = await docs.ValidarSesionAsync(tienda.Id, token);
            var bytes = Pdf(5);
            async Task<DocumentoPdf> Subir()
            {
                await using var stream = new MemoryStream(bytes);
                return await docs.SubirAsync(tienda.Id, sesion, "archivo.pdf", "application/pdf", bytes.Length, stream, default);
            }
            var a = await Subir(); var b = await Subir();
            CrearItemPedidoDto Item(DocumentoPdf d) => new() { ProductoId = servicio.Id, DocumentoPdfId = d.Id,
                Paginas = "todas", Juegos = 1, DobleCara = true, Cantidad = 5,
                RevisionImpresion = servicio.RevisionImpresion, PrecioEsperado = 3m };
            var dto = new CrearPedidoDto { TiendaId = tienda.Id, ClienteId = cliente.Id,
                SesionDocumentoHash = sesion.TokenHash, ClaveIdempotencia = Guid.NewGuid(), TotalEsperado = 30m,
                Items = new() { Item(a), Item(b) } };
            var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => new PedidoService(db, docs).CrearAsync(dto));
            Assert.Equal("stock_insuficiente", ex.Codigo);
            Assert.Equal(5, (await db.Productos.FindAsync(papel.Id))!.Stock);
            Assert.Empty(await db.Pedidos.ToListAsync());
            Assert.Empty(await db.MovimientosInventario.ToListAsync());
            papel = (await db.Productos.FindAsync(papel.Id))!; papel.Stock = 6; await db.SaveChangesAsync();
            var pedido = await new PedidoService(db, docs).CrearAsync(dto);
            Assert.Equal(2, pedido.Items.Count);
            Assert.Equal(0, papel.Stock);
            Assert.Equal(2, await db.MovimientosInventario.CountAsync());
        }
        finally { if (Directory.Exists(raiz)) Directory.Delete(raiz, true); }
    }

    [Fact]
    public async Task RechazaContenidoNoPdfYTamano()
    {
        var raiz = Path.Combine(Path.GetTempPath(), "qvd-pdf-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using var db = TestDbContextFactory.Create();
            var (tienda, cliente, _, _) = await Sembrar(db);
            var docs = Servicio(db, raiz);
            var sesion = await docs.ValidarSesionAsync(tienda.Id, await docs.CrearSesionAsync(tienda.Id, cliente.Id));
            foreach (var (nombre, contenido) in new[] { ("texto.pdf", "no es pdf"), ("foto.jpg", "foto") })
            {
                var bytes = Encoding.UTF8.GetBytes(contenido);
                await using var stream = new MemoryStream(bytes);
                await Assert.ThrowsAsync<ReglaNegocioException>(() => docs.SubirAsync(tienda.Id, sesion, nombre,
                    "application/pdf", bytes.Length, stream, default));
            }
            await using var vacio = new MemoryStream(Pdf(1));
            Assert.Equal("pdf_tamano_invalido", (await Assert.ThrowsAsync<ReglaNegocioException>(() =>
                docs.SubirAsync(tienda.Id, sesion, "grande.pdf", "application/pdf", docs.MaxBytes + 1, vacio, default))).Codigo);
            var protegido = Pdf(1, cifrado: true);
            await using var cifrado = new MemoryStream(protegido);
            await Assert.ThrowsAsync<ReglaNegocioException>(() => docs.SubirAsync(tienda.Id, sesion,
                "protegido.pdf", "application/pdf", protegido.Length, cifrado, default));
            var truncado = Pdf(2)[..30];
            await using var corrupto = new MemoryStream(truncado);
            await Assert.ThrowsAsync<ReglaNegocioException>(() => docs.SubirAsync(tienda.Id, sesion,
                "corrupto.pdf", "application/pdf", truncado.Length, corrupto, default));
            Assert.Empty(await db.DocumentosPdf.ToListAsync());
        }
        finally { if (Directory.Exists(raiz)) Directory.Delete(raiz, true); }
    }

    [Fact]
    public async Task ArchivoAjenoNoPuedeAsociarseYSesionNoCruzaTienda()
    {
        var raiz = Path.Combine(Path.GetTempPath(), "qvd-pdf-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using var db = TestDbContextFactory.Create();
            var (tienda, cliente, papel, servicio) = await Sembrar(db);
            var otro = new Cliente { Nombre = "Otro", Telefono = "111" };
            db.Clientes.Add(otro); await db.SaveChangesAsync();
            var docs = Servicio(db, raiz);
            var token = await docs.CrearSesionAsync(tienda.Id, cliente.Id);
            var sesion = await docs.ValidarSesionAsync(tienda.Id, token);
            await Assert.ThrowsAsync<ReglaNegocioException>(() => docs.ValidarSesionAsync(tienda.Id + 1, token));
            var bytes = Pdf(1);
            await using var stream = new MemoryStream(bytes);
            var documento = await docs.SubirAsync(tienda.Id, sesion, "privado.pdf", "application/pdf", bytes.Length, stream, default);
            var dto = new CrearPedidoDto { TiendaId = tienda.Id, ClienteId = otro.Id,
                SesionDocumentoHash = sesion.TokenHash, ClaveIdempotencia = Guid.NewGuid(), TotalEsperado = 3m,
                Items = new() { new() { ProductoId = servicio.Id, DocumentoPdfId = documento.Id,
                    Paginas = "todas", Juegos = 1, DobleCara = false, Cantidad = 1,
                    RevisionImpresion = servicio.RevisionImpresion, PrecioEsperado = 3m } } };
            Assert.Equal("pdf_no_disponible", (await Assert.ThrowsAsync<ReglaNegocioException>(() =>
                new PedidoService(db, docs).CrearAsync(dto))).Codigo);
            Assert.Empty(await db.Pedidos.ToListAsync());
            Assert.Equal(100, papel.Stock);
        }
        finally { if (Directory.Exists(raiz)) Directory.Delete(raiz, true); }
    }

    [Fact]
    public async Task CargaTemporalVencidaSeLimpiaSinPedidoYNoSePuedeReutilizar()
    {
        var raiz = Path.Combine(Path.GetTempPath(), "qvd-pdf-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using var db = TestDbContextFactory.Create();
            var (tienda, cliente, papel, servicio) = await Sembrar(db);
            var docs = Servicio(db, raiz);
            var sesion = await docs.ValidarSesionAsync(tienda.Id, await docs.CrearSesionAsync(tienda.Id, cliente.Id));
            var bytes = Pdf(1);
            await using var stream = new MemoryStream(bytes);
            var documento = await docs.SubirAsync(tienda.Id, sesion, "temporal.pdf", "application/pdf", bytes.Length, stream, default);
            documento.ExpiraEnUtc = DateTime.UtcNow.AddHours(-1);
            await db.SaveChangesAsync();
            await docs.LimpiarAsync(default);
            await docs.LimpiarAsync(default);
            Assert.Equal("Eliminado", documento.Estado);
            Assert.Empty(await db.Pedidos.ToListAsync());
            Assert.Equal(100, papel.Stock);
            var dto = new CrearPedidoDto { TiendaId = tienda.Id, ClienteId = cliente.Id,
                SesionDocumentoHash = sesion.TokenHash, ClaveIdempotencia = Guid.NewGuid(), TotalEsperado = 3m,
                Items = new() { new() { ProductoId = servicio.Id, DocumentoPdfId = documento.Id,
                    Paginas = "todas", Juegos = 1, DobleCara = false, Cantidad = 1,
                    RevisionImpresion = servicio.RevisionImpresion, PrecioEsperado = 3m } } };
            Assert.Equal("pdf_no_disponible", (await Assert.ThrowsAsync<ReglaNegocioException>(() =>
                new PedidoService(db, docs).CrearAsync(dto))).Codigo);
        }
        finally { if (Directory.Exists(raiz)) Directory.Delete(raiz, true); }
    }

    [Fact]
    public async Task FalloAlFinalizarArchivoReviertePedidoYPermiteReintento()
    {
        var raiz = Path.Combine(Path.GetTempPath(), "qvd-pdf-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using var db = TestDbContextFactory.Create();
            var (tienda, cliente, papel, servicio) = await Sembrar(db);
            var docs = Servicio(db, raiz);
            var sesion = await docs.ValidarSesionAsync(tienda.Id, await docs.CrearSesionAsync(tienda.Id, cliente.Id));
            var bytes = Pdf(1);
            await using var stream = new MemoryStream(bytes);
            var documento = await docs.SubirAsync(tienda.Id, sesion, "reintento.pdf", "application/pdf", bytes.Length, stream, default);
            var destino = Path.Combine(raiz, "pedidos", documento.Id.ToString("N") + ".pdf");
            await File.WriteAllTextAsync(destino, "colisión ficticia");
            var dto = new CrearPedidoDto { TiendaId = tienda.Id, ClienteId = cliente.Id,
                SesionDocumentoHash = sesion.TokenHash, ClaveIdempotencia = Guid.NewGuid(), TotalEsperado = 3m,
                Items = new() { new() { ProductoId = servicio.Id, DocumentoPdfId = documento.Id,
                    Paginas = "todas", Juegos = 1, DobleCara = false, Cantidad = 1,
                    RevisionImpresion = servicio.RevisionImpresion, PrecioEsperado = 3m } } };
            await Assert.ThrowsAnyAsync<IOException>(() => new PedidoService(db, docs).CrearAsync(dto));
            Assert.Empty(await db.Pedidos.ToListAsync());
            Assert.Empty(await db.MovimientosInventario.ToListAsync());
            Assert.Equal(100, (await db.Productos.FindAsync(papel.Id))!.Stock);
            Assert.Equal("Temporal", (await db.DocumentosPdf.FindAsync(documento.Id))!.Estado);
            File.Delete(destino);
            var pedido = await new PedidoService(db, docs).CrearAsync(dto);
            Assert.Equal(3m, pedido.Total);
            Assert.Equal(99, (await db.Productos.FindAsync(papel.Id))!.Stock);
        }
        finally { if (Directory.Exists(raiz)) Directory.Delete(raiz, true); }
    }

    [Fact]
    public async Task ReintentoTrasPerderRespuestaRecuperaPedidoAunqueLaVentanaYaNoExista()
    {
        var raiz = Path.Combine(Path.GetTempPath(), "qvd-pdf-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using var db = TestDbContextFactory.Create();
            var (tienda, cliente, papel, servicio) = await Sembrar(db);
            var zona = TimeZoneInfo.FindSystemTimeZoneById("America/Hermosillo");
            var fecha = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zona)).AddDays(2);
            tienda.OperacionJson = JsonSerializer.Serialize(new OperacionTiendaDto { PedidosProgramados = true,
                VentanasServicio = new() { new() { Dia = fecha.DayOfWeek, Inicio = "08:00", Fin = "23:59" } } });
            await db.SaveChangesAsync();
            var docs = Servicio(db, raiz);
            var sesion = await docs.ValidarSesionAsync(tienda.Id, await docs.CrearSesionAsync(tienda.Id, cliente.Id));
            var bytes = Pdf(5);
            await using var stream = new MemoryStream(bytes);
            var documento = await docs.SubirAsync(tienda.Id, sesion, "programado.pdf", "application/pdf", bytes.Length, stream, default);
            var dto = new CrearPedidoDto { TiendaId = tienda.Id, ClienteId = cliente.Id,
                SesionDocumentoHash = sesion.TokenHash, ClaveIdempotencia = Guid.NewGuid(), TotalEsperado = 15m,
                Modalidad = ModalidadPedido.Programado, FechaServicio = fecha, VentanaSolicitada = "08:00-23:59",
                Items = new() { new() { ProductoId = servicio.Id, DocumentoPdfId = documento.Id,
                    Paginas = "todas", Juegos = 1, DobleCara = false, Cantidad = 5,
                    RevisionImpresion = servicio.RevisionImpresion, PrecioEsperado = 3m } } };
            var pedidos = new PedidoService(db, docs);
            var primero = await pedidos.CrearAsync(dto);
            Assert.Equal(95, papel.Stock);
            // Simula una respuesta perdida y un reintento cuando ya cambió la operación.
            tienda.OperacionJson = JsonSerializer.Serialize(new OperacionTiendaDto { PedidosProgramados = false });
            await db.SaveChangesAsync();
            var recuperado = await pedidos.CrearAsync(dto);
            Assert.Equal(primero.Id, recuperado.Id);
            Assert.Equal(fecha, recuperado.FechaServicio);
            Assert.Equal(95, papel.Stock);
            Assert.Single(await db.Pedidos.ToListAsync());
            Assert.Single(await db.MovimientosInventario.ToListAsync());
        }
        finally { if (Directory.Exists(raiz)) Directory.Delete(raiz, true); }
    }

    private static DocumentoService Servicio(QueVendecion.Infrastructure.Data.QueVendecionDbContext db, string raiz) =>
        new(db, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Documentos:Ruta"] = raiz }).Build());

    private static async Task<(Tienda, Cliente, Producto, Producto)> Sembrar(QueVendecion.Infrastructure.Data.QueVendecionDbContext db)
    {
        var tienda = new Tienda { Nombre = "Ficticia", Slug = "ficticia" };
        var cliente = new Cliente { Nombre = "Prueba", Telefono = "000" };
        db.Tiendas.Add(tienda); db.Clientes.Add(cliente); await db.SaveChangesAsync();
        var papel = new Producto { TiendaId = tienda.Id, Nombre = "Hoja", Precio = 1m, TieneInventario = true,
            Stock = 100, EntregaPermitida = EntregaProducto.Ambas };
        db.Productos.Add(papel); await db.SaveChangesAsync();
        var servicio = new Producto { TiendaId = tienda.Id, Nombre = "Impresión", Precio = 3m, CostoUnitario = .70m,
            PapelInsumoId = papel.Id, AdmiteDobleCara = true, RequierePdf = true,
            RevisionImpresion = Guid.NewGuid().ToString("N"), EntregaPermitida = EntregaProducto.Recoleccion };
        db.Productos.Add(servicio); await db.SaveChangesAsync();
        return (tienda, cliente, papel, servicio);
    }

    public static byte[] Pdf(int paginas, bool cifrado = false)
    {
        var objetos = new List<string> { "<< /Type /Catalog /Pages 2 0 R >>",
            $"<< /Type /Pages /Kids [{string.Join(' ', Enumerable.Range(3, paginas).Select(i => $"{i} 0 R"))}] /Count {paginas} >>" };
        for (var i = 0; i < paginas; i++)
            objetos.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << >> /Contents {3 + paginas} 0 R >>");
        objetos.Add("<< /Length 0 >>\nstream\n\nendstream");
        if (cifrado)
            objetos.Add("<< /Filter /Standard /V 1 /R 2 /P -4 /O <0000000000000000000000000000000000000000000000000000000000000000> /U <0000000000000000000000000000000000000000000000000000000000000000> >>");
        var sb = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int> { 0 };
        for (var i = 0; i < objetos.Count; i++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(sb.ToString()));
            sb.Append($"{i + 1} 0 obj\n{objetos[i]}\nendobj\n");
        }
        var xref = Encoding.ASCII.GetByteCount(sb.ToString());
        sb.Append($"xref\n0 {objetos.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1)) sb.Append($"{offset:D10} 00000 n \n");
        sb.Append($"trailer\n<< /Size {objetos.Count + 1} /Root 1 0 R{(cifrado ? $" /Encrypt {objetos.Count} 0 R /ID [<00112233445566778899AABBCCDDEEFF><00112233445566778899AABBCCDDEEFF>]" : "")} >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(sb.ToString());
    }
}

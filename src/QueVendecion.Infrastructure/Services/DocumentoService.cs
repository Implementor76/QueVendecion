using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using QueVendecion.Application.DTOs;
using QueVendecion.Application.Exceptions;
using QueVendecion.Domain.Entities;
using QueVendecion.Domain.Enums;
using QueVendecion.Infrastructure.Data;
using UglyToad.PdfPig;

namespace QueVendecion.Infrastructure.Services;

public sealed class DocumentoService
{
    private readonly QueVendecionDbContext db;
    private readonly string raiz;
    public long MaxBytes { get; }
    public int MaxPaginas { get; }
    public int MaxTrabajos { get; }
    public int HorasTemporal { get; }
    public int DiasRetencion { get; }

    public DocumentoService(QueVendecionDbContext db, IConfiguration config)
    {
        this.db = db;
        raiz = Path.GetFullPath(config["Documentos:Ruta"] ??
            (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development"
                ? Path.Combine(Path.GetTempPath(), "quevendecion-documentos-dev")
                : "/var/lib/quevendecion/documentos"));
        var contenido = Path.GetFullPath(Directory.GetCurrentDirectory());
        if (raiz.StartsWith(contenido + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            raiz.Equals(contenido, StringComparison.OrdinalIgnoreCase) ||
            raiz.StartsWith("/var/www/", StringComparison.Ordinal))
            throw new InvalidOperationException("Documentos:Ruta debe estar fuera de la publicación y del sitio web.");
        MaxBytes = LeerLong(config, "Documentos:MaxBytes", 20L * 1024 * 1024);
        MaxPaginas = LeerInt(config, "Documentos:MaxPaginas", 200);
        MaxTrabajos = LeerInt(config, "Documentos:MaxTrabajosPorPedido", 5);
        HorasTemporal = LeerInt(config, "Documentos:HorasTemporal", 24);
        DiasRetencion = LeerInt(config, "Documentos:DiasRetencion", 30);
        if (MaxBytes is < 1 or > 100L * 1024 * 1024 || MaxPaginas is < 1 or > 500 ||
            MaxTrabajos is < 1 or > 20 || HorasTemporal is < 1 or > 168 || DiasRetencion is < 1 or > 365)
            throw new InvalidOperationException("Límites de Documentos fuera del rango permitido.");
        Directory.CreateDirectory(Path.Combine(raiz, "temporal"));
        Directory.CreateDirectory(Path.Combine(raiz, "pedidos"));
        if (!OperatingSystem.IsWindows())
        {
            const UnixFileMode soloServicio = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
            File.SetUnixFileMode(raiz, soloServicio);
            File.SetUnixFileMode(Path.Combine(raiz, "temporal"), soloServicio);
            File.SetUnixFileMode(Path.Combine(raiz, "pedidos"), soloServicio);
        }
    }

    private static int LeerInt(IConfiguration config, string clave, int predeterminado) =>
        config[clave] == null ? predeterminado : int.Parse(config[clave]!);
    private static long LeerLong(IConfiguration config, string clave, long predeterminado) =>
        config[clave] == null ? predeterminado : long.Parse(config[clave]!);

    public static string HuellaToken(string token) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));

    public async Task<string> CrearSesionAsync(int tiendaId, int clienteId)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        db.SesionesDocumento.Add(new SesionDocumento { TiendaId = tiendaId, ClienteId = clienteId,
            TokenHash = HuellaToken(token), ExpiraEnUtc = DateTime.UtcNow.AddDays(30) });
        await db.SaveChangesAsync();
        return token;
    }

    public async Task<SesionDocumento> ValidarSesionAsync(int tiendaId, string? token)
    {
        if (token?.Length != 64 || !token.All(Uri.IsHexDigit))
            throw new ReglaNegocioException("pdf_sesion_invalida", "Inicia sesión de nuevo para adjuntar o consultar documentos.");
        var hash = HuellaToken(token);
        return await db.SesionesDocumento.AsNoTracking().FirstOrDefaultAsync(s =>
            s.TokenHash == hash && s.TiendaId == tiendaId && s.ExpiraEnUtc > DateTime.UtcNow)
            ?? throw new ReglaNegocioException("pdf_sesion_invalida", "La sesión de documentos venció. Inicia sesión de nuevo.");
    }

    public async Task<DocumentoPdf> SubirAsync(int tiendaId, SesionDocumento sesion, string nombre, string? mime,
        long longitud, Stream origen, CancellationToken ct)
    {
        if (longitud is < 1 || longitud > MaxBytes)
            throw new ReglaNegocioException("pdf_tamano_invalido", "El PDF supera el límite permitido.");
        if (!Path.GetExtension(nombre).Equals(".pdf", StringComparison.OrdinalIgnoreCase) || mime != "application/pdf")
            throw new ReglaNegocioException("pdf_tipo_invalido", "Adjunta únicamente un archivo PDF.");
        var id = Guid.NewGuid();
        var interno = id.ToString("N") + ".pdf";
        var parcial = Path.Combine(raiz, "temporal", interno + ".part");
        var temporal = Path.Combine(raiz, "temporal", interno);
        try
        {
            await using (var destino = new FileStream(parcial, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(parcial, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                var buffer = new byte[81920]; long leidos = 0;
                int n;
                while ((n = await origen.ReadAsync(buffer, ct)) != 0)
                {
                    leidos += n;
                    if (leidos > MaxBytes) throw new ReglaNegocioException("pdf_tamano_invalido", "El PDF supera el límite permitido.");
                    await destino.WriteAsync(buffer.AsMemory(0, n), ct);
                }
                if (leidos != longitud) throw new ReglaNegocioException("pdf_invalido", "La carga del PDF quedó incompleta.");
                await destino.FlushAsync(ct);
            }
            int paginas;
            try
            {
                using var pdf = PdfDocument.Open(parcial);
                if (pdf.IsEncrypted) throw new ReglaNegocioException("pdf_protegido", "No se aceptan PDF cifrados o protegidos con contraseña.");
                paginas = pdf.NumberOfPages;
                if (paginas is < 1 || paginas > MaxPaginas)
                    throw new ReglaNegocioException("pdf_paginas_invalidas", $"El PDF debe tener entre 1 y {MaxPaginas} páginas.");
                for (var p = 1; p <= paginas; p++) _ = pdf.GetPage(p);
            }
            catch (ReglaNegocioException) { throw; }
            catch { throw new ReglaNegocioException("pdf_invalido", "El archivo no es un PDF válido y legible."); }
            string sha;
            await using (var lectura = File.OpenRead(parcial))
                sha = Convert.ToHexString(await SHA256.HashDataAsync(lectura, ct));
            File.Move(parcial, temporal);
            var documento = new DocumentoPdf { Id = id, TiendaId = tiendaId, ClienteId = sesion.ClienteId,
                SesionHash = sesion.TokenHash, NombreInterno = interno,
                NombreOriginal = Path.GetFileName(nombre).Length > 255 ? Path.GetFileName(nombre)[..255] : Path.GetFileName(nombre),
                TamanoBytes = longitud, Tipo = "application/pdf", PaginasTotales = paginas, Sha256 = sha,
                ExpiraEnUtc = DateTime.UtcNow.AddHours(HorasTemporal) };
            db.DocumentosPdf.Add(documento);
            await db.SaveChangesAsync(ct);
            return documento;
        }
        catch
        {
            if (File.Exists(parcial)) File.Delete(parcial);
            if (File.Exists(temporal)) File.Delete(temporal);
            throw;
        }
    }

    private string Ruta(DocumentoPdf d, bool asociado) => Path.Combine(raiz, asociado ? "pedidos" : "temporal", d.Id.ToString("N") + ".pdf");

    public async Task ValidarParaPedidoAsync(DocumentoPdf d, CancellationToken ct = default)
    {
        var path = File.Exists(Ruta(d, false)) ? Ruta(d, false) : Ruta(d, true);
        if (!File.Exists(path) || new FileInfo(path).Length != d.TamanoBytes)
            throw new ReglaNegocioException("pdf_no_disponible", "El PDF ya no está íntegro; vuelve a subirlo.");
        await using (var lectura = File.OpenRead(path))
            if (Convert.ToHexString(await SHA256.HashDataAsync(lectura, ct)) != d.Sha256)
                throw new ReglaNegocioException("pdf_no_disponible", "El PDF ya no está íntegro; vuelve a subirlo.");
        try
        {
            using var pdf = PdfDocument.Open(path);
            if (pdf.IsEncrypted || pdf.NumberOfPages != d.PaginasTotales)
                throw new ReglaNegocioException("pdf_no_disponible", "El PDF cambió; vuelve a subirlo.");
        }
        catch (ReglaNegocioException) { throw; }
        catch { throw new ReglaNegocioException("pdf_no_disponible", "El PDF ya no es legible; vuelve a subirlo."); }
    }

    public bool Finalizar(DocumentoPdf d)
    {
        var origen = Ruta(d, false); var destino = Ruta(d, true);
        if (File.Exists(origen)) { File.Move(origen, destino, false); return true; }
        else if (!File.Exists(destino)) throw new ReglaNegocioException("pdf_no_disponible", "El archivo ya no está disponible; vuelve a subirlo.");
        return false; // Reintento tras una caída: el archivo ya estaba finalizado.
    }

    public void DeshacerFinalizacion(DocumentoPdf d)
    {
        var origen = Ruta(d, true); var destino = Ruta(d, false);
        if (File.Exists(origen) && !File.Exists(destino)) File.Move(origen, destino);
    }

    public string RutaDescarga(DocumentoPdf d)
    {
        if (d.Estado != "Asociado") throw new ReglaNegocioException("pdf_no_disponible", "Archivo eliminado por política de retención.");
        var path = Ruta(d, true);
        if (!File.Exists(path)) throw new ReglaNegocioException("pdf_no_disponible", "El archivo ya no está disponible.");
        return path;
    }

    public async Task LimpiarAsync(CancellationToken ct)
    {
        var ahora = DateTime.UtcNow;
        foreach (var parcial in Directory.EnumerateFiles(Path.Combine(raiz, "temporal"), "*.part"))
            if (Path.GetFileName(parcial).Length == 41 &&
                Path.GetFileName(parcial)[..32].All(Uri.IsHexDigit) &&
                File.GetLastWriteTimeUtc(parcial) < ahora.AddHours(-HorasTemporal))
                File.Delete(parcial);
        var docs = await db.DocumentosPdf.Include(d => d.ItemPedido).ThenInclude(i => i!.Pedido)
            .Where(d => d.Estado != "Eliminado").ToListAsync(ct);
        foreach (var d in docs)
        {
            var expiro = d.Estado == "Temporal" && d.ExpiraEnUtc <= ahora;
            var cierre = d.ItemPedido?.Pedido == null ? null : InstanteCierre(d.ItemPedido.Pedido);
            var cerrado = d.Estado == "Asociado" && cierre.HasValue && cierre.Value <= ahora.AddDays(-DiasRetencion);
            if (!expiro && !cerrado) continue;
            var ruta = Ruta(d, d.Estado == "Asociado");
            if (File.Exists(ruta)) File.Delete(ruta);
            if (expiro && File.Exists(Ruta(d, true))) File.Delete(Ruta(d, true));
            d.Estado = "Eliminado";
            await db.SaveChangesAsync(ct);
        }
        // Un cierre inesperado entre el movimiento de archivo y la escritura SQL puede
        // dejar archivos sin fila. Solo se limpian nombres generados dentro de estas carpetas.
        var idsConFila = (await db.DocumentosPdf.AsNoTracking().Select(d => d.Id).ToListAsync(ct))
            .ToHashSet();
        foreach (var carpeta in new[] { "temporal", "pedidos" })
        foreach (var archivo in Directory.EnumerateFiles(Path.Combine(raiz, carpeta), "*.pdf"))
        {
            var nombre = Path.GetFileNameWithoutExtension(archivo);
            if (nombre.Length == 32 && Guid.TryParseExact(nombre, "N", out var id) &&
                !idsConFila.Contains(id) && File.GetLastWriteTimeUtc(archivo) < ahora.AddHours(-HorasTemporal))
                File.Delete(archivo);
        }
    }

    private static DateTime? InstanteCierre(Pedido pedido)
    {
        if (pedido.Estado == EstadoPedido.Entregado && pedido.EntregadoEnUtc.HasValue)
            return pedido.EntregadoEnUtc.Value;
        if (pedido.Estado is not (EstadoPedido.Entregado or EstadoPedido.Cancelado) ||
            string.IsNullOrWhiteSpace(pedido.HistorialEstadosJson)) return null;
        try
        {
            return JsonSerializer.Deserialize<List<CambioEstadoPedidoDto>>(pedido.HistorialEstadosJson)?
                .LastOrDefault(c => c.Nuevo == pedido.Estado)?.FechaUtc;
        }
        catch (JsonException) { return null; }
    }
}

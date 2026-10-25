using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QueVendecion.Application.Exceptions;
using QueVendecion.Infrastructure.Data;
using QueVendecion.Infrastructure.Services;

namespace QueVendecion.Api.Controllers;

[ApiController]
[Route("api/documentos")]
public class DocumentosController : ControllerBase
{
    private readonly QueVendecionDbContext db;
    private readonly DocumentoService docs;
    public DocumentosController(QueVendecionDbContext db, DocumentoService docs)
    { this.db = db; this.docs = docs; }

    [HttpPost("tienda/{slug}/pdf")]
    [RequestSizeLimit(22L * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 22L * 1024 * 1024)]
    public async Task<IActionResult> Subir(string slug, IFormFile archivo, CancellationToken ct)
    {
        var tienda = await db.Tiendas.AsNoTracking().FirstOrDefaultAsync(t => t.Slug == slug, ct);
        if (tienda == null) return NotFound();
        var sesion = await docs.ValidarSesionAsync(tienda.Id, Request.Headers["X-Documento-Sesion"].ToString());
        if (archivo == null) throw new ReglaNegocioException("pdf_invalido", "Adjunta un PDF.");
        await using var stream = archivo.OpenReadStream();
        var doc = await docs.SubirAsync(tienda.Id, sesion, archivo.FileName, archivo.ContentType, archivo.Length, stream, ct);
        return Ok(new { doc.Id, nombre = doc.NombreOriginal, tamano = doc.TamanoBytes,
            paginas = doc.PaginasTotales, venceEnUtc = doc.ExpiraEnUtc });
    }

    [HttpGet("tienda/{slug}/{id:guid}")]
    public async Task<IActionResult> DescargarCliente(string slug, Guid id)
    {
        var tienda = await db.Tiendas.AsNoTracking().FirstOrDefaultAsync(t => t.Slug == slug);
        if (tienda == null) return NotFound();
        var sesion = await docs.ValidarSesionAsync(tienda.Id, Request.Headers["X-Documento-Sesion"].ToString());
        var doc = await db.DocumentosPdf.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id && d.TiendaId == tienda.Id &&
            d.ClienteId == sesion.ClienteId && d.SesionHash == sesion.TokenHash && d.ItemPedidoId != null);
        if (doc == null) return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return PhysicalFile(docs.RutaDescarga(doc), "application/pdf", doc.NombreOriginal);
    }

    [Authorize(Roles = "Admin,Staff")]
    [HttpGet("mi-tienda/{id:guid}")]
    public async Task<IActionResult> DescargarAdmin(Guid id)
    {
        var tiendaId = int.Parse(User.FindFirst("TiendaId")?.Value ?? "0");
        var doc = await db.DocumentosPdf.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id && d.TiendaId == tiendaId && d.ItemPedidoId != null);
        if (doc == null) return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return PhysicalFile(docs.RutaDescarga(doc), "application/pdf", doc.NombreOriginal);
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QueVendecion.Api.Controllers;
using QueVendecion.Application.DTOs;
using QueVendecion.Application.Interfaces;
using QueVendecion.Domain.Entities;
using QueVendecion.Infrastructure.Services;
using QueVendecion.Tests.Helpers;

namespace QueVendecion.Tests.Services;

public class ProductoHttpTests
{
    [Fact]
    public async Task PostDevuelve201ConLocationConsultableSoloPorSuTienda()
    {
        using var db = TestDbContextFactory.Create();
        var tienda = new Tienda { Nombre = "HTTP", Slug = "http" };
        db.Tiendas.Add(tienda);
        await db.SaveChangesAsync();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(db);
        builder.Services.AddScoped<ITiendaService, TiendaService>();
        builder.Services.AddScoped<IProductoService, ProductoService>();
        builder.Services.AddControllers().AddApplicationPart(typeof(TiendasController).Assembly);
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuth>("Test", _ => { });
        builder.Services.AddAuthorization();
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        client.DefaultRequestHeaders.Add("X-Test-Tienda", tienda.Id.ToString());
        var response = await client.PostAsJsonAsync("/api/tiendas/mi-tienda/productos",
            new CrearProductoDto { Nombre = "Hielo", TieneInventario = true, Stock = 10, CostoUnitario = 3.9167m, ExistenciaMinima = 2 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Contains("/mi-tienda/productos/", response.Headers.Location.ToString());
        var get = await client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Equal(10, (await get.Content.ReadFromJsonAsync<ProductoDto>())!.Stock);
        Assert.Contains("costoUnitario", await get.Content.ReadAsStringAsync());
        var publicCatalog = await client.GetAsync($"/api/tiendas/{tienda.Id}/productos");
        Assert.Equal(HttpStatusCode.OK, publicCatalog.StatusCode);
        Assert.DoesNotContain("costoUnitario", await publicCatalog.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        client.DefaultRequestHeaders.Remove("X-Test-Tienda");
        client.DefaultRequestHeaders.Add("X-Test-Tienda", (tienda.Id + 1).ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(response.Headers.Location)).StatusCode);
        await app.StopAsync();
    }

    public class TestAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(
            AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(
                new ClaimsIdentity([new Claim("TiendaId", Request.Headers["X-Test-Tienda"].ToString())], "Test")), "Test")));
    }
}

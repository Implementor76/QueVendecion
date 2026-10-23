using System.Text.Json;
using QueVendecion.Application.DTOs;
using QueVendecion.Application.Exceptions;
using QueVendecion.Domain.Entities;
using QueVendecion.Domain.Enums;
using QueVendecion.Infrastructure.Services;
using QueVendecion.Tests.Helpers;
using QueVendecion.Api.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using System.Security.Claims;

namespace QueVendecion.Tests.Services;

public class OperacionTests
{
    private static readonly DateTime Hoy = new(2026, 10, 21, 18, 0, 0, DateTimeKind.Utc); // 11:00 Hermosillo
    private static OperacionTiendaDto Config() => new()
    {
        PedidosAlMomento = true, PedidosProgramados = true, Recoleccion = true, Domicilio = true,
        ZonaHoraria = "America/Hermosillo", AnticipacionMaximaDias = 7,
        DiasPreviosCorte = 1, HoraCorte = "12:00",
        HorariosAtencion = new() { new() { Dia = DayOfWeek.Wednesday, Inicio = "09:00", Fin = "17:00" } },
        VentanasServicio = new() { new() { Dia = DayOfWeek.Thursday, Inicio = "13:00", Fin = "15:00" } }
    };

    [Fact]
    public void CorteExactoEsValido_YUnSegundoDespuesNo()
    {
        var config = Config();
        var fecha = new DateOnly(2026, 10, 22);
        Assert.True(OperacionReglas.PuedeProgramarse(config, fecha, "13:00-15:00", new DateTime(2026, 10, 21, 19, 0, 0, DateTimeKind.Utc)));
        Assert.False(OperacionReglas.PuedeProgramarse(config, fecha, "13:00-15:00", new DateTime(2026, 10, 21, 19, 0, 1, DateTimeKind.Utc)));
    }

    [Fact]
    public void PedidoProgramadoYProductoPorFecha()
    {
        var config = Config();
        var dto = new CrearPedidoDto { Modalidad = ModalidadPedido.Programado, Entrega = FormaEntrega.Domicilio,
            DireccionEntrega = "Calle 1", FechaServicio = new DateOnly(2026, 10, 22), VentanaSolicitada = "13:00-15:00" };
        var producto = new Producto { Nombre = "Comida", EntregaPermitida = EntregaProducto.Ambas, FechasServicioJson = JsonSerializer.Serialize(new[] { dto.FechaServicio }) };
        OperacionReglas.ValidarPedido(config, dto, new[] { producto }, Hoy);
        producto.FechasServicioJson = JsonSerializer.Serialize(new[] { new DateOnly(2026, 10, 23) });
        Assert.Equal("producto_fecha_incompatible", Assert.Throws<ReglaNegocioException>(() => OperacionReglas.ValidarPedido(config, dto, new[] { producto }, Hoy)).Codigo);
        dto.VentanaSolicitada = "20:00-22:00";
        Assert.Equal("horario_no_disponible", Assert.Throws<ReglaNegocioException>(() => OperacionReglas.ValidarPedido(config, dto, new[] { producto }, Hoy)).Codigo);
        dto.Modalidad = ModalidadPedido.AlMomento;
        dto.FechaServicio = new DateOnly(2026, 10, 21);
        dto.VentanaSolicitada = null;
        Assert.Equal("producto_modalidad_incompatible", Assert.Throws<ReglaNegocioException>(() => OperacionReglas.ValidarPedido(config, dto, new[] { producto }, Hoy)).Codigo);
    }

    [Fact]
    public void AlMomentoRespetaAtencionYFormaEntrega()
    {
        var config = Config();
        var dto = new CrearPedidoDto { Modalidad = ModalidadPedido.AlMomento, Entrega = FormaEntrega.Recoleccion };
        OperacionReglas.ValidarPedido(config, dto, Array.Empty<Producto>(), Hoy);
        Assert.Equal("fuera_de_atencion", Assert.Throws<ReglaNegocioException>(() => OperacionReglas.ValidarPedido(config, dto, Array.Empty<Producto>(), Hoy.AddHours(8))).Codigo);
        config.Recoleccion = false;
        Assert.Equal("entrega_no_disponible", Assert.Throws<ReglaNegocioException>(() => OperacionReglas.ValidarPedido(config, dto, Array.Empty<Producto>(), Hoy)).Codigo);
        config.Recoleccion = true;
        dto.Entrega = FormaEntrega.Domicilio;
        Assert.Equal("direccion_entrega_invalida", Assert.Throws<ReglaNegocioException>(() => OperacionReglas.ValidarPedido(config, dto, Array.Empty<Producto>(), Hoy)).Codigo);
        dto.DireccionEntrega = "Calle 1";
        config.Domicilio = false;
        Assert.Equal("entrega_no_disponible", Assert.Throws<ReglaNegocioException>(() => OperacionReglas.ValidarPedido(config, dto, Array.Empty<Producto>(), Hoy)).Codigo);
    }

    [Fact]
    public void AnticipacionMinimaYMaximaLimitanOpciones()
    {
        var config = Config();
        config.DiasPreviosCorte = 0;
        config.AnticipacionMinimaMinutos = 27 * 60;
        Assert.False(OperacionReglas.PuedeProgramarse(config, new DateOnly(2026, 10, 22), "13:00-15:00", Hoy));
        config.AnticipacionMinimaMinutos = 0;
        config.AnticipacionMaximaDias = 1;
        config.VentanasServicio.Add(new() { Dia = DayOfWeek.Friday, Inicio = "13:00", Fin = "15:00" });
        Assert.False(OperacionReglas.PuedeProgramarse(config, new DateOnly(2026, 10, 23), "13:00-15:00", Hoy));
    }

    [Fact]
    public void ImpresionesSeRecibenAntesDeLaRecoleccionNocturna()
    {
        var config = Config();
        config.DiasPreviosCorte = 0;
        config.VentanasServicio = new() { new() { Dia = DayOfWeek.Wednesday, Inicio = "20:00", Fin = "22:00" } };
        var dto = new CrearPedidoDto { Modalidad = ModalidadPedido.Programado, Entrega = FormaEntrega.Recoleccion,
            FechaServicio = new DateOnly(2026, 10, 21), VentanaSolicitada = "20:00-22:00" };
        OperacionReglas.ValidarPedido(config, dto, Array.Empty<Producto>(), Hoy);
    }

    [Fact]
    public async Task HistorialSinFechaSigueConsultableYFiltroNoLoInventa()
    {
        using var db = TestDbContextFactory.Create();
        var tienda = new Tienda { Nombre = "Histórica", Slug = "historica" };
        db.Tiendas.Add(tienda);
        await db.SaveChangesAsync();
        db.Pedidos.Add(new Pedido { TiendaId = tienda.Id, Total = 10 });
        await db.SaveChangesAsync();
        var service = new PedidoService(db);
        var todos = await service.ObtenerPorTiendaAsync(tienda.Id);
        Assert.Single(todos);
        Assert.Null(todos[0].FechaServicio);
        Assert.Empty(await service.ObtenerPorTiendaAsync(tienda.Id, fechaServicio: new DateOnly(2026, 10, 21)));
    }

    [Fact]
    public async Task AdministradorNoPuedeVerNiModificarPedidosDeOtraTienda()
    {
        using var db = TestDbContextFactory.Create();
        var primera = new Tienda { Nombre = "Primera", Slug = "primera" };
        var segunda = new Tienda { Nombre = "Segunda", Slug = "segunda" };
        db.Tiendas.AddRange(primera, segunda);
        await db.SaveChangesAsync();
        var pedido = new Pedido { TiendaId = segunda.Id, Total = 10 };
        db.Pedidos.Add(pedido);
        await db.SaveChangesAsync();
        var controller = new PedidosController(new PedidoService(db))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("TiendaId", primera.Id.ToString()) }, "Test"))
            } }
        };
        Assert.IsType<ForbidResult>((await controller.ObtenerPorId(pedido.Id)).Result);
        Assert.IsType<ForbidResult>((await controller.ActualizarEstado(pedido.Id, new ActualizarEstadoPedidoDto { Estado = EstadoPedido.Confirmado })).Result);
        Assert.IsType<ForbidResult>((await controller.ConfirmarHorario(pedido.Id, new ConfirmarHorarioPedidoDto { HorarioConfirmado = "20:00" })).Result);
    }

    [Fact]
    public async Task UpgradeSqliteConservaDatosHistoricosYEsRepetible()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "CREATE TABLE Tiendas (Id INTEGER PRIMARY KEY, Nombre TEXT); CREATE TABLE Productos (Id INTEGER PRIMARY KEY, Nombre TEXT); CREATE TABLE Pedidos (Id INTEGER PRIMARY KEY, TiendaId INTEGER, Estado INTEGER); INSERT INTO Tiendas (Id, Nombre) VALUES (1, 'Existente'); INSERT INTO Pedidos (Id, TiendaId, Estado) VALUES (7, 1, 0);";
            await command.ExecuteNonQueryAsync();
        }
        using var db = new QueVendecion.Infrastructure.Data.QueVendecionDbContext(
            new DbContextOptionsBuilder<QueVendecion.Infrastructure.Data.QueVendecionDbContext>().UseSqlite(connection).Options);
        await QueVendecion.Infrastructure.Data.OperacionSchemaUpgrade.AplicarAsync(db);
        await QueVendecion.Infrastructure.Data.OperacionSchemaUpgrade.AplicarAsync(db);
        await using var count = connection.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM Pedidos WHERE Id = 7 AND FechaServicio IS NULL";
        Assert.Equal(1L, await count.ExecuteScalarAsync());
    }

    [Fact]
    public async Task PrecioDeHieloDependeDelProductoYEntregaExplicita()
    {
        using var db = TestDbContextFactory.Create();
        var tienda = new Tienda { Nombre = "Catalogo", Slug = "catalogo",
            OperacionJson = JsonSerializer.Serialize(new OperacionTiendaDto { PedidosAlMomento = true, Domicilio = true }) };
        db.Tiendas.Add(tienda);
        await db.SaveChangesAsync();
        var productos = new ProductoService(db);
        var recoger = await productos.CrearAsync(tienda.Id, new() { Nombre = "Bolsa recoger", Precio = 25,
            EntregaPermitida = EntregaProducto.Recoleccion });
        var domicilio = await productos.CrearAsync(tienda.Id, new() { Nombre = "Bolsa domicilio", Precio = 30,
            EntregaPermitida = EntregaProducto.Domicilio });
        var antiguo = new Producto { TiendaId = tienda.Id, Nombre = "Sin asignar", Precio = 25 };
        db.Productos.Add(antiguo);
        await db.SaveChangesAsync();
        var service = new PedidoService(db);
        CrearPedidoDto Crear(int productoId, FormaEntrega entrega) => new()
        {
            TiendaId = tienda.Id, Entrega = entrega, DireccionEntrega = entrega == FormaEntrega.Domicilio ? "Calle 1" : null,
            Items = new() { new() { ProductoId = productoId, Cantidad = 1 } }
        };
        Assert.Equal(25, (await service.CrearAsync(Crear(recoger.Id, FormaEntrega.Recoleccion))).Total);
        Assert.Equal(30, (await service.CrearAsync(Crear(domicilio.Id, FormaEntrega.Domicilio))).Total);
        foreach (var dto in new[] { Crear(recoger.Id, FormaEntrega.Domicilio), Crear(domicilio.Id, FormaEntrega.Recoleccion),
            Crear(antiguo.Id, FormaEntrega.Recoleccion) })
            Assert.Equal("producto_entrega_incompatible", (await Assert.ThrowsAsync<ReglaNegocioException>(() => service.CrearAsync(dto))).Codigo);
        Assert.Equal(2, await db.Pedidos.CountAsync());
    }
}

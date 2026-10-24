using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QueVendecion.Domain.Entities;
using QueVendecion.Domain.Enums;
using QueVendecion.Infrastructure.Data;
using QueVendecion.SqliteUpgrade;

namespace QueVendecion.Tests.Services;

public class SqliteUpgradeToolTests
{
    [Fact]
    public async Task DiagnosticoIdentificaCampoSinRevelarDatos()
    {
        var path = Path.Combine(Path.GetTempPath(), "qvd-diagnostico-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            await CrearVersionAnterior(path);
            await using var conn = new SqliteConnection($"Data Source={path};Pooling=False");
            await conn.OpenAsync();
            var anterior = await SqliteUpgradeValidation.CapturarAsync(conn);
            await using (var alteracion = conn.CreateCommand())
            {
                alteracion.CommandText = "UPDATE Productos SET Stock=13 WHERE Id=2";
                await alteracion.ExecuteNonQueryAsync();
            }
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => SqliteUpgradeValidation.VerificarAsync(conn, anterior));
            Assert.Contains("Productos Id=2 campo=Stock", error.Message);
            Assert.DoesNotContain("ficticio", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("@", error.Message);
        }
        finally { SqliteConnection.ClearAllPools(); File.Delete(path); }
    }

    [Fact]
    public async Task VersionAnteriorConPinyexYMovimientosSeConservaEnDosPasadas()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "QueVendecion.sln"))) root = root.Parent;
        Assert.NotNull(root);
        var carpeta = Path.Combine(Path.GetTempPath(), "qvd-upgrade-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(carpeta);
        var origen = Path.Combine(carpeta, "anterior.db");
        var primera = Path.Combine(carpeta, "primera.db");
        var segunda = Path.Combine(carpeta, "segunda.db");
        try
        {
            await CrearVersionAnterior(origen);
            var dll = Path.Combine(root.FullName, "tools", "QueVendecion.SqliteUpgrade", "bin", "Debug", "net9.0", "QueVendecion.SqliteUpgrade.dll");
            Assert.True(File.Exists(dll), dll);
            var resultado1 = await Ejecutar(dll, origen, primera);
            Assert.True(resultado1.Code == 0, resultado1.Output);
            Assert.Contains("Pedidos=2", resultado1.Output);
            var resultado2 = await Ejecutar(dll, primera, segunda);
            Assert.True(resultado2.Code == 0, resultado2.Output);
            await using var conn = new SqliteConnection($"Data Source={segunda};Pooling=False");
            await conn.OpenAsync();
            Assert.Equal("ok", await Escalar(conn, "PRAGMA integrity_check"));
            Assert.Null(await Escalar(conn, "PRAGMA foreign_key_check"));
            Assert.Equal(5L, await Escalar(conn, "SELECT count(*) FROM Productos WHERE TiendaId=2"));
            Assert.Equal(12L, await Escalar(conn, "SELECT Stock FROM Productos WHERE TiendaId=2 AND Nombre='Pluma ficticia'"));
            Assert.Equal(2L, await Escalar(conn, "SELECT count(*) FROM MovimientosInventario WHERE ProductoId=2"));
            Assert.Equal(11L, await Escalar(conn, "SELECT StockNuevo FROM MovimientosInventario WHERE ProductoId=2 AND Tipo=4"));
            Assert.Equal(12L, await Escalar(conn, "SELECT StockNuevo FROM MovimientosInventario WHERE ProductoId=2 AND Tipo=2"));
            Assert.Equal(0L, await Escalar(conn, "SELECT Origen FROM Pedidos WHERE Id=1"));
            Assert.Equal(DBNull.Value, await Escalar(conn, "SELECT ClaveIdempotencia FROM Pedidos WHERE Id=1"));
        }
        finally { SqliteConnection.ClearAllPools(); Directory.Delete(carpeta, true); }
    }

    private static async Task CrearVersionAnterior(string path)
    {
        var options = new DbContextOptionsBuilder<QueVendecionDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options;
        await using (var db = new QueVendecionDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            db.Tiendas.AddRange(new Tienda { Nombre = "Jax ficticia", Slug = "jax-ficticia" },
                new Tienda { Nombre = "Pinyex ficticia", Slug = "pinyex-ficticia" });
            await db.SaveChangesAsync();
            db.UsuariosTienda.Add(new UsuarioTienda { TiendaId = 2, Nombre = "Admin ficticia", Email = "a@example.test", PasswordHash = "hash-ficticio", Rol = RolUsuario.Admin });
            var cliente = new Cliente { Nombre = "Cliente ficticio", Telefono = "000" };
            db.Clientes.Add(cliente);
            db.Productos.Add(new Producto { TiendaId = 1, Nombre = "Hielo ficticio", Precio = 25m });
            db.Productos.AddRange(
                new Producto { TiendaId = 2, Nombre = "Pluma ficticia", Precio = 10m, CostoUnitario = 3.9167m, TieneInventario = true, Stock = 12, ExistenciaMinima = 3 },
                new Producto { TiendaId = 2, Nombre = "Libreta ficticia", Precio = 20m, CostoUnitario = 8m, TieneInventario = true, Stock = 5 },
                new Producto { TiendaId = 2, Nombre = "Copia ficticia", Precio = 3m, CostoUnitario = .70m },
                new Producto { TiendaId = 2, Nombre = "Servicio sin costo conocido", Precio = 4m },
                new Producto { TiendaId = 2, Nombre = "Servicio costo cero", Precio = 5m, CostoUnitario = 0m });
            await db.SaveChangesAsync();
            db.Pedidos.AddRange(
                new Pedido { TiendaId = 1, ClienteId = cliente.Id, ClienteNombre = cliente.Nombre, Total = 25m,
                    Estado = EstadoPedido.Cancelado, Items = new List<ItemPedido> { new() { ProductoId = 1, Cantidad = 1, PrecioUnitario = 25m } } },
                new Pedido { TiendaId = 2, ClienteId = cliente.Id, ClienteNombre = cliente.Nombre, Total = 20m,
                    Estado = EstadoPedido.Entregado, EstadoPago = EstadoPago.Pagado, EntregadoEnUtc = DateTime.UtcNow,
                    HistorialEstadosJson = "[]", Items = new List<ItemPedido> { new() { ProductoId = 2, Cantidad = 2, PrecioUnitario = 10m, CostoUnitarioHistorico = 3.9167m } } });
            db.MovimientosInventario.AddRange(
                new MovimientoInventario { ProductoId = 2, Tipo = TipoMovimiento.Inicial, Cantidad = 11, StockAnterior = 0, StockNuevo = 11, UsuarioId = 1 },
                new MovimientoInventario { ProductoId = 2, Tipo = TipoMovimiento.Ajuste, Cantidad = 1, StockAnterior = 11, StockNuevo = 12, UsuarioId = 1, Observaciones = "Ajuste ficticio" });
            await db.SaveChangesAsync();
        }
        await using var conn = new SqliteConnection($"Data Source={path};Pooling=False");
        await conn.OpenAsync();
        foreach (var sql in new[] { "DROP INDEX IX_Pedidos_TiendaId_ClaveIdempotencia",
            "ALTER TABLE Pedidos DROP COLUMN ClaveIdempotencia", "ALTER TABLE Pedidos DROP COLUMN HuellaSolicitud",
            "ALTER TABLE Pedidos DROP COLUMN PagadoEnUtc", "ALTER TABLE Pedidos DROP COLUMN CobradoPorUsuarioId",
            "ALTER TABLE Pedidos DROP COLUMN Origen" })
        {
            await using var cmd = conn.CreateCommand(); cmd.CommandText = sql; await cmd.ExecuteNonQueryAsync();
        }
    }

    private static async Task<(int Code, string Output)> Ejecutar(string dll, string origen, string destino)
    {
        var info = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        info.ArgumentList.Add(dll); info.ArgumentList.Add(origen); info.ArgumentList.Add(destino);
        using var process = Process.Start(info)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await stdout + await stderr);
    }

    private static async Task<object?> Escalar(SqliteConnection conn, string sql)
    {
        await using var cmd = conn.CreateCommand(); cmd.CommandText = sql; return await cmd.ExecuteScalarAsync();
    }
}

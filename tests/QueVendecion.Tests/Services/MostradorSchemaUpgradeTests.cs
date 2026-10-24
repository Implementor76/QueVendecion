using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QueVendecion.Infrastructure.Data;

namespace QueVendecion.Tests.Services;

public class MostradorSchemaUpgradeTests
{
    [Fact]
    public async Task EsquemaHeredadoSeAmpliaDosVecesSinCambiarPedidos()
    {
        await using var conn = new SqliteConnection("Data Source=:memory:");
        await conn.OpenAsync();
        await using var db = new QueVendecionDbContext(new DbContextOptionsBuilder<QueVendecionDbContext>()
            .UseSqlite(conn).Options);
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE Pedidos (Id INTEGER PRIMARY KEY, TiendaId INTEGER NOT NULL, Total TEXT NOT NULL)");
        await db.Database.ExecuteSqlRawAsync("INSERT INTO Pedidos (Id,TiendaId,Total) VALUES (7,3,'25.0'),(8,3,'30.0')");
        await MostradorSchemaUpgrade.AplicarAsync(db);
        await MostradorSchemaUpgrade.AplicarAsync(db);
        await MostradorSchemaUpgrade.VerificarAsync(db);
        await using var check = conn.CreateCommand();
        check.CommandText = "SELECT group_concat(Id || ':' || TiendaId || ':' || Total || ':' || Origen, ',') FROM Pedidos ORDER BY Id";
        Assert.Equal("7:3:25.0:0,8:3:30.0:0", await check.ExecuteScalarAsync());
        check.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name='IX_Pedidos_TiendaId_ClaveIdempotencia'";
        Assert.Equal(1L, await check.ExecuteScalarAsync());
        check.CommandText = "PRAGMA integrity_check";
        Assert.Equal("ok", await check.ExecuteScalarAsync());
        check.CommandText = "PRAGMA foreign_key_check";
        Assert.Null(await check.ExecuteScalarAsync());
    }
}

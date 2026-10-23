using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace QueVendecion.Infrastructure.Data;

public static class CostosSchemaUpgrade
{
    private static readonly (string Tabla, string Columna, string Tipo)[] Campos =
    {
        ("Productos", "CostoUnitario", "TEXT"), ("Productos", "ExistenciaMinima", "INTEGER"),
        ("ItemsPedido", "CostoUnitarioHistorico", "TEXT"),
        ("MovimientosInventario", "UsuarioId", "INTEGER"), ("MovimientosInventario", "CostoUnitario", "TEXT"),
        ("Pedidos", "EntregadoEnUtc", "TEXT")
    };

    public static async Task AplicarAsync(QueVendecionDbContext db)
    {
        if (!db.Database.IsSqlite()) throw new NotSupportedException("Solo SQLite.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        foreach (var (tabla, columna, tipo) in Campos)
        {
            if (await ExisteAsync(db, tabla, columna)) continue;
#pragma warning disable EF1002
            await db.Database.ExecuteSqlRawAsync($"ALTER TABLE \"{tabla}\" ADD COLUMN \"{columna}\" {tipo} NULL");
#pragma warning restore EF1002
        }
        await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS \"IX_Pedidos_TiendaId_EntregadoEnUtc\" ON \"Pedidos\" (\"TiendaId\", \"EntregadoEnUtc\")");
        await tx.CommitAsync();
    }

    public static async Task VerificarAsync(QueVendecionDbContext db)
    {
        if (db.Database.IsSqlite())
            foreach (var (tabla, columna, _) in Campos)
                if (!await ExisteAsync(db, tabla, columna)) throw new InvalidOperationException("Falta el esquema de costos.");
    }

    private static async Task<bool> ExisteAsync(QueVendecionDbContext db, string tabla, string columna)
    {
        var conn = db.Database.GetDbConnection();
        var abrir = conn.State != ConnectionState.Open;
        if (abrir) await conn.OpenAsync();
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            cmd.CommandText = $"PRAGMA table_info(\"{tabla}\")";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) if (reader.GetString(1) == columna) return true;
            return false;
        }
        finally { if (abrir) await conn.CloseAsync(); }
    }
}

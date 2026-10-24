using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace QueVendecion.Infrastructure.Data;

public static class MostradorSchemaUpgrade
{
    private static readonly (string Nombre, string Tipo)[] Campos =
    {
        ("Origen", "INTEGER NOT NULL DEFAULT 0"),
        ("ClaveIdempotencia", "TEXT NULL"),
        ("HuellaSolicitud", "TEXT NULL"),
        ("PagadoEnUtc", "TEXT NULL"),
        ("CobradoPorUsuarioId", "INTEGER NULL")
    };

    public static async Task AplicarAsync(QueVendecionDbContext db)
    {
        if (!db.Database.IsSqlite()) throw new NotSupportedException("Solo SQLite.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        foreach (var (nombre, tipo) in Campos)
        {
            if (await ExisteAsync(db, nombre)) continue;
#pragma warning disable EF1002
            await db.Database.ExecuteSqlRawAsync($"ALTER TABLE \"Pedidos\" ADD COLUMN \"{nombre}\" {tipo}");
#pragma warning restore EF1002
        }
        await db.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Pedidos_TiendaId_ClaveIdempotencia\" ON \"Pedidos\" (\"TiendaId\", \"ClaveIdempotencia\")");
        await tx.CommitAsync();
    }

    public static async Task VerificarAsync(QueVendecionDbContext db)
    {
        if (!db.Database.IsSqlite()) return;
        foreach (var (nombre, _) in Campos)
            if (!await ExisteAsync(db, nombre)) throw new InvalidOperationException("Falta el esquema de mostrador.");
    }

    private static async Task<bool> ExisteAsync(QueVendecionDbContext db, string columna)
    {
        var conn = db.Database.GetDbConnection();
        var abrir = conn.State != ConnectionState.Open;
        if (abrir) await conn.OpenAsync();
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            cmd.CommandText = "PRAGMA table_info(\"Pedidos\")";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) if (reader.GetString(1) == columna) return true;
            return false;
        }
        finally { if (abrir) await conn.CloseAsync(); }
    }
}

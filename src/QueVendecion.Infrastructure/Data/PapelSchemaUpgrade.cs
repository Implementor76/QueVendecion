using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace QueVendecion.Infrastructure.Data;

public static class PapelSchemaUpgrade
{
    private static readonly (string Tabla, string Columna, string Tipo)[] Campos =
    {
        ("Productos", "PapelInsumoId", "INTEGER NULL"),
        ("Productos", "AdmiteDobleCara", "INTEGER NOT NULL DEFAULT 0"),
        ("ItemsPedido", "CarasPorJuego", "INTEGER NULL"),
        ("ItemsPedido", "Juegos", "INTEGER NULL"),
        ("ItemsPedido", "DobleCara", "INTEGER NULL"),
        ("ItemsPedido", "PapelInsumoIdHistorico", "INTEGER NULL"),
        ("ItemsPedido", "HojasConsumidas", "INTEGER NULL")
    };

    public static async Task AplicarAsync(QueVendecionDbContext db)
    {
        if (!db.Database.IsSqlite()) throw new NotSupportedException("Solo SQLite.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        foreach (var (tabla, columna, tipo) in Campos)
        {
            if (await ExisteAsync(db, tabla, columna)) continue;
#pragma warning disable EF1002
            await db.Database.ExecuteSqlRawAsync($"ALTER TABLE \"{tabla}\" ADD COLUMN \"{columna}\" {tipo}");
#pragma warning restore EF1002
        }
        await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS \"IX_Productos_PapelInsumoId\" ON \"Productos\" (\"PapelInsumoId\")");
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER IF NOT EXISTS Papel_Validar_Insert BEFORE INSERT ON Productos
            WHEN NEW.PapelInsumoId IS NOT NULL
            BEGIN
              SELECT RAISE(ABORT, 'papel_invalido') WHERE NEW.TieneInventario <> 0 OR
                NOT EXISTS (SELECT 1 FROM Productos i WHERE i.Id=NEW.PapelInsumoId AND i.TiendaId=NEW.TiendaId AND i.TieneInventario=1 AND i.PapelInsumoId IS NULL);
            END
            """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER IF NOT EXISTS Papel_Validar_Update BEFORE UPDATE OF PapelInsumoId, TiendaId, TieneInventario ON Productos
            WHEN NEW.PapelInsumoId IS NOT NULL
            BEGIN
              SELECT RAISE(ABORT, 'papel_invalido') WHERE NEW.TieneInventario <> 0 OR NEW.PapelInsumoId=NEW.Id OR
                NOT EXISTS (SELECT 1 FROM Productos i WHERE i.Id=NEW.PapelInsumoId AND i.TiendaId=NEW.TiendaId AND i.TieneInventario=1 AND i.PapelInsumoId IS NULL);
            END
            """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER IF NOT EXISTS Papel_Proteger_Delete BEFORE DELETE ON Productos
            WHEN EXISTS (SELECT 1 FROM Productos s WHERE s.PapelInsumoId=OLD.Id)
            BEGIN SELECT RAISE(ABORT, 'insumo_referenciado'); END
            """);
        await tx.CommitAsync();
    }

    public static async Task VerificarAsync(QueVendecionDbContext db)
    {
        if (!db.Database.IsSqlite()) return;
        foreach (var (tabla, columna, _) in Campos)
            if (!await ExisteAsync(db, tabla, columna)) throw new InvalidOperationException("Falta el esquema de papel.");
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

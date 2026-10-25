using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace QueVendecion.Infrastructure.Data;

public static class DocumentosSchemaUpgrade
{
    public static async Task AplicarAsync(QueVendecionDbContext db)
    {
        if (!db.Database.IsSqlite()) throw new NotSupportedException("Solo SQLite.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        if (!await ExisteColumna(db, "Productos", "RequierePdf"))
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Productos\" ADD COLUMN \"RequierePdf\" INTEGER NOT NULL DEFAULT 0");
        if (!await ExisteColumna(db, "Productos", "RevisionImpresion"))
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Productos\" ADD COLUMN \"RevisionImpresion\" TEXT NULL");
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "SesionesDocumento" (
              "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
              "TiendaId" INTEGER NOT NULL, "ClienteId" INTEGER NOT NULL,
              "TokenHash" TEXT NOT NULL, "ExpiraEnUtc" TEXT NOT NULL)
            """);
        await db.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_SesionesDocumento_TokenHash\" ON \"SesionesDocumento\" (\"TokenHash\")");
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "DocumentosPdf" (
              "Id" TEXT NOT NULL PRIMARY KEY, "TiendaId" INTEGER NOT NULL,
              "ClienteId" INTEGER NOT NULL, "SesionHash" TEXT NOT NULL,
              "ItemPedidoId" INTEGER NULL, "NombreOriginal" TEXT NOT NULL,
              "NombreInterno" TEXT NOT NULL, "TamanoBytes" INTEGER NOT NULL,
              "Tipo" TEXT NOT NULL, "PaginasTotales" INTEGER NOT NULL,
              "Sha256" TEXT NOT NULL, "CreadoEnUtc" TEXT NOT NULL,
              "ExpiraEnUtc" TEXT NULL, "Estado" TEXT NOT NULL,
              "PaginasSeleccionadas" TEXT NULL,
              CONSTRAINT "FK_DocumentosPdf_ItemsPedido_ItemPedidoId" FOREIGN KEY ("ItemPedidoId") REFERENCES "ItemsPedido" ("Id") ON DELETE RESTRICT)
            """);
        await db.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_DocumentosPdf_ItemPedidoId\" ON \"DocumentosPdf\" (\"ItemPedidoId\")");
        await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS \"IX_DocumentosPdf_TiendaId_ClienteId_Estado\" ON \"DocumentosPdf\" (\"TiendaId\", \"ClienteId\", \"Estado\")");
        await tx.CommitAsync();
    }

    public static async Task VerificarAsync(QueVendecionDbContext db)
    {
        if (!db.Database.IsSqlite()) return;
        if (!await ExisteColumna(db, "Productos", "RequierePdf") ||
            !await ExisteColumna(db, "DocumentosPdf", "Id") ||
            !await ExisteColumna(db, "SesionesDocumento", "Id"))
            throw new InvalidOperationException("Falta el esquema de documentos PDF.");
    }

    private static async Task<bool> ExisteColumna(QueVendecionDbContext db, string tabla, string columna)
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

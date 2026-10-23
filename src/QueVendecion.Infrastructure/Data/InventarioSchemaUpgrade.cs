using System.Data;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace QueVendecion.Infrastructure.Data;

/// <summary>Additive bridge for databases created with EnsureCreated.</summary>
public static class InventarioSchemaUpgrade
{
    public static async Task AplicarAsync(QueVendecionDbContext db)
    {
        if (!db.Database.IsSqlite())
            throw new NotSupportedException("Este upgrade es exclusivo de SQLite de desarrollo. PostgreSQL requiere EF Core Migrations.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var columns = await LeerColumnasAsync(db, "Productos");
        if (columns.Count == 0)
            throw new InvalidOperationException("No existe la tabla Productos. El upgrade requiere una base existente del proyecto.");

        if (!columns.Contains("TieneInventario"))
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Productos\" ADD COLUMN \"TieneInventario\" INTEGER NOT NULL DEFAULT 0");
        if (!columns.Contains("Stock"))
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Productos\" ADD COLUMN \"Stock\" INTEGER NULL");

        // Use the provider's model DDL to preserve identity, FK and date types.
        var script = db.Database.GenerateCreateScript();
        foreach (Match match in Regex.Matches(script,
            "CREATE TABLE \"MovimientosInventario\"[\\s\\S]*?;|CREATE INDEX \"[^\"]+\" ON \"MovimientosInventario\"[^;]*;"))
        {
            var sql = match.Value.Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ")
                .Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS ");
            await db.Database.ExecuteSqlRawAsync(sql);
        }
        await VerificarAsync(db);
        await transaction.CommitAsync();
    }

    public static async Task VerificarAsync(QueVendecionDbContext db)
    {
        // Inspect SQLite metadata first: querying Producto on a legacy schema fails.
        if (db.Database.IsSqlite())
        {
            var columns = await LeerColumnasAsync(db, "Productos");
            var movimientos = await LeerColumnasAsync(db, "MovimientosInventario");
            if (!columns.Contains("TieneInventario") || !columns.Contains("Stock") || movimientos.Count == 0)
                throw new InvalidOperationException("Esquema de inventario pendiente. Ejecuta InventarioSchemaUpgrade.AplicarAsync antes de consultar las entidades.");
        }
        await db.Productos.AsNoTracking().OrderBy(p => p.Id).Select(p => new { p.Id, p.TieneInventario, p.Stock }).Take(1).ToListAsync();
        await db.MovimientosInventario.AsNoTracking().OrderBy(m => m.Id).Take(1).ToListAsync();
        if (await db.Productos.AnyAsync(p => p.TieneInventario && (p.Stock == null || p.Stock < 0)))
            throw new InvalidOperationException("Hay inventarios inválidos. Revísalos antes de completar la actualización.");
    }

    private static async Task<HashSet<string>> LeerColumnasAsync(QueVendecionDbContext db, string table)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            // Table names only come from constants in this class.
            command.CommandText = db.Database.IsSqlite()
                ? $"PRAGMA table_info(\"{table}\")"
                : $"SELECT * FROM \"{table}\" WHERE 1 = 0";
            await using var reader = await command.ExecuteReaderAsync();
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (db.Database.IsSqlite())
            {
                while (await reader.ReadAsync()) columns.Add(reader.GetString(1));
            }
            else
            {
                for (var i = 0; i < reader.FieldCount; i++) columns.Add(reader.GetName(i));
            }
            return columns;
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }
}

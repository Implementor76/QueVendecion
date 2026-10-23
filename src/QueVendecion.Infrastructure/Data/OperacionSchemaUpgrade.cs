using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace QueVendecion.Infrastructure.Data;

/// <summary>Additive upgrade for existing SQLite databases created with EnsureCreated.</summary>
public static class OperacionSchemaUpgrade
{
    public static async Task AplicarAsync(QueVendecionDbContext db)
    {
        if (!db.Database.IsSqlite()) throw new NotSupportedException("Este upgrade solo aplica a SQLite.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        foreach (var (table, column, type) in new (string, string, string)[]
        {
            ("Tiendas", "OperacionJson", "TEXT"),
            ("Productos", "FechasServicioJson", "TEXT"),
            ("Productos", "EntregaPermitida", "INTEGER"),
            ("Pedidos", "Modalidad", "INTEGER"),
            ("Pedidos", "Entrega", "INTEGER"),
            ("Pedidos", "DireccionEntrega", "TEXT"),
            ("Pedidos", "FechaServicio", "TEXT"),
            ("Pedidos", "VentanaSolicitada", "TEXT"),
            ("Pedidos", "HorarioConfirmado", "TEXT")
            ,("Pedidos", "HistorialEstadosJson", "TEXT")
        })
        {
            if (!await TieneColumnaAsync(db, table, column))
            {
                // All identifiers and types come from the fixed tuple list above, never from input.
#pragma warning disable EF1002
                await db.Database.ExecuteSqlRawAsync($"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {type} NULL");
#pragma warning restore EF1002
            }
        }
        await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS \"IX_Pedidos_TiendaId_FechaServicio_Estado\" ON \"Pedidos\" (\"TiendaId\", \"FechaServicio\", \"Estado\")");
        await transaction.CommitAsync();
    }

    public static async Task VerificarAsync(QueVendecionDbContext db)
    {
        if (db.Database.IsSqlite())
        {
            foreach (var (table, column) in new[] { ("Tiendas", "OperacionJson"), ("Productos", "FechasServicioJson"), ("Productos", "EntregaPermitida"), ("Pedidos", "FechaServicio"), ("Pedidos", "HorarioConfirmado"), ("Pedidos", "HistorialEstadosJson"), ("Pedidos", "DireccionEntrega") })
                if (!await TieneColumnaAsync(db, table, column)) throw new InvalidOperationException("Falta actualizar el esquema de operación.");
        }
        await db.Tiendas.AsNoTracking().Select(t => t.OperacionJson).Take(1).ToListAsync();
        await db.Productos.AsNoTracking().Select(p => new { p.FechasServicioJson, p.EntregaPermitida }).Take(1).ToListAsync();
        await db.Pedidos.AsNoTracking().Select(p => new { p.Modalidad, p.Entrega, p.DireccionEntrega, p.FechaServicio, p.VentanaSolicitada, p.HorarioConfirmado, p.HistorialEstadosJson }).Take(1).ToListAsync();
    }

    private static async Task<bool> TieneColumnaAsync(QueVendecionDbContext db, string table, string column)
    {
        var connection = db.Database.GetDbConnection();
        var opened = connection.State != ConnectionState.Open;
        if (opened) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = $"PRAGMA table_info(\"{table}\")";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) if (reader.GetString(1).Equals(column, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
        finally { if (opened) await connection.CloseAsync(); }
    }
}

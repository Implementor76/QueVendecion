using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QueVendecion.Infrastructure.Data;

if (args.Length != 2)
    throw new ArgumentException("Uso: QueVendecion.SqliteUpgrade <origen.db> <copia-nueva.db>");

var source = Path.GetFullPath(args[0]);
var destination = Path.GetFullPath(args[1]);
if (!File.Exists(source)) throw new FileNotFoundException("No existe la base de origen.", source);
if (File.Exists(destination)) throw new IOException("La copia destino ya existe; el programa nunca la sobrescribe.");
if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
    throw new IOException("El origen y la copia deben ser archivos diferentes.");
if (!Directory.Exists(Path.GetDirectoryName(destination)))
    throw new DirectoryNotFoundException("Crea el directorio destino antes de ejecutar el ensayo.");

var sourceBuilder = new SqliteConnectionStringBuilder { DataSource = source, Mode = SqliteOpenMode.ReadOnly, Pooling = false };
var destinationBuilder = new SqliteConnectionStringBuilder { DataSource = destination, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false };
await using var sourceConnection = new SqliteConnection(sourceBuilder.ToString());
await using var destinationConnection = new SqliteConnection(destinationBuilder.ToString());
await sourceConnection.OpenAsync();
await destinationConnection.OpenAsync();
sourceConnection.BackupDatabase(destinationConnection);
var before = await Counts(destinationConnection);
var beforeHasStock = await HasColumn(destinationConnection, "Productos", "Stock");
var beforeValues = await BusinessValues(destinationConnection, beforeHasStock);
await destinationConnection.CloseAsync();

var dbOptions = new DbContextOptionsBuilder<QueVendecionDbContext>().UseSqlite(destinationBuilder.ToString()).Options;
await using (var db = new QueVendecionDbContext(dbOptions))
{
    await InventarioSchemaUpgrade.AplicarAsync(db);
    await OperacionSchemaUpgrade.AplicarAsync(db);
    await CostosSchemaUpgrade.AplicarAsync(db);
    await InventarioSchemaUpgrade.VerificarAsync(db);
    await OperacionSchemaUpgrade.VerificarAsync(db);
    await CostosSchemaUpgrade.VerificarAsync(db);
}

await destinationConnection.OpenAsync();
var after = await Counts(destinationConnection);
if (!before.SequenceEqual(after)) throw new InvalidOperationException("Los conteos cambiaron durante el ensayo.");
if (!beforeValues.SequenceEqual(await BusinessValues(destinationConnection, beforeHasStock)))
    throw new InvalidOperationException("Cambió un precio, pedido o existencia durante el ensayo.");
await using (var integrity = destinationConnection.CreateCommand())
{
    integrity.CommandText = "PRAGMA integrity_check";
    if ((string?)await integrity.ExecuteScalarAsync() != "ok")
        throw new InvalidOperationException("La copia no pasó PRAGMA integrity_check.");
}
await using (var foreignKeys = destinationConnection.CreateCommand())
{
    foreignKeys.CommandText = "PRAGMA foreign_key_check";
    await using var reader = await foreignKeys.ExecuteReaderAsync();
    if (await reader.ReadAsync()) throw new InvalidOperationException("La copia tiene claves foráneas inválidas.");
}
Console.WriteLine($"Copia actualizada y verificada: {destination}");
Console.WriteLine(string.Join(", ", after.Select(kv => $"{kv.Key}={kv.Value}")));

static async Task<Dictionary<string, long>> Counts(SqliteConnection connection)
{
    var result = new Dictionary<string, long>();
    foreach (var table in new[] { "Tiendas", "UsuariosTienda", "Clientes", "Productos", "Pedidos", "ItemsPedido" })
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM \"{table}\""; // Fixed table names only.
        result[table] = (long)(await command.ExecuteScalarAsync() ?? 0L);
    }
    return result;
}

static async Task<bool> HasColumn(SqliteConnection connection, string table, string column)
{
    await using var command = connection.CreateCommand();
    command.CommandText = $"PRAGMA table_info(\"{table}\")";
    await using var rows = await command.ExecuteReaderAsync();
    while (await rows.ReadAsync()) if (rows.GetString(1) == column) return true;
    return false;
}

static async Task<List<string>> BusinessValues(SqliteConnection connection, bool includeStock)
{
    var result = new List<string>();
    foreach (var (table, columns) in new[]
    {
        ("Productos", includeStock ? new[] { "Id", "Precio", "Stock" } : new[] { "Id", "Precio" }),
        ("Pedidos", new[] { "Id", "Total", "Estado" }),
        ("ItemsPedido", new[] { "Id", "PrecioUnitario", "Cantidad" })
    })
    {
        var available = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var schema = connection.CreateCommand())
        {
            schema.CommandText = $"PRAGMA table_info(\"{table}\")";
            await using var reader = await schema.ExecuteReaderAsync();
            while (await reader.ReadAsync()) available.Add(reader.GetString(1));
        }
        var selected = columns.Where(available.Contains).ToArray();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {string.Join(", ", selected.Select(c => $"\"{c}\""))} FROM \"{table}\" ORDER BY \"Id\"";
        await using var rows = await command.ExecuteReaderAsync();
        while (await rows.ReadAsync())
            result.Add(table + ":" + string.Join("|", Enumerable.Range(0, rows.FieldCount).Select(i => rows.IsDBNull(i) ? "NULL" : Convert.ToString(rows.GetValue(i), System.Globalization.CultureInfo.InvariantCulture))));
    }
    return result;
}

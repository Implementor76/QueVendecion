using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QueVendecion.Infrastructure.Data;
using QueVendecion.SqliteUpgrade;

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
var before = await SqliteUpgradeValidation.CapturarAsync(destinationConnection);
await destinationConnection.CloseAsync();

var dbOptions = new DbContextOptionsBuilder<QueVendecionDbContext>().UseSqlite(destinationBuilder.ToString()).Options;
await using (var db = new QueVendecionDbContext(dbOptions))
{
    await InventarioSchemaUpgrade.AplicarAsync(db);
    await OperacionSchemaUpgrade.AplicarAsync(db);
    await CostosSchemaUpgrade.AplicarAsync(db);
    await MostradorSchemaUpgrade.AplicarAsync(db);
    await PapelSchemaUpgrade.AplicarAsync(db);
    await InventarioSchemaUpgrade.VerificarAsync(db);
    await OperacionSchemaUpgrade.VerificarAsync(db);
    await CostosSchemaUpgrade.VerificarAsync(db);
    await MostradorSchemaUpgrade.VerificarAsync(db);
    await PapelSchemaUpgrade.VerificarAsync(db);
}

await destinationConnection.OpenAsync();
await SqliteUpgradeValidation.VerificarAsync(destinationConnection, before);
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
Console.WriteLine(string.Join(", ", before.Conteos));

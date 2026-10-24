using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace QueVendecion.SqliteUpgrade;

internal static class SqliteUpgradeValidation
{
    private static readonly string[] Tablas =
    {
        "Tiendas", "UsuariosTienda", "Clientes", "Productos", "Pedidos", "ItemsPedido", "MovimientosInventario"
    };

    internal sealed record TablaAnterior(string[] Columnas, Dictionary<long, Dictionary<string, string>> Filas);
    internal sealed record Instantanea(Dictionary<string, TablaAnterior> Tablas)
    {
        public IEnumerable<string> Conteos => Tablas.Select(t => $"{t.Key}={t.Value.Filas.Count}");
    }

    internal static async Task<Instantanea> CapturarAsync(SqliteConnection conexion)
    {
        var tablas = new Dictionary<string, TablaAnterior>(StringComparer.OrdinalIgnoreCase);
        foreach (var tabla in Tablas)
        {
            var columnas = await ColumnasAsync(conexion, tabla);
            if (columnas.Length == 0)
            {
                if (tabla == "MovimientosInventario") continue; // Legacy inventory bridge creates it.
                throw new InvalidOperationException($"Falta la tabla necesaria {tabla} en el origen.");
            }
            if (!columnas.Contains("Id", StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException($"La tabla {tabla} no tiene Id; no se puede comprobar fila por fila.");
            tablas[tabla] = new TablaAnterior(columnas, await FilasAsync(conexion, tabla, columnas));
        }
        return new Instantanea(tablas);
    }

    internal static async Task VerificarAsync(SqliteConnection conexion, Instantanea anterior)
    {
        foreach (var (tabla, guardada) in anterior.Tablas)
        {
            var actuales = await ColumnasAsync(conexion, tabla);
            foreach (var columna in guardada.Columnas)
                if (!actuales.Contains(columna, StringComparer.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"No se conservó {tabla}: falta el campo anterior {columna}.");

            // Read the exact original columns, in their original order. New columns never enter this comparison.
            var filas = await FilasAsync(conexion, tabla, guardada.Columnas);
            foreach (var id in guardada.Filas.Keys)
            {
                if (!filas.TryGetValue(id, out var actual))
                    throw new InvalidOperationException($"No se conservó {tabla} Id={id}: falta la fila.");
                foreach (var columna in guardada.Columnas)
                    if (guardada.Filas[id][columna] != actual[columna])
                        throw new InvalidOperationException($"No se conservó {tabla} Id={id} campo={columna}.");
            }
            var adicional = filas.Keys.FirstOrDefault(id => !guardada.Filas.ContainsKey(id));
            if (filas.Count != guardada.Filas.Count)
                throw new InvalidOperationException($"No se conservó {tabla}: filas antes={guardada.Filas.Count}, después={filas.Count}, primer Id adicional={adicional}.");

            foreach (var columna in actuales.Except(guardada.Columnas, StringComparer.OrdinalIgnoreCase))
                await VerificarColumnaNuevaAsync(conexion, tabla, columna);
        }

        if (!anterior.Tablas.ContainsKey("MovimientosInventario"))
        {
            if ((await ColumnasAsync(conexion, "MovimientosInventario")).Length == 0)
                throw new InvalidOperationException("No se creó MovimientosInventario.");
            await using var vacia = conexion.CreateCommand();
            vacia.CommandText = "SELECT COUNT(*) FROM \"MovimientosInventario\"";
            if ((long)(await vacia.ExecuteScalarAsync() ?? -1L) != 0)
                throw new InvalidOperationException("La tabla nueva MovimientosInventario no está vacía.");
        }

        await VerificarIndiceMostradorAsync(conexion);
    }

    private static async Task VerificarColumnaNuevaAsync(SqliteConnection conexion, string tabla, string columna)
    {
        var cero = tabla == "Productos" && columna is "TieneInventario" or "AdmiteDobleCara" || tabla == "Pedidos" && columna == "Origen";
        await using var comando = conexion.CreateCommand();
        comando.CommandText = $"SELECT \"Id\" FROM {Identificador(tabla)} WHERE {Identificador(columna)} IS " +
            (cero ? "NULL OR " + Identificador(columna) + " <> 0" : "NOT NULL") + " LIMIT 1";
        var id = await comando.ExecuteScalarAsync();
        if (id != null)
            throw new InvalidOperationException($"Valor inicial inválido en {tabla} Id={id} campo nuevo={columna}; se esperaba {(cero ? "0" : "NULL")}.");
    }

    private static async Task VerificarIndiceMostradorAsync(SqliteConnection conexion)
    {
        var encontrado = false;
        await using (var lista = conexion.CreateCommand())
        {
            lista.CommandText = "PRAGMA index_list(\"Pedidos\")";
            await using var lector = await lista.ExecuteReaderAsync();
            while (await lector.ReadAsync())
                if (lector.GetString(1) == "IX_Pedidos_TiendaId_ClaveIdempotencia" && lector.GetInt64(2) == 1)
                    encontrado = true;
        }
        if (!encontrado)
            throw new InvalidOperationException("Falta el índice único de idempotencia de mostrador.");
        var columnas = new List<string>();
        await using var indice = conexion.CreateCommand();
        indice.CommandText = "PRAGMA index_info(\"IX_Pedidos_TiendaId_ClaveIdempotencia\")";
        await using var filas = await indice.ExecuteReaderAsync();
        while (await filas.ReadAsync()) columnas.Add(filas.GetString(2));
        if (!columnas.SequenceEqual(new[] { "TiendaId", "ClaveIdempotencia" }))
            throw new InvalidOperationException("El índice de idempotencia tiene columnas distintas a las esperadas.");
    }

    private static async Task<string[]> ColumnasAsync(SqliteConnection conexion, string tabla)
    {
        var columnas = new List<string>();
        await using var comando = conexion.CreateCommand();
        comando.CommandText = $"PRAGMA table_info({Identificador(tabla)})";
        await using var filas = await comando.ExecuteReaderAsync();
        while (await filas.ReadAsync()) columnas.Add(filas.GetString(1));
        return columnas.ToArray();
    }

    private static async Task<Dictionary<long, Dictionary<string, string>>> FilasAsync(SqliteConnection conexion, string tabla, string[] columnas)
    {
        var resultado = new Dictionary<long, Dictionary<string, string>>();
        await using var comando = conexion.CreateCommand();
        comando.CommandText = $"SELECT {string.Join(", ", columnas.Select(Identificador))} FROM {Identificador(tabla)} ORDER BY \"Id\"";
        await using var filas = await comando.ExecuteReaderAsync();
        var idIndex = Array.FindIndex(columnas, c => c.Equals("Id", StringComparison.OrdinalIgnoreCase));
        while (await filas.ReadAsync())
        {
            var campos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < columnas.Length; i++) campos[columnas[i]] = Huella(filas.GetValue(i));
            var id = filas.GetInt64(idIndex);
            if (!resultado.TryAdd(id, campos)) throw new InvalidOperationException($"Id duplicado en {tabla}.");
        }
        return resultado;
    }

    // Hashes prevent sensitive values (clients, password hashes, notes) from appearing in diagnostics.
    private static string Huella(object valor)
    {
        if (valor == DBNull.Value) return "NULL";
        byte[] bytes = valor switch
        {
            byte[] blob => [.. Encoding.UTF8.GetBytes("blob:"), .. blob],
            long entero => [.. Encoding.UTF8.GetBytes("integer:"), .. BitConverter.GetBytes(entero)],
            double real => [.. Encoding.UTF8.GetBytes("real:"), .. BitConverter.GetBytes(real)],
            _ => Encoding.UTF8.GetBytes("text:" + Convert.ToString(valor, CultureInfo.InvariantCulture))
        };
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static string Identificador(string nombre) => "\"" + nombre.Replace("\"", "\"\"") + "\"";
}

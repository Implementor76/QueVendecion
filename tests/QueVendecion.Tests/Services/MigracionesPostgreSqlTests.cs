using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using QueVendecion.Infrastructure.Data;
using QueVendecion.Infrastructure.Migrations.PostgreSql;

namespace QueVendecion.Tests.Services;

public class MigracionesPostgreSqlTests
{
    private const string Provider = "Npgsql.EntityFrameworkCore.PostgreSQL";
    private const string Baseline = "20260917143531_EsquemaAnteriorInventario";
    private const string Inventario = "20260917143552_AgregarInventario";
    private const string Operacion = "20261022054127_AgregarOperacionTienda";
    private const string Direccion = "20261022055059_AgregarDireccionEntrega";
    private const string EntregaEstados = "20261022060414_AsignarEntregaYRegistrarEstados";
    private const string Costos = "20261023064705_AgregarCostosYResumen";
    private const string Mostrador = "20261024162911_AgregarVentaMostrador";
    private const string Papel = "20261024194902_AgregarPapelEnServicios";
    private const string Pdf = "20261024220710_AgregarImpresionPdf";

    private static QueVendecionDbContext Create() => new(new DbContextOptionsBuilder<QueVendecionDbContext>()
        .UseNpgsql("Host=localhost;Database=design_only;Username=design_only").Options);

    [Fact]
    public void InventarioSoloAgregaColumnasTablaEIndices()
    {
        var migration = new AgregarInventario { ActiveProvider = Provider };
        Assert.All(migration.UpOperations, op => Assert.True(op is AddColumnOperation or CreateTableOperation or CreateIndexOperation));
        var columns = migration.UpOperations.OfType<AddColumnOperation>().ToList();
        Assert.Equal(2, columns.Count);
        var control = Assert.Single(columns, c => c.Name == "TieneInventario");
        Assert.Equal("Productos", control.Table);
        Assert.False(control.IsNullable);
        Assert.Equal(false, control.DefaultValue);
        Assert.Equal("boolean", control.ColumnType);
        var stock = Assert.Single(columns, c => c.Name == "Stock");
        Assert.Equal("Productos", stock.Table);
        Assert.True(stock.IsNullable);
        Assert.Equal("integer", stock.ColumnType);
        var table = Assert.Single(migration.UpOperations.OfType<CreateTableOperation>());
        Assert.Equal("MovimientosInventario", table.Name);
        Assert.Equal(2, table.ForeignKeys.Count);
    }

    [Fact]
    public void SnapshotCoincideConModeloPostgreSql()
    {
        using var db = Create();
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Equal(new[] { Baseline, Inventario, Operacion, Direccion, EntregaEstados, Costos, Mostrador, Papel, Pdf }, db.Database.GetMigrations());
    }

    [Fact]
    public void ScriptIncrementalEsIdempotenteYNoReconstruyeProductos()
    {
        using var db = Create();
        var sql = db.GetService<IMigrator>().GenerateScript(Baseline, Inventario, MigrationsSqlGenerationOptions.Idempotent);
        Assert.Contains("ADD \"TieneInventario\" boolean NOT NULL DEFAULT FALSE", sql);
        Assert.Contains("ADD \"Stock\" integer", sql);
        Assert.Contains("CREATE TABLE \"MovimientosInventario\"", sql);
        Assert.Contains("IF NOT EXISTS(SELECT 1 FROM \"__EFMigrationsHistory\"", sql);
        Assert.DoesNotContain("DROP ", sql);
        Assert.DoesNotContain("DELETE FROM ", sql);
        Assert.DoesNotContain("TRUNCATE ", sql);
        Assert.DoesNotContain("CREATE TABLE \"Productos\"", sql);
        Assert.DoesNotContain("UPDATE \"Productos\"", sql);

        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "QueVendecion.sln"))) root = root.Parent;
        Assert.NotNull(root);
        var artifact = File.ReadAllText(Path.Combine(root.FullName, "deploy", "postgresql", "inventario.sql"));
        Assert.Equal(System.Text.RegularExpressions.Regex.Replace(sql.Replace("\r\n", "\n"), @"'9\.0\.\d+'", "'9.0.x'").Trim(),
            System.Text.RegularExpressions.Regex.Replace(artifact.Replace("\r\n", "\n"), @"'9\.0\.\d+'", "'9.0.x'").Trim());
    }

    [Fact]
    public void CostosSoloAgregaColumnasEIndice()
    {
        var migration = new AgregarCostosYResumen { ActiveProvider = Provider };
        Assert.All(migration.UpOperations, op => Assert.True(op is AddColumnOperation or CreateIndexOperation));
        Assert.Equal(6, migration.UpOperations.OfType<AddColumnOperation>().Count());
        Assert.All(migration.UpOperations.OfType<AddColumnOperation>(), c => Assert.True(c.IsNullable));
        using var db = Create();
        var sql = db.GetService<IMigrator>().GenerateScript(EntregaEstados, Costos, MigrationsSqlGenerationOptions.Idempotent);
        Assert.Contains("CostoUnitarioHistorico", sql);
        Assert.Contains("EntregadoEnUtc", sql);
        Assert.DoesNotContain("DROP TABLE", sql);
        Assert.DoesNotContain("UPDATE \"Productos\"", sql);
    }

    [Fact]
    public async Task PuenteSqliteRechazaPostgreSqlAntesDeConectar()
    {
        using var db = Create();
        await Assert.ThrowsAsync<NotSupportedException>(() => InventarioSchemaUpgrade.AplicarAsync(db));
    }

    [Fact]
    public void MostradorSoloAgregaColumnasEIndiceUnico()
    {
        var migration = new AgregarVentaMostrador { ActiveProvider = Provider };
        Assert.All(migration.UpOperations, op => Assert.True(op is AddColumnOperation or CreateIndexOperation));
        Assert.Equal(5, migration.UpOperations.OfType<AddColumnOperation>().Count());
        var origen = Assert.Single(migration.UpOperations.OfType<AddColumnOperation>(), c => c.Name == "Origen");
        Assert.Equal(0, origen.DefaultValue);
        Assert.False(origen.IsNullable);
        Assert.True(Assert.Single(migration.UpOperations.OfType<CreateIndexOperation>()).IsUnique);
        using var db = Create();
        var sql = db.GetService<IMigrator>().GenerateScript(Costos, Mostrador, MigrationsSqlGenerationOptions.Idempotent);
        Assert.Contains("ClaveIdempotencia", sql);
        Assert.DoesNotContain("DROP TABLE", sql);
        Assert.DoesNotContain("DELETE FROM", sql);
    }

    [Fact]
    public void MigracionesRechazanSqliteYRollbackDestructivo()
    {
        Assert.Throws<NotSupportedException>(() => new AgregarInventario { ActiveProvider = "Microsoft.EntityFrameworkCore.Sqlite" }.UpOperations);
        Assert.Throws<NotSupportedException>(() => new EsquemaAnteriorInventario { ActiveProvider = "Microsoft.EntityFrameworkCore.Sqlite" }.UpOperations);
        Assert.Throws<NotSupportedException>(() => new AgregarInventario { ActiveProvider = Provider }.DownOperations);
        Assert.Throws<NotSupportedException>(() => new EsquemaAnteriorInventario { ActiveProvider = Provider }.DownOperations);
        Assert.Throws<NotSupportedException>(() => new AgregarCostosYResumen { ActiveProvider = "Microsoft.EntityFrameworkCore.Sqlite" }.UpOperations);
        Assert.Throws<NotSupportedException>(() => new AgregarCostosYResumen { ActiveProvider = Provider }.DownOperations);
        Assert.Throws<NotSupportedException>(() => new AgregarVentaMostrador { ActiveProvider = "Microsoft.EntityFrameworkCore.Sqlite" }.UpOperations);
        Assert.Throws<NotSupportedException>(() => new AgregarVentaMostrador { ActiveProvider = Provider }.DownOperations);
        Assert.Throws<NotSupportedException>(() => new AgregarPapelEnServicios { ActiveProvider = "Microsoft.EntityFrameworkCore.Sqlite" }.UpOperations);
        Assert.Throws<NotSupportedException>(() => new AgregarPapelEnServicios { ActiveProvider = Provider }.DownOperations);
        Assert.Throws<NotSupportedException>(() => new AgregarImpresionPdf { ActiveProvider = "Microsoft.EntityFrameworkCore.Sqlite" }.UpOperations);
        Assert.Throws<NotSupportedException>(() => new AgregarImpresionPdf { ActiveProvider = Provider }.DownOperations);
    }
}

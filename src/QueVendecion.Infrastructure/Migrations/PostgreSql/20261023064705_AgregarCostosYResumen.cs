using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QueVendecion.Infrastructure.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class AgregarCostosYResumen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider != "Npgsql.EntityFrameworkCore.PostgreSQL")
                throw new NotSupportedException("These migrations only support PostgreSQL.");
            migrationBuilder.AddColumn<decimal>(
                name: "CostoUnitario",
                table: "Productos",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExistenciaMinima",
                table: "Productos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EntregadoEnUtc",
                table: "Pedidos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CostoUnitario",
                table: "MovimientosInventario",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UsuarioId",
                table: "MovimientosInventario",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CostoUnitarioHistorico",
                table: "ItemsPedido",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_TiendaId_EntregadoEnUtc",
                table: "Pedidos",
                columns: new[] { "TiendaId", "EntregadoEnUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("La reversión eliminaría costos y fechas de entrega registrados.");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QueVendecion.Infrastructure.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class AgregarOperacionTienda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider != "Npgsql.EntityFrameworkCore.PostgreSQL")
                throw new NotSupportedException("These migrations only support PostgreSQL.");
            migrationBuilder.DropIndex(
                name: "IX_Pedidos_TiendaId",
                table: "Pedidos");

            migrationBuilder.AddColumn<string>(
                name: "OperacionJson",
                table: "Tiendas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FechasServicioJson",
                table: "Productos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Entrega",
                table: "Pedidos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "FechaServicio",
                table: "Pedidos",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HorarioConfirmado",
                table: "Pedidos",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Modalidad",
                table: "Pedidos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VentanaSolicitada",
                table: "Pedidos",
                type: "character varying(11)",
                maxLength: 11,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_TiendaId_FechaServicio_Estado",
                table: "Pedidos",
                columns: new[] { "TiendaId", "FechaServicio", "Estado" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("La reversión eliminaría datos de operación y programación.");
        }
    }
}

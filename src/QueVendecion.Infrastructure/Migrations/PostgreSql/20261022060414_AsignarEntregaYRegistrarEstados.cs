using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QueVendecion.Infrastructure.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class AsignarEntregaYRegistrarEstados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider != "Npgsql.EntityFrameworkCore.PostgreSQL")
                throw new NotSupportedException("These migrations only support PostgreSQL.");
            migrationBuilder.AddColumn<int>(
                name: "EntregaPermitida",
                table: "Productos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HistorialEstadosJson",
                table: "Pedidos",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("La reversión eliminaría las asignaciones y el historial de estados.");
        }
    }
}

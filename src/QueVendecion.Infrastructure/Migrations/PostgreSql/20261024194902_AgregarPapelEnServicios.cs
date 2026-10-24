using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QueVendecion.Infrastructure.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class AgregarPapelEnServicios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider != "Npgsql.EntityFrameworkCore.PostgreSQL")
                throw new NotSupportedException("These migrations only support PostgreSQL.");
            migrationBuilder.AddColumn<bool>(
                name: "AdmiteDobleCara",
                table: "Productos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PapelInsumoId",
                table: "Productos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CarasPorJuego",
                table: "ItemsPedido",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DobleCara",
                table: "ItemsPedido",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HojasConsumidas",
                table: "ItemsPedido",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Juegos",
                table: "ItemsPedido",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PapelInsumoIdHistorico",
                table: "ItemsPedido",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Productos_PapelInsumoId",
                table: "Productos",
                column: "PapelInsumoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Productos_Productos_PapelInsumoId",
                table: "Productos",
                column: "PapelInsumoId",
                principalTable: "Productos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("La reversión eliminaría la configuración y el consumo histórico de papel.");
        }
    }
}

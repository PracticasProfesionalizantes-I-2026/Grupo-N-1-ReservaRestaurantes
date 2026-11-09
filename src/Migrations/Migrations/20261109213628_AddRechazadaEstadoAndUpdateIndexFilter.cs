using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddRechazadaEstadoAndUpdateIndexFilter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reservas_MesaId_FechaHora",
                table: "Reservas");

            migrationBuilder.CreateIndex(
                name: "IX_Reservas_MesaId_FechaHora",
                table: "Reservas",
                columns: new[] { "MesaId", "FechaHora" },
                unique: true,
                filter: "\"Estado\" NOT IN (4, 5, 6, 7)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reservas_MesaId_FechaHora",
                table: "Reservas");

            migrationBuilder.CreateIndex(
                name: "IX_Reservas_MesaId_FechaHora",
                table: "Reservas",
                columns: new[] { "MesaId", "FechaHora" },
                unique: true,
                filter: "\"Estado\" NOT IN (4, 5, 6)");
        }
    }
}

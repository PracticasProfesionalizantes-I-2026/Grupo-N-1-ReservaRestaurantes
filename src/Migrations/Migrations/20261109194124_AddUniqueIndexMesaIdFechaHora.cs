using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueIndexMesaIdFechaHora : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Eliminar el índice simple existente en (MesaId, FechaHora)
            migrationBuilder.DropIndex(
                name: "IX_Reservas_MesaId_FechaHora",
                table: "Reservas");

            // Crear índice único parcial: solo aplica a reservas activas (excluyendo Cancelada=4, Rechazada=5, NoShow=6)
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_Reservas_MesaId_FechaHora\" ON \"Reservas\" (\"MesaId\", \"FechaHora\") " +
                "WHERE \"Estado\" NOT IN (4, 5, 6);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Eliminar el índice único parcial
            migrationBuilder.Sql(
                "DROP INDEX IF EXISTS \"IX_Reservas_MesaId_FechaHora\";");

            // Restaurar el índice simple original
            migrationBuilder.CreateIndex(
                name: "IX_Reservas_MesaId_FechaHora",
                table: "Reservas",
                columns: new[] { "MesaId", "FechaHora" });
        }
    }
}

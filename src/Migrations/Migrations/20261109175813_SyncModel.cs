using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Migrations.Migrations
{
    /// <inheritdoc />
    public partial class SyncModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reservas_ClienteId",
                table: "Reservas");

            migrationBuilder.DropIndex(
                name: "IX_Reservas_MesaId",
                table: "Reservas");

            migrationBuilder.AlterColumn<int>(
                name: "DuracionEstimadaMinutos",
                table: "Reservas",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldDefaultValue: 120);

            migrationBuilder.AlterColumn<bool>(
                name: "Activa",
                table: "Mesas",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "INTEGER",
                oldDefaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "Activo",
                table: "Clientes",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "Clientes",
                type: "TEXT",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Rol",
                table: "Clientes",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Reservas_ClienteId_FechaHora",
                table: "Reservas",
                columns: new[] { "ClienteId", "FechaHora" });

            migrationBuilder.CreateIndex(
                name: "IX_Reservas_Estado",
                table: "Reservas",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_Reservas_FechaHora",
                table: "Reservas",
                column: "FechaHora");

            migrationBuilder.CreateIndex(
                name: "IX_Reservas_MesaId_FechaHora",
                table: "Reservas",
                columns: new[] { "MesaId", "FechaHora" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reservas_ClienteId_FechaHora",
                table: "Reservas");

            migrationBuilder.DropIndex(
                name: "IX_Reservas_Estado",
                table: "Reservas");

            migrationBuilder.DropIndex(
                name: "IX_Reservas_FechaHora",
                table: "Reservas");

            migrationBuilder.DropIndex(
                name: "IX_Reservas_MesaId_FechaHora",
                table: "Reservas");

            migrationBuilder.DropColumn(
                name: "Activo",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "Rol",
                table: "Clientes");

            migrationBuilder.AlterColumn<int>(
                name: "DuracionEstimadaMinutos",
                table: "Reservas",
                type: "INTEGER",
                nullable: false,
                defaultValue: 120,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<bool>(
                name: "Activa",
                table: "Mesas",
                type: "INTEGER",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "INTEGER");

            migrationBuilder.CreateIndex(
                name: "IX_Reservas_ClienteId",
                table: "Reservas",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Reservas_MesaId",
                table: "Reservas",
                column: "MesaId");
        }
    }
}

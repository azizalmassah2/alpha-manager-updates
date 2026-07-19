using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MikroTikVoucherPrinter.Infrastructure.Migrations.PlatformDb
{
    /// <inheritdoc />
    public partial class AddMacAddressIdentifier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MacAddress",
                table: "Devices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_MacAddress",
                table: "Devices",
                column: "MacAddress",
                unique: true,
                filter: "\"MacAddress\" IS NOT NULL AND \"MacAddress\" != ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Devices_MacAddress",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "MacAddress",
                table: "Devices");
        }
    }
}

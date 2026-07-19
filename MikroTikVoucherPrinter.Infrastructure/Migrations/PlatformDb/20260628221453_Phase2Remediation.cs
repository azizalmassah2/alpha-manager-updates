using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MikroTikVoucherPrinter.Infrastructure.Migrations.PlatformDb
{
    /// <inheritdoc />
    public partial class Phase2Remediation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Devices_SerialNumber",
                table: "Devices");

            migrationBuilder.DropIndex(
                name: "IX_Devices_SoftwareId",
                table: "Devices");

            migrationBuilder.AlterColumn<string>(
                name: "SoftwareId",
                table: "Devices",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "SerialNumber",
                table: "Devices",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AddColumn<string>(
                name: "Protocol",
                table: "ConnectionProfiles",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_SerialNumber",
                table: "Devices",
                column: "SerialNumber",
                unique: true,
                filter: "\"SerialNumber\" IS NOT NULL AND \"SerialNumber\" != ''");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_SoftwareId",
                table: "Devices",
                column: "SoftwareId",
                unique: true,
                filter: "\"SoftwareId\" IS NOT NULL AND \"SoftwareId\" != ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Devices_SerialNumber",
                table: "Devices");

            migrationBuilder.DropIndex(
                name: "IX_Devices_SoftwareId",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "Protocol",
                table: "ConnectionProfiles");

            migrationBuilder.AlterColumn<string>(
                name: "SoftwareId",
                table: "Devices",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SerialNumber",
                table: "Devices",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_SerialNumber",
                table: "Devices",
                column: "SerialNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_SoftwareId",
                table: "Devices",
                column: "SoftwareId",
                unique: true);
        }
    }
}

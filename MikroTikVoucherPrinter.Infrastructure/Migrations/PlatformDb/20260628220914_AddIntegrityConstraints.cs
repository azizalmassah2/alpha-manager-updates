using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MikroTikVoucherPrinter.Infrastructure.Migrations.PlatformDb
{
    /// <inheritdoc />
    public partial class AddIntegrityConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CurrentConnectionProfileId",
                table: "Devices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SyncQueue_CreatedAt",
                table: "SyncQueue",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SyncQueue_Status",
                table: "SyncQueue",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Name",
                table: "Projects",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_CurrentConnectionProfileId",
                table: "Devices",
                column: "CurrentConnectionProfileId");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SyncQueue_CreatedAt",
                table: "SyncQueue");

            migrationBuilder.DropIndex(
                name: "IX_SyncQueue_Status",
                table: "SyncQueue");

            migrationBuilder.DropIndex(
                name: "IX_Projects_Name",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Devices_CurrentConnectionProfileId",
                table: "Devices");

            migrationBuilder.DropIndex(
                name: "IX_Devices_SerialNumber",
                table: "Devices");

            migrationBuilder.DropIndex(
                name: "IX_Devices_SoftwareId",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "CurrentConnectionProfileId",
                table: "Devices");
        }
    }
}

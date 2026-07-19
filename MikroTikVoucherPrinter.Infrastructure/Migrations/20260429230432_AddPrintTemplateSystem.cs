using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MikroTikVoucherPrinter.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPrintTemplateSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TemplateId",
                table: "Profiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TemplateConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    Columns = table.Column<int>(type: "INTEGER", nullable: false),
                    Rows = table.Column<int>(type: "INTEGER", nullable: false),
                    CardWidth = table.Column<float>(type: "REAL", nullable: false),
                    CardHeight = table.Column<float>(type: "REAL", nullable: false),
                    BackgroundImagePath = table.Column<string>(type: "TEXT", nullable: true),
                    ShowUsername = table.Column<bool>(type: "INTEGER", nullable: false),
                    UsernameX = table.Column<float>(type: "REAL", nullable: false),
                    UsernameY = table.Column<float>(type: "REAL", nullable: false),
                    ShowPassword = table.Column<bool>(type: "INTEGER", nullable: false),
                    PasswordX = table.Column<float>(type: "REAL", nullable: false),
                    PasswordY = table.Column<float>(type: "REAL", nullable: false),
                    ShowPrice = table.Column<bool>(type: "INTEGER", nullable: false),
                    PriceX = table.Column<float>(type: "REAL", nullable: false),
                    PriceY = table.Column<float>(type: "REAL", nullable: false),
                    ShowQr = table.Column<bool>(type: "INTEGER", nullable: false),
                    QrX = table.Column<float>(type: "REAL", nullable: false),
                    QrY = table.Column<float>(type: "REAL", nullable: false),
                    QrSize = table.Column<float>(type: "REAL", nullable: false),
                    FontSize = table.Column<float>(type: "REAL", nullable: false),
                    FontColorHex = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateConfigs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Profiles_TemplateId",
                table: "Profiles",
                column: "TemplateId");

            migrationBuilder.AddForeignKey(
                name: "FK_Profiles_TemplateConfigs_TemplateId",
                table: "Profiles",
                column: "TemplateId",
                principalTable: "TemplateConfigs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Profiles_TemplateConfigs_TemplateId",
                table: "Profiles");

            migrationBuilder.DropTable(
                name: "TemplateConfigs");

            migrationBuilder.DropIndex(
                name: "IX_Profiles_TemplateId",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "TemplateId",
                table: "Profiles");
        }
    }
}

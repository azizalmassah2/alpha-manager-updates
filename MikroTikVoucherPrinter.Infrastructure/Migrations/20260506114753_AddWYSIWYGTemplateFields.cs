using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MikroTikVoucherPrinter.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWYSIWYGTemplateFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float>(
                name: "BarcodeSize",
                table: "TemplateConfigs",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "BarcodeX",
                table: "TemplateConfigs",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "BarcodeY",
                table: "TemplateConfigs",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<string>(
                name: "FontFamily",
                table: "TemplateConfigs",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FrameColorHex",
                table: "TemplateConfigs",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<float>(
                name: "FrameSize",
                table: "TemplateConfigs",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<bool>(
                name: "IsBold",
                table: "TemplateConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsItalic",
                table: "TemplateConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LinkedProfileName",
                table: "TemplateConfigs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoImagePath",
                table: "TemplateConfigs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "MarginX",
                table: "TemplateConfigs",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "MarginY",
                table: "TemplateConfigs",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "PrintDateX",
                table: "TemplateConfigs",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "PrintDateY",
                table: "TemplateConfigs",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "SerialNumberX",
                table: "TemplateConfigs",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "SerialNumberY",
                table: "TemplateConfigs",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<bool>(
                name: "ShowBarcode",
                table: "TemplateConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShowPrintDate",
                table: "TemplateConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShowSerialNumber",
                table: "TemplateConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShowTime",
                table: "TemplateConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShowValidity",
                table: "TemplateConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<float>(
                name: "TimeX",
                table: "TemplateConfigs",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "TimeY",
                table: "TemplateConfigs",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "ValidityX",
                table: "TemplateConfigs",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "ValidityY",
                table: "TemplateConfigs",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BarcodeSize",
                table: "TemplateConfigs");

            migrationBuilder.DropColumn(
                name: "BarcodeX",
                table: "TemplateConfigs");

            migrationBuilder.DropColumn(
                name: "BarcodeY",
                table: "TemplateConfigs");

            migrationBuilder.DropColumn(
                name: "FontFamily",
                table: "TemplateConfigs");

            migrationBuilder.DropColumn(
                name: "FrameColorHex",
                table: "TemplateConfigs");

            migrationBuilder.DropColumn(
                name: "FrameSize",
                table: "TemplateConfigs");

            migrationBuilder.DropColumn(
                name: "IsBold",
                table: "TemplateConfigs");

            migrationBuilder.DropColumn(
                name: "IsItalic",
                table: "TemplateConfigs");

            migrationBuilder.DropColumn(
                name: "LinkedProfileName",
                table: "TemplateConfigs");

            migrationBuilder.DropColumn(
                name: "LogoImagePath",
                table: "TemplateConfigs");

            migrationBuilder.DropColumn(
                name: "MarginX",
                table: "TemplateConfigs");

            migrationBuilder.DropColumn(
                name: "MarginY",
                table: "TemplateConfigs");

            migrationBuilder.DropColumn(
                name: "PrintDateX",
                table: "TemplateConfigs");

            migrationBuilder.DropColumn(
                name: "PrintDateY",
                table: "TemplateConfigs");

            migrationBuilder.DropColumn(
                name: "SerialNumberX",
                table: "TemplateConfigs");

            migrationBuilder.DropColumn(
                name: "SerialNumberY",
                table: "TemplateConfigs");

            migrationBuilder.DropColumn(
                name: "ShowBarcode",
                table: "TemplateConfigs");

            migrationBuilder.DropColumn(
                name: "ShowPrintDate",
                table: "TemplateConfigs");

            migrationBuilder.DropColumn(
                name: "ShowSerialNumber",
                table: "TemplateConfigs");

            migrationBuilder.DropColumn(
                name: "ShowTime",
                table: "TemplateConfigs");

            migrationBuilder.DropColumn(
                name: "ShowValidity",
                table: "TemplateConfigs");

            migrationBuilder.DropColumn(
                name: "TimeX",
                table: "TemplateConfigs");

            migrationBuilder.DropColumn(
                name: "TimeY",
                table: "TemplateConfigs");

            migrationBuilder.DropColumn(
                name: "ValidityX",
                table: "TemplateConfigs");

            migrationBuilder.DropColumn(
                name: "ValidityY",
                table: "TemplateConfigs");
        }
    }
}

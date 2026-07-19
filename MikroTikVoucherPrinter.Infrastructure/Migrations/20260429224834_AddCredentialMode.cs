using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MikroTikVoucherPrinter.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCredentialMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CredentialMode",
                table: "Vouchers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CredentialMode",
                table: "Vouchers");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MikroTikVoucherPrinter.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVoucherCompositeIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_RouterId_IsDeleted_CreatedAt",
                table: "Vouchers",
                columns: new[] { "RouterId", "IsDeleted", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Vouchers_RouterId_IsDeleted_CreatedAt",
                table: "Vouchers");
        }
    }
}

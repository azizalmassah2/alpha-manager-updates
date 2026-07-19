using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MikroTikVoucherPrinter.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakeUsernameIndexComposite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Vouchers_Username\";");
            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Vouchers_Username_RouterId\" ON \"Vouchers\" (\"Username\", \"RouterId\");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Vouchers_Username_RouterId\";");
            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Vouchers_Username\" ON \"Vouchers\" (\"Username\");");
        }
    }
}

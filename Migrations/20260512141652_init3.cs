using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace projectweb.Migrations
{
    /// <inheritdoc />
    public partial class init3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "BlockName",
                table: "Blocks",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_Committees_CommitteeNumber",
                table: "Committees",
                column: "CommitteeNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Blocks_BlockName",
                table: "Blocks",
                column: "BlockName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Committees_CommitteeNumber",
                table: "Committees");

            migrationBuilder.DropIndex(
                name: "IX_Blocks_BlockName",
                table: "Blocks");

            migrationBuilder.AlterColumn<string>(
                name: "BlockName",
                table: "Blocks",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }
    }
}

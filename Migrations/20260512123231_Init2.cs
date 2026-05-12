using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace projectweb.Migrations
{
    /// <inheritdoc />
    public partial class Init2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StudentId",
                table: "ReportPersons",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportPersons_StudentId",
                table: "ReportPersons",
                column: "StudentId");

            migrationBuilder.AddForeignKey(
                name: "FK_ReportPersons_Students_StudentId",
                table: "ReportPersons",
                column: "StudentId",
                principalTable: "Students",
                principalColumn: "StudentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReportPersons_Students_StudentId",
                table: "ReportPersons");

            migrationBuilder.DropIndex(
                name: "IX_ReportPersons_StudentId",
                table: "ReportPersons");

            migrationBuilder.DropColumn(
                name: "StudentId",
                table: "ReportPersons");
        }
    }
}

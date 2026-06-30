using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace projectweb.Migrations
{
    /// <inheritdoc />
    public partial class AddAcademicYearToExamLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AcademicYear",
                table: "ExamLocations",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExamLocations_AcademicYear",
                table: "ExamLocations",
                column: "AcademicYear",
                unique: true,
                filter: "[Type] = 0 AND [AcademicYear] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExamLocations_AcademicYear",
                table: "ExamLocations");

            migrationBuilder.DropColumn(
                name: "AcademicYear",
                table: "ExamLocations");
        }
    }
}

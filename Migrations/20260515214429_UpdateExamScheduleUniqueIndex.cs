using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace projectweb.Migrations
{
    /// <inheritdoc />
    public partial class UpdateExamScheduleUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExamSchedules_ExamId",
                table: "ExamSchedules");

            migrationBuilder.DropIndex(
                name: "IX_ExamSchedules_ScheduledDate_CommitteeId",
                table: "ExamSchedules");

            migrationBuilder.CreateIndex(
                name: "IX_ExamSchedules_ExamId_CommitteeId",
                table: "ExamSchedules",
                columns: new[] { "ExamId", "CommitteeId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExamSchedules_ExamId_CommitteeId",
                table: "ExamSchedules");

            migrationBuilder.CreateIndex(
                name: "IX_ExamSchedules_ExamId",
                table: "ExamSchedules",
                column: "ExamId");

            migrationBuilder.CreateIndex(
                name: "IX_ExamSchedules_ScheduledDate_CommitteeId",
                table: "ExamSchedules",
                columns: new[] { "ScheduledDate", "CommitteeId" },
                unique: true);
        }
    }
}

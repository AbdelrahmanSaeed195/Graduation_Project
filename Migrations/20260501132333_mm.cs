using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace projectweb.Migrations
{
    /// <inheritdoc />
    public partial class mm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Students_Committees_CommitteeId",
                table: "Students");

            migrationBuilder.RenameColumn(
                name: "CommitteeId",
                table: "Students",
                newName: "CommitteeID");

            migrationBuilder.RenameIndex(
                name: "IX_Students_CommitteeId",
                table: "Students",
                newName: "IX_Students_CommitteeID");

            migrationBuilder.AlterColumn<string>(
                name: "NationalId",
                table: "Students",
                type: "nvarchar(14)",
                maxLength: 14,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<int>(
                name: "ExamScheduleId",
                table: "Students",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_ExamScheduleId",
                table: "Students",
                column: "ExamScheduleId");

            migrationBuilder.AddForeignKey(
                name: "FK_Students_Committees_CommitteeID",
                table: "Students",
                column: "CommitteeID",
                principalTable: "Committees",
                principalColumn: "CommitteeID");

            migrationBuilder.AddForeignKey(
                name: "FK_Students_ExamSchedules_ExamScheduleId",
                table: "Students",
                column: "ExamScheduleId",
                principalTable: "ExamSchedules",
                principalColumn: "ExamScheduleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Students_Committees_CommitteeID",
                table: "Students");

            migrationBuilder.DropForeignKey(
                name: "FK_Students_ExamSchedules_ExamScheduleId",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Students_ExamScheduleId",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "ExamScheduleId",
                table: "Students");

            migrationBuilder.RenameColumn(
                name: "CommitteeID",
                table: "Students",
                newName: "CommitteeId");

            migrationBuilder.RenameIndex(
                name: "IX_Students_CommitteeID",
                table: "Students",
                newName: "IX_Students_CommitteeId");

            migrationBuilder.AlterColumn<string>(
                name: "NationalId",
                table: "Students",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(14)",
                oldMaxLength: 14);

            migrationBuilder.AddForeignKey(
                name: "FK_Students_Committees_CommitteeId",
                table: "Students",
                column: "CommitteeId",
                principalTable: "Committees",
                principalColumn: "CommitteeID");
        }
    }
}

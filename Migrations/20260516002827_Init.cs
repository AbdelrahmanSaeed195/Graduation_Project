using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace projectweb.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReportPersons_Persons_PersonID",
                table: "ReportPersons");

            migrationBuilder.DropForeignKey(
                name: "FK_ReportPersons_Reports_ReportID",
                table: "ReportPersons");

            migrationBuilder.DropForeignKey(
                name: "FK_ReportPersons_Roles_RoleID",
                table: "ReportPersons");

            migrationBuilder.DropForeignKey(
                name: "FK_Reports_ExamSchedules_ScheduleID",
                table: "Reports");

            migrationBuilder.RenameColumn(
                name: "ScheduleID",
                table: "Reports",
                newName: "ScheduleId");

            migrationBuilder.RenameColumn(
                name: "ReportID",
                table: "Reports",
                newName: "ReportId");

            migrationBuilder.RenameIndex(
                name: "IX_Reports_ScheduleID",
                table: "Reports",
                newName: "IX_Reports_ScheduleId");

            migrationBuilder.RenameColumn(
                name: "RoleID",
                table: "ReportPersons",
                newName: "RoleId");

            migrationBuilder.RenameColumn(
                name: "PersonID",
                table: "ReportPersons",
                newName: "PersonId");

            migrationBuilder.RenameColumn(
                name: "ReportID",
                table: "ReportPersons",
                newName: "ReportId");

            migrationBuilder.RenameIndex(
                name: "IX_ReportPersons_RoleID",
                table: "ReportPersons",
                newName: "IX_ReportPersons_RoleId");

            migrationBuilder.RenameIndex(
                name: "IX_ReportPersons_PersonID",
                table: "ReportPersons",
                newName: "IX_ReportPersons_PersonId");

            migrationBuilder.AddForeignKey(
                name: "FK_ReportPersons_Persons_PersonId",
                table: "ReportPersons",
                column: "PersonId",
                principalTable: "Persons",
                principalColumn: "PersonId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReportPersons_Reports_ReportId",
                table: "ReportPersons",
                column: "ReportId",
                principalTable: "Reports",
                principalColumn: "ReportId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReportPersons_Roles_RoleId",
                table: "ReportPersons",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "RoleID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Reports_ExamSchedules_ScheduleId",
                table: "Reports",
                column: "ScheduleId",
                principalTable: "ExamSchedules",
                principalColumn: "ExamScheduleId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReportPersons_Persons_PersonId",
                table: "ReportPersons");

            migrationBuilder.DropForeignKey(
                name: "FK_ReportPersons_Reports_ReportId",
                table: "ReportPersons");

            migrationBuilder.DropForeignKey(
                name: "FK_ReportPersons_Roles_RoleId",
                table: "ReportPersons");

            migrationBuilder.DropForeignKey(
                name: "FK_Reports_ExamSchedules_ScheduleId",
                table: "Reports");

            migrationBuilder.RenameColumn(
                name: "ScheduleId",
                table: "Reports",
                newName: "ScheduleID");

            migrationBuilder.RenameColumn(
                name: "ReportId",
                table: "Reports",
                newName: "ReportID");

            migrationBuilder.RenameIndex(
                name: "IX_Reports_ScheduleId",
                table: "Reports",
                newName: "IX_Reports_ScheduleID");

            migrationBuilder.RenameColumn(
                name: "RoleId",
                table: "ReportPersons",
                newName: "RoleID");

            migrationBuilder.RenameColumn(
                name: "PersonId",
                table: "ReportPersons",
                newName: "PersonID");

            migrationBuilder.RenameColumn(
                name: "ReportId",
                table: "ReportPersons",
                newName: "ReportID");

            migrationBuilder.RenameIndex(
                name: "IX_ReportPersons_RoleId",
                table: "ReportPersons",
                newName: "IX_ReportPersons_RoleID");

            migrationBuilder.RenameIndex(
                name: "IX_ReportPersons_PersonId",
                table: "ReportPersons",
                newName: "IX_ReportPersons_PersonID");

            migrationBuilder.AddForeignKey(
                name: "FK_ReportPersons_Persons_PersonID",
                table: "ReportPersons",
                column: "PersonID",
                principalTable: "Persons",
                principalColumn: "PersonId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReportPersons_Reports_ReportID",
                table: "ReportPersons",
                column: "ReportID",
                principalTable: "Reports",
                principalColumn: "ReportID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReportPersons_Roles_RoleID",
                table: "ReportPersons",
                column: "RoleID",
                principalTable: "Roles",
                principalColumn: "RoleID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Reports_ExamSchedules_ScheduleID",
                table: "Reports",
                column: "ScheduleID",
                principalTable: "ExamSchedules",
                principalColumn: "ExamScheduleId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

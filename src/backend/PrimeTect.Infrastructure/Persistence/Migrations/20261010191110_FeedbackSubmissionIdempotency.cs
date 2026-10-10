using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrimeTect.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FeedbackSubmissionIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SubmissionKey",
                table: "room_meter_issues",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_room_meter_issues_OrganizationId_ReporterUserId_SubmissionK~",
                table: "room_meter_issues",
                columns: new[] { "OrganizationId", "ReporterUserId", "SubmissionKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_room_meter_issues_OrganizationId_ReporterUserId_SubmissionK~",
                table: "room_meter_issues");

            migrationBuilder.DropColumn(
                name: "SubmissionKey",
                table: "room_meter_issues");
        }
    }
}

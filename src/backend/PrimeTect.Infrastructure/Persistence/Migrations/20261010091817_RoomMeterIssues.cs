using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrimeTect.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RoomMeterIssues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "room_meter_issues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReporterUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IssueType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ObservedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    LastChangedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastChangedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolutionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_room_meter_issues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_room_meter_issues_properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_room_meter_issues_units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "room_meter_issue_audit_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomMeterIssueId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BeforeJson = table.Column<string>(type: "text", nullable: true),
                    AfterJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_room_meter_issue_audit_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_room_meter_issue_audit_entries_room_meter_issues_RoomMeterI~",
                        column: x => x.RoomMeterIssueId,
                        principalTable: "room_meter_issues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_room_meter_issue_audit_entries_OrganizationId_RoomMeterIssu~",
                table: "room_meter_issue_audit_entries",
                columns: new[] { "OrganizationId", "RoomMeterIssueId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_room_meter_issue_audit_entries_RoomMeterIssueId",
                table: "room_meter_issue_audit_entries",
                column: "RoomMeterIssueId");

            migrationBuilder.CreateIndex(
                name: "IX_room_meter_issues_OrganizationId_CreatedAtUtc",
                table: "room_meter_issues",
                columns: new[] { "OrganizationId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_room_meter_issues_OrganizationId_PropertyId_UnitId_Status",
                table: "room_meter_issues",
                columns: new[] { "OrganizationId", "PropertyId", "UnitId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_room_meter_issues_PropertyId",
                table: "room_meter_issues",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_room_meter_issues_UnitId",
                table: "room_meter_issues",
                column: "UnitId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "room_meter_issue_audit_entries");

            migrationBuilder.DropTable(
                name: "room_meter_issues");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrimeTect.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ManualCollectionReporting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "collection_reports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BillingUnitBillId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccupancyAssignmentIdSnapshot = table.Column<Guid>(type: "uuid", nullable: true),
                    UnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitNameSnapshot = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FloorNameSnapshot = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TenantIdSnapshot = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TenantPhoneSnapshot = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BillingMonth = table.Column<DateOnly>(type: "date", nullable: false),
                    BilledAmountSnapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ReporterUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AmountReceived = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ReceivedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PaymentMethod = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ReferenceNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SubmissionKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    LastChangedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastChangedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReturnReason = table.Column<string>(type: "text", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_collection_reports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_collection_reports_billing_unit_bills_BillingUnitBillId",
                        column: x => x.BillingUnitBillId,
                        principalTable: "billing_unit_bills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "collection_reversals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CollectionReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    LastChangedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastChangedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReturnReason = table.Column<string>(type: "text", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_collection_reversals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_collection_reversals_collection_reports_CollectionReportId",
                        column: x => x.CollectionReportId,
                        principalTable: "collection_reports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "collection_audit_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CollectionReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    CollectionReversalId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BeforeJson = table.Column<string>(type: "text", nullable: true),
                    AfterJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_collection_audit_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_collection_audit_entries_collection_reports_CollectionRepor~",
                        column: x => x.CollectionReportId,
                        principalTable: "collection_reports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_collection_audit_entries_collection_reversals_CollectionRev~",
                        column: x => x.CollectionReversalId,
                        principalTable: "collection_reversals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_collection_audit_entries_CollectionReportId",
                table: "collection_audit_entries",
                column: "CollectionReportId");

            migrationBuilder.CreateIndex(
                name: "IX_collection_audit_entries_CollectionReversalId",
                table: "collection_audit_entries",
                column: "CollectionReversalId");

            migrationBuilder.CreateIndex(
                name: "IX_collection_audit_entries_OrganizationId_CollectionReportId_~",
                table: "collection_audit_entries",
                columns: new[] { "OrganizationId", "CollectionReportId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_collection_reports_BillingUnitBillId",
                table: "collection_reports",
                column: "BillingUnitBillId");

            migrationBuilder.CreateIndex(
                name: "IX_collection_reports_OrganizationId_BillingUnitBillId_Status",
                table: "collection_reports",
                columns: new[] { "OrganizationId", "BillingUnitBillId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_collection_reports_OrganizationId_SubmissionKey",
                table: "collection_reports",
                columns: new[] { "OrganizationId", "SubmissionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_collection_reversals_CollectionReportId",
                table: "collection_reversals",
                column: "CollectionReportId");

            migrationBuilder.CreateIndex(
                name: "IX_collection_reversals_OrganizationId_CollectionReportId_Stat~",
                table: "collection_reversals",
                columns: new[] { "OrganizationId", "CollectionReportId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "collection_audit_entries");

            migrationBuilder.DropTable(
                name: "collection_reversals");

            migrationBuilder.DropTable(
                name: "collection_reports");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrimeTect.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MonthlyBillingMilestone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "billing_cycles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    BillingMonth = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    LastChangedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastChangedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SubmittedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReturnedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReturnedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReturnReason = table.Column<string>(type: "text", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ApprovedTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_billing_cycles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_billing_cycles_properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "billing_audit_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BillingCycleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BeforeJson = table.Column<string>(type: "text", nullable: true),
                    AfterJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_billing_audit_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_billing_audit_entries_billing_cycles_BillingCycleId",
                        column: x => x.BillingCycleId,
                        principalTable: "billing_cycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "billing_unit_bills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BillingCycleId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitNameSnapshot = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OccupancyAssignmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TenantPhoneSnapshot = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    OccupancyStartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    OccupancyEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    BlockingReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ElectricityWarning = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_billing_unit_bills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_billing_unit_bills_billing_cycles_BillingCycleId",
                        column: x => x.BillingCycleId,
                        principalTable: "billing_cycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_billing_unit_bills_occupancy_assignments_OccupancyAssignmen~",
                        column: x => x.OccupancyAssignmentId,
                        principalTable: "occupancy_assignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_billing_unit_bills_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_billing_unit_bills_units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "billing_charge_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BillingUnitBillId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChargeType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ChargeNameSnapshot = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SettingModeSnapshot = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsMetered = table.Column<bool>(type: "boolean", nullable: false),
                    RateSnapshot = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    PreviousReading = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    CurrentReading = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    ConsumptionUnits = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    PreviousReadingSource = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_billing_charge_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_billing_charge_lines_billing_unit_bills_BillingUnitBillId",
                        column: x => x.BillingUnitBillId,
                        principalTable: "billing_unit_bills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_billing_audit_entries_BillingCycleId",
                table: "billing_audit_entries",
                column: "BillingCycleId");

            migrationBuilder.CreateIndex(
                name: "IX_billing_audit_entries_OrganizationId_BillingCycleId_Occurre~",
                table: "billing_audit_entries",
                columns: new[] { "OrganizationId", "BillingCycleId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_billing_charge_lines_BillingUnitBillId_ChargeType",
                table: "billing_charge_lines",
                columns: new[] { "BillingUnitBillId", "ChargeType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_billing_cycles_OrganizationId_PropertyId_BillingMonth",
                table: "billing_cycles",
                columns: new[] { "OrganizationId", "PropertyId", "BillingMonth" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_billing_cycles_PropertyId",
                table: "billing_cycles",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_billing_unit_bills_BillingCycleId_UnitId",
                table: "billing_unit_bills",
                columns: new[] { "BillingCycleId", "UnitId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_billing_unit_bills_OccupancyAssignmentId",
                table: "billing_unit_bills",
                column: "OccupancyAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_billing_unit_bills_TenantId",
                table: "billing_unit_bills",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_billing_unit_bills_UnitId",
                table: "billing_unit_bills",
                column: "UnitId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "billing_audit_entries");

            migrationBuilder.DropTable(
                name: "billing_charge_lines");

            migrationBuilder.DropTable(
                name: "billing_unit_bills");

            migrationBuilder.DropTable(
                name: "billing_cycles");
        }
    }
}

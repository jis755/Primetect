using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrimeTect.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UtilityAccountsMainMetersAndSubmeters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MeterInstallationId",
                table: "meter_photos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MeterInstallationId",
                table: "billing_charge_lines",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "billing_meter_segments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BillingChargeLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    MeterInstallationId = table.Column<Guid>(type: "uuid", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    SegmentStartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    SegmentEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PreviousReading = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    PreviousReadingSource = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PreviousReadingDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CurrentReading = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    CurrentReadingDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ConsumptionUnits = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    FinalReadingUnavailable = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_billing_meter_segments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_billing_meter_segments_billing_charge_lines_BillingChargeLi~",
                        column: x => x.BillingChargeLineId,
                        principalTable: "billing_charge_lines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "meter_installation_audit_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstallationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousInstallationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReplacementDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BeforeJson = table.Column<string>(type: "text", nullable: true),
                    AfterJson = table.Column<string>(type: "text", nullable: true),
                    SubmissionKey = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meter_installation_audit_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_meter_installation_audit_entries_properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_meter_installation_audit_entries_units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "utility_accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CustomerNumber = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    DisplayLabel = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_utility_accounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_utility_accounts_properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "utility_main_meters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    UtilityAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    MeterNumber = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    DisplayLabel = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    InstalledOn = table.Column<DateOnly>(type: "date", nullable: true),
                    EndedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_utility_main_meters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_utility_main_meters_properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_utility_main_meters_utility_accounts_UtilityAccountId",
                        column: x => x.UtilityAccountId,
                        principalTable: "utility_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "meter_installations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    MainMeterId = table.Column<Guid>(type: "uuid", nullable: true),
                    SerialNumber = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    DisplayLabel = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    InstallationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    OpeningReading = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    OpeningReadingDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ReplacedInstallationId = table.Column<Guid>(type: "uuid", nullable: true),
                    IdentityKnown = table.Column<bool>(type: "boolean", nullable: false),
                    EndedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    EndedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    EndReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    FinalReading = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    FinalReadingUnavailable = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meter_installations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_meter_installations_properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_meter_installations_units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_meter_installations_utility_main_meters_MainMeterId",
                        column: x => x.MainMeterId,
                        principalTable: "utility_main_meters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_billing_meter_segments_BillingChargeLineId",
                table: "billing_meter_segments",
                column: "BillingChargeLineId");

            migrationBuilder.CreateIndex(
                name: "IX_billing_meter_segments_OrganizationId_BillingChargeLineId",
                table: "billing_meter_segments",
                columns: new[] { "OrganizationId", "BillingChargeLineId" });

            migrationBuilder.CreateIndex(
                name: "IX_meter_installation_audit_entries_OrganizationId_Installatio~",
                table: "meter_installation_audit_entries",
                columns: new[] { "OrganizationId", "InstallationId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_meter_installation_audit_entries_OrganizationId_SubmissionK~",
                table: "meter_installation_audit_entries",
                columns: new[] { "OrganizationId", "SubmissionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_meter_installation_audit_entries_PropertyId",
                table: "meter_installation_audit_entries",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_meter_installation_audit_entries_UnitId",
                table: "meter_installation_audit_entries",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_meter_installations_active_unit",
                table: "meter_installations",
                column: "UnitId",
                unique: true,
                filter: "\"EndedOn\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_meter_installations_MainMeterId",
                table: "meter_installations",
                column: "MainMeterId");

            migrationBuilder.CreateIndex(
                name: "IX_meter_installations_OrganizationId_PropertyId_UnitId",
                table: "meter_installations",
                columns: new[] { "OrganizationId", "PropertyId", "UnitId" });

            migrationBuilder.CreateIndex(
                name: "IX_meter_installations_PropertyId",
                table: "meter_installations",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_utility_accounts_OrganizationId_PropertyId",
                table: "utility_accounts",
                columns: new[] { "OrganizationId", "PropertyId" });

            migrationBuilder.CreateIndex(
                name: "IX_utility_accounts_PropertyId",
                table: "utility_accounts",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_utility_main_meters_OrganizationId_PropertyId",
                table: "utility_main_meters",
                columns: new[] { "OrganizationId", "PropertyId" });

            migrationBuilder.CreateIndex(
                name: "IX_utility_main_meters_OrganizationId_UtilityAccountId",
                table: "utility_main_meters",
                columns: new[] { "OrganizationId", "UtilityAccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_utility_main_meters_PropertyId",
                table: "utility_main_meters",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_utility_main_meters_UtilityAccountId",
                table: "utility_main_meters",
                column: "UtilityAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "billing_meter_segments");

            migrationBuilder.DropTable(
                name: "meter_installation_audit_entries");

            migrationBuilder.DropTable(
                name: "meter_installations");

            migrationBuilder.DropTable(
                name: "utility_main_meters");

            migrationBuilder.DropTable(
                name: "utility_accounts");

            migrationBuilder.DropColumn(
                name: "MeterInstallationId",
                table: "meter_photos");

            migrationBuilder.DropColumn(
                name: "MeterInstallationId",
                table: "billing_charge_lines");
        }
    }
}

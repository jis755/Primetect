using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrimeTect.Infrastructure.Persistence.Migrations;

public partial class QrMeterPhotosAndLocations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "Latitude",
            table: "properties",
            type: "numeric(9,6)",
            precision: 9,
            scale: 6,
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "Longitude",
            table: "properties",
            type: "numeric(9,6)",
            precision: 9,
            scale: 6,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "MeterPhotoId",
            table: "billing_charge_lines",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "meter_qr_links",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                UnitId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                Placement = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                RevokedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_meter_qr_links", x => x.Id);
                table.ForeignKey(
                    name: "FK_meter_qr_links_properties_PropertyId",
                    column: x => x.PropertyId,
                    principalTable: "properties",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_meter_qr_links_units_UnitId",
                    column: x => x.UnitId,
                    principalTable: "units",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "meter_photos",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                BillingCycleId = table.Column<Guid>(type: "uuid", nullable: false),
                BillingUnitBillId = table.Column<Guid>(type: "uuid", nullable: false),
                UnitId = table.Column<Guid>(type: "uuid", nullable: false),
                UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                StorageKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                ContentType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                FileExtension = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                ByteLength = table.Column<long>(type: "bigint", nullable: false),
                Width = table.Column<int>(type: "integer", nullable: false),
                Height = table.Column<int>(type: "integer", nullable: false),
                OriginalFileName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                OcrOutcome = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                OcrProviderVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                OcrSuggestion = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                OcrConfidence = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                UploadedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                AttachedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                ConfirmedReading = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                ConfirmedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_meter_photos", x => x.Id);
                table.ForeignKey(
                    name: "FK_meter_photos_billing_cycles_BillingCycleId",
                    column: x => x.BillingCycleId,
                    principalTable: "billing_cycles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_meter_photos_billing_unit_bills_BillingUnitBillId",
                    column: x => x.BillingUnitBillId,
                    principalTable: "billing_unit_bills",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_meter_photos_properties_PropertyId",
                    column: x => x.PropertyId,
                    principalTable: "properties",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_meter_photos_units_UnitId",
                    column: x => x.UnitId,
                    principalTable: "units",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_billing_charge_lines_MeterPhotoId", "billing_charge_lines", "MeterPhotoId");
        migrationBuilder.CreateIndex("IX_meter_qr_links_TokenHash", "meter_qr_links", "TokenHash", unique: true);
        migrationBuilder.CreateIndex("IX_meter_qr_links_OrganizationId_PropertyId_UnitId_RevokedAtUtc", "meter_qr_links", new[] { "OrganizationId", "PropertyId", "UnitId", "RevokedAtUtc" });
        migrationBuilder.CreateIndex("IX_meter_qr_links_PropertyId", "meter_qr_links", "PropertyId");
        migrationBuilder.CreateIndex("IX_meter_qr_links_UnitId", "meter_qr_links", "UnitId");
        migrationBuilder.CreateIndex("IX_meter_photos_OrganizationId_BillingCycleId_UnitId_Status", "meter_photos", new[] { "OrganizationId", "BillingCycleId", "UnitId", "Status" });
        migrationBuilder.CreateIndex("IX_meter_photos_OrganizationId_StorageKey", "meter_photos", new[] { "OrganizationId", "StorageKey" }, unique: true);
        migrationBuilder.CreateIndex("IX_meter_photos_BillingCycleId", "meter_photos", "BillingCycleId");
        migrationBuilder.CreateIndex("IX_meter_photos_BillingUnitBillId", "meter_photos", "BillingUnitBillId");
        migrationBuilder.CreateIndex("IX_meter_photos_PropertyId", "meter_photos", "PropertyId");
        migrationBuilder.CreateIndex("IX_meter_photos_UnitId", "meter_photos", "UnitId");

        migrationBuilder.AddForeignKey(
            name: "FK_billing_charge_lines_meter_photos_MeterPhotoId",
            table: "billing_charge_lines",
            column: "MeterPhotoId",
            principalTable: "meter_photos",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey("FK_billing_charge_lines_meter_photos_MeterPhotoId", "billing_charge_lines");
        migrationBuilder.DropTable("meter_photos");
        migrationBuilder.DropTable("meter_qr_links");
        migrationBuilder.DropIndex("IX_billing_charge_lines_MeterPhotoId", "billing_charge_lines");
        migrationBuilder.DropColumn("MeterPhotoId", "billing_charge_lines");
        migrationBuilder.DropColumn("Latitude", "properties");
        migrationBuilder.DropColumn("Longitude", "properties");
    }
}

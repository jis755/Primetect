using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrimeTect.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConfigurableElectricityWarningsAndReadingDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WarningMode",
                table: "charge_configurations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Custom");

            migrationBuilder.AddColumn<decimal>(
                name: "WarningThreshold",
                table: "charge_configurations",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "CurrentReadingDate",
                table: "billing_charge_lines",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PreviousReadingDate",
                table: "billing_charge_lines",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WarningEnabled",
                table: "billing_charge_lines",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WarningThreshold",
                table: "billing_charge_lines",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.Sql("UPDATE charge_configurations SET \"WarningMode\" = CASE WHEN \"UnitId\" IS NULL THEN 'Custom' ELSE 'Inherited' END, \"WarningThreshold\" = CASE WHEN \"ChargeType\" = 'Electricity' AND \"UnitId\" IS NULL THEN 200.00 ELSE NULL END;");

            migrationBuilder.Sql("UPDATE billing_charge_lines SET \"WarningEnabled\" = CASE WHEN \"ChargeType\" = 'Electricity' THEN TRUE ELSE FALSE END, \"WarningThreshold\" = CASE WHEN \"ChargeType\" = 'Electricity' THEN 200.00 ELSE NULL END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WarningMode",
                table: "charge_configurations");

            migrationBuilder.DropColumn(
                name: "WarningThreshold",
                table: "charge_configurations");

            migrationBuilder.DropColumn(
                name: "CurrentReadingDate",
                table: "billing_charge_lines");

            migrationBuilder.DropColumn(
                name: "PreviousReadingDate",
                table: "billing_charge_lines");

            migrationBuilder.DropColumn(
                name: "WarningEnabled",
                table: "billing_charge_lines");

            migrationBuilder.DropColumn(
                name: "WarningThreshold",
                table: "billing_charge_lines");
        }
    }
}

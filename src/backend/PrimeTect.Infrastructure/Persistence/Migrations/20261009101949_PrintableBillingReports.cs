using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrimeTect.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PrintableBillingReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FloorNameSnapshot",
                table: "billing_unit_bills",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PropertyAddressSnapshot",
                table: "billing_cycles",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PropertyNameSnapshot",
                table: "billing_cycles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FloorNameSnapshot",
                table: "billing_unit_bills");

            migrationBuilder.DropColumn(
                name: "PropertyAddressSnapshot",
                table: "billing_cycles");

            migrationBuilder.DropColumn(
                name: "PropertyNameSnapshot",
                table: "billing_cycles");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioThermometer.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMeterReadDirectionIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "idx_meter_reads_connection_direction",
                table: "meter_reads",
                columns: new[] { "connection_id", "direction" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_meter_reads_connection_direction",
                table: "meter_reads");
        }
    }
}

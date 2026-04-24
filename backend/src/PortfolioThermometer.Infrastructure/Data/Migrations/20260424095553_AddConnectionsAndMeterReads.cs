using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioThermometer.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConnectionsAndMeterReads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "connections",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    crm_external_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ean = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    product_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    delivery_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    connection_type_id = table.Column<int>(type: "integer", nullable: true),
                    imported_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_connections", x => x.id);
                    table.ForeignKey(
                        name: "FK_connections_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "meter_reads",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    crm_external_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    connection_id = table.Column<Guid>(type: "uuid", nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    consumption = table.Column<decimal>(type: "numeric(15,4)", precision: 15, scale: 4, nullable: true),
                    unit = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    usage_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    direction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    quality = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    imported_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meter_reads", x => x.id);
                    table.ForeignKey(
                        name: "FK_meter_reads_connections_connection_id",
                        column: x => x.connection_id,
                        principalTable: "connections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "idx_connections_customer_id",
                table: "connections",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "idx_connections_ean",
                table: "connections",
                column: "ean");

            migrationBuilder.CreateIndex(
                name: "uq_connections_crm_id",
                table: "connections",
                column: "crm_external_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_meter_reads_connection_id",
                table: "meter_reads",
                column: "connection_id");

            migrationBuilder.CreateIndex(
                name: "idx_meter_reads_start_date",
                table: "meter_reads",
                column: "start_date");

            migrationBuilder.CreateIndex(
                name: "uq_meter_reads_crm_id",
                table: "meter_reads",
                column: "crm_external_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "meter_reads");

            migrationBuilder.DropTable(
                name: "connections");
        }
    }
}

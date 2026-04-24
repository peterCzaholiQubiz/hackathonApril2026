using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioThermometer.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "customers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    crm_external_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    company_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    segment = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    account_manager = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    onboarding_date = table.Column<DateOnly>(type: "date", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    imported_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "portfolio_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    total_customers = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    green_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    yellow_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    red_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    green_pct = table.Column<decimal>(type: "numeric(5,2)", nullable: false, defaultValue: 0m),
                    yellow_pct = table.Column<decimal>(type: "numeric(5,2)", nullable: false, defaultValue: 0m),
                    red_pct = table.Column<decimal>(type: "numeric(5,2)", nullable: false, defaultValue: 0m),
                    avg_churn_score = table.Column<decimal>(type: "numeric(5,2)", nullable: false, defaultValue: 0m),
                    avg_payment_score = table.Column<decimal>(type: "numeric(5,2)", nullable: false, defaultValue: 0m),
                    avg_margin_score = table.Column<decimal>(type: "numeric(5,2)", nullable: false, defaultValue: 0m),
                    segment_breakdown = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_portfolio_snapshots", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "complaints",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    crm_external_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_date = table.Column<DateOnly>(type: "date", nullable: true),
                    resolved_date = table.Column<DateOnly>(type: "date", nullable: true),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    imported_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_complaints", x => x.id);
                    table.ForeignKey(
                        name: "FK_complaints_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "contracts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    crm_external_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    contract_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    monthly_value = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "EUR"),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    auto_renew = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    imported_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contracts", x => x.id);
                    table.ForeignKey(
                        name: "FK_contracts_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "interactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    crm_external_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    interaction_date = table.Column<DateOnly>(type: "date", nullable: true),
                    channel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    direction = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    summary = table.Column<string>(type: "text", nullable: true),
                    sentiment = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    imported_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_interactions", x => x.id);
                    table.ForeignKey(
                        name: "FK_interactions_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "invoices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    crm_external_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    invoice_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    issued_date = table.Column<DateOnly>(type: "date", nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "EUR"),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    imported_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoices", x => x.id);
                    table.ForeignKey(
                        name: "FK_invoices_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "risk_scores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    churn_score = table.Column<int>(type: "integer", nullable: false),
                    payment_score = table.Column<int>(type: "integer", nullable: false),
                    margin_score = table.Column<int>(type: "integer", nullable: false),
                    overall_score = table.Column<int>(type: "integer", nullable: false),
                    heat_level = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    scored_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_risk_scores", x => x.id);
                    table.CheckConstraint("chk_churn_score_range", "churn_score BETWEEN 0 AND 100");
                    table.CheckConstraint("chk_heat_level_values", "heat_level IN ('green', 'yellow', 'red')");
                    table.CheckConstraint("chk_margin_score_range", "margin_score BETWEEN 0 AND 100");
                    table.CheckConstraint("chk_overall_score_range", "overall_score BETWEEN 0 AND 100");
                    table.CheckConstraint("chk_payment_score_range", "payment_score BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_risk_scores_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_risk_scores_portfolio_snapshots_snapshot_id",
                        column: x => x.snapshot_id,
                        principalTable: "portfolio_snapshots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    crm_external_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payment_date = table.Column<DateOnly>(type: "date", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    days_late = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    imported_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.id);
                    table.ForeignKey(
                        name: "FK_payments_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_payments_invoices_invoice_id",
                        column: x => x.invoice_id,
                        principalTable: "invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "risk_explanations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    risk_score_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    risk_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    explanation = table.Column<string>(type: "text", nullable: false),
                    confidence = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    generated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    model_used = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "claude-sonnet-4-6")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_risk_explanations", x => x.id);
                    table.CheckConstraint("chk_confidence_values", "confidence IN ('high', 'medium', 'low')");
                    table.CheckConstraint("chk_risk_type_values", "risk_type IN ('churn', 'payment', 'margin', 'overall')");
                    table.ForeignKey(
                        name: "FK_risk_explanations_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_risk_explanations_risk_scores_risk_score_id",
                        column: x => x.risk_score_id,
                        principalTable: "risk_scores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "suggested_actions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    risk_score_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    priority = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    generated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_suggested_actions", x => x.id);
                    table.CheckConstraint("chk_action_type_values", "action_type IN ('outreach', 'discount', 'review', 'escalate', 'upsell')");
                    table.CheckConstraint("chk_priority_values", "priority IN ('high', 'medium', 'low')");
                    table.ForeignKey(
                        name: "FK_suggested_actions_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_suggested_actions_risk_scores_risk_score_id",
                        column: x => x.risk_score_id,
                        principalTable: "risk_scores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_complaints_created_date",
                table: "complaints",
                column: "created_date");

            migrationBuilder.CreateIndex(
                name: "idx_complaints_customer_id",
                table: "complaints",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "idx_complaints_resolved_date",
                table: "complaints",
                column: "resolved_date");

            migrationBuilder.CreateIndex(
                name: "idx_complaints_severity",
                table: "complaints",
                column: "severity");

            migrationBuilder.CreateIndex(
                name: "uq_complaints_crm_id",
                table: "complaints",
                column: "crm_external_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_contracts_customer_id",
                table: "contracts",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "idx_contracts_end_date",
                table: "contracts",
                column: "end_date");

            migrationBuilder.CreateIndex(
                name: "idx_contracts_status",
                table: "contracts",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "uq_contracts_crm_id",
                table: "contracts",
                column: "crm_external_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_customers_is_active",
                table: "customers",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "idx_customers_segment",
                table: "customers",
                column: "segment");

            migrationBuilder.CreateIndex(
                name: "uq_customers_crm_id",
                table: "customers",
                column: "crm_external_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_interactions_customer_id",
                table: "interactions",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "idx_interactions_direction",
                table: "interactions",
                column: "direction");

            migrationBuilder.CreateIndex(
                name: "idx_interactions_interaction_date",
                table: "interactions",
                column: "interaction_date");

            migrationBuilder.CreateIndex(
                name: "idx_interactions_sentiment",
                table: "interactions",
                column: "sentiment");

            migrationBuilder.CreateIndex(
                name: "uq_interactions_crm_id",
                table: "interactions",
                column: "crm_external_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_invoices_customer_id",
                table: "invoices",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "idx_invoices_due_date",
                table: "invoices",
                column: "due_date");

            migrationBuilder.CreateIndex(
                name: "idx_invoices_status",
                table: "invoices",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "uq_invoices_crm_id",
                table: "invoices",
                column: "crm_external_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_payments_customer_id",
                table: "payments",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "idx_payments_invoice_id",
                table: "payments",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "idx_payments_payment_date",
                table: "payments",
                column: "payment_date");

            migrationBuilder.CreateIndex(
                name: "uq_payments_crm_id",
                table: "payments",
                column: "crm_external_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_portfolio_snapshots_created_at",
                table: "portfolio_snapshots",
                column: "created_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "idx_risk_explanations_customer_id",
                table: "risk_explanations",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "idx_risk_explanations_risk_score_id",
                table: "risk_explanations",
                column: "risk_score_id");

            migrationBuilder.CreateIndex(
                name: "idx_risk_explanations_risk_type",
                table: "risk_explanations",
                column: "risk_type");

            migrationBuilder.CreateIndex(
                name: "idx_risk_scores_customer_id",
                table: "risk_scores",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "idx_risk_scores_heat_level",
                table: "risk_scores",
                column: "heat_level");

            migrationBuilder.CreateIndex(
                name: "idx_risk_scores_overall_score",
                table: "risk_scores",
                column: "overall_score",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "idx_risk_scores_snapshot_id",
                table: "risk_scores",
                column: "snapshot_id");

            migrationBuilder.CreateIndex(
                name: "idx_suggested_actions_customer_id",
                table: "suggested_actions",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "idx_suggested_actions_priority",
                table: "suggested_actions",
                column: "priority");

            migrationBuilder.CreateIndex(
                name: "idx_suggested_actions_risk_score_id",
                table: "suggested_actions",
                column: "risk_score_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "complaints");

            migrationBuilder.DropTable(
                name: "contracts");

            migrationBuilder.DropTable(
                name: "interactions");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "risk_explanations");

            migrationBuilder.DropTable(
                name: "suggested_actions");

            migrationBuilder.DropTable(
                name: "invoices");

            migrationBuilder.DropTable(
                name: "risk_scores");

            migrationBuilder.DropTable(
                name: "customers");

            migrationBuilder.DropTable(
                name: "portfolio_snapshots");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudNativePaymentsLab.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentsFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Topic",
                table: "OutboxMessages",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "orders.order-created");

            migrationBuilder.CreateTable(
                name: "DeadLetterMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalMessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Topic = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ConsumerName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    EventType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    AggregateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeadLetterMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ProviderTransactionId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    ProviderResponseCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ProviderResponseMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentAttempts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ProviderTransactionId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NextRetryAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    OriginalMessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalTopic = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    OriginalEventType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Topic",
                table: "OutboxMessages",
                column: "Topic");

            migrationBuilder.CreateIndex(
                name: "IX_DeadLetterMessages_AggregateId",
                table: "DeadLetterMessages",
                column: "AggregateId");

            migrationBuilder.CreateIndex(
                name: "IX_DeadLetterMessages_ConsumerName",
                table: "DeadLetterMessages",
                column: "ConsumerName");

            migrationBuilder.CreateIndex(
                name: "IX_DeadLetterMessages_CreatedAt",
                table: "DeadLetterMessages",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_DeadLetterMessages_EventType",
                table: "DeadLetterMessages",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_DeadLetterMessages_OriginalMessageId",
                table: "DeadLetterMessages",
                column: "OriginalMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_DeadLetterMessages_OriginalMessageId_ConsumerName",
                table: "DeadLetterMessages",
                columns: new[] { "OriginalMessageId", "ConsumerName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeadLetterMessages_Topic",
                table: "DeadLetterMessages",
                column: "Topic");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAttempts_CreatedAt",
                table: "PaymentAttempts",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAttempts_IdempotencyKey",
                table: "PaymentAttempts",
                column: "IdempotencyKey");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAttempts_OrderId",
                table: "PaymentAttempts",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAttempts_OrderId_AttemptNumber",
                table: "PaymentAttempts",
                columns: new[] { "OrderId", "AttemptNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAttempts_PaymentId",
                table: "PaymentAttempts",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAttempts_Status",
                table: "PaymentAttempts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CreatedAt",
                table: "Payments",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CustomerId",
                table: "Payments",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_IdempotencyKey",
                table: "Payments",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_NextRetryAt",
                table: "Payments",
                column: "NextRetryAt");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OrderId",
                table: "Payments",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ProviderTransactionId",
                table: "Payments",
                column: "ProviderTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Status",
                table: "Payments",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeadLetterMessages");

            migrationBuilder.DropTable(
                name: "PaymentAttempts");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_Topic",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "Topic",
                table: "OutboxMessages");
        }
    }
}

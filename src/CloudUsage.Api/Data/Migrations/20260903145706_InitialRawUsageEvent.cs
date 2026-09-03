using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudUsage.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialRawUsageEvent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "raw_usage_event",
                columns: table => new
                {
                    raw_event_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    event_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_external_id = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    product_code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    event_type = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    received_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    properties_json = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ingestion_status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, defaultValue: "Pending")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_raw_usage_event", x => x.raw_event_id);
                    table.CheckConstraint("CK_raw_usage_event_properties_json", "[properties_json] IS NULL OR ISJSON([properties_json]) = 1");
                });

            migrationBuilder.CreateIndex(
                name: "IX_raw_usage_event_status_received_at",
                table: "raw_usage_event",
                columns: new[] { "ingestion_status", "received_at_utc" });

            migrationBuilder.CreateIndex(
                name: "UX_raw_usage_event_event_id",
                table: "raw_usage_event",
                column: "event_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "raw_usage_event");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RondiTrack.Migrations
{
    /// <inheritdoc />
    public partial class Add64JobsAndContributionStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Contributions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Paid");

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAtUtc",
                table: "ContributionCycles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "EndsOn",
                table: "ContributionCycles",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Arrears",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StokvelId = table.Column<Guid>(type: "uuid", nullable: false),
                    CycleId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShortfallCents = table.Column<long>(type: "bigint", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Arrears", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Arrears_ContributionCycles_CycleId",
                        column: x => x.CycleId,
                        principalTable: "ContributionCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IdempotencyEntries",
                columns: table => new
                {
                    Key = table.Column<string>(type: "text", nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResponseJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyEntries", x => x.Key);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_ExpiresAtUtc",
                table: "RefreshTokens",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ContributionCycles_EndsOn_Open",
                table: "ContributionCycles",
                column: "EndsOn",
                filter: "\"ClosedAtUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Arrears_CycleId_UserId",
                table: "Arrears",
                columns: new[] { "CycleId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyEntries_CreatedAtUtc",
                table: "IdempotencyEntries",
                column: "CreatedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Arrears");

            migrationBuilder.DropTable(
                name: "IdempotencyEntries");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_ExpiresAtUtc",
                table: "RefreshTokens");

            migrationBuilder.DropIndex(
                name: "IX_ContributionCycles_EndsOn_Open",
                table: "ContributionCycles");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Contributions");

            migrationBuilder.DropColumn(
                name: "ClosedAtUtc",
                table: "ContributionCycles");

            migrationBuilder.DropColumn(
                name: "EndsOn",
                table: "ContributionCycles");
        }
    }
}

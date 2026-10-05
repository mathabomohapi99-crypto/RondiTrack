using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RondiTrack.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueConstraintsAndXminTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // EDITED BY HAND: the generator also wrote four AddColumn("xmin") calls here.
            // I removed them, because xmin is a PostgreSQL system column that already exists on every row.
            // Trying to add it would fail. The model mapping (Version -> xmin) is still in the Designer
            // file and the model snapshot, so EF Core keeps using xmin as the concurrency token.

            // The old non-unique index is replaced by a unique one with the same name.
            migrationBuilder.DropIndex(
                name: "IX_Contributions_CycleId_UserId",
                table: "Contributions");

            // Database version of the rule: a member is paid at most once per stokvel.
            migrationBuilder.CreateIndex(
                name: "IX_Payouts_StokvelId_RecipientUserId",
                table: "Payouts",
                columns: new[] { "StokvelId", "RecipientUserId" },
                unique: true);

            // Database version of the rule: a member contributes once per cycle.
            migrationBuilder.CreateIndex(
                name: "IX_Contributions_CycleId_UserId",
                table: "Contributions",
                columns: new[] { "CycleId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // EDITED BY HAND: the DropColumn("xmin") calls were removed too (the column was never added by us).
            migrationBuilder.DropIndex(
                name: "IX_Payouts_StokvelId_RecipientUserId",
                table: "Payouts");

            migrationBuilder.DropIndex(
                name: "IX_Contributions_CycleId_UserId",
                table: "Contributions");

            // Put back the old non-unique index.
            migrationBuilder.CreateIndex(
                name: "IX_Contributions_CycleId_UserId",
                table: "Contributions",
                columns: new[] { "CycleId", "UserId" });
        }
    }
}
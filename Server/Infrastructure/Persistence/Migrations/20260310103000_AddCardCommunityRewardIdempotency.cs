using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShadowverseBeyond.Server.Infrastructure.Persistence.Migrations
{
    public partial class AddCardCommunityRewardIdempotency : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CoinTransactions_UserId_Type_RelatedEntityId",
                table: "CoinTransactions");

            migrationBuilder.CreateIndex(
                name: "IX_CoinTransactions_UserId_Type_RelatedEntityId",
                table: "CoinTransactions",
                columns: new[] { "UserId", "Type", "RelatedEntityId" },
                unique: true,
                filter: "[RelatedEntityId] IS NOT NULL AND [Type] IN (1, 2, 3, 4, 8, 9)");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CoinTransactions_UserId_Type_RelatedEntityId",
                table: "CoinTransactions");

            migrationBuilder.CreateIndex(
                name: "IX_CoinTransactions_UserId_Type_RelatedEntityId",
                table: "CoinTransactions",
                columns: new[] { "UserId", "Type", "RelatedEntityId" },
                unique: true,
                filter: "[RelatedEntityId] IS NOT NULL AND [Type] IN (1, 2, 3, 4)");
        }
    }
}

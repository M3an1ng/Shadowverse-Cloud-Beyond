using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShadowverseBeyond.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRedundantExpressionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConditionExpression",
                table: "CardAbilities");

            migrationBuilder.DropColumn(
                name: "EffectExpression",
                table: "CardAbilities");

            migrationBuilder.DropColumn(
                name: "TargetExpression",
                table: "CardAbilities");

            migrationBuilder.DropColumn(
                name: "TriggerExpression",
                table: "CardAbilities");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConditionExpression",
                table: "CardAbilities",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EffectExpression",
                table: "CardAbilities",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetExpression",
                table: "CardAbilities",
                type: "TEXT",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TriggerExpression",
                table: "CardAbilities",
                type: "TEXT",
                maxLength: 200,
                nullable: true);
        }
    }
}

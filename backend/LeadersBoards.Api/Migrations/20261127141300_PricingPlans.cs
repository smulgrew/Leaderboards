using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadersBoards.Api.Migrations
{
    /// <inheritdoc />
    public partial class PricingPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_payments_leaderboards_leaderboard_id",
                table: "payments");

            migrationBuilder.RenameColumn(
                name: "plan_expires_at",
                table: "leaderboards",
                newName: "expires_at");

            migrationBuilder.AlterColumn<Guid>(
                name: "leaderboard_id",
                table: "payments",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateIndex(
                name: "ix_leaderboards_expires_at",
                table: "leaderboards",
                column: "expires_at",
                filter: "expires_at IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_payments_leaderboards_leaderboard_id",
                table: "payments",
                column: "leaderboard_id",
                principalTable: "leaderboards",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // Map plan/payment names from the first draft of the pricing model.
            migrationBuilder.Sql("UPDATE leaderboards SET plan = 'PublicOneOff' WHERE plan = 'OneOffEvent';");
            migrationBuilder.Sql("UPDATE leaderboards SET plan = 'PrivateUnlimited' WHERE plan = 'Pro';");
            migrationBuilder.Sql("UPDATE payments SET kind = 'PublicOneOff' WHERE kind = 'OneOffEvent';");
            migrationBuilder.Sql("UPDATE payments SET kind = 'PrivateUnlimited' WHERE kind = 'ProMonthly';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_payments_leaderboards_leaderboard_id",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "ix_leaderboards_expires_at",
                table: "leaderboards");

            migrationBuilder.RenameColumn(
                name: "expires_at",
                table: "leaderboards",
                newName: "plan_expires_at");

            migrationBuilder.AlterColumn<Guid>(
                name: "leaderboard_id",
                table: "payments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "fk_payments_leaderboards_leaderboard_id",
                table: "payments",
                column: "leaderboard_id",
                principalTable: "leaderboards",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

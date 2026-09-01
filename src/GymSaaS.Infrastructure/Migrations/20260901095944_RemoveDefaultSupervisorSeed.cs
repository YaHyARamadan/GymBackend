using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymSaaS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDefaultSupervisorSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Supervisors",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "FailedTotpAttempts",
                table: "Supervisors",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TokenVersion",
                table: "Supervisors",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "TotpLockoutUntil",
                table: "Supervisors",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "IdempotencyKey",
                table: "PaymentRecords",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<int>(
                name: "FailedTotpAttempts",
                table: "Owners",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "TotpLockoutUntil",
                table: "Owners",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FailedTotpAttempts",
                table: "Supervisors");

            migrationBuilder.DropColumn(
                name: "TokenVersion",
                table: "Supervisors");

            migrationBuilder.DropColumn(
                name: "TotpLockoutUntil",
                table: "Supervisors");

            migrationBuilder.DropColumn(
                name: "FailedTotpAttempts",
                table: "Owners");

            migrationBuilder.DropColumn(
                name: "TotpLockoutUntil",
                table: "Owners");

            migrationBuilder.AlterColumn<string>(
                name: "IdempotencyKey",
                table: "PaymentRecords",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.InsertData(
                table: "Supervisors",
                columns: new[] { "Id", "CreatedAt", "Email", "FailedLoginAttempts", "LockoutUntil", "MustChangePassword", "PasswordHash", "TotpEnabled", "TotpSecretEncrypted" },
                values: new object[] { 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "admin@gymsaas.com", 0, null, true, "$2a$11$.i47pprId9CpkrE311CheeikBNswQX29r4rnYpGUT0BhjXLHOCziy", false, null });
        }
    }
}

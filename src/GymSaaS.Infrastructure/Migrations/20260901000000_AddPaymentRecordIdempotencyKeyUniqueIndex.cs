using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymSaaS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentRecordIdempotencyKeyUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Enforce idempotency at the database level: a unique index closes the TOCTOU
            // window in UnlockFacilityCommand / ActivateFacilityAddOnCommand, where two
            // concurrent requests with the same IdempotencyKey could both pass the
            // "check-then-act" AnyAsync check before either committed.
            migrationBuilder.CreateIndex(
                name: "IX_PaymentRecords_IdempotencyKey",
                table: "PaymentRecords",
                column: "IdempotencyKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PaymentRecords_IdempotencyKey",
                table: "PaymentRecords");
        }
    }
}

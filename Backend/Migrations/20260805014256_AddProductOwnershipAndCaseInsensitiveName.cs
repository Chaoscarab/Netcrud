using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddProductOwnershipAndCaseInsensitiveName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_Name",
                table: "Products");

            migrationBuilder.AddColumn<int>(
                name: "OwnerId",
                table: "Products",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameLower",
                table: "Products",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                computedColumnSql: "lower(\"Name\")",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_ExpiresAtUtc",
                table: "Sessions",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Products_OwnerId_NameLower",
                table: "Products",
                columns: new[] { "OwnerId", "NameLower" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Users_OwnerId",
                table: "Products",
                column: "OwnerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_Users_OwnerId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_ExpiresAtUtc",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_Products_OwnerId_NameLower",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "NameLower",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Products");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Name",
                table: "Products",
                column: "Name",
                unique: true);
        }
    }
}

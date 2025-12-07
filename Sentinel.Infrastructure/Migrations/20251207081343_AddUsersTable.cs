using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sentinel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUsersTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "RedirectUris",
                table: "Clients",
                newName: "_redirectUris");

            migrationBuilder.RenameColumn(
                name: "AllowedScopes",
                table: "Clients",
                newName: "_allowedScopes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "_redirectUris",
                table: "Clients",
                newName: "RedirectUris");

            migrationBuilder.RenameColumn(
                name: "_allowedScopes",
                table: "Clients",
                newName: "AllowedScopes");
        }
    }
}

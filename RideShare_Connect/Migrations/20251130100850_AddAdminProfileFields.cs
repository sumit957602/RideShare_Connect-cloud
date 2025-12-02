using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RideShare_Connect.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminProfileFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FullName",
                table: "Admins",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ProfilePicUrl",
                table: "Admins",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FullName",
                table: "Admins");

            migrationBuilder.DropColumn(
                name: "ProfilePicUrl",
                table: "Admins");
        }
    }
}

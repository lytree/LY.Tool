using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LYBox.Layout.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIsFolderToSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsFolder",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsFolder",
                table: "Settings");
        }
    }
}

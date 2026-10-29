using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UrantijTwitchMusicPlayer.Shared.Migrations
{
    /// <inheritdoc />
    public partial class TwitchDisplayName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TwitchDisplayname",
                table: "TwitchUsers",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TwitchDisplayname",
                table: "TwitchUsers");
        }
    }
}

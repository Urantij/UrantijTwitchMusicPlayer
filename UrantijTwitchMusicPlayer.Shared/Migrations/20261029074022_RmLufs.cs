using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UrantijTwitchMusicPlayer.Shared.Migrations
{
    /// <inheritdoc />
    public partial class RmLufs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Lufs",
                table: "TrackCache");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Lufs",
                table: "TrackCache",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);
        }
    }
}

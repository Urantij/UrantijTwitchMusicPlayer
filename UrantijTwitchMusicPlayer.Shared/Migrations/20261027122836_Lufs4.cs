using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UrantijTwitchMusicPlayer.Shared.Migrations
{
    /// <inheritdoc />
    public partial class Lufs4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Lufs",
                table: "TrackCache",
                newName: "I");

            migrationBuilder.AddColumn<double>(
                name: "TruePeak",
                table: "TrackCache",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "Lra",
                table: "TrackCache",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "Threshold",
                table: "TrackCache",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "I",
                table: "TrackCache");

            migrationBuilder.DropColumn(
                name: "Lra",
                table: "TrackCache");

            migrationBuilder.DropColumn(
                name: "Threshold",
                table: "TrackCache");

            migrationBuilder.RenameColumn(
                name: "I",
                table: "TrackCache",
                newName: "Lufs");
        }
    }
}

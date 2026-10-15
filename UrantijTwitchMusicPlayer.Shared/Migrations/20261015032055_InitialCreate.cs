using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UrantijTwitchMusicPlayer.Shared.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Requests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TargetUrl = table.Column<string>(type: "TEXT", nullable: false),
                    TrackData = table.Column<string>(type: "TEXT", nullable: false),
                    RequestDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    RequestLastDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Requests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TrackCache",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Link = table.Column<string>(type: "TEXT", nullable: false),
                    Lufs = table.Column<double>(type: "REAL", nullable: false),
                    CreationDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastCheckDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackCache", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TwitchUsers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TwitchId = table.Column<string>(type: "TEXT", nullable: false),
                    TwitchUsername = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TwitchUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RequestApiData",
                columns: table => new
                {
                    RequestId = table.Column<int>(type: "INTEGER", nullable: false),
                    BehalfUserId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestApiData", x => x.RequestId);
                    table.ForeignKey(
                        name: "FK_RequestApiData_Requests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "Requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RequestApiData_TwitchUsers_BehalfUserId",
                        column: x => x.BehalfUserId,
                        principalTable: "TwitchUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RequestTwitchData",
                columns: table => new
                {
                    RequestId = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestTwitchData", x => x.RequestId);
                    table.ForeignKey(
                        name: "FK_RequestTwitchData_Requests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "Requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RequestTwitchData_TwitchUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "TwitchUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RequestApiData_BehalfUserId",
                table: "RequestApiData",
                column: "BehalfUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestTwitchData_UserId",
                table: "RequestTwitchData",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RequestApiData");

            migrationBuilder.DropTable(
                name: "RequestTwitchData");

            migrationBuilder.DropTable(
                name: "TrackCache");

            migrationBuilder.DropTable(
                name: "Requests");

            migrationBuilder.DropTable(
                name: "TwitchUsers");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LabWeb.Data.Migrations
{
    /// <inheritdoc />
    public partial class GuideBlocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GuideBlocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CommunityId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Text = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    Position = table.Column<double>(type: "double precision", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuideBlocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuideBlocks_Communities_CommunityId",
                        column: x => x.CommunityId,
                        principalTable: "Communities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuideBlocks_CommunityId_Position",
                table: "GuideBlocks",
                columns: new[] { "CommunityId", "Position" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GuideBlocks");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CitizenTech.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionEvaluations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SESSION_EVALUATIONS",
                columns: table => new
                {
                    ID = table.Column<string>(type: "VARCHAR2(36)", unicode: false, maxLength: 36, nullable: false),
                    SESSION_ID = table.Column<string>(type: "VARCHAR2(36)", unicode: false, maxLength: 36, nullable: false),
                    OUTCOME = table.Column<string>(type: "VARCHAR2(19)", unicode: false, maxLength: 19, nullable: false),
                    RATING = table.Column<int>(type: "NUMBER(2)", nullable: false),
                    NEEDED_HUMAN_HELP = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    SOURCE = table.Column<string>(type: "VARCHAR2(6)", unicode: false, maxLength: 6, nullable: false),
                    CREATED_AT_UTC = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SESSION_EVALUATIONS", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "UX_SESSION_EVAL_SESSION_ID",
                table: "SESSION_EVALUATIONS",
                column: "SESSION_ID",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SESSION_EVALUATIONS");
        }
    }
}

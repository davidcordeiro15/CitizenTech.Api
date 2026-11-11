using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CitizenTech.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialGuidanceRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GUIDANCE_RECORDS",
                columns: table => new
                {
                    REQUEST_ID = table.Column<string>(type: "VARCHAR2(36)", unicode: false, maxLength: 36, nullable: false),
                    SESSION_ID = table.Column<string>(type: "VARCHAR2(36)", unicode: false, maxLength: 36, nullable: false),
                    CREATED_AT_UTC = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ACTION = table.Column<string>(type: "VARCHAR2(16)", unicode: false, maxLength: 16, nullable: true),
                    RESULT = table.Column<string>(type: "VARCHAR2(7)", unicode: false, maxLength: 7, nullable: false),
                    ERROR_CODE = table.Column<string>(type: "VARCHAR2(64)", unicode: false, maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GUIDANCE_RECORDS", x => x.REQUEST_ID);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GUIDANCE_RECORDS");
        }
    }
}

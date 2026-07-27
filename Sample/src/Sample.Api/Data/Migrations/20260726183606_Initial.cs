using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sample.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Sample");

            migrationBuilder.CreateTable(
                name: "WeatherForecasts",
                schema: "Sample",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    TemperatureC = table.Column<int>(type: "integer", nullable: false),
                    Summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeatherForecasts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WeatherForecasts_Id",
                schema: "Sample",
                table: "WeatherForecasts",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_WeatherForecasts_Location_Date",
                schema: "Sample",
                table: "WeatherForecasts",
                columns: new[] { "Location", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WeatherForecasts",
                schema: "Sample");
        }
    }
}

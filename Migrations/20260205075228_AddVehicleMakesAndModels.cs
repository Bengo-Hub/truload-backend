using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleMakesAndModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vehicle_makes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_makes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vehicle_models",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    make_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Truck"),
                    axle_configuration_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_models", x => x.id);
                    table.CheckConstraint("chk_vehicle_category", "vehicle_category IN ('Truck', 'Trailer', 'Bus', 'Van', 'Other')");
                    table.ForeignKey(
                        name: "FK_vehicle_models_axle_configurations_axle_configuration_id",
                        column: x => x.axle_configuration_id,
                        principalTable: "axle_configurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_vehicle_models_vehicle_makes_make_id",
                        column: x => x.make_id,
                        principalTable: "vehicle_makes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "idx_vehicle_makes_code",
                table: "vehicle_makes",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_vehicle_makes_country",
                table: "vehicle_makes",
                column: "country");

            migrationBuilder.CreateIndex(
                name: "idx_vehicle_makes_is_active",
                table: "vehicle_makes",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "idx_vehicle_makes_name",
                table: "vehicle_makes",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "idx_vehicle_models_category",
                table: "vehicle_models",
                column: "vehicle_category");

            migrationBuilder.CreateIndex(
                name: "idx_vehicle_models_code",
                table: "vehicle_models",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_vehicle_models_is_active",
                table: "vehicle_models",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "idx_vehicle_models_make_id",
                table: "vehicle_models",
                column: "make_id");

            migrationBuilder.CreateIndex(
                name: "idx_vehicle_models_name",
                table: "vehicle_models",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_models_axle_configuration_id",
                table: "vehicle_models",
                column: "axle_configuration_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vehicle_models");

            migrationBuilder.DropTable(
                name: "vehicle_makes");
        }
    }
}

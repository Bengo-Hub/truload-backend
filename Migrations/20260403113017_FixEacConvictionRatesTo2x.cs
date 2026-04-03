using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class FixEacConvictionRatesTo2x : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fix EAC 2nd conviction rates from 5x to 2x per Section 20 EAC VLC Act
            // First offence: up to $15,000. Subsequent offence: up to $30,000 (2x, not 5x)
            migrationBuilder.Sql(@"
                -- Delete incorrect 5x EAC 2nd conviction bands and re-insert with correct 2x rates
                DELETE FROM axle_fee_schedules
                WHERE legal_framework = 'EAC' AND conviction_number = 2;

                -- EAC GVW 2nd conviction bands (2x per-kg rates)
                INSERT INTO axle_fee_schedules (""Id"", legal_framework, fee_type, overload_min_kg, overload_max_kg, fee_per_kg_usd, flat_fee_usd, fee_per_kg_kes, flat_fee_kes, conviction_number, demerit_points, penalty_description, effective_from, is_active, created_at, updated_at)
                VALUES
                (gen_random_uuid(), 'EAC', 'GVW', 1, 500, 1.00, 0, 0, 0, 2, 2, 'EAC 2nd conviction GVW (1-500 kg) - 2x penalty per Section 20 EAC VLC Act', '2024-01-01', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
                (gen_random_uuid(), 'EAC', 'GVW', 501, 1000, 1.50, 0, 0, 0, 2, 4, 'EAC 2nd conviction GVW (501-1000 kg) - 2x penalty per Section 20 EAC VLC Act', '2024-01-01', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
                (gen_random_uuid(), 'EAC', 'GVW', 1001, 1500, 2.00, 0, 0, 0, 2, 6, 'EAC 2nd conviction GVW (1001-1500 kg) - 2x penalty per Section 20 EAC VLC Act', '2024-01-01', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
                (gen_random_uuid(), 'EAC', 'GVW', 1501, 3000, 5.00, 0, 0, 0, 2, 8, 'EAC 2nd conviction GVW (1501-3000 kg) - 2x penalty per Section 20 EAC VLC Act', '2024-01-01', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
                (gen_random_uuid(), 'EAC', 'GVW', 3001, NULL, 10.00, 1000, 0, 0, 2, 15, 'EAC 2nd conviction GVW (>3000 kg) - 2x penalty per Section 20 EAC VLC Act', '2024-01-01', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
                -- EAC Axle 2nd conviction bands (2x per-kg rates)
                (gen_random_uuid(), 'EAC', 'AXLE', 1, 200, 0.80, 0, 0, 0, 2, 2, 'EAC 2nd conviction Axle (1-200 kg) - 2x penalty per Section 20 EAC VLC Act', '2024-01-01', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
                (gen_random_uuid(), 'EAC', 'AXLE', 201, 500, 1.20, 0, 0, 0, 2, 4, 'EAC 2nd conviction Axle (201-500 kg) - 2x penalty per Section 20 EAC VLC Act', '2024-01-01', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
                (gen_random_uuid(), 'EAC', 'AXLE', 501, 1000, 2.00, 0, 0, 0, 2, 6, 'EAC 2nd conviction Axle (501-1000 kg) - 2x penalty per Section 20 EAC VLC Act', '2024-01-01', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
                (gen_random_uuid(), 'EAC', 'AXLE', 1001, 1500, 3.00, 0, 0, 0, 2, 7, 'EAC 2nd conviction Axle (1001-1500 kg) - 2x penalty per Section 20 EAC VLC Act', '2024-01-01', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
                (gen_random_uuid(), 'EAC', 'AXLE', 1501, NULL, 6.00, 400, 0, 0, 2, 12, 'EAC 2nd conviction Axle (>1500 kg) - 2x penalty per Section 20 EAC VLC Act', '2024-01-01', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}

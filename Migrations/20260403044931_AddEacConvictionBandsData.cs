using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddEacConvictionBandsData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add EAC 2nd conviction fee bands (5x penalty per Section 20(1)(a) of EAC VLC Act 2016)
            // Both GVW and Axle bands for repeat offenders
            migrationBuilder.Sql(@"
                -- EAC GVW 2nd conviction bands (5x per-kg rates)
                INSERT INTO axle_fee_schedules (""Id"", legal_framework, fee_type, overload_min_kg, overload_max_kg, fee_per_kg_usd, flat_fee_usd, fee_per_kg_kes, flat_fee_kes, conviction_number, demerit_points, penalty_description, effective_from, is_active, created_at, updated_at)
                SELECT gen_random_uuid(), v.lf, v.ft, v.mn, v.mx, v.pkg, v.ff, 0, 0, 2, v.dp, v.pd, '2024-01-01', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP
                FROM (VALUES
                    ('EAC', 'GVW', 1, 500, 2.50, 0.0, 2, 'EAC 2nd conviction GVW (1-500 kg) - 5x penalty'),
                    ('EAC', 'GVW', 501, 1000, 3.75, 0.0, 4, 'EAC 2nd conviction GVW (501-1000 kg) - 5x penalty'),
                    ('EAC', 'GVW', 1001, 1500, 5.00, 0.0, 6, 'EAC 2nd conviction GVW (1001-1500 kg) - 5x penalty'),
                    ('EAC', 'GVW', 1501, 3000, 12.50, 0.0, 8, 'EAC 2nd conviction GVW (1501-3000 kg) - 5x penalty'),
                    ('EAC', 'GVW', 3001, NULL, 25.00, 2500.0, 15, 'EAC 2nd conviction GVW (>3000 kg) - 5x penalty')
                ) AS v(lf, ft, mn, mx, pkg, ff, dp, pd)
                WHERE NOT EXISTS (
                    SELECT 1 FROM axle_fee_schedules afs
                    WHERE afs.legal_framework = 'EAC' AND afs.fee_type = 'GVW' AND afs.conviction_number = 2
                );

                -- EAC Axle 2nd conviction bands (5x per-kg rates)
                INSERT INTO axle_fee_schedules (""Id"", legal_framework, fee_type, overload_min_kg, overload_max_kg, fee_per_kg_usd, flat_fee_usd, fee_per_kg_kes, flat_fee_kes, conviction_number, demerit_points, penalty_description, effective_from, is_active, created_at, updated_at)
                SELECT gen_random_uuid(), v.lf, v.ft, v.mn, v.mx, v.pkg, v.ff, 0, 0, 2, v.dp, v.pd, '2024-01-01', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP
                FROM (VALUES
                    ('EAC', 'AXLE', 1, 200, 2.00, 0.0, 2, 'EAC 2nd conviction Axle (1-200 kg) - 5x penalty'),
                    ('EAC', 'AXLE', 201, 500, 3.00, 0.0, 4, 'EAC 2nd conviction Axle (201-500 kg) - 5x penalty'),
                    ('EAC', 'AXLE', 501, 1000, 5.00, 0.0, 6, 'EAC 2nd conviction Axle (501-1000 kg) - 5x penalty'),
                    ('EAC', 'AXLE', 1001, 1500, 7.50, 0.0, 7, 'EAC 2nd conviction Axle (1001-1500 kg) - 5x penalty'),
                    ('EAC', 'AXLE', 1501, NULL, 15.00, 1000.0, 12, 'EAC 2nd conviction Axle (>1500 kg) - 5x penalty')
                ) AS v(lf, ft, mn, mx, pkg, ff, dp, pd)
                WHERE NOT EXISTS (
                    SELECT 1 FROM axle_fee_schedules afs
                    WHERE afs.legal_framework = 'EAC' AND afs.fee_type = 'AXLE' AND afs.conviction_number = 2
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}

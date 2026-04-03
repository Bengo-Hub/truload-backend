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
            // Add EAC 2nd conviction fee bands (2x penalty per Section 20 of EAC VLC Act 2016)
            // Both GVW and Axle bands for repeat offenders
            migrationBuilder.Sql(@"
                -- EAC GVW 2nd conviction bands (2x per-kg rates per Section 20 EAC VLC Act)
                INSERT INTO axle_fee_schedules (""Id"", legal_framework, fee_type, overload_min_kg, overload_max_kg, fee_per_kg_usd, flat_fee_usd, fee_per_kg_kes, flat_fee_kes, conviction_number, demerit_points, penalty_description, effective_from, is_active, created_at, updated_at)
                SELECT gen_random_uuid(), v.lf, v.ft, v.mn, v.mx, v.pkg, v.ff, 0, 0, 2, v.dp, v.pd, '2024-01-01', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP
                FROM (VALUES
                    ('EAC', 'GVW', 1, 500, 1.00, 0.0, 2, 'EAC 2nd conviction GVW (1-500 kg) - 2x penalty per Section 20 EAC VLC Act'),
                    ('EAC', 'GVW', 501, 1000, 1.50, 0.0, 4, 'EAC 2nd conviction GVW (501-1000 kg) - 2x penalty per Section 20 EAC VLC Act'),
                    ('EAC', 'GVW', 1001, 1500, 2.00, 0.0, 6, 'EAC 2nd conviction GVW (1001-1500 kg) - 2x penalty per Section 20 EAC VLC Act'),
                    ('EAC', 'GVW', 1501, 3000, 5.00, 0.0, 8, 'EAC 2nd conviction GVW (1501-3000 kg) - 2x penalty per Section 20 EAC VLC Act'),
                    ('EAC', 'GVW', 3001, NULL, 10.00, 1000.0, 15, 'EAC 2nd conviction GVW (>3000 kg) - 2x penalty per Section 20 EAC VLC Act')
                ) AS v(lf, ft, mn, mx, pkg, ff, dp, pd)
                WHERE NOT EXISTS (
                    SELECT 1 FROM axle_fee_schedules afs
                    WHERE afs.legal_framework = 'EAC' AND afs.fee_type = 'GVW' AND afs.conviction_number = 2
                );

                -- EAC Axle 2nd conviction bands (2x per-kg rates per Section 20 EAC VLC Act)
                INSERT INTO axle_fee_schedules (""Id"", legal_framework, fee_type, overload_min_kg, overload_max_kg, fee_per_kg_usd, flat_fee_usd, fee_per_kg_kes, flat_fee_kes, conviction_number, demerit_points, penalty_description, effective_from, is_active, created_at, updated_at)
                SELECT gen_random_uuid(), v.lf, v.ft, v.mn, v.mx, v.pkg, v.ff, 0, 0, 2, v.dp, v.pd, '2024-01-01', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP
                FROM (VALUES
                    ('EAC', 'AXLE', 1, 200, 0.80, 0.0, 2, 'EAC 2nd conviction Axle (1-200 kg) - 2x penalty per Section 20 EAC VLC Act'),
                    ('EAC', 'AXLE', 201, 500, 1.20, 0.0, 4, 'EAC 2nd conviction Axle (201-500 kg) - 2x penalty per Section 20 EAC VLC Act'),
                    ('EAC', 'AXLE', 501, 1000, 2.00, 0.0, 6, 'EAC 2nd conviction Axle (501-1000 kg) - 2x penalty per Section 20 EAC VLC Act'),
                    ('EAC', 'AXLE', 1001, 1500, 3.00, 0.0, 7, 'EAC 2nd conviction Axle (1001-1500 kg) - 2x penalty per Section 20 EAC VLC Act'),
                    ('EAC', 'AXLE', 1501, NULL, 6.00, 400.0, 12, 'EAC 2nd conviction Axle (>1500 kg) - 2x penalty per Section 20 EAC VLC Act')
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

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    public partial class AddVectorHnswIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent: only creates the index if it doesn't already exist
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_case_registers_ViolationDetailsEmbedding"" ON ""case_registers"" USING hnsw (""ViolationDetailsEmbedding"" vector_cosine_ops);");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_case_subfiles_ContentEmbedding"" ON ""case_subfiles"" USING hnsw (""ContentEmbedding"" vector_cosine_ops);");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_court_hearings_MinuteNotesEmbedding"" ON ""court_hearings"" USING hnsw (""MinuteNotesEmbedding"" vector_cosine_ops);");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_vehicles_DescriptionEmbedding"" ON ""vehicles"" USING hnsw (""DescriptionEmbedding"" vector_cosine_ops);");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_weighing_transactions_ViolationReasonEmbedding"" ON ""weighing_transactions"" USING hnsw (""ViolationReasonEmbedding"" vector_cosine_ops);");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_prosecution_cases_CaseNotesEmbedding"" ON ""prosecution_cases"" USING hnsw (""CaseNotesEmbedding"" vector_cosine_ops);");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_vehicle_tags_ReasonEmbedding"" ON ""vehicle_tags"" USING hnsw (""ReasonEmbedding"" vector_cosine_ops);");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_case_registers_ViolationDetailsEmbedding"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_case_subfiles_ContentEmbedding"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_court_hearings_MinuteNotesEmbedding"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_vehicles_DescriptionEmbedding"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_weighing_transactions_ViolationReasonEmbedding"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_prosecution_cases_CaseNotesEmbedding"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_vehicle_tags_ReasonEmbedding"";");
        }
    }
}

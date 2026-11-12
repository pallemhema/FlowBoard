using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowBoard.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ProjectDatesAsDateOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "Projects"
                    ALTER COLUMN "StartDate" TYPE date
                        USING (("StartDate" AT TIME ZONE 'UTC')::date),
                    ALTER COLUMN "EndDate" TYPE date
                        USING (("EndDate" AT TIME ZONE 'UTC')::date);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "Projects"
                    ALTER COLUMN "StartDate" TYPE timestamp with time zone
                        USING ("StartDate"::timestamp AT TIME ZONE 'UTC'),
                    ALTER COLUMN "EndDate" TYPE timestamp with time zone
                        USING ("EndDate"::timestamp AT TIME ZONE 'UTC');
                """);
        }
    }
}

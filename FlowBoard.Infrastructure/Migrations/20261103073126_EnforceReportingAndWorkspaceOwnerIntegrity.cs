using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowBoard.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnforceReportingAndWorkspaceOwnerIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $migration$
                DECLARE
                    duplicate_assignments text;
                    invalid_workspace_owners text;
                BEGIN
                    SELECT string_agg(
                        format(
                            'Employee %s is assigned to Managers [%s]',
                            duplicates."EmployeeId",
                            duplicates.manager_ids),
                        E'\n')
                    INTO duplicate_assignments
                    FROM (
                        SELECT
                            "EmployeeId",
                            string_agg("ManagerId"::text, ', ' ORDER BY "ManagerId") AS manager_ids
                        FROM "ManagerEmployees"
                        GROUP BY "EmployeeId"
                        HAVING count(*) > 1
                    ) AS duplicates;

                    IF duplicate_assignments IS NOT NULL THEN
                        RAISE EXCEPTION
                            'Cannot enforce one Manager per Employee. Resolve these existing relationships manually, then retry: %',
                            duplicate_assignments;
                    END IF;

                    SELECT string_agg(
                        format(
                            'Workspace %s is owned by user %s (role: %s)',
                            workspaces."Id",
                            workspaces."OwnerId",
                            users."Role"),
                        E'\n')
                    INTO invalid_workspace_owners
                    FROM "Workspaces" AS workspaces
                    INNER JOIN "Users" AS users
                        ON users."Id" = workspaces."OwnerId"
                    WHERE users."Role" NOT IN ('Admin', 'Manager');

                    IF invalid_workspace_owners IS NOT NULL THEN
                        RAISE EXCEPTION
                            'Cannot enforce workspace owner roles. Resolve these owners manually, then retry: %',
                            invalid_workspace_owners;
                    END IF;
                END
                $migration$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_ManagerEmployees_EmployeeId",
                table: "ManagerEmployees");

            migrationBuilder.CreateIndex(
                name: "IX_ManagerEmployees_EmployeeId",
                table: "ManagerEmployees",
                column: "EmployeeId",
                unique: true);

            migrationBuilder.Sql(
                """
                CREATE FUNCTION "EnforceWorkspaceOwnerRole"()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                DECLARE
                    owner_role text;
                BEGIN
                    SELECT "Role"
                    INTO owner_role
                    FROM "Users"
                    WHERE "Id" = NEW."OwnerId"
                    FOR UPDATE;

                    IF FOUND AND owner_role NOT IN ('Admin', 'Manager') THEN
                        RAISE EXCEPTION
                            'Only Admins and Managers can own workspaces.'
                            USING ERRCODE = '23514',
                                  CONSTRAINT = 'CK_Workspaces_OwnerRole';
                    END IF;

                    RETURN NEW;
                END
                $function$;

                CREATE TRIGGER "TR_Workspaces_EnforceOwnerRole"
                BEFORE INSERT OR UPDATE OF "OwnerId"
                ON "Workspaces"
                FOR EACH ROW
                EXECUTE FUNCTION "EnforceWorkspaceOwnerRole"();
                """);

            migrationBuilder.Sql(
                """
                CREATE FUNCTION "PreventEmployeeWorkspaceOwnership"()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                BEGIN
                    IF NEW."Role" NOT IN ('Admin', 'Manager')
                       AND OLD."Role" IS DISTINCT FROM NEW."Role"
                       AND EXISTS (
                           SELECT 1
                           FROM "Workspaces"
                           WHERE "OwnerId" = NEW."Id"
                       ) THEN
                        RAISE EXCEPTION
                            'A user who owns workspaces cannot be changed to the Employee role.'
                            USING ERRCODE = '23514',
                                  CONSTRAINT = 'CK_Users_OwnedWorkspacesRole';
                    END IF;

                    RETURN NEW;
                END
                $function$;

                CREATE TRIGGER "TR_Users_PreventEmployeeWorkspaceOwner"
                BEFORE UPDATE OF "Role"
                ON "Users"
                FOR EACH ROW
                EXECUTE FUNCTION "PreventEmployeeWorkspaceOwnership"();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER "TR_Users_PreventEmployeeWorkspaceOwner" ON "Users";
                DROP FUNCTION "PreventEmployeeWorkspaceOwnership"();
                DROP TRIGGER "TR_Workspaces_EnforceOwnerRole" ON "Workspaces";
                DROP FUNCTION "EnforceWorkspaceOwnerRole"();
                """);

            migrationBuilder.DropIndex(
                name: "IX_ManagerEmployees_EmployeeId",
                table: "ManagerEmployees");

            migrationBuilder.CreateIndex(
                name: "IX_ManagerEmployees_EmployeeId",
                table: "ManagerEmployees",
                column: "EmployeeId");
        }
    }
}

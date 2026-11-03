using FlowBoard.Domain.Entities;
using FlowBoard.Domain.Enums;
using FlowBoard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit.Sdk;

namespace FlowBoard.Tests;

public sealed class PostgreSqlIntegrityMigrationTests
{
    private const string PreviousMigration =
        "20261102095744_UserInvitations";

    [PostgreSqlFact]
    public async Task MigrationEnforcesReportingAndWorkspaceOwnerRules()
    {
        await WithTemporarySchemaAsync(async connectionString =>
        {
            await using var context = CreateContext(connectionString);
            await context.Database.MigrateAsync();

            var admin = CreateUser(UserRole.Admin, "admin");
            var manager = CreateUser(UserRole.Manager, "manager");
            var otherManager = CreateUser(UserRole.Manager, "other-manager");
            var firstEmployee = CreateUser(UserRole.Employee, "first-employee");
            var secondEmployee = CreateUser(UserRole.Employee, "second-employee");
            context.Users.AddRange(
                admin,
                manager,
                otherManager,
                firstEmployee,
                secondEmployee);
            await context.SaveChangesAsync();

            var adminWorkspace = new Workspace
            {
                Id = Guid.NewGuid(),
                Name = "Admin workspace",
                OwnerId = admin.Id
            };
            var managerWorkspace = new Workspace
            {
                Id = Guid.NewGuid(),
                Name = "Manager workspace",
                OwnerId = manager.Id
            };
            context.Workspaces.AddRange(adminWorkspace, managerWorkspace);
            await context.SaveChangesAsync();

            var insertRejected = await Assert.ThrowsAsync<PostgresException>(() =>
                context.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    INSERT INTO "Workspaces" ("Id", "Name", "OwnerId", "CreatedAt")
                    VALUES ({Guid.NewGuid()}, 'Employee workspace', {firstEmployee.Id}, now())
                    """));
            Assert.Equal(PostgresErrorCodes.CheckViolation, insertRejected.SqlState);

            var ownerUpdateRejected = await Assert.ThrowsAsync<PostgresException>(() =>
                context.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    UPDATE "Workspaces"
                    SET "OwnerId" = {firstEmployee.Id}
                    WHERE "Id" = {managerWorkspace.Id}
                    """));
            Assert.Equal(PostgresErrorCodes.CheckViolation, ownerUpdateRejected.SqlState);

            var managerDemotionRejected = await Assert.ThrowsAsync<PostgresException>(() =>
                context.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    UPDATE "Users"
                    SET "Role" = 'Employee'
                    WHERE "Id" = {manager.Id}
                    """));
            Assert.Equal(PostgresErrorCodes.CheckViolation, managerDemotionRejected.SqlState);

            var adminDemotionRejected = await Assert.ThrowsAsync<PostgresException>(() =>
                context.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    UPDATE "Users"
                    SET "Role" = 'Employee'
                    WHERE "Id" = {admin.Id}
                    """));
            Assert.Equal(PostgresErrorCodes.CheckViolation, adminDemotionRejected.SqlState);

            await context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO "ManagerEmployees" ("ManagerId", "EmployeeId", "CreatedAt")
                VALUES ({manager.Id}, {firstEmployee.Id}, now())
                """);
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO "ManagerEmployees" ("ManagerId", "EmployeeId", "CreatedAt")
                VALUES ({otherManager.Id}, {secondEmployee.Id}, now())
                """);

            var secondManagerRejected = await Assert.ThrowsAsync<PostgresException>(() =>
                context.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    INSERT INTO "ManagerEmployees" ("ManagerId", "EmployeeId", "CreatedAt")
                    VALUES ({otherManager.Id}, {firstEmployee.Id}, now())
                    """));
            Assert.Equal(PostgresErrorCodes.UniqueViolation, secondManagerRejected.SqlState);

            await context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                DELETE FROM "ManagerEmployees"
                WHERE "ManagerId" = {manager.Id} AND "EmployeeId" = {firstEmployee.Id}
                """);
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO "ManagerEmployees" ("ManagerId", "EmployeeId", "CreatedAt")
                VALUES ({otherManager.Id}, {firstEmployee.Id}, now())
                """);

            context.Workspaces.RemoveRange(adminWorkspace, managerWorkspace);
            await context.SaveChangesAsync();
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"""UPDATE "Users" SET "Role" = 'Employee' WHERE "Id" = {manager.Id}""");
        });
    }

    [PostgreSqlFact]
    public async Task MigrationPreflightReportsAndPreservesExistingViolations()
    {
        await WithTemporarySchemaAsync(async connectionString =>
        {
            var admin = CreateUser(UserRole.Admin, "preflight-admin");
            var manager = CreateUser(UserRole.Manager, "preflight-manager");
            var otherManager = CreateUser(UserRole.Manager, "preflight-other-manager");
            var employee = CreateUser(UserRole.Employee, "preflight-employee");
            var workspace = new Workspace
            {
                Id = Guid.NewGuid(),
                Name = "Invalid existing owner",
                OwnerId = employee.Id
            };

            await using (var context = CreateContext(connectionString))
            {
                await context.Database
                    .GetService<IMigrator>()
                    .MigrateAsync(PreviousMigration);
                context.Users.AddRange(admin, manager, otherManager, employee);
                context.Workspaces.Add(workspace);
                await context.SaveChangesAsync();
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    INSERT INTO "ManagerEmployees" ("ManagerId", "EmployeeId", "CreatedAt")
                    VALUES ({manager.Id}, {employee.Id}, now()),
                           ({otherManager.Id}, {employee.Id}, now())
                    """);
            }

            var duplicateError = await TryMigrateAsync(connectionString);
            Assert.Contains(employee.Id.ToString(), duplicateError.Message);
            Assert.Contains(manager.Id.ToString(), duplicateError.Message);
            Assert.Contains(otherManager.Id.ToString(), duplicateError.Message);

            await using (var context = CreateContext(connectionString))
            {
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    DELETE FROM "ManagerEmployees"
                    WHERE "ManagerId" = {otherManager.Id}
                      AND "EmployeeId" = {employee.Id}
                    """);
            }

            var ownerError = await TryMigrateAsync(connectionString);
            Assert.Contains(employee.Id.ToString(), ownerError.Message);
            Assert.Contains(workspace.Id.ToString(), ownerError.Message);

            await using var verifyContext = CreateContext(connectionString);
            Assert.Equal(
                1,
                await verifyContext.ManagerEmployees.CountAsync(
                    relationship => relationship.EmployeeId == employee.Id));
            Assert.Equal(
                employee.Id,
                (await verifyContext.Workspaces
                    .SingleAsync(item => item.Id == workspace.Id)).OwnerId);
        });
    }

    private static async Task<PostgresException> TryMigrateAsync(
        string connectionString)
    {
        await using var context = CreateContext(connectionString);
        return await Assert.ThrowsAsync<PostgresException>(
            () => context.Database.MigrateAsync());
    }

    private static FlowBoardDbContext CreateContext(string connectionString)
    {
        var searchPath = new NpgsqlConnectionStringBuilder(connectionString).SearchPath;
        if (string.IsNullOrWhiteSpace(searchPath))
        {
            throw new InvalidOperationException(
                "The PostgreSQL test connection must specify a search path.");
        }

        var schema = searchPath
            .Split(',', StringSplitOptions.RemoveEmptyEntries)[0]
            .Trim();
        var options = new DbContextOptionsBuilder<FlowBoardDbContext>()
            .UseNpgsql(
                connectionString,
                postgres => postgres.MigrationsHistoryTable(
                    "__EFMigrationsHistory",
                    schema))
            .Options;
        return new FlowBoardDbContext(
            options,
            new TestCurrentUser());
    }

    private static async Task WithTemporarySchemaAsync(
        Func<string, Task> action)
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "FLOWBOARD_TEST_POSTGRES_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "The PostgreSQL test connection string is required.");
        }

        var schema = $"flowboard_test_{Guid.NewGuid():N}";
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using (var createSchema = connection.CreateCommand())
        {
            createSchema.CommandText = $"CREATE SCHEMA \"{schema}\"";
            await createSchema.ExecuteNonQueryAsync();
        }

        try
        {
            var scopedConnectionString =
                new NpgsqlConnectionStringBuilder(connectionString)
                {
                    SearchPath = schema
                }.ConnectionString;
            await action(scopedConnectionString);
        }
        finally
        {
            await using var dropSchema = connection.CreateCommand();
            dropSchema.CommandText = $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE";
            await dropSchema.ExecuteNonQueryAsync();
        }
    }

    private static User CreateUser(UserRole role, string name) =>
        new()
        {
            Id = Guid.NewGuid(),
            Email = $"{name}@test.invalid",
            PasswordHash = "not-a-real-password-hash",
            FirstName = name,
            LastName = "Test",
            Role = role
        };

    private sealed class TestCurrentUser : ICurrentUserService
    {
        public Guid UserId { get; } = Guid.NewGuid();

        public string? Email => null;

        public string? Role => nameof(UserRole.Admin);

        public bool IsAuthenticated => true;
    }

    private sealed class PostgreSqlFactAttribute : FactAttribute
    {
        public PostgreSqlFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(
                    Environment.GetEnvironmentVariable(
                        "FLOWBOARD_TEST_POSTGRES_CONNECTION_STRING")))
            {
                Skip = "Set FLOWBOARD_TEST_POSTGRES_CONNECTION_STRING to run PostgreSQL migration integration tests.";
            }
        }
    }
}

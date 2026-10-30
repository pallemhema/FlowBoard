using FlowBoard.Domain.Entities;
using FlowBoard.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FlowBoard.Tests;

public sealed class AuditInformationTests
{
    [Fact]
    public async Task SaveChangesAsync_SetsAllAuditFieldsForAddedEntities()
    {
        await using var database = await TestDatabase.CreateAsync();
        var actorId = Guid.NewGuid();
        await using var context = database.CreateContext(
            new TestCurrentUser(actorId, true));

        var team = new Team { Id = Guid.NewGuid(), Name = "Engineering" };
        context.Teams.Add(team);
        var beforeSave = DateTime.UtcNow;

        await context.SaveChangesAsync();

        Assert.InRange(team.CreatedAt, beforeSave, DateTime.UtcNow);
        Assert.Equal(team.CreatedAt, team.ModifiedAt);
        Assert.Equal(actorId, team.CreatedByUserId);
        Assert.Equal(actorId, team.ModifiedByUserId);
    }

    [Fact]
    public async Task SaveChanges_SetsAllAuditFieldsForAddedEntities()
    {
        await using var database = await TestDatabase.CreateAsync();
        var actorId = Guid.NewGuid();
        await using var context = database.CreateContext(
            new TestCurrentUser(actorId, true));

        var team = new Team { Id = Guid.NewGuid(), Name = "Engineering" };
        context.Teams.Add(team);

        context.SaveChanges();

        Assert.NotEqual(default, team.CreatedAt);
        Assert.Equal(team.CreatedAt, team.ModifiedAt);
        Assert.Equal(actorId, team.CreatedByUserId);
        Assert.Equal(actorId, team.ModifiedByUserId);
    }

    [Fact]
    public async Task SaveChangesAsync_UpdatesModificationAuditAndPreservesCreationAudit()
    {
        await using var database = await TestDatabase.CreateAsync();
        var originalActorId = Guid.NewGuid();
        var nextActorId = Guid.NewGuid();
        var team = new Team { Id = Guid.NewGuid(), Name = "Engineering" };

        await using (var context = database.CreateContext(
                         new TestCurrentUser(originalActorId, true)))
        {
            context.Teams.Add(team);
            await context.SaveChangesAsync();
        }

        var createdAt = team.CreatedAt;
        var createdBy = team.CreatedByUserId;
        var originalModifiedAt = team.ModifiedAt;
        await Task.Delay(10);

        await using (var context = database.CreateContext(
                         new TestCurrentUser(nextActorId, true)))
        {
            var updatedTeam = await context.Teams.SingleAsync();
            updatedTeam.CreatedAt = createdAt.AddYears(1);
            updatedTeam.CreatedByUserId = Guid.NewGuid();
            updatedTeam.Name = "Product Engineering";
            await context.SaveChangesAsync();

            Assert.Equal(createdAt, updatedTeam.CreatedAt);
            Assert.Equal(createdBy, updatedTeam.CreatedByUserId);
            Assert.True(updatedTeam.ModifiedAt > originalModifiedAt);
            Assert.Equal(nextActorId, updatedTeam.ModifiedByUserId);
        }
    }

    [Fact]
    public async Task SaveChanges_DoesNotChangeAuditFieldsForUnchangedEntities()
    {
        await using var database = await TestDatabase.CreateAsync();
        var actorId = Guid.NewGuid();
        var team = new Team { Id = Guid.NewGuid(), Name = "Engineering" };

        await using (var context = database.CreateContext(
                         new TestCurrentUser(actorId, true)))
        {
            context.Teams.Add(team);
            await context.SaveChangesAsync();
        }

        var auditValues = (
            team.CreatedAt,
            team.ModifiedAt,
            team.CreatedByUserId,
            team.ModifiedByUserId);
        await using var unchangedContext = database.CreateContext(
            new TestCurrentUser(Guid.NewGuid(), true));
        unchangedContext.Teams.Attach(team);
        unchangedContext.Entry(team).State = EntityState.Unchanged;

        unchangedContext.SaveChanges();

        Assert.Equal(auditValues.CreatedAt, team.CreatedAt);
        Assert.Equal(auditValues.ModifiedAt, team.ModifiedAt);
        Assert.Equal(auditValues.CreatedByUserId, team.CreatedByUserId);
        Assert.Equal(auditValues.ModifiedByUserId, team.ModifiedByUserId);
    }

    [Fact]
    public async Task SaveChangesAsync_UsesSystemActorWhenNoUserIsAuthenticated()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var context = database.CreateContext(
            new TestCurrentUser(Guid.Empty, false));

        var team = new Team { Id = Guid.NewGuid(), Name = "System Team" };
        context.Teams.Add(team);
        await context.SaveChangesAsync();

        Assert.Equal(SystemActors.SystemUserId, team.CreatedByUserId);
        Assert.Equal(SystemActors.SystemUserId, team.ModifiedByUserId);
    }

    private sealed class TestCurrentUser(Guid userId, bool isAuthenticated)
        : ICurrentUserService
    {
        public Guid UserId { get; } = userId;

        public string? Email => null;

        public string? Role => null;

        public bool IsAuthenticated { get; } = isAuthenticated;
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private TestDatabase(SqliteConnection connection)
        {
            _connection = connection;
        }

        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var database = new TestDatabase(connection);
            await using var context = database.CreateContext(
                new TestCurrentUser(Guid.Empty, false));
            await context.Database.EnsureCreatedAsync();
            return database;
        }

        public FlowBoardDbContext CreateContext(ICurrentUserService currentUser)
        {
            var options = new DbContextOptionsBuilder<FlowBoardDbContext>()
                .UseSqlite(_connection)
                .Options;
            return new FlowBoardDbContext(options, currentUser);
        }

        public ValueTask DisposeAsync() => _connection.DisposeAsync();
    }
}

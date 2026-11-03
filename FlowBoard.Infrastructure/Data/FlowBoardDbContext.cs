using FlowBoard.Domain.Entities;
using FlowBoard.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace FlowBoard.Infrastructure.Data;

public class FlowBoardDbContext : DbContext
{
    private readonly ICurrentUserService _currentUserService;

    public FlowBoardDbContext(
        DbContextOptions<FlowBoardDbContext> options,
        ICurrentUserService currentUserService)
        : base(options)
    {
        _currentUserService = currentUserService;
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<UserInvitation> UserInvitations => Set<UserInvitation>();

    public DbSet<UserRoleChangeAudit> UserRoleChangeAudits =>
        Set<UserRoleChangeAudit>();

    public DbSet<Board> Boards => Set<Board>();

    public DbSet<BoardColumn> BoardColumns => Set<BoardColumn>();

    public DbSet<BoardMember> BoardMembers => Set<BoardMember>();

    public DbSet<Card> Cards => Set<Card>();

    public DbSet<CardComment> CardComments => Set<CardComment>();

    public DbSet<Workspace> Workspaces { get; set; }

    public DbSet<Team> Teams => Set<Team>();

    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();

    public DbSet<ManagerEmployee> ManagerEmployees => Set<ManagerEmployee>();

    public DbSet<WorkspaceMember> WorkspaceMembers => Set<WorkspaceMember>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyAuditInformation();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditInformation();
        return base.SaveChangesAsync(
            acceptAllChangesOnSuccess,
            cancellationToken);
    }

    private void ApplyAuditInformation()
    {
        var now = DateTime.UtcNow;
        var actorId = _currentUserService.IsAuthenticated
            ? _currentUserService.UserId
            : SystemActors.SystemUserId;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.ModifiedAt = now;
                entry.Entity.CreatedByUserId = actorId;
                entry.Entity.ModifiedByUserId = actorId;
            }
            else if (entry.State == EntityState.Modified)
            {
                var createdAt = entry.Property(entity => entity.CreatedAt);
                createdAt.CurrentValue = createdAt.OriginalValue;
                createdAt.IsModified = false;

                var createdByUserId =
                    entry.Property(entity => entity.CreatedByUserId);
                createdByUserId.CurrentValue = createdByUserId.OriginalValue;
                createdByUserId.IsModified = false;

                entry.Entity.ModifiedAt = now;
                entry.Entity.ModifiedByUserId = actorId;
            }
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(FlowBoardDbContext).Assembly);
    }
}
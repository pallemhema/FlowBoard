namespace FlowBoard.Domain.Entities;

using FlowBoard.Domain.Common;
using FlowBoard.Domain.Enums;

public class User : BaseEntity
{
    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public UserRole Role { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];

    public ICollection<Board> OwnedBoards { get; set; } = [];

    public ICollection<BoardMember> BoardMemberships { get; set; } = [];

    public ICollection<TeamMember> TeamMemberships { get; set; } = [];

    public ICollection<ManagerEmployee> ManagedEmployees { get; set; } = [];

    public ICollection<ManagerEmployee> ManagerRelationships { get; set; } = [];

    public ICollection<WorkspaceMember> WorkspaceMemberships { get; set; } = [];

    public ICollection<Project> ManagedProjects { get; set; } = [];

    public ICollection<ProjectMember> ProjectMemberships { get; set; } = [];

    public ICollection<Card> AssignedCards { get; set; } = [];

    public ICollection<CardComment> Comments { get; set; } = [];

    public ICollection<Workspace> OwnedWorkspaces { get; set; }
        = new List<Workspace>();
}
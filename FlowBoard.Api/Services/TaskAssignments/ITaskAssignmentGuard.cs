namespace FlowBoard.Api.Services.TaskAssignments;

public interface ITaskAssignmentGuard
{
    Task ValidateAssigneeAsync(
        Guid boardId,
        Guid employeeId,
        CancellationToken cancellationToken = default);

    Task EnsureBoardMemberCanBeRemovedAsync(
        Guid boardId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task EnsureProjectMemberCanBeRemovedAsync(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task EnsureWorkspaceMemberCanBeRemovedAsync(
        Guid workspaceId,
        Guid userId,
        CancellationToken cancellationToken = default);
}

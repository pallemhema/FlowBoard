using FlowBoard.Api.Exceptions;
using FlowBoard.Api.Services.BoardAuthorization;
using FlowBoard.Api.Services.Authentication;
using FlowBoard.Api.Services.Boards;
using FlowBoard.Api.Services.BoardColumns;
using FlowBoard.Api.Services.Cards;
using FlowBoard.Api.Services.BoardMembers;
using FlowBoard.Api.Services.CardComments;
using FlowBoard.Api.Services.Projects;
using FlowBoard.Api.Services.TaskAssignments;
using FlowBoard.Api.Services.Workspaces;
using FlowBoard.Domain.Entities;
using FlowBoard.Domain.Enums;
using FlowBoard.Api.Configuration;
using FlowBoard.Infrastructure.Data;
using FlowBoard.Shared.DTOs.BoardMembers;
using FlowBoard.Shared.DTOs.CardComments;
using FlowBoard.Shared.DTOs.Common;
using FlowBoard.Shared.DTOs.Projects;
using FlowBoard.Shared.DTOs.Workspaces;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FlowBoard.Tests;

public sealed class AuthorizationRefinementTests
{
    [Fact]
    public async Task AdminCanCreateProjectWithManagerWhoCanAccessWorkspace()
    {
        await using var database = await TestDatabase.CreateAsync();
        var adminId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();

        await using (var context = database.CreateContext(adminId, UserRole.Admin))
        {
            AddUser(context, adminId, UserRole.Admin);
            AddUser(context, managerId, UserRole.Manager);
            context.Workspaces.Add(new Workspace
            {
                Id = workspaceId,
                Name = "Engineering",
                OwnerId = adminId
            });
            context.WorkspaceMembers.Add(new WorkspaceMember
            {
                WorkspaceId = workspaceId,
                UserId = managerId,
                JoinedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }

        await using var projectContext =
            database.CreateContext(adminId, UserRole.Admin);
        var project = await CreateProjectService(
                projectContext,
                adminId,
                UserRole.Admin)
            .CreateAsync(ProjectRequest(workspaceId, managerId));

        Assert.Equal(managerId, project.ManagerId);
        Assert.Equal(workspaceId, project.WorkspaceId);
    }

    [Fact]
    public async Task AdminProjectCreationRequiresAnEligibleManagerWithWorkspaceAccess()
    {
        await using var database = await TestDatabase.CreateAsync();
        var adminId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();

        await using (var context = database.CreateContext(adminId, UserRole.Admin))
        {
            AddUser(context, adminId, UserRole.Admin);
            AddUser(context, managerId, UserRole.Manager);
            AddUser(context, employeeId, UserRole.Employee);
            context.Workspaces.Add(new Workspace
            {
                Id = workspaceId,
                Name = "Engineering",
                OwnerId = adminId
            });
            await context.SaveChangesAsync();
        }

        await using var adminContext =
            database.CreateContext(adminId, UserRole.Admin);
        var service = CreateProjectService(adminContext, adminId, UserRole.Admin);

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateAsync(ProjectRequest(workspaceId, null)));
        await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateAsync(ProjectRequest(workspaceId, employeeId)));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.CreateAsync(ProjectRequest(workspaceId, Guid.NewGuid())));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.CreateAsync(ProjectRequest(workspaceId, managerId)));
    }

    [Fact]
    public async Task ManagerCreatesProjectAsSelfAndCannotAssignAnotherManager()
    {
        await using var database = await TestDatabase.CreateAsync();
        var managerId = Guid.NewGuid();
        var otherManagerId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var sharedWorkspaceId = Guid.NewGuid();

        await using (var context = database.CreateContext(managerId, UserRole.Manager))
        {
            AddUser(context, managerId, UserRole.Manager);
            AddUser(context, otherManagerId, UserRole.Manager);
            context.Workspaces.Add(new Workspace
            {
                Id = workspaceId,
                Name = "Owned workspace",
                OwnerId = managerId
            });
            context.Workspaces.Add(new Workspace
            {
                Id = sharedWorkspaceId,
                Name = "Member workspace",
                OwnerId = otherManagerId
            });
            context.WorkspaceMembers.Add(new WorkspaceMember
            {
                WorkspaceId = sharedWorkspaceId,
                UserId = managerId,
                JoinedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }

        await using var managerContext =
            database.CreateContext(managerId, UserRole.Manager);
        var service = CreateProjectService(
            managerContext,
            managerId,
            UserRole.Manager);
        var project = await service.CreateAsync(ProjectRequest(workspaceId, null));

        Assert.Equal(managerId, project.ManagerId);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.CreateAsync(ProjectRequest(workspaceId, otherManagerId)));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.CreateAsync(ProjectRequest(sharedWorkspaceId, null)));
    }

    [Fact]
    public async Task EmployeeCannotCreateProjectOrWorkspace()
    {
        await using var database = await TestDatabase.CreateAsync();
        var employeeId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();

        await using (var context = database.CreateContext(employeeId, UserRole.Employee))
        {
            AddUser(context, employeeId, UserRole.Employee);
            context.Workspaces.Add(new Workspace
            {
                Id = workspaceId,
                Name = "Workspace",
                OwnerId = employeeId
            });
            await context.SaveChangesAsync();
        }

        await using var employeeContext =
            database.CreateContext(employeeId, UserRole.Employee);
        var assignments = new TaskAssignmentGuard(
            employeeContext,
            new TestCurrentUser(employeeId, UserRole.Employee));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            CreateProjectService(
                    employeeContext,
                    employeeId,
                    UserRole.Employee)
                .CreateAsync(ProjectRequest(workspaceId, null)));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new WorkspaceService(
                    employeeContext,
                    new TestCurrentUser(employeeId, UserRole.Employee),
                    assignments)
                .CreateAsync(new CreateWorkspaceRequest { Name = "Blocked" }));
    }

    [Fact]
    public async Task UserDirectoryIsAdminWideAndManagerScopedToDirectReports()
    {
        await using var database = await TestDatabase.CreateAsync();
        var adminId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var otherManagerId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var unrelatedEmployeeId = Guid.NewGuid();

        await using (var context = database.CreateContext(adminId, UserRole.Admin))
        {
            AddUser(context, adminId, UserRole.Admin);
            AddUser(context, managerId, UserRole.Manager);
            AddUser(context, otherManagerId, UserRole.Manager);
            AddUser(context, employeeId, UserRole.Employee);
            AddUser(context, unrelatedEmployeeId, UserRole.Employee);
            context.ManagerEmployees.Add(new ManagerEmployee
            {
                ManagerId = managerId,
                EmployeeId = employeeId,
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }

        await using (var managerContext =
                     database.CreateContext(managerId, UserRole.Manager))
        {
            var users = await CreateAuthService(
                    managerContext,
                    managerId,
                    UserRole.Manager)
                .GetUsersAsync(new PagedSearchRequest());

            var user = Assert.Single(users.Items);
            Assert.Equal(employeeId, user.UserId);
        }

        await using (var adminContext =
                     database.CreateContext(adminId, UserRole.Admin))
        {
            var users = await CreateAuthService(
                    adminContext,
                    adminId,
                    UserRole.Admin)
                .GetUsersAsync(new PagedSearchRequest());
            Assert.Equal(5, users.TotalCount);
        }

        await using var employeeContext =
            database.CreateContext(employeeId, UserRole.Employee);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            CreateAuthService(
                    employeeContext,
                    employeeId,
                    UserRole.Employee)
                .GetUsersAsync(new PagedSearchRequest()));
    }

    [Fact]
    public async Task ManagerCannotEditAnotherManagersWorkspaceButOwnerCan()
    {
        await using var database = await TestDatabase.CreateAsync();
        var managerId = Guid.NewGuid();
        var otherManagerId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();

        await using (var context = database.CreateContext(managerId, UserRole.Manager))
        {
            AddUser(context, managerId, UserRole.Manager);
            AddUser(context, otherManagerId, UserRole.Manager);
            context.Workspaces.Add(new Workspace
            {
                Id = workspaceId,
                Name = "Other workspace",
                OwnerId = otherManagerId
            });
            await context.SaveChangesAsync();
        }

        await using (var context = database.CreateContext(managerId, UserRole.Manager))
        {
            var service = CreateWorkspaceService(context, managerId, UserRole.Manager);
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                service.UpdateAsync(
                    workspaceId,
                    new UpdateWorkspaceRequest { Name = "Unauthorized edit" }));
        }

        await using (var context = database.CreateContext(otherManagerId, UserRole.Manager))
        {
            var service = CreateWorkspaceService(
                context,
                otherManagerId,
                UserRole.Manager);
            var updated = await service.UpdateAsync(
                workspaceId,
                new UpdateWorkspaceRequest { Name = "Owner edit" });

            Assert.Equal("Owner edit", updated.Name);
        }
    }

    [Fact]
    public async Task WorkspaceDeletionConflictsWhenItWouldCascadeBoardData()
    {
        await using var database = await TestDatabase.CreateAsync();
        var managerId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();

        await using (var context = database.CreateContext(managerId, UserRole.Manager))
        {
            AddUser(context, managerId, UserRole.Manager);
            var workspace = new Workspace
            {
                Id = workspaceId,
                Name = "Owned workspace",
                OwnerId = managerId
            };
            context.Workspaces.Add(workspace);
            context.Boards.Add(new Board
            {
                Id = Guid.NewGuid(),
                Name = "Board with cards",
                OwnerId = managerId,
                WorkspaceId = workspaceId,
                Purpose = BoardPurpose.GeneralLearning
            });
            await context.SaveChangesAsync();
        }

        await using var managerContext =
            database.CreateContext(managerId, UserRole.Manager);
        await Assert.ThrowsAsync<ConflictException>(() =>
            CreateWorkspaceService(
                    managerContext,
                    managerId,
                    UserRole.Manager)
                .DeleteAsync(workspaceId));
        Assert.Equal(1, await managerContext.Boards.CountAsync());
        Assert.Equal(1, await managerContext.Workspaces.CountAsync());
    }

    [Fact]
    public async Task WorkspaceDeletionConflictsWhenMembersRemain()
    {
        await using var database = await TestDatabase.CreateAsync();
        var managerId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();

        await using (var context = database.CreateContext(managerId, UserRole.Manager))
        {
            AddUser(context, managerId, UserRole.Manager);
            AddUser(context, employeeId, UserRole.Employee);
            context.Workspaces.Add(new Workspace
            {
                Id = workspaceId,
                Name = "Owned workspace",
                OwnerId = managerId
            });
            context.WorkspaceMembers.Add(new WorkspaceMember
            {
                WorkspaceId = workspaceId,
                UserId = employeeId,
                JoinedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }

        await using var managerContext =
            database.CreateContext(managerId, UserRole.Manager);
        await Assert.ThrowsAsync<ConflictException>(() =>
            CreateWorkspaceService(
                    managerContext,
                    managerId,
                    UserRole.Manager)
                .DeleteAsync(workspaceId));
        Assert.Equal(1, await managerContext.WorkspaceMembers.CountAsync());
    }

    [Fact]
    public async Task WorkspaceMemberCannotBeRemovedWhileBoardMembershipRemains()
    {
        await using var database = await TestDatabase.CreateAsync();
        var boardSeed = await database.SeedCommentBoardAsync();

        await using var context = database.CreateContext(
            boardSeed.OwnerId,
            UserRole.Admin);
        var workspaceId = await context.Boards
            .Where(board => board.Id == boardSeed.BoardId)
            .Select(board => board.WorkspaceId)
            .SingleAsync();
        var service = CreateWorkspaceService(
            context,
            boardSeed.OwnerId,
            UserRole.Admin);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.RemoveMemberAsync(
                workspaceId,
                boardSeed.EditorId));
        Assert.True(await context.WorkspaceMembers.AnyAsync(
            member => member.WorkspaceId == workspaceId &&
                      member.UserId == boardSeed.EditorId));
        Assert.True(await context.BoardMembers.AnyAsync(
            member => member.BoardId == boardSeed.BoardId &&
                      member.UserId == boardSeed.EditorId));
    }

    [Fact]
    public async Task ProjectDeletionConflictsWhenMembersRemain()
    {
        await using var database = await TestDatabase.CreateAsync();
        var managerId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        await using (var context = database.CreateContext(managerId, UserRole.Manager))
        {
            AddUser(context, managerId, UserRole.Manager);
            AddUser(context, employeeId, UserRole.Employee);
            context.Workspaces.Add(new Workspace
            {
                Id = workspaceId,
                Name = "Owned workspace",
                OwnerId = managerId
            });
            context.Projects.Add(new Project
            {
                Id = projectId,
                Name = "Project",
                WorkspaceId = workspaceId,
                ManagerId = managerId,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(1)
            });
            context.ProjectMembers.Add(new ProjectMember
            {
                ProjectId = projectId,
                UserId = employeeId,
                JoinedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }

        await using var managerContext =
            database.CreateContext(managerId, UserRole.Manager);
        await Assert.ThrowsAsync<ConflictException>(() =>
            CreateProjectService(
                    managerContext,
                    managerId,
                    UserRole.Manager)
                .DeleteAsync(projectId));
        Assert.Equal(1, await managerContext.ProjectMembers.CountAsync());
    }

    [Fact]
    public async Task ProjectMemberCannotBeRemovedWhileBoardMembershipRemains()
    {
        await using var database = await TestDatabase.CreateAsync();
        var boardSeed = await database.SeedProjectBoardAsync();

        await using (var setupContext = database.CreateContext(
                         boardSeed.OwnerId,
                         UserRole.Admin))
        {
            setupContext.BoardMembers.Add(new BoardMember
            {
                BoardId = boardSeed.BoardId,
                UserId = boardSeed.ProjectMemberId,
                Role = BoardMemberRole.Editor,
                JoinedAt = DateTime.UtcNow
            });
            await setupContext.SaveChangesAsync();
        }

        await using var context = database.CreateContext(
            boardSeed.OwnerId,
            UserRole.Admin);
        var service = CreateProjectService(
            context,
            boardSeed.OwnerId,
            UserRole.Admin);
        var projectId = await context.Projects
            .Where(project => project.Boards.Any(board => board.Id == boardSeed.BoardId))
            .Select(project => project.Id)
            .SingleAsync();

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.RemoveMemberAsync(
                projectId,
                boardSeed.ProjectMemberId));
        Assert.True(await context.ProjectMembers.AnyAsync(
            member => member.ProjectId == projectId &&
                      member.UserId == boardSeed.ProjectMemberId));
        Assert.True(await context.BoardMembers.AnyAsync(
            member => member.BoardId == boardSeed.BoardId &&
                      member.UserId == boardSeed.ProjectMemberId));
    }

    [Fact]
    public async Task ManagerCannotRemoveEmployeeOutsideTheirManagerEmployeeScope()
    {
        await using var database = await TestDatabase.CreateAsync();
        var managerId = Guid.NewGuid();
        var otherManagerId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();

        await using (var context = database.CreateContext(managerId, UserRole.Manager))
        {
            AddUser(context, managerId, UserRole.Manager);
            AddUser(context, otherManagerId, UserRole.Manager);
            AddUser(context, employeeId, UserRole.Employee);
            context.Workspaces.Add(new Workspace
            {
                Id = workspaceId,
                Name = "Owned workspace",
                OwnerId = managerId
            });
            context.WorkspaceMembers.Add(new WorkspaceMember
            {
                WorkspaceId = workspaceId,
                UserId = employeeId,
                JoinedAt = DateTime.UtcNow
            });
            context.ManagerEmployees.Add(new ManagerEmployee
            {
                ManagerId = otherManagerId,
                EmployeeId = employeeId,
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }

        await using var managerContext =
            database.CreateContext(managerId, UserRole.Manager);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            CreateWorkspaceService(
                    managerContext,
                    managerId,
                    UserRole.Manager)
                .RemoveMemberAsync(workspaceId, employeeId));
        Assert.Equal(1, await managerContext.WorkspaceMembers.CountAsync());
    }

    [Fact]
    public async Task EmployeeCannotManageWorkspaceMembership()
    {
        await using var database = await TestDatabase.CreateAsync();
        var ownerId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();

        await using (var context = database.CreateContext(ownerId, UserRole.Admin))
        {
            AddUser(context, ownerId, UserRole.Admin);
            AddUser(context, employeeId, UserRole.Employee);
            context.Workspaces.Add(new Workspace
            {
                Id = workspaceId,
                Name = "Workspace",
                OwnerId = ownerId
            });
            await context.SaveChangesAsync();
        }

        await using var employeeContext =
            database.CreateContext(employeeId, UserRole.Employee);
        var service = CreateWorkspaceService(
            employeeContext,
            employeeId,
            UserRole.Employee);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.AddMemberAsync(
                workspaceId,
                new FlowBoard.Shared.DTOs.Workspaces.AddWorkspaceMemberRequest
                {
                    UserId = employeeId
                }));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.RemoveMemberAsync(workspaceId, ownerId));
    }

    [Fact]
    public async Task ProjectMembershipAndWorkspaceMembershipDoNotGrantProjectBoardAccess()
    {
        await using var database = await TestDatabase.CreateAsync();
        var boardSeed = await database.SeedProjectBoardAsync();

        await using var projectMemberContext = database.CreateContext(
            boardSeed.ProjectMemberId,
            UserRole.Employee);
        var projectAccess = new BoardAuthorizationService(
            projectMemberContext,
            new TestCurrentUser(boardSeed.ProjectMemberId, UserRole.Employee));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            projectAccess.EnsureCanViewAsync(boardSeed.BoardId));

        await using var workspaceMemberContext = database.CreateContext(
            boardSeed.WorkspaceMemberId,
            UserRole.Employee);
        var workspaceAccess = new BoardAuthorizationService(
            workspaceMemberContext,
            new TestCurrentUser(boardSeed.WorkspaceMemberId, UserRole.Employee));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            workspaceAccess.EnsureCanViewAsync(boardSeed.BoardId));

        await using var assignmentContext = database.CreateContext(
            boardSeed.OwnerId,
            UserRole.Admin);
        var assignmentGuard = new TaskAssignmentGuard(
            assignmentContext,
            new TestCurrentUser(boardSeed.OwnerId, UserRole.Admin));
        await Assert.ThrowsAsync<ConflictException>(() =>
            assignmentGuard.ValidateAssigneeAsync(
                boardSeed.BoardId,
                boardSeed.BoardOnlyMemberId));
    }

    [Fact]
    public async Task EmployeeCanAssignOnlyThemselvesAndManagerCanAddOnlyDirectReportsToBoard()
    {
        await using var database = await TestDatabase.CreateAsync();
        var commentBoard = await database.SeedCommentBoardAsync();

        await using (var employeeContext = database.CreateContext(
                         commentBoard.EditorId,
                         UserRole.Employee))
        {
            var guard = new TaskAssignmentGuard(
                employeeContext,
                new TestCurrentUser(commentBoard.EditorId, UserRole.Employee));
            await guard.ValidateAssigneeAsync(
                commentBoard.BoardId,
                commentBoard.EditorId);
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                guard.ValidateAssigneeAsync(
                    commentBoard.BoardId,
                    commentBoard.ViewerId));
        }

        var managerSeed = await database.SeedManagerProjectBoardAsync();
        await using (var managerContext = database.CreateContext(
                         managerSeed.ManagerId,
                         UserRole.Manager))
        {
            var manager = new TestCurrentUser(
                managerSeed.ManagerId,
                UserRole.Manager);
            var service = CreateBoardMemberService(managerContext, manager);
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                service.AddAsync(
                    managerSeed.BoardId,
                    new AddBoardMemberRequest
                    {
                        UserId = managerSeed.EmployeeId,
                        Role = FlowBoard.Shared.Enums.BoardMemberRole.Editor
                    }));
        }

        await using (var adminContext =
                     database.CreateContext(managerSeed.ManagerId, UserRole.Admin))
        {
            adminContext.ManagerEmployees.Add(new ManagerEmployee
            {
                ManagerId = managerSeed.ManagerId,
                EmployeeId = managerSeed.EmployeeId,
                CreatedAt = DateTime.UtcNow
            });
            await adminContext.SaveChangesAsync();
        }

        await using var managerContextWithDirectReport =
            database.CreateContext(managerSeed.ManagerId, UserRole.Manager);
        var managerWithDirectReport = new TestCurrentUser(
            managerSeed.ManagerId,
            UserRole.Manager);
        var added = await CreateBoardMemberService(
                managerContextWithDirectReport,
                managerWithDirectReport)
            .AddAsync(
                managerSeed.BoardId,
                new AddBoardMemberRequest
                {
                    UserId = managerSeed.EmployeeId,
                    Role = FlowBoard.Shared.Enums.BoardMemberRole.Editor
                });
        Assert.Equal(managerSeed.EmployeeId, added.UserId);
    }

    [Fact]
    public async Task BoardCannotBeDeletedWhileColumnsOrMembersExist()
    {
        await using var database = await TestDatabase.CreateAsync();
        var boardSeed = await database.SeedCommentBoardAsync();

        await using var context = database.CreateContext(
            boardSeed.OwnerId,
            UserRole.Admin);
        var currentUser = new TestCurrentUser(
            boardSeed.OwnerId,
            UserRole.Admin);
        var service = new BoardService(
            context,
            currentUser,
            new BoardAuthorizationService(context, currentUser));

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.DeleteAsync(boardSeed.BoardId));

        Assert.True(await context.Boards.AnyAsync(
            board => board.Id == boardSeed.BoardId));
        Assert.True(await context.BoardColumns.AnyAsync(
            column => column.BoardId == boardSeed.BoardId));
    }

    [Fact]
    public async Task ColumnCannotBeDeletedWhileCardsExist()
    {
        await using var database = await TestDatabase.CreateAsync();
        var boardSeed = await database.SeedCommentBoardAsync();

        await using var context = database.CreateContext(
            boardSeed.EditorId,
            UserRole.Employee);
        var service = new BoardColumnService(
            context,
            new BoardAuthorizationService(
                context,
                new TestCurrentUser(boardSeed.EditorId, UserRole.Employee)));

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.DeleteAsync(boardSeed.BoardId, boardSeed.ColumnId));

        Assert.True(await context.BoardColumns.AnyAsync(
            column => column.Id == boardSeed.ColumnId));
        Assert.True(await context.Cards.AnyAsync(
            card => card.Id == boardSeed.CardId));
    }

    [Fact]
    public async Task CardCannotBeDeletedWhileCommentsExist()
    {
        await using var database = await TestDatabase.CreateAsync();
        var boardSeed = await database.SeedCommentBoardAsync();

        await using var context = database.CreateContext(
            boardSeed.EditorId,
            UserRole.Employee);
        await CreateCommentService(
                context,
                boardSeed.EditorId,
                UserRole.Employee)
            .AddAsync(
                boardSeed.CardId,
                new CreateCardCommentRequest { Content = "Keep this comment" });

        var currentUser = new TestCurrentUser(
            boardSeed.EditorId,
            UserRole.Employee);
        var service = new CardService(
            context,
            new BoardAuthorizationService(context, currentUser),
            new TaskAssignmentGuard(context, currentUser));

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.DeleteAsync(boardSeed.CardId));

        Assert.True(await context.Cards.AnyAsync(
            card => card.Id == boardSeed.CardId));
        Assert.Single(await context.CardComments.ToListAsync());
    }

    [Fact]
    public async Task CommentAuthorCanCreateAndEditWithinWindowButOtherMemberCannotEdit()
    {
        await using var database = await TestDatabase.CreateAsync();
        var boardSeed = await database.SeedCommentBoardAsync();
        Guid commentId;

        await using (var authorContext = database.CreateContext(
                         boardSeed.EditorId,
                         UserRole.Employee))
        {
            var service = CreateCommentService(
                authorContext,
                boardSeed.EditorId,
                UserRole.Employee);
            var comment = await service.AddAsync(
                boardSeed.CardId,
                new CreateCardCommentRequest { Content = "Initial note" });

            Assert.Equal(boardSeed.EditorId, comment.UserId);
            Assert.True(await service.GetByCardAsync(boardSeed.CardId) is { Count: 1 });
            await service.UpdateAsync(
                comment.Id,
                new UpdateCardCommentRequest { Content = "Updated note" });
            commentId = comment.Id;
        }

        await using (var viewerContext = database.CreateContext(
                         boardSeed.ViewerId,
                         UserRole.Employee))
        {
            var service = CreateCommentService(
                viewerContext,
                boardSeed.ViewerId,
                UserRole.Employee);
            var comment = Assert.Single(await service.GetByCardAsync(boardSeed.CardId));
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                service.UpdateAsync(
                    comment.Id,
                    new UpdateCardCommentRequest { Content = "Impersonated edit" }));
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                service.DeleteAsync(comment.Id));
        }

        await using (var adminContext = database.CreateContext(
                         boardSeed.OwnerId,
                         UserRole.Admin))
        {
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                CreateCommentService(
                        adminContext,
                        boardSeed.OwnerId,
                        UserRole.Admin)
                    .DeleteAsync(commentId));
        }

        await using var authorDeleteContext = database.CreateContext(
            boardSeed.EditorId,
            UserRole.Employee);
        await CreateCommentService(
                authorDeleteContext,
                boardSeed.EditorId,
                UserRole.Employee)
            .DeleteAsync(commentId);
        Assert.Empty(await authorDeleteContext.CardComments.ToListAsync());
    }

    [Fact]
    public async Task AuthorizedBoardViewerCanCreateCommentsAsAuthenticatedUser()
    {
        await using var database = await TestDatabase.CreateAsync();
        var boardSeed = await database.SeedCommentBoardAsync();

        await using var viewerContext = database.CreateContext(
            boardSeed.ViewerId,
            UserRole.Employee);
        var service = CreateCommentService(
            viewerContext,
            boardSeed.ViewerId,
            UserRole.Employee);
        var comment = await service.AddAsync(
            boardSeed.CardId,
            new CreateCardCommentRequest { Content = "Viewer feedback" });

        Assert.Equal(boardSeed.ViewerId, comment.UserId);
        Assert.Equal(
            "Viewer feedback",
            Assert.Single(await service.GetByCardAsync(boardSeed.CardId)).Content);
    }

    [Fact]
    public async Task CommentAuthorCannotEditAfterThreeMinutes()
    {
        await using var database = await TestDatabase.CreateAsync();
        var boardSeed = await database.SeedCommentBoardAsync();
        Guid commentId;

        await using (var context = database.CreateContext(
                         boardSeed.EditorId,
                         UserRole.Employee))
        {
            var service = CreateCommentService(
                context,
                boardSeed.EditorId,
                UserRole.Employee);
            var comment = await service.AddAsync(
                boardSeed.CardId,
                new CreateCardCommentRequest { Content = "Old note" });
            commentId = comment.Id;
        }

        await using (var context = database.CreateContext(
                         boardSeed.EditorId,
                         UserRole.Employee))
        {
            var oldCreatedAt = DateTime.UtcNow.AddMinutes(-4);
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"CardComments\" SET \"CreatedAt\" = {oldCreatedAt} WHERE \"Id\" = {commentId}");
        }

        await using var authorContext = database.CreateContext(
            boardSeed.EditorId,
            UserRole.Employee);
        await Assert.ThrowsAsync<ConflictException>(() =>
            CreateCommentService(
                    authorContext,
                    boardSeed.EditorId,
                    UserRole.Employee)
                .UpdateAsync(
                    commentId,
                    new UpdateCardCommentRequest { Content = "Too late" }));
    }

    [Fact]
    public async Task UserWithoutBoardAccessCannotReadComments()
    {
        await using var database = await TestDatabase.CreateAsync();
        var boardSeed = await database.SeedCommentBoardAsync();

        Guid commentId;
        await using (var authorContext = database.CreateContext(
                         boardSeed.EditorId,
                         UserRole.Employee))
        {
            var comment = await CreateCommentService(
                    authorContext,
                    boardSeed.EditorId,
                    UserRole.Employee)
                .AddAsync(
                    boardSeed.CardId,
                    new CreateCardCommentRequest { Content = "Private board note" });
            commentId = comment.Id;
        }

        await using var outsiderContext = database.CreateContext(
            boardSeed.OutsiderId,
            UserRole.Employee);
        var outsiderService = CreateCommentService(
            outsiderContext,
            boardSeed.OutsiderId,
            UserRole.Employee);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            outsiderService.GetByCardAsync(boardSeed.CardId));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            outsiderService.UpdateAsync(
                commentId,
                new UpdateCardCommentRequest { Content = "Unauthorized edit" }));
    }

    private static ProjectService CreateProjectService(
        FlowBoardDbContext context,
        Guid userId,
        UserRole role)
    {
        var currentUser = new TestCurrentUser(userId, role);
        return new ProjectService(
            context,
            currentUser,
            new TaskAssignmentGuard(context, currentUser));
    }

    private static WorkspaceService CreateWorkspaceService(
        FlowBoardDbContext context,
        Guid userId,
        UserRole role)
    {
        var currentUser = new TestCurrentUser(userId, role);
        return new WorkspaceService(
            context,
            currentUser,
            new TaskAssignmentGuard(context, currentUser));
    }

    private static CardCommentService CreateCommentService(
        FlowBoardDbContext context,
        Guid userId,
        UserRole role)
    {
        var currentUser = new TestCurrentUser(userId, role);
        return new CardCommentService(
            context,
            currentUser,
            new BoardAuthorizationService(context, currentUser));
    }

    private static BoardMemberService CreateBoardMemberService(
        FlowBoardDbContext context,
        TestCurrentUser currentUser)
    {
        var authorization = new BoardAuthorizationService(context, currentUser);
        return new BoardMemberService(
            context,
            currentUser,
            authorization,
            new TaskAssignmentGuard(context, currentUser));
    }

    private static AuthService CreateAuthService(
        FlowBoardDbContext context,
        Guid userId,
        UserRole role)
    {
        return new AuthService(
            context,
            new UnusedPasswordHasher(),
            new UnusedTokenService(),
            Options.Create(new JwtSettings()),
            new TestCurrentUser(userId, role));
    }

    private static CreateProjectRequest ProjectRequest(
        Guid workspaceId,
        Guid? managerId) =>
        new()
        {
            Name = "Test project",
            WorkspaceId = workspaceId,
            ManagerId = managerId,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddMonths(1)
        };

    private static void AddUser(
        FlowBoardDbContext context,
        Guid userId,
        UserRole role)
    {
        context.Users.Add(new User
        {
            Id = userId,
            Email = $"{userId:N}@example.test",
            PasswordHash = "not-used",
            FirstName = "Test",
            LastName = "User",
            Role = role
        });
    }

    private sealed class TestCurrentUser(Guid userId, UserRole role)
        : ICurrentUserService
    {
        public Guid UserId { get; } = userId;

        public string? Email => null;

        public string? Role { get; } = role.ToString();

        public bool IsAuthenticated => true;
    }

    private sealed class UnusedPasswordHasher : IPasswordHasher
    {
        public string Hash(string password) =>
            throw new NotSupportedException();

        public bool Verify(string password, string passwordHash) =>
            throw new NotSupportedException();
    }

    private sealed class UnusedTokenService : ITokenService
    {
        public string GenerateAccessToken(Guid userId, string email, string role) =>
            throw new NotSupportedException();

        public string GenerateRefreshToken() =>
            throw new NotSupportedException();
    }

    private sealed record ProjectBoardSeed(
        Guid OwnerId,
        Guid ProjectMemberId,
        Guid WorkspaceMemberId,
        Guid BoardOnlyMemberId,
        Guid BoardId);

    private sealed record CommentBoardSeed(
        Guid OwnerId,
        Guid EditorId,
        Guid ViewerId,
        Guid OutsiderId,
        Guid BoardId,
        Guid ColumnId,
        Guid CardId);

    private sealed record ManagerProjectBoardSeed(
        Guid ManagerId,
        Guid EmployeeId,
        Guid BoardId);

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
            await using var context = database.CreateContext(Guid.Empty, UserRole.Admin);
            await context.Database.EnsureCreatedAsync();
            return database;
        }

        public FlowBoardDbContext CreateContext(Guid userId, UserRole role)
        {
            var options = new DbContextOptionsBuilder<FlowBoardDbContext>()
                .UseSqlite(_connection)
                .Options;
            return new FlowBoardDbContext(
                options,
                new TestCurrentUser(userId, role));
        }

        public async Task<ProjectBoardSeed> SeedProjectBoardAsync()
        {
            var ownerId = Guid.NewGuid();
            var projectMemberId = Guid.NewGuid();
            var workspaceMemberId = Guid.NewGuid();
            var boardOnlyMemberId = Guid.NewGuid();
            var workspaceId = Guid.NewGuid();
            var projectId = Guid.NewGuid();
            var boardId = Guid.NewGuid();

            await using var context = CreateContext(ownerId, UserRole.Admin);
            AddUser(context, ownerId, UserRole.Admin);
            AddUser(context, projectMemberId, UserRole.Employee);
            AddUser(context, workspaceMemberId, UserRole.Employee);
            AddUser(context, boardOnlyMemberId, UserRole.Employee);
            context.Workspaces.Add(new Workspace
            {
                Id = workspaceId,
                Name = "Project workspace",
                OwnerId = ownerId
            });
            context.WorkspaceMembers.Add(new WorkspaceMember
            {
                WorkspaceId = workspaceId,
                UserId = workspaceMemberId,
                JoinedAt = DateTime.UtcNow
            });
            context.Projects.Add(new Project
            {
                Id = projectId,
                Name = "Project",
                WorkspaceId = workspaceId,
                ManagerId = ownerId,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(1)
            });
            context.ProjectMembers.Add(new ProjectMember
            {
                ProjectId = projectId,
                UserId = projectMemberId,
                JoinedAt = DateTime.UtcNow
            });
            context.Boards.Add(new Board
            {
                Id = boardId,
                Name = "Project board",
                OwnerId = ownerId,
                WorkspaceId = workspaceId,
                ProjectId = projectId,
                Purpose = BoardPurpose.Project
            });
            context.BoardMembers.Add(new BoardMember
            {
                BoardId = boardId,
                UserId = boardOnlyMemberId,
                Role = BoardMemberRole.Editor,
                JoinedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            return new ProjectBoardSeed(
                ownerId,
                projectMemberId,
                workspaceMemberId,
                boardOnlyMemberId,
                boardId);
        }

        public async Task<CommentBoardSeed> SeedCommentBoardAsync()
        {
            var ownerId = Guid.NewGuid();
            var editorId = Guid.NewGuid();
            var viewerId = Guid.NewGuid();
            var outsiderId = Guid.NewGuid();
            var workspaceId = Guid.NewGuid();
            var boardId = Guid.NewGuid();
            var columnId = Guid.NewGuid();
            var cardId = Guid.NewGuid();

            await using var context = CreateContext(ownerId, UserRole.Admin);
            AddUser(context, ownerId, UserRole.Admin);
            AddUser(context, editorId, UserRole.Employee);
            AddUser(context, viewerId, UserRole.Employee);
            AddUser(context, outsiderId, UserRole.Employee);
            context.Workspaces.Add(new Workspace
            {
                Id = workspaceId,
                Name = "Comment workspace",
                OwnerId = ownerId
            });
            context.Boards.Add(new Board
            {
                Id = boardId,
                Name = "Comment board",
                OwnerId = ownerId,
                WorkspaceId = workspaceId,
                Purpose = BoardPurpose.GeneralLearning
            });
            context.BoardMembers.AddRange(
                new BoardMember
                {
                    BoardId = boardId,
                    UserId = editorId,
                    Role = BoardMemberRole.Editor,
                    JoinedAt = DateTime.UtcNow
                },
                new BoardMember
                {
                    BoardId = boardId,
                    UserId = viewerId,
                    Role = BoardMemberRole.Viewer,
                    JoinedAt = DateTime.UtcNow
                });
            context.WorkspaceMembers.AddRange(
                new WorkspaceMember
                {
                    WorkspaceId = workspaceId,
                    UserId = editorId,
                    JoinedAt = DateTime.UtcNow
                },
                new WorkspaceMember
                {
                    WorkspaceId = workspaceId,
                    UserId = viewerId,
                    JoinedAt = DateTime.UtcNow
                });
            context.BoardColumns.Add(new BoardColumn
            {
                Id = columnId,
                BoardId = boardId,
                Name = "To Do",
                Position = 1
            });
            context.Cards.Add(new Card
            {
                Id = cardId,
                BoardColumnId = columnId,
                Title = "Comment test card",
                Priority = CardPriority.Medium,
                Position = 1
            });
            await context.SaveChangesAsync();

            return new CommentBoardSeed(
                ownerId,
                editorId,
                viewerId,
                outsiderId,
                boardId,
                columnId,
                cardId);
        }

        public async Task<ManagerProjectBoardSeed> SeedManagerProjectBoardAsync()
        {
            var managerId = Guid.NewGuid();
            var employeeId = Guid.NewGuid();
            var workspaceId = Guid.NewGuid();
            var projectId = Guid.NewGuid();
            var boardId = Guid.NewGuid();

            await using var context = CreateContext(managerId, UserRole.Manager);
            AddUser(context, managerId, UserRole.Manager);
            AddUser(context, employeeId, UserRole.Employee);
            context.Workspaces.Add(new Workspace
            {
                Id = workspaceId,
                Name = "Managed workspace",
                OwnerId = managerId
            });
            context.Projects.Add(new Project
            {
                Id = projectId,
                Name = "Managed project",
                WorkspaceId = workspaceId,
                ManagerId = managerId,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(1)
            });
            context.ProjectMembers.Add(new ProjectMember
            {
                ProjectId = projectId,
                UserId = employeeId,
                JoinedAt = DateTime.UtcNow
            });
            context.Boards.Add(new Board
            {
                Id = boardId,
                Name = "Managed board",
                OwnerId = managerId,
                WorkspaceId = workspaceId,
                ProjectId = projectId,
                Purpose = BoardPurpose.Project
            });
            await context.SaveChangesAsync();

            return new ManagerProjectBoardSeed(managerId, employeeId, boardId);
        }

        public ValueTask DisposeAsync() => _connection.DisposeAsync();
    }
}

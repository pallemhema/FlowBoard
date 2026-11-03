using FlowBoard.Api.Exceptions;
using FlowBoard.Api.Services.BoardAuthorization;
using FlowBoard.Api.Services.Authentication;
using FlowBoard.Api.Services.Boards;
using FlowBoard.Api.Services.BoardColumns;
using FlowBoard.Api.Services.Cards;
using FlowBoard.Api.Services.BoardMembers;
using FlowBoard.Api.Services.CardComments;
using FlowBoard.Api.Services.Projects;
using FlowBoard.Api.Services.Teams;
using FlowBoard.Api.Services.MyTasks;
using FlowBoard.Api.Services.ManagerEmployees;
using FlowBoard.Api.Services.TaskAssignments;
using FlowBoard.Api.Services.Workspaces;
using FlowBoard.Domain.Entities;
using FlowBoard.Domain.Enums;
using FlowBoard.Api.Configuration;
using FlowBoard.Infrastructure.Data;
using FlowBoard.Shared.DTOs.Boards;
using FlowBoard.Shared.DTOs.BoardMembers;
using FlowBoard.Shared.DTOs.CardComments;
using FlowBoard.Shared.DTOs.Cards;
using FlowBoard.Shared.DTOs.Common;
using FlowBoard.Shared.DTOs.MyTasks;
using FlowBoard.Shared.DTOs.ManagerEmployees;
using FlowBoard.Shared.DTOs.Projects;
using FlowBoard.Shared.DTOs.Teams;
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
    public async Task AdminCanReassignProjectToAccessibleManagerButOtherRolesCannot()
    {
        await using var database = await TestDatabase.CreateAsync();
        var adminId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var replacementManagerId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();

        await using (var context = database.CreateContext(adminId, UserRole.Admin))
        {
            AddUser(context, adminId, UserRole.Admin);
            AddUser(context, managerId, UserRole.Manager);
            AddUser(context, replacementManagerId, UserRole.Manager);
            AddUser(context, employeeId, UserRole.Employee);
            context.Workspaces.Add(new Workspace
            {
                Id = workspaceId,
                Name = "Engineering",
                OwnerId = adminId
            });
            context.WorkspaceMembers.AddRange(
                new WorkspaceMember
                {
                    WorkspaceId = workspaceId,
                    UserId = managerId,
                    JoinedAt = DateTime.UtcNow
                },
                new WorkspaceMember
                {
                    WorkspaceId = workspaceId,
                    UserId = replacementManagerId,
                    JoinedAt = DateTime.UtcNow
                });
            await context.SaveChangesAsync();
        }

        Guid projectId;
        await using (var context = database.CreateContext(adminId, UserRole.Admin))
        {
            var project = await CreateProjectService(
                    context,
                    adminId,
                    UserRole.Admin)
                .CreateAsync(ProjectRequest(workspaceId, managerId));
            projectId = project.ProjectId;

            var updated = await CreateProjectService(
                    context,
                    adminId,
                    UserRole.Admin)
                .UpdateAsync(
                    projectId,
                    new UpdateProjectRequest
                    {
                        Name = project.Name,
                        ManagerId = replacementManagerId,
                        Status = FlowBoard.Shared.Enums.ProjectStatus.Active,
                        StartDate = project.StartDate,
                        EndDate = project.EndDate
                    });

            Assert.Equal(replacementManagerId, updated.ManagerId);
            await Assert.ThrowsAsync<ValidationException>(() =>
                CreateProjectService(context, adminId, UserRole.Admin)
                    .UpdateAsync(
                        projectId,
                        new UpdateProjectRequest
                        {
                            Name = project.Name,
                            ManagerId = employeeId,
                            Status = FlowBoard.Shared.Enums.ProjectStatus.Active,
                            StartDate = project.StartDate,
                            EndDate = project.EndDate
                        }));
        }

        await using var managerContext =
            database.CreateContext(replacementManagerId, UserRole.Manager);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            CreateProjectService(
                    managerContext,
                    replacementManagerId,
                    UserRole.Manager)
                .UpdateAsync(
                    projectId,
                    new UpdateProjectRequest
                    {
                        Name = "Reassigned by manager",
                        ManagerId = managerId,
                        Status = FlowBoard.Shared.Enums.ProjectStatus.Active,
                        StartDate = DateTime.UtcNow,
                        EndDate = DateTime.UtcNow.AddDays(1)
                    }));
    }

    [Fact]
    public async Task TeamMembershipIsEmployeeOnlyUniqueAndBlocksDeletingNonemptyTeam()
    {
        await using var database = await TestDatabase.CreateAsync();
        var adminId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var teamId = Guid.NewGuid();

        await using (var context = database.CreateContext(adminId, UserRole.Admin))
        {
            AddUser(context, adminId, UserRole.Admin);
            AddUser(context, employeeId, UserRole.Employee);
            AddUser(context, managerId, UserRole.Manager);
            context.Teams.Add(new Team
            {
                Id = teamId,
                Name = "Platform"
            });
            await context.SaveChangesAsync();
        }

        await using var adminContext =
            database.CreateContext(adminId, UserRole.Admin);
        var service = new TeamService(
            adminContext,
            new TestCurrentUser(adminId, UserRole.Admin));

        await service.AddMemberAsync(
            teamId,
            new AddTeamMemberRequest { UserId = employeeId });

        var members = await service.GetMembersAsync(
            teamId,
            new PagedSearchRequest());
        Assert.Equal(nameof(UserRole.Employee), Assert.Single(members.Items).User.Role);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.AddMemberAsync(
                teamId,
                new AddTeamMemberRequest { UserId = employeeId }));
        await Assert.ThrowsAsync<ValidationException>(() =>
            service.AddMemberAsync(
                teamId,
                new AddTeamMemberRequest { UserId = managerId }));
        await Assert.ThrowsAsync<ValidationException>(() =>
            service.AddMemberAsync(
                teamId,
                new AddTeamMemberRequest { UserId = adminId }));
        await Assert.ThrowsAsync<ConflictException>(() =>
            service.DeleteAsync(teamId));
    }

        [Fact]
        public async Task AdminCanAssignAndRemoveOnlyValidManagerEmployeeRelationships()
        {
            await using var database = await TestDatabase.CreateAsync();
            var adminId = Guid.NewGuid();
            var managerId = Guid.NewGuid();
            var otherManagerId = Guid.NewGuid();
            var employeeId = Guid.NewGuid();
            var otherEmployeeId = Guid.NewGuid();

            await using (var context = database.CreateContext(adminId, UserRole.Admin))
            {
                AddUser(context, adminId, UserRole.Admin);
                AddUser(context, managerId, UserRole.Manager);
                AddUser(context, otherManagerId, UserRole.Manager);
                AddUser(context, employeeId, UserRole.Employee);
                AddUser(context, otherEmployeeId, UserRole.Employee);
                await context.SaveChangesAsync();
            }

            await using var adminContext =
                database.CreateContext(adminId, UserRole.Admin);
            var service = new ManagerEmployeeService(
                adminContext,
                new TestCurrentUser(adminId, UserRole.Admin));

            var relationship = await service.AssignAsync(
                managerId,
                new AssignEmployeeToManagerRequest { EmployeeId = employeeId });

            Assert.Equal(managerId, relationship.ManagerId);
            Assert.Equal(employeeId, relationship.EmployeeId);
            await Assert.ThrowsAsync<ConflictException>(() =>
                service.AssignAsync(
                    managerId,
                    new AssignEmployeeToManagerRequest { EmployeeId = employeeId }));
            await Assert.ThrowsAsync<ConflictException>(() =>
                service.AssignAsync(
                    otherManagerId,
                    new AssignEmployeeToManagerRequest { EmployeeId = employeeId }));
            await Assert.ThrowsAsync<ValidationException>(() =>
                service.AssignAsync(
                    employeeId,
                    new AssignEmployeeToManagerRequest { EmployeeId = managerId }));

            await service.RemoveAsync(managerId, employeeId);
            var reassigned = await service.AssignAsync(
                otherManagerId,
                new AssignEmployeeToManagerRequest { EmployeeId = employeeId });
            var differentEmployee = await service.AssignAsync(
                managerId,
                new AssignEmployeeToManagerRequest { EmployeeId = otherEmployeeId });

            Assert.Equal(otherManagerId, reassigned.ManagerId);
            Assert.Equal(managerId, differentEmployee.ManagerId);
            Assert.Equal(
                new[] { employeeId, otherEmployeeId }.OrderBy(id => id),
                (await adminContext.ManagerEmployees
                    .Select(item => item.EmployeeId)
                    .ToListAsync()).OrderBy(id => id));
        }

        [Fact]
        public async Task EmployeeIdUniqueIndexRejectsDirectDuplicateRelationshipInSqlite()
        {
            await using var database = await TestDatabase.CreateAsync();
            var adminId = Guid.NewGuid();
            var managerId = Guid.NewGuid();
            var otherManagerId = Guid.NewGuid();
            var employeeId = Guid.NewGuid();

            await using var context = database.CreateContext(adminId, UserRole.Admin);
            AddUser(context, adminId, UserRole.Admin);
            AddUser(context, managerId, UserRole.Manager);
            AddUser(context, otherManagerId, UserRole.Manager);
            AddUser(context, employeeId, UserRole.Employee);
            context.ManagerEmployees.Add(new ManagerEmployee
            {
                ManagerId = managerId,
                EmployeeId = employeeId,
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            context.ManagerEmployees.Add(new ManagerEmployee
            {
                ManagerId = otherManagerId,
                EmployeeId = employeeId,
                CreatedAt = DateTime.UtcNow
            });

            await Assert.ThrowsAsync<DbUpdateException>(
                () => context.SaveChangesAsync());
        }

        [Fact]
        public async Task ManagersAndEmployeesSeeOnlyTheirManagerEmployeeRelationships()
        {
            await using var database = await TestDatabase.CreateAsync();
            var adminId = Guid.NewGuid();
            var seniorManagerId = Guid.NewGuid();
            var managerId = Guid.NewGuid();
            var employeeId = Guid.NewGuid();

            await using (var context = database.CreateContext(adminId, UserRole.Admin))
            {
                AddUser(context, adminId, UserRole.Admin);
                AddUser(context, seniorManagerId, UserRole.Manager);
                AddUser(context, managerId, UserRole.Manager);
                AddUser(context, employeeId, UserRole.Employee);
                context.ManagerEmployees.Add(new ManagerEmployee
                {
                    ManagerId = seniorManagerId,
                    EmployeeId = managerId,
                    CreatedAt = DateTime.UtcNow
                });
                await context.SaveChangesAsync();
            }

            await using (var adminContext = database.CreateContext(adminId, UserRole.Admin))
            {
                await new ManagerEmployeeService(
                        adminContext,
                        new TestCurrentUser(adminId, UserRole.Admin))
                    .AssignAsync(
                        managerId,
                        new AssignEmployeeToManagerRequest { EmployeeId = employeeId });
            }

            await using (var managerContext = database.CreateContext(managerId, UserRole.Manager))
            {
                var managerService = new ManagerEmployeeService(
                    managerContext,
                    new TestCurrentUser(managerId, UserRole.Manager));
                var employees = await managerService.GetManagedEmployeesAsync(
                    new PagedRequest());

                Assert.Single(employees.Items);
                Assert.Equal(employeeId, employees.Items[0].EmployeeId);
                Assert.Equal(nameof(UserRole.Employee), employees.Items[0].Employee.Role);

                var reportingManagers = await managerService.GetMyReportingManagersAsync(
                    new PagedRequest());
                Assert.Single(reportingManagers.Items);
                Assert.Equal(seniorManagerId, reportingManagers.Items[0].ManagerId);
                Assert.Equal(nameof(UserRole.Manager), reportingManagers.Items[0].Manager.Role);
            }

            await using (var employeeContext =
                         database.CreateContext(employeeId, UserRole.Employee))
            {
                var employeeService = new ManagerEmployeeService(
                    employeeContext,
                    new TestCurrentUser(employeeId, UserRole.Employee));
                var managers = await employeeService.GetMyManagersAsync(
                    new PagedRequest());

                Assert.Single(managers.Items);
                Assert.Equal(managerId, managers.Items[0].ManagerId);
                Assert.Equal(nameof(UserRole.Manager), managers.Items[0].Manager.Role);
            }

            await using var managerReadContext =
                database.CreateContext(managerId, UserRole.Manager);
            var remainingRelationships = await managerReadContext.ManagerEmployees.ToListAsync();
            Assert.Equal(2, remainingRelationships.Count);
            Assert.Contains(
                remainingRelationships,
                relationship => relationship.ManagerId == managerId &&
                                relationship.EmployeeId == employeeId);
            Assert.Contains(
                remainingRelationships,
                relationship => relationship.ManagerId == seniorManagerId &&
                                relationship.EmployeeId == managerId);
        }

    [Fact]
    public async Task EmployeeReportingGroupReturnsOnlyCoworkersSharingTheirManager()
    {
        await using var database = await TestDatabase.CreateAsync();
        var adminId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var otherManagerId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var colleagueId = Guid.NewGuid();
        var unrelatedEmployeeId = Guid.NewGuid();

        await using (var context = database.CreateContext(adminId, UserRole.Admin))
        {
            AddUser(context, adminId, UserRole.Admin);
            AddUser(context, managerId, UserRole.Manager);
            AddUser(context, otherManagerId, UserRole.Manager);
            AddUser(context, employeeId, UserRole.Employee);
            AddUser(context, colleagueId, UserRole.Employee);
            AddUser(context, unrelatedEmployeeId, UserRole.Employee);
            context.ManagerEmployees.AddRange(
                new ManagerEmployee
                {
                    ManagerId = managerId,
                    EmployeeId = employeeId,
                    CreatedAt = DateTime.UtcNow
                },
                new ManagerEmployee
                {
                    ManagerId = managerId,
                    EmployeeId = colleagueId,
                    CreatedAt = DateTime.UtcNow
                },
                new ManagerEmployee
                {
                    ManagerId = otherManagerId,
                    EmployeeId = unrelatedEmployeeId,
                    CreatedAt = DateTime.UtcNow
                });
            await context.SaveChangesAsync();
        }

        await using var employeeContext =
            database.CreateContext(employeeId, UserRole.Employee);
        var employeeService = new ManagerEmployeeService(
            employeeContext,
            new TestCurrentUser(employeeId, UserRole.Employee));
        var reportingGroup = await employeeService.GetMyReportingGroupAsync(
            new PagedRequest());

        var colleague = Assert.Single(reportingGroup.Items);
        Assert.Equal(colleagueId, colleague.EmployeeId);
        Assert.Equal(nameof(UserRole.Employee), colleague.Role);
        Assert.DoesNotContain(
            reportingGroup.Items,
            member => member.EmployeeId == employeeId ||
                      member.EmployeeId == unrelatedEmployeeId);

        employeeContext.ManagerEmployees.Add(new ManagerEmployee
        {
            ManagerId = otherManagerId,
            EmployeeId = employeeId,
            CreatedAt = DateTime.UtcNow
        });
        await Assert.ThrowsAsync<DbUpdateException>(
            () => employeeContext.SaveChangesAsync());

        await using var managerContext =
            database.CreateContext(managerId, UserRole.Manager);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new ManagerEmployeeService(
                    managerContext,
                    new TestCurrentUser(managerId, UserRole.Manager))
                .GetMyReportingGroupAsync(new PagedRequest()));
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
        var adminId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();

        await using (var context = database.CreateContext(adminId, UserRole.Admin))
        {
            AddUser(context, adminId, UserRole.Admin);
            AddUser(context, employeeId, UserRole.Employee);
            context.Workspaces.Add(new Workspace
            {
                Id = workspaceId,
                Name = "Workspace",
                OwnerId = adminId
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
            CreateWorkspaceService(
                    employeeContext,
                    employeeId,
                    UserRole.Employee)
                .CreateAsync(new CreateWorkspaceRequest
                {
                    Name = "Unauthorized"
                }));
    }

    [Fact]
    public async Task EmployeeMyProjectsReturnsOnlyWorkspaceAccessibleMembershipsAndRejectsOtherProjectDetails()
    {
        await using var database = await TestDatabase.CreateAsync();
        var boardSeed = await database.SeedProjectBoardAsync();

        Guid projectId;
        await using (var setupContext = database.CreateContext(
                         boardSeed.OwnerId,
                         UserRole.Admin))
        {
            projectId = await setupContext.Boards
                .Where(board => board.Id == boardSeed.BoardId)
                .Select(board => board.ProjectId!.Value)
                .SingleAsync();
            setupContext.WorkspaceMembers.Add(new WorkspaceMember
            {
                WorkspaceId = await setupContext.Boards
                    .Where(board => board.Id == boardSeed.BoardId)
                    .Select(board => board.WorkspaceId)
                    .SingleAsync(),
                UserId = boardSeed.ProjectMemberId,
                JoinedAt = DateTime.UtcNow
            });
            await setupContext.SaveChangesAsync();
        }

        await using (var employeeContext = database.CreateContext(
                         boardSeed.ProjectMemberId,
                         UserRole.Employee))
        {
            var service = CreateProjectService(
                employeeContext,
                boardSeed.ProjectMemberId,
                UserRole.Employee);
            var projects = await service.GetMineAsync(new ProjectQueryRequest());

            var project = Assert.Single(projects.Items);
            Assert.Equal(projectId, project.ProjectId);
            Assert.Equal("Project", project.Name);
            Assert.Equal("Project workspace", project.WorkspaceName);
            Assert.Equal(
                FlowBoard.Shared.Enums.ProjectStatus.Active,
                project.Status);
        }

        await using var workspaceOnlyContext = database.CreateContext(
            boardSeed.WorkspaceMemberId,
            UserRole.Employee);
        var workspaceOnlyService = CreateProjectService(
            workspaceOnlyContext,
            boardSeed.WorkspaceMemberId,
            UserRole.Employee);

        var workspaceOnlyProjects = await workspaceOnlyService.GetMineAsync(
            new ProjectQueryRequest());
        Assert.Empty(workspaceOnlyProjects.Items);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            workspaceOnlyService.GetByIdAsync(projectId));
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
            Assert.Equal(nameof(UserRole.Employee), user.Role);
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
            Assert.Contains(users.Items, user =>
                user.UserId == adminId &&
                user.Role == nameof(UserRole.Admin));
            Assert.Contains(users.Items, user =>
                user.UserId == managerId &&
                user.Role == nameof(UserRole.Manager));
            Assert.Contains(users.Items, user =>
                user.UserId == employeeId &&
                user.Role == nameof(UserRole.Employee));
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
    public async Task AdminAndManagerCanCreateWorkspacesOwnedByThemselves()
    {
        await using var database = await TestDatabase.CreateAsync();
        var adminId = Guid.NewGuid();
        var managerId = Guid.NewGuid();

        await using (var setupContext = database.CreateContext(adminId, UserRole.Admin))
        {
            AddUser(setupContext, adminId, UserRole.Admin);
            AddUser(setupContext, managerId, UserRole.Manager);
            await setupContext.SaveChangesAsync();
        }

        await using (var managerContext = database.CreateContext(managerId, UserRole.Manager))
        {
            var created = await CreateWorkspaceService(
                    managerContext,
                    managerId,
                    UserRole.Manager)
                .CreateAsync(new CreateWorkspaceRequest
                {
                    Name = "Manager workspace"
                });

            Assert.Equal(managerId, created.OwnerId);
        }

        await using (var adminContext = database.CreateContext(adminId, UserRole.Admin))
        {
            var created = await CreateWorkspaceService(
                    adminContext,
                    adminId,
                    UserRole.Admin)
                .CreateAsync(new CreateWorkspaceRequest
                {
                    Name = "Admin workspace"
                });

            Assert.Equal(adminId, created.OwnerId);
        }
    }

    [Fact]
    public async Task WorkspaceListAndDetailsIncludeOwnerNameAndEmail()
    {
        await using var database = await TestDatabase.CreateAsync();
        var adminId = Guid.NewGuid();

        await using (var setupContext = database.CreateContext(adminId, UserRole.Admin))
        {
            setupContext.Users.Add(new User
            {
                Id = adminId,
                Email = "owner@example.test",
                PasswordHash = "not-used",
                FirstName = "Avery",
                LastName = "Owner",
                Role = UserRole.Admin
            });
            await setupContext.SaveChangesAsync();
        }

        await using var context = database.CreateContext(adminId, UserRole.Admin);
        var service = CreateWorkspaceService(context, adminId, UserRole.Admin);
        var created = await service.CreateAsync(new CreateWorkspaceRequest
        {
            Name = "Owner information workspace"
        });

        Assert.Equal(adminId, created.OwnerId);
        Assert.Equal("Avery Owner", created.OwnerName);
        Assert.Equal("owner@example.test", created.OwnerEmail);

        var listed = Assert.Single(
            (await service.GetAsync(new PagedSearchRequest())).Items);
        Assert.Equal(adminId, listed.OwnerId);
        Assert.Equal("Avery Owner", listed.OwnerName);
        Assert.Equal("owner@example.test", listed.OwnerEmail);

        var details = await service.GetByIdAsync(created.Id);
        Assert.Equal(adminId, details.OwnerId);
        Assert.Equal("Avery Owner", details.OwnerName);
        Assert.Equal("owner@example.test", details.OwnerEmail);
    }

    [Fact]
    public async Task AdminCanManageWorkspaceOwnedByAnotherUser()
    {
        await using var database = await TestDatabase.CreateAsync();
        var adminId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();

        await using (var setupContext = database.CreateContext(adminId, UserRole.Admin))
        {
            AddUser(setupContext, adminId, UserRole.Admin);
            AddUser(setupContext, managerId, UserRole.Manager);
            AddUser(setupContext, employeeId, UserRole.Employee);
            setupContext.Workspaces.Add(new Workspace
            {
                Id = workspaceId,
                Name = "Manager workspace",
                OwnerId = managerId
            });
            await setupContext.SaveChangesAsync();
        }

        await using var adminContext =
            database.CreateContext(adminId, UserRole.Admin);
        var service = CreateWorkspaceService(
            adminContext,
            adminId,
            UserRole.Admin);

        var allWorkspaces = await service.GetAsync(new PagedSearchRequest());
        Assert.Contains(allWorkspaces.Items, item => item.Id == workspaceId);

        var updated = await service.UpdateAsync(
            workspaceId,
            new UpdateWorkspaceRequest { Name = "Updated by admin" });
        Assert.Equal("Updated by admin", updated.Name);

        var added = await service.AddMemberAsync(
            workspaceId,
            new AddWorkspaceMemberRequest { UserId = employeeId });
        Assert.Equal(employeeId, added.UserId);
        await service.RemoveMemberAsync(workspaceId, employeeId);
        await service.DeleteAsync(workspaceId);

        Assert.Empty(await adminContext.Workspaces.ToListAsync());
    }

    [Fact]
    public async Task ManagerWorkspaceOwnerCanAddOnlyDirectReports()
    {
        await using var database = await TestDatabase.CreateAsync();
        var managerId = Guid.NewGuid();
        var directReportId = Guid.NewGuid();
        var unrelatedEmployeeId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();

        await using (var setupContext = database.CreateContext(managerId, UserRole.Manager))
        {
            AddUser(setupContext, managerId, UserRole.Manager);
            AddUser(setupContext, directReportId, UserRole.Employee);
            AddUser(setupContext, unrelatedEmployeeId, UserRole.Employee);
            setupContext.Workspaces.Add(new Workspace
            {
                Id = workspaceId,
                Name = "Manager workspace",
                OwnerId = managerId
            });
            setupContext.ManagerEmployees.Add(new ManagerEmployee
            {
                ManagerId = managerId,
                EmployeeId = directReportId,
                CreatedAt = DateTime.UtcNow
            });
            await setupContext.SaveChangesAsync();
        }

        await using var managerContext =
            database.CreateContext(managerId, UserRole.Manager);
        var service = CreateWorkspaceService(
            managerContext,
            managerId,
            UserRole.Manager);

        var added = await service.AddMemberAsync(
            workspaceId,
            new AddWorkspaceMemberRequest { UserId = directReportId });

        Assert.Equal(directReportId, added.UserId);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.AddMemberAsync(
                workspaceId,
                new AddWorkspaceMemberRequest { UserId = unrelatedEmployeeId }));
        Assert.Single(await managerContext.WorkspaceMembers.ToListAsync());
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
    public async Task EmployeeWorkspaceListAndDetailsAreLimitedToAccessibleWorkspaces()
    {
        await using var database = await TestDatabase.CreateAsync();
        var adminId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var otherEmployeeId = Guid.NewGuid();
        var accessibleWorkspaceId = Guid.NewGuid();
        var inaccessibleWorkspaceId = Guid.NewGuid();

        await using (var setupContext = database.CreateContext(adminId, UserRole.Admin))
        {
            AddUser(setupContext, adminId, UserRole.Admin);
            AddUser(setupContext, employeeId, UserRole.Employee);
            AddUser(setupContext, otherEmployeeId, UserRole.Employee);
            setupContext.Workspaces.AddRange(
                new Workspace
                {
                    Id = accessibleWorkspaceId,
                    Name = "Employee workspace",
                    OwnerId = adminId
                },
                new Workspace
                {
                    Id = inaccessibleWorkspaceId,
                    Name = "Private workspace",
                    OwnerId = adminId
                });
            setupContext.WorkspaceMembers.Add(new WorkspaceMember
            {
                WorkspaceId = accessibleWorkspaceId,
                UserId = employeeId,
                JoinedAt = DateTime.UtcNow
            });
            await setupContext.SaveChangesAsync();
        }

        await using var employeeContext =
            database.CreateContext(employeeId, UserRole.Employee);
        var service = CreateWorkspaceService(
            employeeContext,
            employeeId,
            UserRole.Employee);

        var workspaces = await service.GetAsync(new PagedSearchRequest());
        Assert.Equal(accessibleWorkspaceId, Assert.Single(workspaces.Items).Id);
        Assert.Equal(
            accessibleWorkspaceId,
            (await service.GetByIdAsync(accessibleWorkspaceId)).Id);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.GetByIdAsync(inaccessibleWorkspaceId));

        await using var outsiderContext =
            database.CreateContext(otherEmployeeId, UserRole.Employee);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            CreateWorkspaceService(
                    outsiderContext,
                    otherEmployeeId,
                    UserRole.Employee)
                .GetByIdAsync(accessibleWorkspaceId));
    }

    [Fact]
    public async Task ManagerWorkspaceListAndDetailsAreLimitedToOwnedOrMemberWorkspaces()
    {
        await using var database = await TestDatabase.CreateAsync();
        var managerId = Guid.NewGuid();
        var otherManagerId = Guid.NewGuid();
        var ownedWorkspaceId = Guid.NewGuid();
        var memberWorkspaceId = Guid.NewGuid();
        var inaccessibleWorkspaceId = Guid.NewGuid();

        await using (var setupContext = database.CreateContext(managerId, UserRole.Admin))
        {
            AddUser(setupContext, managerId, UserRole.Manager);
            AddUser(setupContext, otherManagerId, UserRole.Manager);
            setupContext.Workspaces.AddRange(
                new Workspace
                {
                    Id = ownedWorkspaceId,
                    Name = "Owned workspace",
                    OwnerId = managerId
                },
                new Workspace
                {
                    Id = memberWorkspaceId,
                    Name = "Member workspace",
                    OwnerId = otherManagerId
                },
                new Workspace
                {
                    Id = inaccessibleWorkspaceId,
                    Name = "Private workspace",
                    OwnerId = otherManagerId
                });
            setupContext.WorkspaceMembers.Add(new WorkspaceMember
            {
                WorkspaceId = memberWorkspaceId,
                UserId = managerId,
                JoinedAt = DateTime.UtcNow
            });
            await setupContext.SaveChangesAsync();
        }

        await using var managerContext =
            database.CreateContext(managerId, UserRole.Manager);
        var service = CreateWorkspaceService(
            managerContext,
            managerId,
            UserRole.Manager);

        var workspaces = await service.GetAsync(new PagedSearchRequest());
        Assert.Equal(
            new[] { memberWorkspaceId, ownedWorkspaceId }.OrderBy(id => id),
            workspaces.Items.Select(workspace => workspace.Id).OrderBy(id => id));
        Assert.Equal(
            memberWorkspaceId,
            (await service.GetByIdAsync(memberWorkspaceId)).Id);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.GetByIdAsync(inaccessibleWorkspaceId));
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
    public async Task ManagerBoardListIncludesAuthorizedBoardsButNotOtherWorkspaceBoards()
    {
        await using var database = await TestDatabase.CreateAsync();
        var managerSeed = await database.SeedManagerProjectBoardAsync();
        var unrelatedBoardId = Guid.NewGuid();

        await using (var context = database.CreateContext(
                         managerSeed.ManagerId,
                         UserRole.Manager))
        {
            var workspaceId = await context.Boards
                .Where(board => board.Id == managerSeed.BoardId)
                .Select(board => board.WorkspaceId)
                .SingleAsync();
            var ownerId = await context.Workspaces
                .Where(workspace => workspace.Id == workspaceId)
                .Select(workspace => workspace.OwnerId)
                .SingleAsync();
            context.Boards.Add(new Board
            {
                Id = unrelatedBoardId,
                Name = "Workspace-only board",
                OwnerId = ownerId,
                WorkspaceId = workspaceId,
                Purpose = BoardPurpose.GeneralLearning
            });
            await context.SaveChangesAsync();
        }

        await using var managerContext = database.CreateContext(
            managerSeed.ManagerId,
            UserRole.Manager);
        var currentUser = new TestCurrentUser(
            managerSeed.ManagerId,
            UserRole.Manager);
        var service = new BoardService(
            managerContext,
            currentUser,
            new BoardAuthorizationService(managerContext, currentUser));

        var boards = await service.GetAsync(new BoardQueryRequest());
        var accessibleBoard = Assert.Single(boards.Items);
        Assert.Equal(managerSeed.BoardId, accessibleBoard.Id);
        Assert.True(accessibleBoard.CanManageBoard);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.GetByIdAsync(unrelatedBoardId));
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
            var boardResponse = await new BoardService(
                    managerContext,
                    manager,
                    new BoardAuthorizationService(managerContext, manager))
                .GetByIdAsync(managerSeed.BoardId);
            Assert.True(boardResponse.CanManageBoard);

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
    public async Task BoardEditorCannotManageBoardOrMembership()
    {
        await using var database = await TestDatabase.CreateAsync();
        var boardSeed = await database.SeedCommentBoardAsync();

        await using var context = database.CreateContext(
            boardSeed.EditorId,
            UserRole.Employee);
        var currentUser = new TestCurrentUser(
            boardSeed.EditorId,
            UserRole.Employee);
        var authorization = new BoardAuthorizationService(context, currentUser);
        var boardService = new BoardService(
            context,
            currentUser,
            authorization);
        var memberService = CreateBoardMemberService(context, currentUser);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            boardService.UpdateAsync(
                boardSeed.BoardId,
                new UpdateBoardRequest { Name = "Editor update" }));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            boardService.DeleteAsync(boardSeed.BoardId));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            memberService.AddAsync(
                boardSeed.BoardId,
                new AddBoardMemberRequest
                {
                    UserId = boardSeed.OutsiderId,
                    Role = FlowBoard.Shared.Enums.BoardMemberRole.Viewer
                }));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            memberService.UpdateRoleAsync(
                boardSeed.BoardId,
                boardSeed.ViewerId,
                new UpdateBoardMemberRoleRequest
                {
                    Role = FlowBoard.Shared.Enums.BoardMemberRole.Editor
                }));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            memberService.RemoveAsync(
                boardSeed.BoardId,
                boardSeed.ViewerId));
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
    public async Task EmployeeBoardListReturnsAccessibleBoardContextAndRejectsInaccessibleBoard()
    {
        await using var database = await TestDatabase.CreateAsync();
        var boardSeed = await database.SeedCommentBoardAsync();

        await using (var employeeContext = database.CreateContext(
                         boardSeed.EditorId,
                         UserRole.Employee))
        {
            var workspaceId = await employeeContext.Boards
                .Where(board => board.Id == boardSeed.BoardId)
                .Select(board => board.WorkspaceId)
                .SingleAsync();
            var projectId = Guid.NewGuid();
            var projectBoardId = Guid.NewGuid();
            employeeContext.Projects.Add(new Project
            {
                Id = projectId,
                Name = "Comment project",
                WorkspaceId = workspaceId,
                ManagerId = boardSeed.OwnerId,
                StartDate = DateTime.UtcNow.Date,
                EndDate = DateTime.UtcNow.Date.AddDays(30)
            });
            employeeContext.Boards.Add(new Board
            {
                Id = projectBoardId,
                Name = "Comment project board",
                OwnerId = boardSeed.OwnerId,
                WorkspaceId = workspaceId,
                ProjectId = projectId,
                Purpose = BoardPurpose.Project
            });
            employeeContext.BoardMembers.Add(new BoardMember
            {
                BoardId = projectBoardId,
                UserId = boardSeed.EditorId,
                Role = BoardMemberRole.Viewer,
                JoinedAt = DateTime.UtcNow
            });
            await employeeContext.SaveChangesAsync();

            var employee = new TestCurrentUser(
                boardSeed.EditorId,
                UserRole.Employee);
            var service = new BoardService(
                employeeContext,
                employee,
                new BoardAuthorizationService(employeeContext, employee));

            var boards = await service.GetAsync(new BoardQueryRequest());
            var board = Assert.Single(
                boards.Items.Where(item => item.Id == boardSeed.BoardId));

            Assert.Equal(boardSeed.BoardId, board.Id);
            Assert.Equal("Comment workspace", board.WorkspaceName);
            Assert.Equal(
                FlowBoard.Shared.Enums.BoardPurpose.GeneralLearning,
                board.Purpose);
            Assert.Equal(
                FlowBoard.Shared.Enums.BoardMemberRole.Editor,
                board.CurrentUserRole);
            Assert.False(board.CanManageBoard);

            var projectBoard = Assert.Single(
                boards.Items.Where(item => item.Id == projectBoardId));
            Assert.Equal("Comment workspace", projectBoard.WorkspaceName);
            Assert.Equal("Comment project", projectBoard.ProjectName);
            Assert.Equal(
                FlowBoard.Shared.Enums.BoardMemberRole.Viewer,
                projectBoard.CurrentUserRole);
            Assert.False(projectBoard.CanManageBoard);

            var details = await service.GetByIdAsync(boardSeed.BoardId);
            Assert.Equal(board.CurrentUserRole, details.CurrentUserRole);
        }

        await using var outsiderContext = database.CreateContext(
            boardSeed.OutsiderId,
            UserRole.Employee);
        var outsider = new TestCurrentUser(
            boardSeed.OutsiderId,
            UserRole.Employee);
        var outsiderService = new BoardService(
            outsiderContext,
            outsider,
            new BoardAuthorizationService(outsiderContext, outsider));

        var outsiderBoards = await outsiderService.GetAsync(
            new BoardQueryRequest());
        Assert.Empty(outsiderBoards.Items);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            outsiderService.GetByIdAsync(boardSeed.BoardId));
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

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.DeleteAsync(boardSeed.BoardId, boardSeed.ColumnId));

        Assert.True(await context.BoardColumns.AnyAsync(
            column => column.Id == boardSeed.ColumnId));
        Assert.True(await context.Cards.AnyAsync(
            card => card.Id == boardSeed.CardId));

        await using var ownerContext = database.CreateContext(
            boardSeed.OwnerId,
            UserRole.Admin);
        var ownerAuthorization = new BoardAuthorizationService(
            ownerContext,
            new TestCurrentUser(boardSeed.OwnerId, UserRole.Admin));
        var ownerService = new BoardColumnService(
            ownerContext,
            ownerAuthorization);
        await Assert.ThrowsAsync<ConflictException>(() =>
            ownerService.DeleteAsync(boardSeed.BoardId, boardSeed.ColumnId));
    }

    [Fact]
    public async Task TaskQueriesUseBoardAccessAndMyTasksOnlyReturnsAssignments()
    {
        await using var database = await TestDatabase.CreateAsync();
        var boardSeed = await database.SeedCommentBoardAsync();

        await using (var context = database.CreateContext(
                         boardSeed.EditorId,
                         UserRole.Employee))
        {
            var card = await context.Cards.SingleAsync(
                item => item.Id == boardSeed.CardId);
            card.AssignedUserId = boardSeed.EditorId;
            card.Priority = CardPriority.High;
            await context.SaveChangesAsync();

            var currentUser = new TestCurrentUser(
                boardSeed.EditorId,
                UserRole.Employee);
            var authorization = new BoardAuthorizationService(
                context,
                currentUser);
            var assignmentGuard = new TaskAssignmentGuard(
                context,
                currentUser);
            var projectService = new ProjectService(
                context,
                currentUser,
                assignmentGuard);
            var cardService = new CardService(
                context,
                authorization,
                assignmentGuard,
                projectService);
            var boardTasks = await cardService.GetByBoardAsync(
                boardSeed.BoardId,
                new CardQueryRequest());

            Assert.Single(boardTasks.Items);
            Assert.Equal(boardSeed.CardId, boardTasks.Items[0].Id);

            await Assert.ThrowsAsync<ForbiddenException>(() =>
                cardService.CreateAsync(
                    boardSeed.ColumnId,
                    new CreateCardRequest { Title = "Editor-created task" }));
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                cardService.UpdateAsync(
                    boardSeed.CardId,
                    new UpdateCardRequest { Title = "Editor-updated task" }));

            var otherEmployeeTaskId = Guid.NewGuid();
            context.Cards.Add(new Card
            {
                Id = otherEmployeeTaskId,
                BoardColumnId = boardSeed.ColumnId,
                AssignedUserId = boardSeed.ViewerId,
                Title = "Comment test card assigned to another employee",
                Position = 2
            });
            context.Cards.Add(new Card
            {
                Id = Guid.NewGuid(),
                BoardColumnId = boardSeed.ColumnId,
                AssignedUserId = boardSeed.EditorId,
                Title = "Comment test card with a different priority",
                Priority = CardPriority.Medium,
                Position = 3
            });
            await context.SaveChangesAsync();

            var myTasksService = new MyTasksService(
                context,
                currentUser,
                authorization);
            var myTasks = await myTasksService.GetMyTasksAsync(
                new MyTasksQueryRequest
                {
                    BoardId = boardSeed.BoardId,
                    Search = "Comment test card",
                    Priority = FlowBoard.Shared.Enums.CardPriority.High
                });

            Assert.Single(myTasks.Items);
            Assert.Equal(boardSeed.CardId, myTasks.Items[0].CardId);
            Assert.Equal(
                FlowBoard.Shared.Enums.CardPriority.High,
                myTasks.Items[0].Priority);
            Assert.DoesNotContain(
                myTasks.Items,
                task => task.CardId == otherEmployeeTaskId);
        }

        await using (var viewerContext = database.CreateContext(
                         boardSeed.ViewerId,
                         UserRole.Employee))
        {
            var viewer = new TestCurrentUser(
                boardSeed.ViewerId,
                UserRole.Employee);
            var viewerGuard = new TaskAssignmentGuard(viewerContext, viewer);
            var viewerCards = new CardService(
                viewerContext,
                new BoardAuthorizationService(viewerContext, viewer),
                viewerGuard,
                new ProjectService(viewerContext, viewer, viewerGuard));

            await Assert.ThrowsAsync<ForbiddenException>(() =>
                viewerCards.SetAssigneeAsync(boardSeed.CardId, null));
        }

        await using var outsiderContext = database.CreateContext(
            boardSeed.OutsiderId,
            UserRole.Employee);
        var outsider = new TestCurrentUser(
            boardSeed.OutsiderId,
            UserRole.Employee);
        var outsiderGuard = new TaskAssignmentGuard(
            outsiderContext,
            outsider);
        var outsiderCards = new CardService(
            outsiderContext,
            new BoardAuthorizationService(outsiderContext, outsider),
            outsiderGuard,
            new ProjectService(outsiderContext, outsider, outsiderGuard));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            outsiderCards.GetByBoardAsync(
                boardSeed.BoardId,
                new CardQueryRequest()));
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
        var assignmentGuard = new TaskAssignmentGuard(context, currentUser);
        var service = new CardService(
            context,
            new BoardAuthorizationService(context, currentUser),
            assignmentGuard,
            new ProjectService(context, currentUser, assignmentGuard));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.DeleteAsync(boardSeed.CardId));

        Assert.True(await context.Cards.AnyAsync(
            card => card.Id == boardSeed.CardId));
        Assert.Single(await context.CardComments.ToListAsync());

        await using var ownerContext = database.CreateContext(
            boardSeed.OwnerId,
            UserRole.Admin);
        var owner = new TestCurrentUser(boardSeed.OwnerId, UserRole.Admin);
        var ownerGuard = new TaskAssignmentGuard(ownerContext, owner);
        var ownerService = new CardService(
            ownerContext,
            new BoardAuthorizationService(ownerContext, owner),
            ownerGuard,
            new ProjectService(ownerContext, owner, ownerGuard));
        await Assert.ThrowsAsync<ConflictException>(() =>
            ownerService.DeleteAsync(boardSeed.CardId));
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
            var workspaceOwnerId = Guid.NewGuid();
            var workspaceId = Guid.NewGuid();
            var projectId = Guid.NewGuid();
            var boardId = Guid.NewGuid();

            await using var context = CreateContext(managerId, UserRole.Manager);
            AddUser(context, managerId, UserRole.Manager);
            AddUser(context, employeeId, UserRole.Employee);
            AddUser(context, workspaceOwnerId, UserRole.Admin);
            context.Workspaces.Add(new Workspace
            {
                Id = workspaceId,
                Name = "Managed workspace",
                OwnerId = workspaceOwnerId
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
                OwnerId = workspaceOwnerId,
                WorkspaceId = workspaceId,
                ProjectId = projectId,
                Purpose = BoardPurpose.Project
            });
            context.WorkspaceMembers.Add(new WorkspaceMember
            {
                WorkspaceId = workspaceId,
                UserId = managerId,
                JoinedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            return new ManagerProjectBoardSeed(managerId, employeeId, boardId);
        }

        public ValueTask DisposeAsync() => _connection.DisposeAsync();
    }
}

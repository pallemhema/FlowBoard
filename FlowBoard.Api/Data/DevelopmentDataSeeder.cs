using FlowBoard.Api.Services.Authentication;
using FlowBoard.Domain.Entities;
using FlowBoard.Domain.Enums;
using FlowBoard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FlowBoard.Api.Data;

public static class DevelopmentDataSeeder
{
    public static async Task SeedAsync(
        FlowBoardDbContext dbContext,
        IPasswordHasher passwordHasher,
        ILogger logger,
        DevelopmentSeedSettings settings,
        IHostEnvironment environment,
        CancellationToken cancellationToken = default)
    {
        if (!environment.IsDevelopment())
        {
            logger.LogWarning(
                "Development data seeding was requested outside Development. Seeding skipped.");
            return;
        }

        var configuredUsers = new[]
        {
            settings.Owner,
            settings.Ravi,
            settings.Priya,
            settings.Arjun
        };

        if (!HasValidUserConfiguration(configuredUsers))
        {
            logger.LogWarning(
                "Development data seeding skipped because required seed user settings are missing or invalid.");
            return;
        }

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        if (await HasApplicationDataAsync(dbContext, cancellationToken))
        {
            await CompleteExistingSeedAsync(
                dbContext,
                configuredUsers,
                logger,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var now = DateTime.UtcNow;
        var owner = CreateSeedUser(settings.Owner, passwordHasher);
        var manager = CreateSeedUser(settings.Ravi, passwordHasher);
        var priya = CreateSeedUser(settings.Priya, passwordHasher);
        var arjun = CreateSeedUser(settings.Arjun, passwordHasher);
        dbContext.Users.AddRange(owner, manager, priya, arjun);

        var techWorkspace = new Workspace
        {
            Id = Guid.NewGuid(),
            Name = "TechVedika Projects",
            Description = "Development projects and engineering work.",
            OwnerId = owner.Id
        };
        var learningWorkspace = new Workspace
        {
            Id = Guid.NewGuid(),
            Name = "Learning Projects",
            Description = "Learning and experimental projects.",
            OwnerId = owner.Id
        };
        dbContext.Workspaces.AddRange(techWorkspace, learningWorkspace);

        var team = new Team
        {
            Id = Guid.NewGuid(),
            Name = "Engineering Team",
            Description = "Employees contributing to FlowBoard development."
        };
        dbContext.Teams.Add(team);

        dbContext.ManagerEmployees.AddRange(
            new ManagerEmployee
            {
                ManagerId = manager.Id,
                EmployeeId = priya.Id,
                CreatedAt = now
            },
            new ManagerEmployee
            {
                ManagerId = manager.Id,
                EmployeeId = arjun.Id,
                CreatedAt = now
            });

        dbContext.TeamMembers.AddRange(
            new TeamMember { TeamId = team.Id, UserId = priya.Id, JoinedAt = now },
            new TeamMember { TeamId = team.Id, UserId = arjun.Id, JoinedAt = now });

        dbContext.WorkspaceMembers.AddRange(
            CreateWorkspaceMember(techWorkspace.Id, manager.Id, now),
            CreateWorkspaceMember(techWorkspace.Id, priya.Id, now),
            CreateWorkspaceMember(techWorkspace.Id, arjun.Id, now),
            CreateWorkspaceMember(learningWorkspace.Id, manager.Id, now),
            CreateWorkspaceMember(learningWorkspace.Id, priya.Id, now),
            CreateWorkspaceMember(learningWorkspace.Id, arjun.Id, now));

        var flowProject = CreateProject(
            "FlowBoard Development",
            "Build and improve the FlowBoard application.",
            techWorkspace.Id,
            manager.Id,
            now);
        var bugProject = CreateProject(
            "Bug Tracking",
            "Investigate, prioritize, and resolve application defects.",
            techWorkspace.Id,
            manager.Id,
            now);
        dbContext.Projects.AddRange(flowProject, bugProject);

        dbContext.ProjectMembers.AddRange(
            CreateProjectMember(flowProject.Id, priya.Id, now),
            CreateProjectMember(flowProject.Id, arjun.Id, now),
            CreateProjectMember(bugProject.Id, priya.Id, now),
            CreateProjectMember(bugProject.Id, arjun.Id, now));

        var flowBoard = CreateBoard(
            "FlowBoard Development",
            "Development of the FlowBoard application.",
            owner.Id,
            techWorkspace.Id,
            BoardPurpose.Project,
            flowProject.Id);
        var bugBoard = CreateBoard(
            "Bug Tracking",
            "Track and resolve application bugs.",
            owner.Id,
            techWorkspace.Id,
            BoardPurpose.Project,
            bugProject.Id);
        var learningBoard = CreateBoard(
            "Learning Platform",
            "Learning and experimental tasks.",
            owner.Id,
            learningWorkspace.Id,
            BoardPurpose.GeneralLearning,
            projectId: null);
        dbContext.Boards.AddRange(flowBoard, bugBoard, learningBoard);

        dbContext.BoardMembers.AddRange(
            CreateBoardMember(flowBoard.Id, priya.Id, BoardMemberRole.Editor, now),
            CreateBoardMember(flowBoard.Id, arjun.Id, BoardMemberRole.Editor, now),
            CreateBoardMember(bugBoard.Id, priya.Id, BoardMemberRole.Editor, now),
            CreateBoardMember(bugBoard.Id, arjun.Id, BoardMemberRole.Editor, now),
            CreateBoardMember(learningBoard.Id, priya.Id, BoardMemberRole.Editor, now),
            CreateBoardMember(learningBoard.Id, arjun.Id, BoardMemberRole.Editor, now));

        var flowBacklog = CreateColumn(flowBoard.Id, "Backlog", 1);
        var flowTodo = CreateColumn(flowBoard.Id, "To Do", 2);
        var flowProgress = CreateColumn(flowBoard.Id, "In Progress", 3);
        var flowReview = CreateColumn(flowBoard.Id, "Code Review", 4);
        var flowDone = CreateColumn(flowBoard.Id, "Done", 5);
        var bugBacklog = CreateColumn(bugBoard.Id, "Backlog", 1);
        var bugTodo = CreateColumn(bugBoard.Id, "To Do", 2);
        var bugProgress = CreateColumn(bugBoard.Id, "In Progress", 3);
        var bugReview = CreateColumn(bugBoard.Id, "Code Review", 4);
        var bugDone = CreateColumn(bugBoard.Id, "Done", 5);
        var learningTodo = CreateColumn(learningBoard.Id, "To Do", 1);
        var learningProgress = CreateColumn(learningBoard.Id, "In Progress", 2);
        var learningDone = CreateColumn(learningBoard.Id, "Done", 3);
        dbContext.BoardColumns.AddRange(
            flowBacklog, flowTodo, flowProgress, flowReview, flowDone,
            bugBacklog, bugTodo, bugProgress, bugReview, bugDone,
            learningTodo, learningProgress, learningDone);

        var cards = new[]
        {
            CreateCard(flowBacklog.Id, "Design database schema", "Finalize entities and relationships.", CardPriority.High, 1, null),
            CreateCard(flowTodo.Id, "Define API contracts", "Review DTOs and API response models.", CardPriority.Medium, 2, priya.Id),
            CreateCard(flowProgress.Id, "Build workspace page", "Create workspace listing and selection UI.", CardPriority.High, 1, priya.Id),
            CreateCard(flowReview.Id, "Review workspace authorization", "Verify workspace and project access rules.", CardPriority.High, 1, arjun.Id),
            CreateCard(flowDone.Id, "Workspace API", "Workspace CRUD API completed.", CardPriority.Medium, 1, arjun.Id),
            CreateCard(bugBacklog.Id, "Reproduce board membership regression", "Document the steps that expose the access issue.", CardPriority.High, 1, null),
            CreateCard(bugTodo.Id, "Fix login validation", "Review invalid login responses.", CardPriority.High, 1, priya.Id),
            CreateCard(bugProgress.Id, "Fix board authorization", "Review board member access rules.", CardPriority.High, 1, arjun.Id),
            CreateCard(bugReview.Id, "Verify authorization regression tests", "Check member and non-member access cases.", CardPriority.High, 1, priya.Id),
            CreateCard(bugDone.Id, "Fix refresh token flow", "Refresh token rotation is working.", CardPriority.High, 1, null),
            CreateCard(learningTodo.Id, "Study Blazor components", "Learn component parameters and EventCallback.", CardPriority.Medium, 1, priya.Id),
            CreateCard(learningProgress.Id, "Practice bUnit", "Write component tests.", CardPriority.High, 1, arjun.Id),
            CreateCard(learningDone.Id, "Learn JWT authentication", "Understand access and refresh tokens.", CardPriority.Medium, 1, null)
        };
        dbContext.Cards.AddRange(cards);

        dbContext.CardComments.AddRange(
            CreateComment(cards[2].Id, owner.Id, "Workspace page is ready for review."),
            CreateComment(cards[3].Id, priya.Id, "I checked access from both project members."),
            CreateComment(cards[6].Id, manager.Id, "Please verify invalid credential handling."),
            CreateComment(cards[7].Id, priya.Id, "Board access checks are covered in the fix."),
            CreateComment(cards[8].Id, arjun.Id, "The regression cases are ready for review."),
            CreateComment(cards[11].Id, arjun.Id, "I will share the component test results."));

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation(
            "Development data seeded successfully: 4 users, 2 workspaces, 1 team, 2 projects, 3 boards, 13 columns, 13 cards, and 6 comments.");
    }

    private static async Task CompleteExistingSeedAsync(
        FlowBoardDbContext dbContext,
        IReadOnlyCollection<DevelopmentSeedUserSettings> configuredUsers,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var users = await dbContext.Users.ToListAsync(cancellationToken);
        if (users.Count != 4 ||
            configuredUsers.Any(configured =>
                users.Count(user => string.Equals(
                    user.Email,
                    configured.Email.Trim(),
                    StringComparison.OrdinalIgnoreCase)) != 1) ||
            configuredUsers.Any(configured =>
                users.Single(user => string.Equals(
                    user.Email,
                    configured.Email.Trim(),
                    StringComparison.OrdinalIgnoreCase)).Role !=
                Enum.Parse<UserRole>(configured.Role, true)))
        {
            throw new InvalidOperationException(
                "Development data exists but does not match the known seed users. Refusing to modify it.");
        }

        var owner = FindUser(users, configuredUsers.Single(user =>
            Enum.Parse<UserRole>(user.Role, true) == UserRole.Admin));
        var manager = FindUser(users, configuredUsers.Single(user =>
            Enum.Parse<UserRole>(user.Role, true) == UserRole.Manager));
        var employees = configuredUsers
            .Where(user => Enum.Parse<UserRole>(user.Role, true) == UserRole.Employee)
            .Select(user => FindUser(users, user))
            .ToArray();

        var workspaces = await dbContext.Workspaces.ToListAsync(cancellationToken);
        var teams = await dbContext.Teams.ToListAsync(cancellationToken);
        var projects = await dbContext.Projects.ToListAsync(cancellationToken);
        var boards = await dbContext.Boards.ToListAsync(cancellationToken);
        if (workspaces.Count != 2 || teams.Count != 1 ||
            projects.Count != 2 || boards.Count != 3)
        {
            throw new InvalidOperationException(
                "Development data exists but its workspace, team, project, or board set is unexpected. Refusing to modify it.");
        }

        var techWorkspace = FindByName(workspaces, workspace => workspace.Name, "TechVedika Projects");
        var learningWorkspace = FindByName(workspaces, workspace => workspace.Name, "Learning Projects");
        var team = teams.Single();
        if (team.Name is not ("FlowBoard Engineering" or "Engineering Team"))
        {
            throw new InvalidOperationException(
                "Development seed team identity is invalid. Refusing to modify it.");
        }

        team.Name = "Engineering Team";
        if (techWorkspace.OwnerId != owner.Id ||
            learningWorkspace.OwnerId != owner.Id)
        {
            throw new InvalidOperationException(
                "Development seed ownership or team identity is invalid. Refusing to modify it.");
        }

        var flowProject = FindByName(projects, project => project.Name, "FlowBoard Development");
        var bugProject = FindByName(projects, project => project.Name, "Bug Tracking");
        if (new[] { flowProject, bugProject }.Any(project =>
                project.WorkspaceId != techWorkspace.Id ||
                project.ManagerId != manager.Id ||
                project.Status != ProjectStatus.Active))
        {
            throw new InvalidOperationException(
                "Development seed project relationships are invalid. Refusing to modify them.");
        }

        var flowBoard = FindByName(boards, board => board.Name, "FlowBoard Development");
        var bugBoard = FindByName(boards, board => board.Name, "Bug Tracking");
        var learningBoard = FindByName(boards, board => board.Name, "Learning Platform");
        if (flowBoard.OwnerId != owner.Id ||
            bugBoard.OwnerId != owner.Id ||
            learningBoard.OwnerId != owner.Id ||
            flowBoard.WorkspaceId != techWorkspace.Id ||
            bugBoard.WorkspaceId != techWorkspace.Id ||
            learningBoard.WorkspaceId != learningWorkspace.Id ||
            flowBoard.Purpose != BoardPurpose.Project ||
            bugBoard.Purpose != BoardPurpose.Project ||
            flowBoard.ProjectId != flowProject.Id ||
            bugBoard.ProjectId != bugProject.Id ||
            learningBoard.Purpose != BoardPurpose.GeneralLearning ||
            learningBoard.ProjectId is not null)
        {
            throw new InvalidOperationException(
                "Development seed board relationships are invalid. Refusing to modify them.");
        }

        await VerifyExistingMembershipsAsync(
            dbContext,
            team,
            manager,
            employees,
            techWorkspace,
            learningWorkspace,
            flowProject,
            bugProject,
            flowBoard,
            bugBoard,
            learningBoard,
            cancellationToken);

        var boardColumns = await dbContext.BoardColumns
            .Where(column => column.BoardId == flowBoard.Id ||
                            column.BoardId == bugBoard.Id ||
                            column.BoardId == learningBoard.Id)
            .ToListAsync(cancellationToken);
        EnsureExistingColumnSet(boardColumns, flowBoard.Id, bugBoard.Id, learningBoard.Id);
        await EnsureBoardColumnsAsync(
            dbContext,
            boardColumns,
            flowBoard.Id,
            bugBoard.Id,
            learningBoard.Id,
            cancellationToken);

        var columnsByBoardAndName = await dbContext.BoardColumns
            .Where(column => column.BoardId == flowBoard.Id ||
                            column.BoardId == bugBoard.Id ||
                            column.BoardId == learningBoard.Id)
            .ToDictionaryAsync(
                column => (column.BoardId, column.Name),
                cancellationToken);
        await EnsureMissingCardsAndCommentsAsync(
            dbContext,
            columnsByBoardAndName,
            flowBoard,
            bugBoard,
            learningBoard,
            owner,
            employees,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Existing Development seed graph was verified and reconciled without deleting records or duplicating seed data.");
    }

    private static User FindUser(
        IEnumerable<User> users,
        DevelopmentSeedUserSettings settings) =>
        users.Single(user => string.Equals(
            user.Email,
            settings.Email.Trim(),
            StringComparison.OrdinalIgnoreCase));

    private static T FindByName<T>(
        IEnumerable<T> entities,
        Func<T, string> nameSelector,
        string name)
    {
        return entities.Single(entity =>
            string.Equals(
                nameSelector(entity),
                name,
                StringComparison.Ordinal));
    }

    private static async Task VerifyExistingMembershipsAsync(
        FlowBoardDbContext dbContext,
        Team team,
        User manager,
        IReadOnlyCollection<User> employees,
        Workspace techWorkspace,
        Workspace learningWorkspace,
        Project flowProject,
        Project bugProject,
        Board flowBoard,
        Board bugBoard,
        Board learningBoard,
        CancellationToken cancellationToken)
    {
        var commentCount = await dbContext.CardComments.CountAsync(cancellationToken);
        if (await dbContext.TeamMembers.CountAsync(cancellationToken) != 2 ||
            await dbContext.ManagerEmployees.CountAsync(cancellationToken) != 2 ||
            await dbContext.WorkspaceMembers.CountAsync(cancellationToken) != 6 ||
            await dbContext.ProjectMembers.CountAsync(cancellationToken) != 4 ||
            await dbContext.BoardMembers.CountAsync(cancellationToken) != 6 ||
            commentCount is not (4 or 6 or 7 or 8))
        {
            throw new InvalidOperationException(
                "Development seed membership data is unexpected. Refusing to modify it.");
        }

        var employeeIds = employees.Select(user => user.Id).ToArray();
        var expectedWorkspaceMemberships = new[]
        {
            (techWorkspace.Id, manager.Id),
            (techWorkspace.Id, employeeIds[0]),
            (techWorkspace.Id, employeeIds[1]),
            (learningWorkspace.Id, manager.Id),
            (learningWorkspace.Id, employeeIds[0]),
            (learningWorkspace.Id, employeeIds[1])
        };
        var expectedProjectMemberships = new[]
        {
            (flowProject.Id, employeeIds[0]),
            (flowProject.Id, employeeIds[1]),
            (bugProject.Id, employeeIds[0]),
            (bugProject.Id, employeeIds[1])
        };
        var expectedBoardMemberships = new[]
        {
            (flowBoard.Id, employeeIds[0]),
            (flowBoard.Id, employeeIds[1]),
            (bugBoard.Id, employeeIds[0]),
            (bugBoard.Id, employeeIds[1]),
            (learningBoard.Id, employeeIds[0]),
            (learningBoard.Id, employeeIds[1])
        };

        foreach (var employeeId in employeeIds)
        {
            if (!await dbContext.TeamMembers.AnyAsync(
                    item => item.TeamId == team.Id && item.UserId == employeeId,
                    cancellationToken) ||
                !await dbContext.ManagerEmployees.AnyAsync(
                    item => item.ManagerId == manager.Id &&
                            item.EmployeeId == employeeId,
                    cancellationToken))
            {
                throw new InvalidOperationException(
                    "Development seed team or manager membership is incomplete. Refusing to modify it.");
            }
        }

        foreach (var (workspaceId, userId) in expectedWorkspaceMemberships)
        {
            if (!await dbContext.WorkspaceMembers.AnyAsync(
                    item => item.WorkspaceId == workspaceId && item.UserId == userId,
                    cancellationToken))
            {
                throw new InvalidOperationException(
                    "Development seed workspace membership is incomplete. Refusing to modify it.");
            }
        }

        foreach (var (projectId, userId) in expectedProjectMemberships)
        {
            if (!await dbContext.ProjectMembers.AnyAsync(
                    item => item.ProjectId == projectId && item.UserId == userId,
                    cancellationToken))
            {
                throw new InvalidOperationException(
                    "Development seed project membership is incomplete. Refusing to modify it.");
            }
        }

        foreach (var (boardId, userId) in expectedBoardMemberships)
        {
            if (!await dbContext.BoardMembers.AnyAsync(
                    item => item.BoardId == boardId && item.UserId == userId,
                    cancellationToken))
            {
                throw new InvalidOperationException(
                    "Development seed board membership is incomplete. Refusing to modify it.");
            }
        }
    }

    private static void EnsureExistingColumnSet(
        IReadOnlyCollection<BoardColumn> columns,
        Guid flowBoardId,
        Guid bugBoardId,
        Guid learningBoardId)
    {
        var expectedByBoard = new Dictionary<Guid, HashSet<string>>
        {
            [flowBoardId] = ["To Do", "In Progress", "Done", "Backlog", "Code Review"],
            [bugBoardId] = ["To Do", "In Progress", "Done", "Backlog", "Code Review"],
            [learningBoardId] = ["To Do", "In Progress", "Done"]
        };

        foreach (var (boardId, expected) in expectedByBoard)
        {
            var actual = columns
                .Where(column => column.BoardId == boardId)
                .Select(column => column.Name)
                .ToHashSet(StringComparer.Ordinal);
            var initial = new HashSet<string>(
                ["To Do", "In Progress", "Done"],
                StringComparer.Ordinal);
            if (!actual.SetEquals(expected) &&
                !actual.SetEquals(initial))
            {
                throw new InvalidOperationException(
                    "Development seed columns are unexpected. Refusing to modify them.");
            }
        }

        if (columns.Count != 9 && columns.Count != 13)
        {
            throw new InvalidOperationException(
                "Development seed column count is unexpected. Refusing to modify it.");
        }
    }

    private static async Task EnsureBoardColumnsAsync(
        FlowBoardDbContext dbContext,
        List<BoardColumn> existingColumns,
        Guid flowBoardId,
        Guid bugBoardId,
        Guid learningBoardId,
        CancellationToken cancellationToken)
    {
        var desired = new Dictionary<Guid, string[]>
        {
            [flowBoardId] = ["Backlog", "To Do", "In Progress", "Code Review", "Done"],
            [bugBoardId] = ["Backlog", "To Do", "In Progress", "Code Review", "Done"],
            [learningBoardId] = ["To Do", "In Progress", "Done"]
        };

        foreach (var boardId in desired.Keys.Where(id => id != learningBoardId))
        {
            var current = existingColumns
                .Where(column => column.BoardId == boardId)
                .OrderBy(column => column.Position)
                .ToList();
            for (var index = 0; index < current.Count; index++)
            {
                current[index].Position = int.MinValue + index;
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            var names = current.Select(column => column.Name)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var name in desired[boardId])
            {
                if (!names.Contains(name))
                {
                    var column = CreateColumn(
                        boardId,
                        name,
                        int.MinValue + current.Count);
                    current.Add(column);
                    dbContext.BoardColumns.Add(column);
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            for (var index = 0; index < desired[boardId].Length; index++)
            {
                current.Single(column => column.Name == desired[boardId][index])
                    .Position = index + 1;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task EnsureMissingCardsAndCommentsAsync(
        FlowBoardDbContext dbContext,
        IReadOnlyDictionary<(Guid BoardId, string Name), BoardColumn> columns,
        Board flowBoard,
        Board bugBoard,
        Board learningBoard,
        User owner,
        IReadOnlyList<User> employees,
        CancellationToken cancellationToken)
    {
        var expectedCards = new[]
        {
            (flowBoard.Id, "Design database schema", "Backlog", "Finalize entities and relationships.", CardPriority.High, (Guid?)null),
            (flowBoard.Id, "Review workspace authorization", "Code Review", "Verify workspace and project access rules.", CardPriority.High, (Guid?)employees[1].Id),
            (bugBoard.Id, "Reproduce board membership regression", "Backlog", "Document the steps that expose the access issue.", CardPriority.High, (Guid?)null),
            (bugBoard.Id, "Verify authorization regression tests", "Code Review", "Check member and non-member access cases.", CardPriority.High, (Guid?)employees[0].Id)
        };

        var cardsByBoardAndTitle = await dbContext.Cards
            .Where(card => card.BoardColumn.BoardId == flowBoard.Id ||
                           card.BoardColumn.BoardId == bugBoard.Id ||
                           card.BoardColumn.BoardId == learningBoard.Id)
            .Include(card => card.BoardColumn)
            .ToListAsync(cancellationToken);
        if (cardsByBoardAndTitle.Count != 10 &&
            cardsByBoardAndTitle.Count != 13 &&
            cardsByBoardAndTitle.Count != 14)
        {
            throw new InvalidOperationException(
                "Development seed card count is unexpected. Refusing to modify it.");
        }

        var newCards = new List<Card>();
        foreach (var (boardId, title, columnName, description, priority, assigneeId)
                 in expectedCards)
        {
            var existingCard = cardsByBoardAndTitle.SingleOrDefault(card =>
                card.BoardColumn.BoardId == boardId && card.Title == title);
            if (existingCard is not null)
            {
                existingCard.BoardColumnId = columns[(boardId, columnName)].Id;
                continue;
            }

            var card = CreateCard(
                columns[(boardId, columnName)].Id,
                title,
                description,
                priority,
                1,
                assigneeId);
            newCards.Add(card);
            dbContext.Cards.Add(card);
            cardsByBoardAndTitle.Add(card);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var card in newCards)
        {
            var userId = card.AssignedUserId ?? owner.Id;
            var content = card.AssignedUserId.HasValue
                ? "The work is ready for review by the project team."
                : "This issue is documented and ready for triage.";
            if (!await dbContext.CardComments.AnyAsync(
                    comment => comment.CardId == card.Id &&
                               comment.UserId == userId &&
                               comment.Content == content,
                    cancellationToken))
            {
                dbContext.CardComments.Add(
                    CreateComment(card.Id, userId, content));
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static bool HasValidUserConfiguration(
        IReadOnlyCollection<DevelopmentSeedUserSettings> users)
    {
        if (users.Any(user =>
                string.IsNullOrWhiteSpace(user.Email) ||
                string.IsNullOrWhiteSpace(user.Password) ||
                !PasswordPolicy.IsStrong(user.Password) ||
                string.IsNullOrWhiteSpace(user.FirstName) ||
                string.IsNullOrWhiteSpace(user.LastName) ||
                !IsAllowedDevelopmentRole(user.Role)))
        {
            return false;
        }

        var distinctEmails = users
            .Select(user => user.Email.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var distinctPasswords = users
            .Select(user => user.Password)
            .Distinct(StringComparer.Ordinal)
            .Count();
        var roles = users.Select(user => Enum.Parse<UserRole>(user.Role, true)).ToHashSet();

        return distinctEmails == users.Count &&
            distinctPasswords == users.Count &&
            roles.Contains(UserRole.Admin) &&
            roles.Contains(UserRole.Manager) &&
            roles.Contains(UserRole.Employee);
    }

    private static bool IsAllowedDevelopmentRole(string role)
    {
        return Enum.TryParse<UserRole>(role, true, out var parsedRole) &&
            (parsedRole == UserRole.Employee ||
             parsedRole == UserRole.Manager ||
             parsedRole == UserRole.Admin);
    }

    private static User CreateSeedUser(
        DevelopmentSeedUserSettings settings,
        IPasswordHasher passwordHasher)
    {
        Enum.TryParse<UserRole>(settings.Role, true, out var role);
        return new User
        {
            Id = Guid.NewGuid(),
            Email = settings.Email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHasher.Hash(settings.Password),
            FirstName = settings.FirstName.Trim(),
            LastName = settings.LastName.Trim(),
            Role = role
        };
    }

    private static async Task<bool> HasApplicationDataAsync(
        FlowBoardDbContext dbContext,
        CancellationToken cancellationToken)
    {
        return await dbContext.Users.AnyAsync(cancellationToken) ||
            await dbContext.RefreshTokens.AnyAsync(cancellationToken) ||
            await dbContext.Workspaces.AnyAsync(cancellationToken) ||
            await dbContext.Teams.AnyAsync(cancellationToken) ||
            await dbContext.TeamMembers.AnyAsync(cancellationToken) ||
            await dbContext.ManagerEmployees.AnyAsync(cancellationToken) ||
            await dbContext.WorkspaceMembers.AnyAsync(cancellationToken) ||
            await dbContext.Projects.AnyAsync(cancellationToken) ||
            await dbContext.ProjectMembers.AnyAsync(cancellationToken) ||
            await dbContext.Boards.AnyAsync(cancellationToken) ||
            await dbContext.BoardMembers.AnyAsync(cancellationToken) ||
            await dbContext.BoardColumns.AnyAsync(cancellationToken) ||
            await dbContext.Cards.AnyAsync(cancellationToken) ||
            await dbContext.CardComments.AnyAsync(cancellationToken);
    }

    private static WorkspaceMember CreateWorkspaceMember(
        Guid workspaceId,
        Guid userId,
        DateTime now) =>
        new() { WorkspaceId = workspaceId, UserId = userId, JoinedAt = now };

    private static Project CreateProject(
        string name,
        string description,
        Guid workspaceId,
        Guid managerId,
        DateTime now) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            WorkspaceId = workspaceId,
            ManagerId = managerId,
            Status = ProjectStatus.Active,
            StartDate = DateOnly.FromDateTime(now),
            EndDate = DateOnly.FromDateTime(now).AddMonths(6)
        };

    private static ProjectMember CreateProjectMember(
        Guid projectId,
        Guid userId,
        DateTime now) =>
        new() { ProjectId = projectId, UserId = userId, JoinedAt = now };

    private static Board CreateBoard(
        string name,
        string description,
        Guid ownerId,
        Guid workspaceId,
        BoardPurpose purpose,
        Guid? projectId) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            OwnerId = ownerId,
            WorkspaceId = workspaceId,
            Purpose = purpose,
            ProjectId = projectId
        };

    private static BoardMember CreateBoardMember(
        Guid boardId,
        Guid userId,
        BoardMemberRole role,
        DateTime now) =>
        new()
        {
            BoardId = boardId,
            UserId = userId,
            Role = role,
            JoinedAt = now
        };

    private static BoardColumn CreateColumn(
        Guid boardId,
        string name,
        int position) =>
        new()
        {
            Id = Guid.NewGuid(),
            BoardId = boardId,
            Name = name,
            Position = position
        };

    private static Card CreateCard(
        Guid columnId,
        string title,
        string description,
        CardPriority priority,
        int position,
        Guid? assignedUserId) =>
        new()
        {
            Id = Guid.NewGuid(),
            BoardColumnId = columnId,
            Title = title,
            Description = description,
            Priority = priority,
            Position = position,
            AssignedUserId = assignedUserId
        };

    private static CardComment CreateComment(
        Guid cardId,
        Guid userId,
        string content) =>
        new()
        {
            Id = Guid.NewGuid(),
            CardId = cardId,
            UserId = userId,
            Content = content
        };
}

using FlowBoard.Api.Exceptions;
using FlowBoard.Api.Services.BoardAuthorization;
using FlowBoard.Api.Services.CurrentUser;
using FlowBoard.Domain.Enums;
using FlowBoard.Infrastructure.Data;
using FlowBoard.Shared.DTOs.Common;
using FlowBoard.Shared.DTOs.MyTasks;
using Microsoft.EntityFrameworkCore;
using SharedCardPriority = FlowBoard.Shared.Enums.CardPriority;

namespace FlowBoard.Api.Services.MyTasks;

public class MyTasksService : IMyTasksService
{
    private readonly FlowBoardDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IBoardAuthorizationService _boardAuthorizationService;

    public MyTasksService(
        FlowBoardDbContext dbContext,
        ICurrentUserService currentUserService,
        IBoardAuthorizationService boardAuthorizationService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _boardAuthorizationService = boardAuthorizationService;
    }

    public async Task<PagedResponse<MyTaskResponse>> GetMyTasksAsync(
        MyTasksQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Page < 1 || request.PageSize is < 1 or > 100)
        {
            throw new ValidationException(
                "Page must be greater than zero and page size must be between 1 and 100.");
        }

        if (request.Priority.HasValue &&
            !Enum.IsDefined(request.Priority.Value))
        {
            throw new ValidationException("The selected task priority is invalid.");
        }

        if (request.OverdueOnly)
        {
            throw new ValidationException(
                "Overdue-only filtering is unavailable because board columns do not have a reliable completed-status field.");
        }

        var domainPriority = request.Priority.HasValue
            ? MapToDomainPriority(request.Priority.Value)
            : (CardPriority?)null;

        var userId = _currentUserService.UserId;
        var accessibleBoardIds = _boardAuthorizationService
            .GetAccessibleBoardsQuery()
            .Select(board => board.Id);

        var query = _dbContext.Cards
            .AsNoTracking()
            .Where(card =>
                card.AssignedUserId == userId &&
                accessibleBoardIds.Contains(card.BoardColumn.BoardId));

        if (domainPriority.HasValue)
        {
            query = query.Where(
                card => card.Priority == domainPriority.Value);
        }

        if (request.BoardColumnId.HasValue)
        {
            query = query.Where(
                card => card.BoardColumnId == request.BoardColumnId.Value);
        }

        if (request.BoardId.HasValue)
        {
            query = query.Where(
                card => card.BoardColumn.BoardId == request.BoardId.Value);
        }

        if (request.WorkspaceId.HasValue)
        {
            query = query.Where(
                card => card.BoardColumn.Board.WorkspaceId ==
                        request.WorkspaceId.Value);
        }

        if (request.ProjectId.HasValue)
        {
            query = query.Where(
                card => card.BoardColumn.Board.ProjectId ==
                        request.ProjectId.Value);
        }

        if (request.DueDateFrom.HasValue)
        {
            var dueDateFrom = request.DueDateFrom.Value;
            query = query.Where(
                card => card.DueDate.HasValue &&
                        card.DueDate.Value >= dueDateFrom);
        }

        if (request.DueDateTo.HasValue)
        {
            var dueDateTo = request.DueDateTo.Value;
            query = query.Where(
                card => card.DueDate.HasValue &&
                        card.DueDate.Value <= dueDateTo);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(card =>
                card.Title.Contains(search) ||
                (card.Description != null &&
                 card.Description.Contains(search)) ||
                card.BoardColumn.Board.Name.Contains(search) ||
                (card.BoardColumn.Board.Project != null &&
                 card.BoardColumn.Board.Project.Name.Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var taskData = await query
            .OrderBy(card => card.DueDate == null)
            .ThenBy(card => card.DueDate)
            .ThenBy(card => card.BoardColumn.Board.Name)
            .ThenBy(card => card.BoardColumn.Position)
            .ThenBy(card => card.Position)
            .ThenBy(card => card.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(card => new
            {
                card.Id,
                Title = card.Title,
                Description = card.Description,
                card.Priority,
                DueDate = card.DueDate,
                BoardColumnId = card.BoardColumnId,
                ColumnName = card.BoardColumn.Name,
                BoardId = card.BoardColumn.Board.Id,
                BoardName = card.BoardColumn.Board.Name,
                WorkspaceId = card.BoardColumn.Board.WorkspaceId,
                WorkspaceName = card.BoardColumn.Board.Workspace.Name,
                ProjectId = card.BoardColumn.Board.ProjectId,
                ProjectName = card.BoardColumn.Board.Project == null
                    ? null
                    : card.BoardColumn.Board.Project.Name,
                AssignedUserId = card.AssignedUserId,
                AssignedUserName = card.AssignedUser == null
                    ? null
                    : $"{card.AssignedUser.FirstName} {card.AssignedUser.LastName}"
            })
            .ToListAsync(cancellationToken);

        var items = taskData
            .Select(task => new MyTaskResponse
            {
                CardId = task.Id,
                Title = task.Title,
                Description = task.Description,
                Priority = MapToSharedPriority(task.Priority),
                DueDate = task.DueDate,
                BoardColumnId = task.BoardColumnId,
                ColumnName = task.ColumnName,
                BoardId = task.BoardId,
                BoardName = task.BoardName,
                WorkspaceId = task.WorkspaceId,
                WorkspaceName = task.WorkspaceName,
                ProjectId = task.ProjectId,
                ProjectName = task.ProjectName,
                AssignedUserId = task.AssignedUserId,
                AssignedUserName = task.AssignedUserName
            })
            .ToList();

        return new PagedResponse<MyTaskResponse>
        {
            Items = items,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(
                totalCount / (double)request.PageSize)
        };
    }

    private static CardPriority MapToDomainPriority(
        SharedCardPriority priority) =>
        priority switch
        {
            SharedCardPriority.Low => CardPriority.Low,
            SharedCardPriority.Medium => CardPriority.Medium,
            SharedCardPriority.High => CardPriority.High,
            SharedCardPriority.Critical => CardPriority.Critical,
            _ => throw new ArgumentOutOfRangeException(
                nameof(priority),
                priority,
                "Invalid task priority.")
        };

    private static SharedCardPriority MapToSharedPriority(
        CardPriority priority) =>
        priority switch
        {
            CardPriority.Low => SharedCardPriority.Low,
            CardPriority.Medium => SharedCardPriority.Medium,
            CardPriority.High => SharedCardPriority.High,
            CardPriority.Critical => SharedCardPriority.Critical,
            _ => throw new ArgumentOutOfRangeException(
                nameof(priority),
                priority,
                "Invalid task priority.")
        };
}

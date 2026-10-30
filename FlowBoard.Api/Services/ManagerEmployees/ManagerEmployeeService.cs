using FlowBoard.Api.Exceptions;
using FlowBoard.Api.Services.CurrentUser;
using FlowBoard.Domain.Entities;
using FlowBoard.Domain.Enums;
using FlowBoard.Infrastructure.Data;
using FlowBoard.Shared.DTOs.Auth;
using FlowBoard.Shared.DTOs.Common;
using FlowBoard.Shared.DTOs.ManagerEmployees;
using Microsoft.EntityFrameworkCore;

namespace FlowBoard.Api.Services.ManagerEmployees;

public class ManagerEmployeeService : IManagerEmployeeService
{
    private readonly FlowBoardDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public ManagerEmployeeService(
        FlowBoardDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<ManagerEmployeeResponse> AssignAsync(
        Guid managerId,
        AssignEmployeeToManagerRequest request,
        CancellationToken cancellationToken = default)
    {
        var manager = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                user => user.Id == managerId,
                cancellationToken);

        if (manager is null)
        {
            throw new NotFoundException("Manager was not found.");
        }

        if (manager.Role != UserRole.Manager)
        {
            throw new ValidationException(
                "The selected user does not have the Manager role.");
        }

        if (manager.Id == request.EmployeeId)
        {
            throw new ValidationException(
                "A manager cannot be assigned as their own employee.");
        }

        var employee = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                user => user.Id == request.EmployeeId,
                cancellationToken);

        if (employee is null)
        {
            throw new NotFoundException("Employee was not found.");
        }

        if (employee.Role != UserRole.Employee)
        {
            throw new ValidationException(
                "Only users with the Employee role can be assigned to a manager.");
        }

        var alreadyAssigned = await _dbContext.ManagerEmployees
            .AnyAsync(
                relationship => relationship.ManagerId == managerId &&
                                relationship.EmployeeId == employee.Id,
                cancellationToken);

        if (alreadyAssigned)
        {
            throw new ConflictException(
                "The employee is already assigned to this manager.");
        }

        var relationship = new ManagerEmployee
        {
            ManagerId = manager.Id,
            EmployeeId = employee.Id,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.ManagerEmployees.Add(relationship);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            _dbContext.Entry(relationship).State = EntityState.Detached;
            var duplicateExists = await _dbContext.ManagerEmployees
                .AnyAsync(
                    existing => existing.ManagerId == managerId &&
                                existing.EmployeeId == employee.Id,
                    cancellationToken);

            if (duplicateExists)
            {
                throw new ConflictException(
                    "The employee is already assigned to this manager.");
            }

            throw;
        }

        return MapRelationship(relationship, manager, employee);
    }

    public async Task<PagedResponse<ManagerEmployeeResponse>>
        GetManagedEmployeesAsync(
            PagedRequest request,
            CancellationToken cancellationToken = default)
    {
        var managerId = _currentUserService.UserId;
        await EnsureUserRoleAsync(
            managerId,
            UserRole.Manager,
            "The authenticated user is not a Manager.",
            cancellationToken);

        var query = _dbContext.ManagerEmployees
            .AsNoTracking()
            .Where(relationship => relationship.ManagerId == managerId);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(relationship => relationship.Employee.FirstName)
            .ThenBy(relationship => relationship.Employee.LastName)
            .ThenBy(relationship => relationship.Employee.Email)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(relationship => new ManagerEmployeeResponse
            {
                ManagerId = relationship.ManagerId,
                Manager = new UserDirectoryResponse
                {
                    UserId = relationship.Manager.Id,
                    Email = relationship.Manager.Email,
                    FirstName = relationship.Manager.FirstName,
                    LastName = relationship.Manager.LastName
                },
                EmployeeId = relationship.EmployeeId,
                Employee = new UserDirectoryResponse
                {
                    UserId = relationship.Employee.Id,
                    Email = relationship.Employee.Email,
                    FirstName = relationship.Employee.FirstName,
                    LastName = relationship.Employee.LastName
                },
                CreatedAt = relationship.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return CreatePage(items, request.Page, totalCount, request.PageSize);
    }

    public async Task<PagedResponse<ManagerEmployeeResponse>> GetMyManagersAsync(
        PagedRequest request,
        CancellationToken cancellationToken = default)
    {
        var employeeId = _currentUserService.UserId;
        await EnsureUserRoleAsync(
            employeeId,
            UserRole.Employee,
            "The authenticated user is not an Employee.",
            cancellationToken);

        var query = _dbContext.ManagerEmployees
            .AsNoTracking()
            .Where(relationship => relationship.EmployeeId == employeeId);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(relationship => relationship.Manager.FirstName)
            .ThenBy(relationship => relationship.Manager.LastName)
            .ThenBy(relationship => relationship.Manager.Email)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(relationship => new ManagerEmployeeResponse
            {
                ManagerId = relationship.ManagerId,
                Manager = new UserDirectoryResponse
                {
                    UserId = relationship.Manager.Id,
                    Email = relationship.Manager.Email,
                    FirstName = relationship.Manager.FirstName,
                    LastName = relationship.Manager.LastName
                },
                EmployeeId = relationship.EmployeeId,
                Employee = new UserDirectoryResponse
                {
                    UserId = relationship.Employee.Id,
                    Email = relationship.Employee.Email,
                    FirstName = relationship.Employee.FirstName,
                    LastName = relationship.Employee.LastName
                },
                CreatedAt = relationship.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return CreatePage(items, request.Page, totalCount, request.PageSize);
    }

    public async Task<PagedResponse<ManagerEmployeeResponse>> GetAllAsync(
        PagedRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.ManagerEmployees.AsNoTracking();
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(relationship => relationship.Manager.FirstName)
            .ThenBy(relationship => relationship.Manager.LastName)
            .ThenBy(relationship => relationship.Employee.FirstName)
            .ThenBy(relationship => relationship.Employee.LastName)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(relationship => new ManagerEmployeeResponse
            {
                ManagerId = relationship.ManagerId,
                Manager = new UserDirectoryResponse
                {
                    UserId = relationship.Manager.Id,
                    Email = relationship.Manager.Email,
                    FirstName = relationship.Manager.FirstName,
                    LastName = relationship.Manager.LastName
                },
                EmployeeId = relationship.EmployeeId,
                Employee = new UserDirectoryResponse
                {
                    UserId = relationship.Employee.Id,
                    Email = relationship.Employee.Email,
                    FirstName = relationship.Employee.FirstName,
                    LastName = relationship.Employee.LastName
                },
                CreatedAt = relationship.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return CreatePage(items, request.Page, totalCount, request.PageSize);
    }

    public async Task RemoveManagedEmployeeAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default)
    {
        var managerId = _currentUserService.UserId;
        await EnsureUserRoleAsync(
            managerId,
            UserRole.Manager,
            "The authenticated user is not a Manager.",
            cancellationToken);

        await RemoveRelationshipAsync(
            managerId,
            employeeId,
            cancellationToken);
    }

    public Task RemoveAsync(
        Guid managerId,
        Guid employeeId,
        CancellationToken cancellationToken = default)
    {
        return RemoveRelationshipAsync(
            managerId,
            employeeId,
            cancellationToken);
    }

    private async Task EnsureUserRoleAsync(
        Guid userId,
        UserRole expectedRole,
        string invalidRoleMessage,
        CancellationToken cancellationToken)
    {
        var role = await _dbContext.Users
            .Where(user => user.Id == userId)
            .Select(user => (UserRole?)user.Role)
            .FirstOrDefaultAsync(cancellationToken);

        if (role is null)
        {
            throw new NotFoundException("User was not found.");
        }

        if (role != expectedRole)
        {
            throw new ForbiddenException(invalidRoleMessage);
        }
    }

    private async Task RemoveRelationshipAsync(
        Guid managerId,
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        var relationship = await _dbContext.ManagerEmployees
            .FirstOrDefaultAsync(
                item => item.ManagerId == managerId &&
                        item.EmployeeId == employeeId,
                cancellationToken);

        if (relationship is null)
        {
            throw new NotFoundException(
                "The manager-employee relationship was not found.");
        }

        _dbContext.ManagerEmployees.Remove(relationship);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static ManagerEmployeeResponse MapRelationship(
        ManagerEmployee relationship,
        User manager,
        User employee)
    {
        return new ManagerEmployeeResponse
        {
            ManagerId = relationship.ManagerId,
            Manager = MapUser(manager),
            EmployeeId = relationship.EmployeeId,
            Employee = MapUser(employee),
            CreatedAt = relationship.CreatedAt
        };
    }

    private static UserDirectoryResponse MapUser(User user)
    {
        return new UserDirectoryResponse
        {
            UserId = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName
        };
    }

    private static PagedResponse<ManagerEmployeeResponse> CreatePage(
        IReadOnlyList<ManagerEmployeeResponse> items,
        int page,
        int totalCount,
        int pageSize = 100)
    {
        return new PagedResponse<ManagerEmployeeResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }
}

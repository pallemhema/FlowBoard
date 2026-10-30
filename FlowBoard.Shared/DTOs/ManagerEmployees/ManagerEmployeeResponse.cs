using FlowBoard.Shared.DTOs.Auth;

namespace FlowBoard.Shared.DTOs.ManagerEmployees;

public class ManagerEmployeeResponse
{
    public Guid ManagerId { get; set; }

    public UserDirectoryResponse Manager { get; set; } = new();

    public Guid EmployeeId { get; set; }

    public UserDirectoryResponse Employee { get; set; } = new();

    public DateTime CreatedAt { get; set; }
}

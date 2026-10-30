namespace FlowBoard.Domain.Entities;

public class ManagerEmployee
{
    public Guid ManagerId { get; set; }

    public Guid EmployeeId { get; set; }

    public DateTime CreatedAt { get; set; }

    public User Manager { get; set; } = null!;

    public User Employee { get; set; } = null!;
}

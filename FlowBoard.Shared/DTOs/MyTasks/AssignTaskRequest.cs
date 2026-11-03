using System.ComponentModel.DataAnnotations;

namespace FlowBoard.Shared.DTOs.MyTasks;

public class AssignTaskRequest
{
    [Required]
    public Guid EmployeeId { get; set; }
}

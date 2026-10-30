using System.ComponentModel.DataAnnotations;

namespace FlowBoard.Shared.DTOs.ManagerEmployees;

public class AssignEmployeeToManagerRequest
{
    [Required]
    public Guid EmployeeId { get; set; }
}

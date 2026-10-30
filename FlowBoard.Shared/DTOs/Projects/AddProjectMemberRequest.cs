using System.ComponentModel.DataAnnotations;

namespace FlowBoard.Shared.DTOs.Projects;

public class AddProjectMemberRequest
{
    [Required]
    public Guid UserId { get; set; }
}

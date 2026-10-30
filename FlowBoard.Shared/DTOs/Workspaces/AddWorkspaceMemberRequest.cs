using System.ComponentModel.DataAnnotations;

namespace FlowBoard.Shared.DTOs.Workspaces;

public class AddWorkspaceMemberRequest
{
    [Required]
    public Guid UserId { get; set; }
}

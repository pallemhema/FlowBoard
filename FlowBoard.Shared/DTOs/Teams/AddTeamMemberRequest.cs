using System.ComponentModel.DataAnnotations;

namespace FlowBoard.Shared.DTOs.Teams;

public class AddTeamMemberRequest
{
    [Required]
    public Guid UserId { get; set; }
}

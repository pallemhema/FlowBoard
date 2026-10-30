using System.ComponentModel.DataAnnotations;

namespace FlowBoard.Shared.DTOs.Teams;

public class UpdateTeamRequest
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }
}

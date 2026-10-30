using System.ComponentModel.DataAnnotations;

namespace FlowBoard.Shared.DTOs.Workspaces;

public class UpdateWorkspaceRequest
{
    [Required]
    [StringLength(
        100,
        MinimumLength = 2,
        ErrorMessage = "Workspace name must be between 2 and 100 characters.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(
        500,
        ErrorMessage = "Description cannot exceed 500 characters.")]
    public string? Description { get; set; }
}
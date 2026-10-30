using System.ComponentModel.DataAnnotations;
using FlowBoard.Shared.Enums;

namespace FlowBoard.Shared.DTOs.Boards;

public class CreateBoardRequest
{
    [Required]
    [StringLength(
        100,
        MinimumLength = 2,
        ErrorMessage = "Board name must be between 2 and 100 characters.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(
        500,
        ErrorMessage = "Description cannot exceed 500 characters.")]
    public string? Description { get; set; }

    [Required]
    public Guid WorkspaceId { get; set; }

    public Guid? ProjectId { get; set; }

    [EnumDataType(typeof(BoardPurpose))]
    public BoardPurpose Purpose { get; set; } = BoardPurpose.Project;
}
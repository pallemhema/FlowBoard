using System.ComponentModel.DataAnnotations;

namespace FlowBoard.Shared.DTOs.BoardColumns;

public class CreateBoardColumnRequest
{
    [Required]
    [StringLength(
        50,
        MinimumLength = 1,
        ErrorMessage = "Column name must be between 1 and 50 characters.")]
    public string Name { get; set; } = string.Empty;

    [Range(
        0,
        int.MaxValue,
        ErrorMessage = "Position cannot be negative.")]
    public int Position { get; set; }
}
using System.ComponentModel.DataAnnotations;

namespace FlowBoard.Shared.DTOs.Cards;

public class MoveCardRequest
{
    [Required]
    public Guid BoardColumnId { get; set; }

    [Range(0, int.MaxValue)]
    public int Position { get; set; }
}
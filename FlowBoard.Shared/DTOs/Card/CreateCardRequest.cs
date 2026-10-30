using System.ComponentModel.DataAnnotations;
using FlowBoard.Shared.Enums;

namespace FlowBoard.Shared.DTOs.Cards;

public class CreateCardRequest
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    public CardPriority Priority { get; set; } = CardPriority.Medium;

    public DateTime? DueDate { get; set; }

    public Guid? AssignedUserId { get; set; }

    [Range(0, int.MaxValue)]
    public int Position { get; set; }
}
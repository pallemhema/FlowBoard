using System.ComponentModel.DataAnnotations;
using FlowBoard.Shared.Enums;

namespace FlowBoard.Shared.DTOs.Cards;

public class UpdateCardRequest
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    public CardPriority Priority { get; set; }

    public DateTime? DueDate { get; set; }

    public Guid? AssignedUserId { get; set; }
}
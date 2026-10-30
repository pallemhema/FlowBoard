using FlowBoard.Shared.Enums;

namespace FlowBoard.Shared.DTOs.Cards;

public class CardResponse
{
    public Guid Id { get; set; }

    public Guid BoardColumnId { get; set; }

    public Guid? AssignedUserId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public CardPriority Priority { get; set; }

    public DateTime? DueDate { get; set; }

    public int Position { get; set; }

    public string? AssignedUserName { get; set; }
}
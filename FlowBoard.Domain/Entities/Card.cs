namespace FlowBoard.Domain.Entities;

using FlowBoard.Domain.Common;
using FlowBoard.Domain.Enums;

public class Card : BaseEntity
{
    public Guid BoardColumnId { get; set; }

    public Guid? AssignedUserId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public CardPriority Priority { get; set; } = CardPriority.Medium;

    public DateTime? DueDate { get; set; }

    public int Position { get; set; }

    public BoardColumn BoardColumn { get; set; } = null!;

    public User? AssignedUser { get; set; }

    public ICollection<CardComment> Comments { get; set; } = [];
}
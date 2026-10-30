namespace FlowBoard.Domain.Entities;

using FlowBoard.Domain.Common;

public class CardComment : BaseEntity
{
    public Guid CardId { get; set; }

    public Guid UserId { get; set; }

    public string Content { get; set; } = string.Empty;


    public Card Card { get; set; } = null!;

    public User User { get; set; } = null!;
}
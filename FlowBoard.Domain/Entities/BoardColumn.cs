namespace FlowBoard.Domain.Entities;

using FlowBoard.Domain.Common;

public class BoardColumn : BaseEntity
{
    public Guid BoardId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Position { get; set; }


    public Board Board { get; set; } = null!;

    public ICollection<Card> Cards { get; set; } = [];
}
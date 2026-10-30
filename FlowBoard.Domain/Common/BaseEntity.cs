namespace FlowBoard.Domain.Common;

public abstract class BaseEntity
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTime? ModifiedAt { get; set; }

    public Guid? ModifiedByUserId { get; set; }
}
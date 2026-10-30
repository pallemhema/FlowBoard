namespace FlowBoard.Shared.DTOs.CardComments;

public class CardCommentResponse
{
    public Guid Id { get; set; }

    public Guid CardId { get; set; }

    public Guid UserId { get; set; }

    public string Content { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? ModifiedAt { get; set; }
}
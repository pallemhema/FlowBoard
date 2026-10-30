using System.ComponentModel.DataAnnotations;

namespace FlowBoard.Shared.DTOs.CardComments;

public class UpdateCardCommentRequest
{
    [Required]
    [StringLength(
        2000,
        MinimumLength = 1)]
    public string Content { get; set; } = string.Empty;
}
using System.ComponentModel.DataAnnotations;

namespace FlowBoard.Shared.DTOs.CardComments;

public class CreateCardCommentRequest
{
    [Required]
    [StringLength(
        2000,
        MinimumLength = 1)]
    public string Content { get; set; } = string.Empty;
}
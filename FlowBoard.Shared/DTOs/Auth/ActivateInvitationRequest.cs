using System.ComponentModel.DataAnnotations;

namespace FlowBoard.Shared.DTOs.Auth;

public class ActivateInvitationRequest
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    [MaxLength(100)]
    public string Password { get; set; } = string.Empty;
}

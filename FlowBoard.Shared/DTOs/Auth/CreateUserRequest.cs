using System.ComponentModel.DataAnnotations;

namespace FlowBoard.Shared.DTOs.Auth;

public class CreateUserRequest
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    [RegularExpression(@"^[\p{L}\p{M}][\p{L}\p{M}\s'-]*$")]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 2)]
    [RegularExpression(@"^[\p{L}\p{M}][\p{L}\p{M}\s'-]*$")]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = string.Empty;
}

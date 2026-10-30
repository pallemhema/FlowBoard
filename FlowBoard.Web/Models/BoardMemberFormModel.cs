using System.ComponentModel.DataAnnotations;
using FlowBoard.Shared.Enums;

namespace FlowBoard.Web.Models.BoardMembers;

public class BoardMemberFormModel
{
    [Required(ErrorMessage = "Select a user.")]
    public string UserId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Role is required.")]
    public BoardMemberRole Role { get; set; } = BoardMemberRole.Viewer;
}
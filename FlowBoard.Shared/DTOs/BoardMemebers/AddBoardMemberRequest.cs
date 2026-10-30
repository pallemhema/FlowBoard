
namespace FlowBoard.Shared.DTOs.BoardMembers;

using System.ComponentModel.DataAnnotations;

using FlowBoard.Shared.Enums;
public class AddBoardMemberRequest
{
    [Required]
    public Guid UserId { get; set; }

    [Required]
    public BoardMemberRole Role { get; set; }
}
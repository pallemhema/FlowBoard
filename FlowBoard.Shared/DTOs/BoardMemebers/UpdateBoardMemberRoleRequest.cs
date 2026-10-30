using System.ComponentModel.DataAnnotations;
using FlowBoard.Shared.Enums;

namespace FlowBoard.Shared.DTOs.BoardMembers;

public class UpdateBoardMemberRoleRequest
{
    [Required]
    public BoardMemberRole Role { get; set; }
}
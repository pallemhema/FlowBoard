using FlowBoard.Domain.Common;
using FlowBoard.Domain.Enums;

namespace FlowBoard.Domain.Entities;

public class UserRoleChangeAudit : BaseEntity
{
    public Guid TargetUserId { get; set; }

    public UserRole PreviousRole { get; set; }

    public UserRole NewRole { get; set; }

    public Guid ChangedByUserId { get; set; }

    public DateTime ChangedAt { get; set; }
}

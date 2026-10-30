using FlowBoard.Shared.Enums;
using FlowBoard.Shared.DTOs.Common;

namespace FlowBoard.Shared.DTOs.Cards;

public class CardQueryRequest : PagedSearchRequest
{
    public CardPriority? Priority { get; set; }

    public Guid? AssignedUserId { get; set; }
}
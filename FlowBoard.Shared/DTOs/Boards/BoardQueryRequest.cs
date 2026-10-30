using System.ComponentModel.DataAnnotations;
using FlowBoard.Shared.DTOs.Common;

namespace FlowBoard.Shared.DTOs.Boards;

public class BoardQueryRequest : PagedSearchRequest
{
    public Guid? WorkspaceId { get; set; }
}
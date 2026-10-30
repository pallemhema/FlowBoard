using System.ComponentModel.DataAnnotations;
using FlowBoard.Shared.DTOs.Common;
using FlowBoard.Shared.Enums;

namespace FlowBoard.Shared.DTOs.Projects;

public class ProjectQueryRequest : PagedSearchRequest
{
    public Guid? WorkspaceId { get; set; }

    [EnumDataType(typeof(ProjectStatus))]
    public ProjectStatus? Status { get; set; }
}

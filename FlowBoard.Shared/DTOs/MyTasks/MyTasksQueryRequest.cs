using System.ComponentModel.DataAnnotations;
using FlowBoard.Shared.DTOs.Common;
using FlowBoard.Shared.Enums;

namespace FlowBoard.Shared.DTOs.MyTasks;

public class MyTasksQueryRequest : PagedSearchRequest, IValidatableObject
{
    [EnumDataType(typeof(CardPriority))]
    public CardPriority? Priority { get; set; }

    public Guid? BoardColumnId { get; set; }

    public Guid? ProjectId { get; set; }

    public Guid? WorkspaceId { get; set; }

    public Guid? BoardId { get; set; }

    public DateTime? DueDateFrom { get; set; }

    public DateTime? DueDateTo { get; set; }

    public bool OverdueOnly { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DueDateFrom.HasValue &&
            DueDateTo.HasValue &&
            DueDateFrom.Value > DueDateTo.Value)
        {
            yield return new ValidationResult(
                "The due date start must be on or before the due date end.",
                [nameof(DueDateFrom), nameof(DueDateTo)]);
        }
    }
}

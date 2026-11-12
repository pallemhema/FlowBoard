using System.ComponentModel.DataAnnotations;

namespace FlowBoard.Shared.DTOs.Projects;

public class CreateProjectRequest : IValidatableObject
{
    [Required]
    [StringLength(150, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required]
    public Guid WorkspaceId { get; set; }

    [Display(Name = "Project Manager")]
    public Guid? ManagerId { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EndDate < StartDate)
        {
            yield return new ValidationResult(
                "End date must be on or after the start date.",
                [nameof(EndDate)]);
        }
    }
}

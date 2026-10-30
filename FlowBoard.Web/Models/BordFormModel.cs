using System.ComponentModel.DataAnnotations;

namespace FlowBoard.Web.Models.Boards;

public class BoardFormModel
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }
}
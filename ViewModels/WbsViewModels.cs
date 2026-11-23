using System.ComponentModel.DataAnnotations;
using ConstructionProjectManagement.Models;
using ConstructionProjectManagement.Services;

namespace ConstructionProjectManagement.ViewModels;

public sealed class WbsProjectListViewModel
{
    public required IReadOnlyList<WbsProjectTree> Projects { get; init; }

    public string? Search { get; init; }
}

public sealed class WbsIndexViewModel
{
    public required WbsProjectTree Tree { get; init; }

    public bool IncludeArchived { get; init; }

    public bool CanManageStructure { get; init; }

    public bool CanUpdateStatus { get; init; }

    public bool CanViewArchived { get; init; }

    public bool CanArchiveRestore { get; init; }
}

public sealed class ProjectPhaseViewModel
{
    public int Id { get; set; }

    public int ProjectId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên giai đoạn.")]
    [Display(Name = "Tên giai đoạn")]
    public string Name { get; set; } = string.Empty;

    [Range(0, int.MaxValue, ErrorMessage = "Thứ tự không hợp lệ.")]
    [Display(Name = "Thứ tự")]
    public int SequenceOrder { get; set; }

    [Display(Name = "Trạng thái")]
    public ProjectPhaseStatus Status { get; set; } = ProjectPhaseStatus.NotStarted;
}

public sealed class WorkItemViewModel
{
    public int Id { get; set; }

    public int ProjectId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn giai đoạn.")]
    [Display(Name = "Giai đoạn")]
    public int ProjectPhaseId { get; set; }

    [Display(Name = "Công việc cha")]
    public int? ParentWorkItemId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mã WBS.")]
    [Display(Name = "Mã WBS")]
    public string WbsCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập tên hạng mục.")]
    [Display(Name = "Tên hạng mục")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000, ErrorMessage = "Mô tả không được vượt quá 1000 ký tự.")]
    [Display(Name = "Mô tả")]
    public string? Description { get; set; }

    [Range(0, int.MaxValue)]
    [Display(Name = "Thứ tự")]
    public int SequenceOrder { get; set; }

    [Display(Name = "Trạng thái")]
    public WbsItemStatus Status { get; set; } = WbsItemStatus.NotStarted;
}

public sealed class WbsItemStatusViewModel
{
    public int Id { get; set; }

    [Display(Name = "Trạng thái")]
    public WbsItemStatus Status { get; set; }
}

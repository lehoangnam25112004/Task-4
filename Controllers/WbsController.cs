using ConstructionProjectManagement.Models;
using ConstructionProjectManagement.Services;
using ConstructionProjectManagement.Services.Interfaces;
using ConstructionProjectManagement.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Task = System.Threading.Tasks.Task;

namespace ConstructionProjectManagement.Controllers;

[Authorize]
public class WbsController(
    IWbsService wbsService,
    UserManager<ApplicationUser> userManager) : Controller
{
    public async Task<IActionResult> Index(
        int? projectId,
        bool includeArchived,
        string? search,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var roles = CurrentRoles();
        if (!projectId.HasValue)
        {
            var projects = await wbsService.GetVisibleProjectTreesAsync(
                user.Id,
                roles,
                search,
                cancellationToken);
            if (!projects.Succeeded)
            {
                return Failure(projects);
            }

            return View("Projects", new WbsProjectListViewModel
            {
                Projects = projects.Value!,
                Search = search?.Trim()
            });
        }

        var result = await wbsService.GetProjectTreeAsync(
            projectId.Value,
            user.Id,
            roles,
            includeArchived,
            cancellationToken);
        if (!result.Succeeded)
        {
            return Failure(result);
        }

        var tree = result.Value!;
        var canManageStructure = !tree.IsReadOnly &&
            !HasAnyRole(roles, RoleNames.Admin) &&
            HasAnyRole(roles, "Project Director", "Project Manager");
        var canUpdateStatus = !tree.IsReadOnly &&
            !HasAnyRole(roles, RoleNames.Admin) && HasAnyRole(
            roles,
            "Project Director",
            "Project Manager",
            "Site Manager");
        var canViewArchived = HasAnyRole(
            roles,
            RoleNames.Admin,
            "Project Director",
            "Project Manager");
        var canArchiveRestore = !tree.IsReadOnly && canViewArchived;

        return View(new WbsIndexViewModel
        {
            Tree = tree,
            IncludeArchived = includeArchived,
            CanManageStructure = canManageStructure,
            CanUpdateStatus = canUpdateStatus,
            CanViewArchived = canViewArchived,
            CanArchiveRestore = canArchiveRestore
        });
    }

    [Authorize(Roles = "Project Director,Project Manager")]
    public async Task<IActionResult> CreatePhase(int projectId, CancellationToken cancellationToken)
    {
        var result = await GetWritableTreeAsync(projectId, cancellationToken);
        if (result.Failure != WbsFailureKind.None)
        {
            return Failure(result);
        }

        return View(new ProjectPhaseViewModel { ProjectId = projectId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Project Director,Project Manager")]
    public async Task<IActionResult> CreatePhase(
        ProjectPhaseViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var tree = await GetWritableTreeAsync(model.ProjectId, cancellationToken);
            return tree.Succeeded ? View(model) : Failure(tree);
        }

        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var result = await wbsService.CreatePhaseAsync(
            new ProjectPhase
            {
                ProjectId = model.ProjectId,
                Name = model.Name,
                SequenceOrder = model.SequenceOrder
            },
            user.Id,
            CurrentRoles(),
            cancellationToken);
        if (!result.Succeeded)
        {
            if (result.Failure == WbsFailureKind.Validation)
            {
                ModelState.AddModelError(string.Empty, result.Error!);
                return View(model);
            }

            return Failure(result);
        }

        TempData["SuccessMessage"] = "Đã tạo giai đoạn.";
        return RedirectToAction(nameof(Index), new { projectId = model.ProjectId });
    }

    [Authorize(Roles = "Project Director,Project Manager")]
    public async Task<IActionResult> EditPhase(int id, CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var result = await wbsService.GetPhaseForEditAsync(id, user.Id, CurrentRoles(), cancellationToken);
        if (!result.Succeeded)
        {
            return Failure(result);
        }

        var phase = result.Value!;
        return View(new ProjectPhaseViewModel
        {
            Id = phase.Id,
            ProjectId = phase.ProjectId,
            Name = phase.Name,
            SequenceOrder = phase.SequenceOrder,
            Status = phase.Status
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Project Director,Project Manager")]
    public async Task<IActionResult> EditPhase(
        int id,
        ProjectPhaseViewModel model,
        CancellationToken cancellationToken)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var current = await wbsService.GetPhaseForEditAsync(id, user.Id, CurrentRoles(), cancellationToken);
        if (!current.Succeeded)
        {
            return Failure(current);
        }

        if (model.ProjectId != current.Value!.ProjectId)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await wbsService.UpdatePhaseAsync(
            new ProjectPhase
            {
                Id = id,
                ProjectId = model.ProjectId,
                Name = model.Name,
                SequenceOrder = model.SequenceOrder,
                Status = model.Status
            },
            user.Id,
            CurrentRoles(),
            cancellationToken);
        if (!result.Succeeded)
        {
            if (result.Failure == WbsFailureKind.Validation)
            {
                ModelState.AddModelError(string.Empty, result.Error!);
                return View(model);
            }

            return Failure(result);
        }

        TempData["SuccessMessage"] = "Đã cập nhật giai đoạn.";
        return RedirectToAction(nameof(Index), new { projectId = model.ProjectId });
    }

    [Authorize(Roles = "Project Director,Project Manager")]
    public async Task<IActionResult> CreateItem(int projectId, CancellationToken cancellationToken)
    {
        var result = await LoadItemOptionsAsync(projectId, cancellationToken);
        if (result is not null)
        {
            return result;
        }

        return View(new WorkItemViewModel { ProjectId = projectId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Project Director,Project Manager")]
    public async Task<IActionResult> CreateItem(
        WorkItemViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var loadFailure = await LoadItemOptionsAsync(model.ProjectId, cancellationToken);
            return loadFailure ?? View(model);
        }

        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var result = await wbsService.CreateWorkItemAsync(
            new WorkItem
            {
                ProjectId = model.ProjectId,
                ProjectPhaseId = model.ProjectPhaseId,
                ParentWorkItemId = model.ParentWorkItemId,
                WbsCode = model.WbsCode,
                Name = model.Name,
                Description = model.Description,
                SequenceOrder = model.SequenceOrder,
                Status = model.Status
            },
            user.Id,
            CurrentRoles(),
            cancellationToken);
        if (!result.Succeeded)
        {
            return await ReturnWorkItemFailureAsync(result, model, cancellationToken);
        }

        TempData["SuccessMessage"] = "Đã tạo hạng mục WBS.";
        return RedirectToAction(nameof(Index), new { projectId = model.ProjectId });
    }

    [Authorize(Roles = "Project Director,Project Manager")]
    public async Task<IActionResult> EditItem(int id, CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var result = await wbsService.GetWorkItemForEditAsync(id, user.Id, CurrentRoles(), cancellationToken);
        if (!result.Succeeded)
        {
            return Failure(result);
        }

        var item = result.Value!;
        var loadFailure = await LoadItemOptionsAsync(item.ProjectId, cancellationToken);
        if (loadFailure is not null)
        {
            return loadFailure;
        }

        return View(new WorkItemViewModel
        {
            Id = item.Id,
            ProjectId = item.ProjectId,
            ProjectPhaseId = item.ProjectPhaseId,
            ParentWorkItemId = item.ParentWorkItemId,
            WbsCode = item.WbsCode,
            Name = item.Name,
            Description = item.Description,
            SequenceOrder = item.SequenceOrder,
            Status = item.Status
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Project Director,Project Manager")]
    public async Task<IActionResult> EditItem(
        int id,
        WorkItemViewModel model,
        CancellationToken cancellationToken)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var current = await wbsService.GetWorkItemForEditAsync(id, user.Id, CurrentRoles(), cancellationToken);
        if (!current.Succeeded)
        {
            return Failure(current);
        }

        if (model.ProjectId != current.Value!.ProjectId)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            var loadFailure = await LoadItemOptionsAsync(current.Value.ProjectId, cancellationToken);
            return loadFailure ?? View(model);
        }

        var result = await wbsService.UpdateWorkItemAsync(
            new WorkItem
            {
                Id = id,
                ProjectId = model.ProjectId,
                ProjectPhaseId = model.ProjectPhaseId,
                ParentWorkItemId = model.ParentWorkItemId,
                WbsCode = model.WbsCode,
                Name = model.Name,
                Description = model.Description,
                SequenceOrder = model.SequenceOrder,
                Status = model.Status
            },
            user.Id,
            CurrentRoles(),
            cancellationToken);
        if (!result.Succeeded)
        {
            return await ReturnWorkItemFailureAsync(result, model, cancellationToken);
        }

        if (result.Warning is not null)
        {
            TempData["WarningMessage"] = result.Warning;
        }
        else
        {
            TempData["SuccessMessage"] = "Đã cập nhật hạng mục WBS.";
        }

        return RedirectToAction(nameof(Index), new { projectId = model.ProjectId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Project Director,Project Manager,Site Manager")]
    public async Task<IActionResult> UpdateStatus(
        int id,
        WbsItemStatus status,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var result = await wbsService.UpdateWorkItemStatusAsync(
            id,
            status,
            user.Id,
            CurrentRoles(),
            cancellationToken);
        if (!result.Succeeded)
        {
            return Failure(result);
        }

        TempData[result.Warning is null ? "SuccessMessage" : "WarningMessage"] =
            result.Warning ?? "Đã cập nhật trạng thái hạng mục.";
        return RedirectToAction(nameof(Index), new { projectId = result.Value!.ProjectId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Project Director,Project Manager")]
    public async Task<IActionResult> ArchivePhase(
        int id,
        int projectId,
        CancellationToken cancellationToken)
    {
        return await ArchiveOrRestoreAsync(
            id,
            projectId,
            restore: false,
            phase: true,
            cancellationToken);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Project Director,Project Manager")]
    public async Task<IActionResult> RestorePhase(
        int id,
        int projectId,
        CancellationToken cancellationToken)
    {
        return await ArchiveOrRestoreAsync(
            id,
            projectId,
            restore: true,
            phase: true,
            cancellationToken);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Project Director,Project Manager")]
    public async Task<IActionResult> ArchiveItem(
        int id,
        int projectId,
        CancellationToken cancellationToken)
    {
        return await ArchiveOrRestoreAsync(
            id,
            projectId,
            restore: false,
            phase: false,
            cancellationToken);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Project Director,Project Manager")]
    public async Task<IActionResult> RestoreItem(
        int id,
        int projectId,
        CancellationToken cancellationToken)
    {
        return await ArchiveOrRestoreAsync(
            id,
            projectId,
            restore: true,
            phase: false,
            cancellationToken);
    }

    private async Task<IActionResult> ArchiveOrRestoreAsync(
        int id,
        int projectId,
        bool restore,
        bool phase,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var roles = CurrentRoles();
        WbsOperationResult<bool> result;
        if (phase)
        {
            result = restore
                ? await wbsService.RestorePhaseAsync(id, user.Id, roles, cancellationToken)
                : await wbsService.ArchivePhaseAsync(id, user.Id, roles, cancellationToken);
        }
        else
        {
            result = restore
                ? await wbsService.RestoreWorkItemAsync(id, user.Id, roles, cancellationToken)
                : await wbsService.ArchiveWorkItemAsync(id, user.Id, roles, cancellationToken);
        }

        if (!result.Succeeded)
        {
            return Failure(result);
        }

        TempData["SuccessMessage"] = restore ? "Đã khôi phục dữ liệu WBS." : "Đã lưu trữ dữ liệu WBS.";
        return RedirectToAction(nameof(Index), new { projectId });
    }

    private async Task<IActionResult?> LoadItemOptionsAsync(
        int projectId,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var result = await wbsService.GetProjectTreeForStructureEditAsync(
            projectId,
            user.Id,
            CurrentRoles(),
            cancellationToken);
        if (!result.Succeeded)
        {
            return Failure(result);
        }

        ViewBag.Phases = result.Value!.Phases.Select(phase => phase.Phase).ToList();
        ViewBag.WorkItems = Flatten(result.Value.Phases.SelectMany(phase => phase.RootItems)).ToList();
        return null;
    }

    private async Task<WbsOperationResult<WbsProjectTree>> GetWritableTreeAsync(
        int projectId,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return WbsOperationResult<WbsProjectTree>.Forbidden();
        }

        return await wbsService.GetProjectTreeForStructureEditAsync(
            projectId,
            user.Id,
            CurrentRoles(),
            cancellationToken);
    }

    private async Task<IActionResult> ReturnWorkItemFailureAsync(
        WbsOperationResult<WorkItem> result,
        WorkItemViewModel model,
        CancellationToken cancellationToken)
    {
        if (result.Failure != WbsFailureKind.Validation)
        {
            return Failure(result);
        }

        ModelState.AddModelError(string.Empty, result.Error!);
        var loadFailure = await LoadItemOptionsAsync(model.ProjectId, cancellationToken);
        return loadFailure ?? View(model);
    }

    private IReadOnlyCollection<string> CurrentRoles() =>
        RoleNames.All.Where(User.IsInRole).ToArray();

    private static bool HasAnyRole(IReadOnlyCollection<string> roles, params string[] expected) =>
        roles.Any(role => expected.Contains(role, StringComparer.Ordinal));

    private IActionResult Failure<T>(WbsOperationResult<T> result) =>
        result.Failure switch
        {
            WbsFailureKind.Forbidden => Forbid(),
            WbsFailureKind.NotFound => NotFound(),
            WbsFailureKind.Validation => BadRequest(result.Error),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };

    private static IEnumerable<WorkItem> Flatten(IEnumerable<WbsItemTree> roots)
    {
        foreach (var node in roots)
        {
            yield return node.Item;
            foreach (var child in Flatten(node.Children))
            {
                yield return child;
            }
        }
    }
}

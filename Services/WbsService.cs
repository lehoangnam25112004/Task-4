using ConstructionProjectManagement.Data;
using ConstructionProjectManagement.Models;
using ConstructionProjectManagement.Services.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ConstructionProjectManagement.Services;

public sealed class WbsService(ApplicationDbContext dbContext) : IWbsService
{
    private static readonly string[] StructureRoles =
    [
        "Project Director",
        "Project Manager"
    ];

    private static readonly string[] StatusRoles =
    [
        "Project Director",
        "Project Manager",
        "Site Manager"
    ];

    private static readonly string[] ArchiveRoles =
    [
        RoleNames.Admin,
        "Project Director",
        "Project Manager"
    ];

    private static readonly string[] TaskLinkRoles =
    [
        "Project Manager",
        "Site Manager",
        "Supervising Engineer",
        "Materials Staff"
    ];

    public async Task<WbsOperationResult<IReadOnlyList<WbsProjectTree>>> GetVisibleProjectTreesAsync(
        string userId,
        IReadOnlyCollection<string> roles,
        string? search,
        CancellationToken cancellationToken = default)
    {
        if (!HasKnownRole(roles) ||
            !await dbContext.Users.AnyAsync(
                user => user.Id == userId && user.IsActive,
                cancellationToken))
        {
            return WbsOperationResult<IReadOnlyList<WbsProjectTree>>.Forbidden();
        }

        var projects = dbContext.Projects
            .AsNoTracking()
            .Where(project => !project.IsArchived);

        if (!HasRole(roles, RoleNames.Admin))
        {
            projects = projects.Where(project =>
                project.Members.Any(member => member.UserId == userId && member.IsActive));
        }

        var candidates = await projects
            .OrderBy(project => project.CreatedAt)
            .ThenBy(project => project.Id)
            .Select(project => new { project.Id, project.ClientName })
            .ToListAsync(cancellationToken);

        if (HasRole(roles, "Client Representative") && !HasRole(roles, RoleNames.Admin))
        {
            var organizationName = await dbContext.Users
                .Where(user => user.Id == userId)
                .Select(user => user.OrganizationName)
                .SingleOrDefaultAsync(cancellationToken);
            candidates = candidates
                .Where(project => OrganizationsMatch(organizationName, project.ClientName))
                .ToList();
        }

        var term = search?.Trim();
        var trees = new List<WbsProjectTree>();
        foreach (var candidate in candidates)
        {
            var result = await GetProjectTreeAsync(candidate.Id, userId, roles, cancellationToken: cancellationToken);
            if (!result.Succeeded)
            {
                return CopyFailure<WbsProjectTree, IReadOnlyList<WbsProjectTree>>(result);
            }

            var tree = result.Value!;
            if (string.IsNullOrEmpty(term) ||
                tree.Project.ProjectCode.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                tree.Project.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                tree.Phases.Any(phase => MatchesItemSearch(phase.RootItems, term)))
            {
                trees.Add(tree);
            }
        }

        return WbsOperationResult<IReadOnlyList<WbsProjectTree>>.Success(trees);
    }

    private static bool MatchesItemSearch(IEnumerable<WbsItemTree> items, string term) =>
        items.Any(node =>
            node.Item.WbsCode.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            node.Item.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            MatchesItemSearch(node.Children, term));

    public async Task<WbsOperationResult<WbsProjectTree>> GetProjectTreeForStructureEditAsync(
        int projectId,
        string userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default)
    {
        var access = await AuthorizeProjectAsync(
            projectId, userId, roles, WbsPermission.ManageStructure, cancellationToken);
        if (!access.Succeeded)
        {
            return CopyFailure<Project, WbsProjectTree>(access);
        }

        if (IsProjectReadOnly(access.Value!))
        {
            return WbsOperationResult<WbsProjectTree>.Forbidden();
        }

        return await GetProjectTreeAsync(projectId, userId, roles, cancellationToken: cancellationToken);
    }

    public async Task<WbsOperationResult<WbsProjectTree>> GetProjectTreeAsync(
        int projectId,
        string userId,
        IReadOnlyCollection<string> roles,
        bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        var access = await AuthorizeProjectAsync(
            projectId,
            userId,
            roles,
            WbsPermission.View,
            cancellationToken);
        if (!access.Succeeded)
        {
            return CopyFailure<Project, WbsProjectTree>(access);
        }

        if (includeArchived && !CanViewArchived(roles))
        {
            return WbsOperationResult<WbsProjectTree>.Forbidden();
        }

        var project = access.Value!;
        var phases = await dbContext.ProjectPhases
            .AsNoTracking()
            .Where(phase => phase.ProjectId == projectId &&
                            (includeArchived || !phase.IsArchived))
            .OrderBy(phase => phase.SequenceOrder)
            .ThenBy(phase => phase.Id)
            .ToListAsync(cancellationToken);

        var phaseIds = phases.Select(phase => phase.Id).ToArray();
        var items = await dbContext.WorkItems
            .AsNoTracking()
            .Where(item => item.ProjectId == projectId &&
                           phaseIds.Contains(item.ProjectPhaseId) &&
                           (includeArchived || !item.IsArchived))
            .OrderBy(item => item.SequenceOrder)
            .ThenBy(item => item.WbsCode)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

        var itemIds = items.Select(item => item.Id).ToArray();
        var linkedTaskCounts = itemIds.Length == 0
            ? new Dictionary<int, int>()
            : await dbContext.Tasks
                .Where(task => task.WorkItemId.HasValue && itemIds.Contains(task.WorkItemId.Value))
                .GroupBy(task => task.WorkItemId!.Value)
                .Select(group => new { WorkItemId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(group => group.WorkItemId, group => group.Count, cancellationToken);

        var itemsByPhase = items
            .GroupBy(item => item.ProjectPhaseId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var phaseTrees = new List<WbsPhaseTree>(phases.Count);
        foreach (var phase in phases)
        {
            var phaseItems = itemsByPhase.GetValueOrDefault(phase.Id, []);
            var builtItems = BuildTree(phaseItems, linkedTaskCounts);
            if (builtItems.Failure is not null)
            {
                return WbsOperationResult<WbsProjectTree>.Invalid(builtItems.Failure);
            }

            phaseTrees.Add(new WbsPhaseTree(phase, builtItems.Roots));
        }

        return WbsOperationResult<WbsProjectTree>.Success(
            new WbsProjectTree(project, phaseTrees, IsProjectReadOnly(project)));
    }

    public async Task<WbsOperationResult<ProjectPhase>> GetPhaseForEditAsync(
        int phaseId,
        string userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default)
    {
        var phase = await dbContext.ProjectPhases
            .SingleOrDefaultAsync(item => item.Id == phaseId, cancellationToken);
        if (phase is null)
        {
            return WbsOperationResult<ProjectPhase>.NotFound();
        }

        var access = await AuthorizeProjectAsync(
            phase.ProjectId,
            userId,
            roles,
            WbsPermission.ManageStructure,
            cancellationToken);
        if (!access.Succeeded)
        {
            return CopyFailure<Project, ProjectPhase>(access);
        }

        if (phase.IsArchived)
        {
            return WbsOperationResult<ProjectPhase>.NotFound();
        }

        if (IsProjectReadOnly(access.Value!))
        {
            return WbsOperationResult<ProjectPhase>.Forbidden();
        }

        return WbsOperationResult<ProjectPhase>.Success(phase);
    }

    public async Task<WbsOperationResult<ProjectPhase>> CreatePhaseAsync(
        ProjectPhase phase,
        string userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default)
    {
        var access = await AuthorizeProjectAsync(
            phase.ProjectId,
            userId,
            roles,
            WbsPermission.ManageStructure,
            cancellationToken);
        if (!access.Succeeded)
        {
            return CopyFailure<Project, ProjectPhase>(access);
        }

        if (IsProjectReadOnly(access.Value!))
        {
            return WbsOperationResult<ProjectPhase>.Forbidden();
        }

        var validation = ValidatePhase(phase);
        if (validation is not null)
        {
            return WbsOperationResult<ProjectPhase>.Invalid(validation);
        }

        phase.Name = phase.Name.Trim();
        phase.Status = ProjectPhaseStatus.NotStarted;
        dbContext.ProjectPhases.Add(phase);
        await dbContext.SaveChangesAsync(cancellationToken);
        return WbsOperationResult<ProjectPhase>.Success(phase);
    }

    public async Task<WbsOperationResult<ProjectPhase>> UpdatePhaseAsync(
        ProjectPhase phase,
        string userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.ProjectPhases
            .SingleOrDefaultAsync(item => item.Id == phase.Id, cancellationToken);
        if (existing is null)
        {
            return WbsOperationResult<ProjectPhase>.NotFound();
        }

        var access = await AuthorizeProjectAsync(
            existing.ProjectId,
            userId,
            roles,
            WbsPermission.ManageStructure,
            cancellationToken);
        if (!access.Succeeded)
        {
            return CopyFailure<Project, ProjectPhase>(access);
        }

        if (IsProjectReadOnly(access.Value!) || existing.IsArchived)
        {
            return WbsOperationResult<ProjectPhase>.Forbidden();
        }

        if (phase.ProjectId != existing.ProjectId)
        {
            return WbsOperationResult<ProjectPhase>.Invalid("Không thể chuyển giai đoạn sang dự án khác.");
        }

        var validation = ValidatePhase(phase);
        if (validation is not null)
        {
            return WbsOperationResult<ProjectPhase>.Invalid(validation);
        }

        existing.Name = phase.Name.Trim();
        existing.SequenceOrder = phase.SequenceOrder;
        existing.Status = phase.Status;
        await dbContext.SaveChangesAsync(cancellationToken);
        return WbsOperationResult<ProjectPhase>.Success(existing);
    }

    public async Task<WbsOperationResult<WorkItem>> GetWorkItemForEditAsync(
        int workItemId,
        string userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default)
    {
        var item = await dbContext.WorkItems
            .SingleOrDefaultAsync(workItem => workItem.Id == workItemId, cancellationToken);
        if (item is null)
        {
            return WbsOperationResult<WorkItem>.NotFound();
        }

        var access = await AuthorizeProjectAsync(
            item.ProjectId,
            userId,
            roles,
            WbsPermission.ManageStructure,
            cancellationToken);
        if (!access.Succeeded)
        {
            return CopyFailure<Project, WorkItem>(access);
        }

        if (IsProjectReadOnly(access.Value!) || item.IsArchived)
        {
            return WbsOperationResult<WorkItem>.Forbidden();
        }

        return WbsOperationResult<WorkItem>.Success(item);
    }

    public async Task<WbsOperationResult<WorkItem>> CreateWorkItemAsync(
        WorkItem workItem,
        string userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default)
    {
        var access = await AuthorizeProjectAsync(
            workItem.ProjectId,
            userId,
            roles,
            WbsPermission.ManageStructure,
            cancellationToken);
        if (!access.Succeeded)
        {
            return CopyFailure<Project, WorkItem>(access);
        }

        if (IsProjectReadOnly(access.Value!))
        {
            return WbsOperationResult<WorkItem>.Forbidden();
        }

        var validation = await ValidateWorkItemAsync(workItem, null, cancellationToken);
        if (validation is not null)
        {
            return WbsOperationResult<WorkItem>.Invalid(validation);
        }

        workItem.WbsCode = workItem.WbsCode.Trim();
        workItem.Name = workItem.Name.Trim();
        workItem.Description = workItem.Description?.Trim();
        workItem.Level = await GetLevelForParentAsync(
            workItem.ParentWorkItemId,
            cancellationToken);
        workItem.CreatedByUserId = userId;
        workItem.CreatedAt = DateTime.UtcNow;
        workItem.UpdatedByUserId = userId;
        workItem.UpdatedAt = workItem.CreatedAt;
        dbContext.WorkItems.Add(workItem);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsWbsCodeConflict(exception))
        {
            return WbsOperationResult<WorkItem>.Invalid("Mã WBS đã tồn tại trong dự án (không phân biệt hoa thường).");
        }

        return WbsOperationResult<WorkItem>.Success(workItem);
    }

    public async Task<WbsOperationResult<WorkItem>> UpdateWorkItemAsync(
        WorkItem workItem,
        string userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.WorkItems
            .SingleOrDefaultAsync(item => item.Id == workItem.Id, cancellationToken);
        if (existing is null)
        {
            return WbsOperationResult<WorkItem>.NotFound();
        }

        var access = await AuthorizeProjectAsync(
            existing.ProjectId,
            userId,
            roles,
            WbsPermission.ManageStructure,
            cancellationToken);
        if (!access.Succeeded)
        {
            return CopyFailure<Project, WorkItem>(access);
        }

        if (IsProjectReadOnly(access.Value!) || existing.IsArchived)
        {
            return WbsOperationResult<WorkItem>.Forbidden();
        }

        if (workItem.ProjectId != existing.ProjectId)
        {
            return WbsOperationResult<WorkItem>.Invalid("Không thể chuyển hạng mục sang dự án khác.");
        }

        var validation = await ValidateWorkItemAsync(workItem, existing.Id, cancellationToken);
        if (validation is not null)
        {
            return WbsOperationResult<WorkItem>.Invalid(validation);
        }

        var allProjectItems = await dbContext.WorkItems
            .Where(item => item.ProjectId == existing.ProjectId)
            .ToListAsync(cancellationToken);
        if (!TryRecalculateLevels(workItem, allProjectItems, out var levelError))
        {
            return WbsOperationResult<WorkItem>.Invalid(levelError!);
        }

        existing.ProjectPhaseId = workItem.ProjectPhaseId;
        existing.ParentWorkItemId = workItem.ParentWorkItemId;
        existing.WbsCode = workItem.WbsCode.Trim();
        existing.Name = workItem.Name.Trim();
        existing.Description = workItem.Description?.Trim();
        existing.Level = workItem.Level;
        existing.SequenceOrder = workItem.SequenceOrder;
        existing.Status = workItem.Status;
        existing.UpdatedByUserId = userId;
        existing.UpdatedAt = DateTime.UtcNow;

        var warning = await HasIncompleteChildrenAsync(existing.Id, cancellationToken)
            ? "Cảnh báo: hạng mục cha được đánh dấu Hoàn thành nhưng vẫn còn hạng mục con chưa hoàn thành."
            : null;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsWbsCodeConflict(exception))
        {
            return WbsOperationResult<WorkItem>.Invalid("Mã WBS đã tồn tại trong dự án (không phân biệt hoa thường).");
        }

        return WbsOperationResult<WorkItem>.Success(existing, warning);
    }

    public async Task<WbsOperationResult<WorkItem>> UpdateWorkItemStatusAsync(
        int workItemId,
        WbsItemStatus status,
        string userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(status) || status == WbsItemStatus.Archived)
        {
            return WbsOperationResult<WorkItem>.Invalid("Trạng thái lưu trữ chỉ được thay đổi bằng thao tác Lưu trữ.");
        }

        var item = await dbContext.WorkItems
            .SingleOrDefaultAsync(workItem => workItem.Id == workItemId, cancellationToken);
        if (item is null)
        {
            return WbsOperationResult<WorkItem>.NotFound();
        }

        var access = await AuthorizeProjectAsync(
            item.ProjectId,
            userId,
            roles,
            WbsPermission.UpdateStatus,
            cancellationToken);
        if (!access.Succeeded)
        {
            return CopyFailure<Project, WorkItem>(access);
        }

        if (IsProjectReadOnly(access.Value!) || item.IsArchived)
        {
            return WbsOperationResult<WorkItem>.Forbidden();
        }

        item.Status = status;
        item.UpdatedByUserId = userId;
        item.UpdatedAt = DateTime.UtcNow;
        var warning = status == WbsItemStatus.Completed &&
                      await HasIncompleteChildrenAsync(item.Id, cancellationToken)
            ? "Cảnh báo: hạng mục cha được đánh dấu Hoàn thành nhưng vẫn còn hạng mục con chưa hoàn thành."
            : null;

        await dbContext.SaveChangesAsync(cancellationToken);
        return WbsOperationResult<WorkItem>.Success(item, warning);
    }

    public async Task<WbsOperationResult<bool>> ArchivePhaseAsync(
        int phaseId,
        string userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default)
    {
        var phase = await dbContext.ProjectPhases
            .SingleOrDefaultAsync(item => item.Id == phaseId, cancellationToken);
        if (phase is null)
        {
            return WbsOperationResult<bool>.NotFound();
        }

        var access = await AuthorizeProjectAsync(
            phase.ProjectId,
            userId,
            roles,
            WbsPermission.ArchiveRestore,
            cancellationToken);
        if (!access.Succeeded)
        {
            return CopyFailure<Project, bool>(access);
        }

        if (IsProjectReadOnly(access.Value!) || phase.IsArchived)
        {
            return WbsOperationResult<bool>.Forbidden();
        }

        var now = DateTime.UtcNow;
        var groupId = Guid.NewGuid();
        phase.StatusBeforeArchive = phase.Status;
        phase.Status = ProjectPhaseStatus.Archived;
        SetArchived(phase, userId, now, groupId);

        var phaseItems = await dbContext.WorkItems
            .Where(item => item.ProjectPhaseId == phaseId && !item.IsArchived)
            .ToListAsync(cancellationToken);
        foreach (var item in phaseItems)
        {
            Archive(item, userId, now, groupId);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return WbsOperationResult<bool>.Success(true);
    }

    public async Task<WbsOperationResult<bool>> RestorePhaseAsync(
        int phaseId,
        string userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default)
    {
        var phase = await dbContext.ProjectPhases
            .SingleOrDefaultAsync(item => item.Id == phaseId, cancellationToken);
        if (phase is null)
        {
            return WbsOperationResult<bool>.NotFound();
        }

        var access = await AuthorizeProjectAsync(
            phase.ProjectId,
            userId,
            roles,
            WbsPermission.ArchiveRestore,
            cancellationToken);
        if (!access.Succeeded)
        {
            return CopyFailure<Project, bool>(access);
        }

        if (IsProjectReadOnly(access.Value!) || !phase.IsArchived)
        {
            return WbsOperationResult<bool>.Forbidden();
        }

        var archiveGroupId = phase.ArchiveGroupId;
        Restore(phase);
        if (archiveGroupId.HasValue)
        {
            var groupedItems = await dbContext.WorkItems
                .Where(item => item.ProjectPhaseId == phaseId &&
                               item.IsArchived &&
                               item.ArchiveGroupId == archiveGroupId)
                .ToListAsync(cancellationToken);
            foreach (var item in groupedItems)
            {
                Restore(item);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return WbsOperationResult<bool>.Success(true);
    }

    public async Task<WbsOperationResult<bool>> ArchiveWorkItemAsync(
        int workItemId,
        string userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default)
    {
        var item = await dbContext.WorkItems
            .SingleOrDefaultAsync(workItem => workItem.Id == workItemId, cancellationToken);
        if (item is null)
        {
            return WbsOperationResult<bool>.NotFound();
        }

        var access = await AuthorizeProjectAsync(
            item.ProjectId,
            userId,
            roles,
            WbsPermission.ArchiveRestore,
            cancellationToken);
        if (!access.Succeeded)
        {
            return CopyFailure<Project, bool>(access);
        }

        if (IsProjectReadOnly(access.Value!) || item.IsArchived)
        {
            return WbsOperationResult<bool>.Forbidden();
        }

        var projectItems = await dbContext.WorkItems
            .Where(workItem => workItem.ProjectId == item.ProjectId)
            .ToListAsync(cancellationToken);
        var descendants = GetSubtree(item.Id, projectItems);
        if (descendants is null)
        {
            return WbsOperationResult<bool>.Invalid("Không thể lưu trữ do cây WBS có vòng lặp hoặc liên kết không hợp lệ.");
        }

        var now = DateTime.UtcNow;
        var groupId = Guid.NewGuid();
        foreach (var descendant in descendants.Where(descendant => !descendant.IsArchived))
        {
            Archive(descendant, userId, now, groupId);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return WbsOperationResult<bool>.Success(true);
    }

    public async Task<WbsOperationResult<bool>> RestoreWorkItemAsync(
        int workItemId,
        string userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default)
    {
        var item = await dbContext.WorkItems
            .SingleOrDefaultAsync(workItem => workItem.Id == workItemId, cancellationToken);
        if (item is null)
        {
            return WbsOperationResult<bool>.NotFound();
        }

        var access = await AuthorizeProjectAsync(
            item.ProjectId,
            userId,
            roles,
            WbsPermission.ArchiveRestore,
            cancellationToken);
        if (!access.Succeeded)
        {
            return CopyFailure<Project, bool>(access);
        }

        if (IsProjectReadOnly(access.Value!) || !item.IsArchived)
        {
            return WbsOperationResult<bool>.Forbidden();
        }

        var phase = await dbContext.ProjectPhases
            .SingleOrDefaultAsync(candidate => candidate.Id == item.ProjectPhaseId, cancellationToken);
        if (phase is null)
        {
            return WbsOperationResult<bool>.Invalid("Không tìm thấy giai đoạn của hạng mục.");
        }

        if (phase.IsArchived)
        {
            Restore(phase);
        }

        var projectItems = await dbContext.WorkItems
            .Where(candidate => candidate.ProjectId == item.ProjectId)
            .ToListAsync(cancellationToken);
        var byId = projectItems.ToDictionary(candidate => candidate.Id);
        var chain = new List<WorkItem>();
        var visited = new HashSet<int>();
        var current = item;
        while (true)
        {
            if (!visited.Add(current.Id))
            {
                return WbsOperationResult<bool>.Invalid("Không thể khôi phục do cây WBS có vòng lặp.");
            }

            chain.Add(current);
            if (!current.ParentWorkItemId.HasValue)
            {
                break;
            }

            if (!byId.TryGetValue(current.ParentWorkItemId.Value, out current!))
            {
                return WbsOperationResult<bool>.Invalid("Không tìm thấy hạng mục cha để khôi phục cây WBS.");
            }
        }

        foreach (var node in chain.Where(node => node.IsArchived))
        {
            Restore(node);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return WbsOperationResult<bool>.Success(true);
    }

    public async Task<WbsOperationResult<IReadOnlyCollection<WorkItem>>> GetTaskWorkItemsAsync(
        int projectId,
        int? taskId,
        string userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default)
    {
        var access = await AuthorizeProjectAsync(
            projectId,
            userId,
            roles,
            WbsPermission.LinkTask,
            cancellationToken);
        if (!access.Succeeded)
        {
            return CopyFailure<Project, IReadOnlyCollection<WorkItem>>(access);
        }

        if (IsProjectReadOnly(access.Value!))
        {
            return WbsOperationResult<IReadOnlyCollection<WorkItem>>.Forbidden();
        }

        int? currentWorkItemId = null;
        if (taskId.HasValue)
        {
            currentWorkItemId = await dbContext.Tasks
                .Where(task => task.Id == taskId && task.ProjectId == projectId)
                .Select(task => task.WorkItemId)
                .SingleOrDefaultAsync(cancellationToken);
            if (!currentWorkItemId.HasValue &&
                !await dbContext.Tasks.AnyAsync(
                    task => task.Id == taskId && task.ProjectId == projectId,
                    cancellationToken))
            {
                return WbsOperationResult<IReadOnlyCollection<WorkItem>>.NotFound();
            }
        }

        var items = await dbContext.WorkItems
            .AsNoTracking()
            .Where(item => item.ProjectId == projectId &&
                           ((!item.IsArchived && !item.ProjectPhase.IsArchived) ||
                            item.Id == currentWorkItemId))
            .OrderBy(item => item.WbsCode)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
        return WbsOperationResult<IReadOnlyCollection<WorkItem>>.Success(items);
    }

    public async Task<WbsOperationResult<bool>> CanLinkWorkItemToTaskAsync(
        int projectId,
        int? workItemId,
        int? taskId,
        string userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default)
    {
        var access = await AuthorizeProjectAsync(
            projectId,
            userId,
            roles,
            WbsPermission.LinkTask,
            cancellationToken);
        if (!access.Succeeded)
        {
            return CopyFailure<Project, bool>(access);
        }

        if (IsProjectReadOnly(access.Value!))
        {
            return WbsOperationResult<bool>.Forbidden();
        }

        if (!workItemId.HasValue)
        {
            return WbsOperationResult<bool>.Success(true);
        }

        var item = await dbContext.WorkItems
            .Include(workItem => workItem.ProjectPhase)
            .SingleOrDefaultAsync(workItem => workItem.Id == workItemId.Value, cancellationToken);
        if (item is null || item.ProjectId != projectId)
        {
            return WbsOperationResult<bool>.Invalid("WBS phải thuộc cùng dự án.");
        }

        int? existingWorkItemId = null;
        if (taskId.HasValue)
        {
            var existingTask = await dbContext.Tasks
                .SingleOrDefaultAsync(task => task.Id == taskId && task.ProjectId == projectId, cancellationToken);
            if (existingTask is null)
            {
                return WbsOperationResult<bool>.NotFound();
            }

            existingWorkItemId = existingTask.WorkItemId;
        }

        if ((item.IsArchived || item.ProjectPhase.IsArchived) && existingWorkItemId != item.Id)
        {
            return WbsOperationResult<bool>.Invalid("Không thể gắn công việc mới vào hạng mục WBS đã lưu trữ.");
        }

        return WbsOperationResult<bool>.Success(true);
    }

    private async Task<WbsOperationResult<Project>> AuthorizeProjectAsync(
        int projectId,
        string userId,
        IReadOnlyCollection<string> roles,
        WbsPermission permission,
        CancellationToken cancellationToken)
    {
        if (!HasKnownRole(roles) || !await UserIsActiveAsync(userId, cancellationToken))
        {
            return WbsOperationResult<Project>.Forbidden();
        }

        if (!HasPermission(roles, permission))
        {
            return WbsOperationResult<Project>.Forbidden();
        }

        var project = await dbContext.Projects
            .SingleOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken);
        if (project is null)
        {
            return WbsOperationResult<Project>.NotFound();
        }

        if (!HasRole(roles, RoleNames.Admin))
        {
            var member = await dbContext.ProjectMembers
                .Include(candidate => candidate.User)
                .SingleOrDefaultAsync(
                    candidate => candidate.ProjectId == projectId &&
                                 candidate.UserId == userId &&
                                 candidate.IsActive,
                    cancellationToken);
            if (member is null)
            {
                return WbsOperationResult<Project>.NotFound();
            }

            if (HasRole(roles, "Client Representative") &&
                !OrganizationsMatch(member.User.OrganizationName, project.ClientName))
            {
                return WbsOperationResult<Project>.NotFound();
            }
        }

        return WbsOperationResult<Project>.Success(project);
    }

    private async Task<bool> UserIsActiveAsync(string userId, CancellationToken cancellationToken) =>
        await dbContext.Users.AnyAsync(
            user => user.Id == userId && user.IsActive,
            cancellationToken);

    private static bool HasKnownRole(IReadOnlyCollection<string> roles) =>
        roles.Any(role => RoleNames.All.Contains(role, StringComparer.Ordinal));

    private static bool HasRole(IReadOnlyCollection<string> roles, string role) =>
        roles.Contains(role, StringComparer.Ordinal);

    private static bool HasPermission(IReadOnlyCollection<string> roles, WbsPermission permission)
    {
        if (HasRole(roles, RoleNames.Admin))
        {
            return permission is WbsPermission.View or WbsPermission.ArchiveRestore;
        }

        return permission switch
        {
            WbsPermission.View => HasKnownRole(roles),
            WbsPermission.ManageStructure => roles.Any(role =>
                StructureRoles.Contains(role, StringComparer.Ordinal)),
            WbsPermission.UpdateStatus => roles.Any(role =>
                StatusRoles.Contains(role, StringComparer.Ordinal)),
            WbsPermission.ArchiveRestore => roles.Any(role =>
                ArchiveRoles.Contains(role, StringComparer.Ordinal)),
            WbsPermission.LinkTask => roles.Any(role =>
                TaskLinkRoles.Contains(role, StringComparer.Ordinal)),
            _ => false
        };
    }

    private static bool CanViewArchived(IReadOnlyCollection<string> roles) =>
        roles.Any(role => ArchiveRoles.Contains(role, StringComparer.Ordinal));

    private static bool IsProjectReadOnly(Project project) =>
        project.IsArchived ||
        project.Status is ProjectStatus.Completed or ProjectStatus.Cancelled;

    private static bool OrganizationsMatch(string? userOrganization, string? projectClient)
    {
        if (string.IsNullOrWhiteSpace(userOrganization) || string.IsNullOrWhiteSpace(projectClient))
        {
            return false;
        }

        return string.Equals(
            userOrganization.Trim(),
            projectClient.Trim(),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string? ValidatePhase(ProjectPhase phase)
    {
        if (string.IsNullOrWhiteSpace(phase.Name))
        {
            return "Tên giai đoạn không được để trống.";
        }

        if (phase.Name.Trim().Length > 200)
        {
            return "Tên giai đoạn không được vượt quá 200 ký tự.";
        }

        if (phase.SequenceOrder < 0)
        {
            return "Thứ tự giai đoạn không hợp lệ.";
        }

        if (!Enum.IsDefined(phase.Status) || phase.Status == ProjectPhaseStatus.Archived)
        {
            return "Trạng thái giai đoạn không hợp lệ.";
        }

        return null;
    }

    private async Task<string?> ValidateWorkItemAsync(
        WorkItem item,
        int? existingId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(item.WbsCode) || item.WbsCode.Trim().Length > 50)
        {
            return "Mã WBS không được để trống và tối đa 50 ký tự.";
        }

        if (string.IsNullOrWhiteSpace(item.Name) || item.Name.Trim().Length > 250)
        {
            return "Tên hạng mục không được để trống và tối đa 250 ký tự.";
        }

        if (item.Description?.Length > 1000)
        {
            return "Mô tả không được vượt quá 1000 ký tự.";
        }

        if (item.SequenceOrder < 0)
        {
            return "Thứ tự hạng mục không hợp lệ.";
        }

        if (!Enum.IsDefined(item.Status) || item.Status == WbsItemStatus.Archived)
        {
            return "Trạng thái lưu trữ chỉ được thay đổi bằng thao tác Lưu trữ.";
        }

        var phase = await dbContext.ProjectPhases
            .SingleOrDefaultAsync(candidate => candidate.Id == item.ProjectPhaseId, cancellationToken);
        if (phase is null || phase.ProjectId != item.ProjectId || phase.IsArchived)
        {
            return "Giai đoạn phải đang hoạt động và thuộc cùng dự án.";
        }

        if (item.ParentWorkItemId.HasValue)
        {
            var parent = await dbContext.WorkItems
                .SingleOrDefaultAsync(candidate => candidate.Id == item.ParentWorkItemId, cancellationToken);
            if (parent is null ||
                parent.ProjectId != item.ProjectId ||
                parent.ProjectPhaseId != item.ProjectPhaseId ||
                parent.IsArchived ||
                parent.Id == existingId)
            {
                return "Hạng mục cha phải đang hoạt động và thuộc cùng dự án, cùng giai đoạn.";
            }

            var parentIds = await GetAncestorIdsAsync(parent, cancellationToken);
            if (parentIds is null || (existingId.HasValue && parentIds.Contains(existingId.Value)))
            {
                return "Không thể chuyển hạng mục vào chính nó hoặc hậu duệ của nó.";
            }
        }

        var normalizedCode = item.WbsCode.Trim();
        var codes = await dbContext.WorkItems
            .Where(candidate => candidate.ProjectId == item.ProjectId &&
                                candidate.Id != existingId)
            .Select(candidate => candidate.WbsCode)
            .ToListAsync(cancellationToken);
        if (codes.Any(code => string.Equals(code, normalizedCode, StringComparison.OrdinalIgnoreCase)))
        {
            return "Mã WBS đã tồn tại trong dự án (không phân biệt hoa thường).";
        }

        return null;
    }

    private async Task<HashSet<int>?> GetAncestorIdsAsync(
        WorkItem item,
        CancellationToken cancellationToken)
    {
        var result = new HashSet<int>();
        var current = item;
        while (current.ParentWorkItemId.HasValue)
        {
            if (!result.Add(current.Id))
            {
                return null;
            }

            var parentId = current.ParentWorkItemId.Value;
            current = await dbContext.WorkItems
                .SingleOrDefaultAsync(candidate => candidate.Id == parentId, cancellationToken);
            if (current is null)
            {
                return null;
            }
        }

        result.Add(current.Id);
        return result;
    }

    private async Task<int> GetLevelForParentAsync(
        int? parentWorkItemId,
        CancellationToken cancellationToken)
    {
        if (!parentWorkItemId.HasValue)
        {
            return 1;
        }

        var parent = await dbContext.WorkItems
            .SingleAsync(item => item.Id == parentWorkItemId.Value, cancellationToken);
        return parent.Level + 1;
    }

    private static bool TryRecalculateLevels(
        WorkItem editedItem,
        IReadOnlyCollection<WorkItem> allItems,
        out string? error)
    {
        var byId = allItems.ToDictionary(item => item.Id);
        var children = allItems
            .Where(item => item.ParentWorkItemId.HasValue)
            .GroupBy(item => item.ParentWorkItemId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());
        var visiting = new HashSet<int>();
        var visited = new HashSet<int>();

        if (editedItem.ParentWorkItemId.HasValue)
        {
            var parent = byId[editedItem.ParentWorkItemId.Value];
            editedItem.Level = parent.Level + 1;
        }
        else
        {
            editedItem.Level = 1;
        }

        if (!SetChildLevels(editedItem, editedItem.Level, children, visiting, visited, out error))
        {
            return false;
        }

        error = null;
        return true;
    }

    private static bool SetChildLevels(
        WorkItem parent,
        int parentLevel,
        IReadOnlyDictionary<int, List<WorkItem>> children,
        HashSet<int> visiting,
        HashSet<int> visited,
        out string? error)
    {
        if (!visiting.Add(parent.Id))
        {
            error = "Không thể cập nhật do cây WBS có vòng lặp.";
            return false;
        }

        if (children.TryGetValue(parent.Id, out var directChildren))
        {
            foreach (var child in directChildren)
            {
                if (child.ProjectId != parent.ProjectId || child.ProjectPhaseId != parent.ProjectPhaseId)
                {
                    error = "Nhánh WBS có hạng mục khác dự án hoặc giai đoạn.";
                    return false;
                }

                if (child.Id == parent.Id || !visited.Add(child.Id))
                {
                    error = "Không thể cập nhật do cây WBS có vòng lặp.";
                    return false;
                }

                child.Level = parentLevel + 1;
                if (!SetChildLevels(child, child.Level, children, visiting, visited, out error))
                {
                    return false;
                }
            }
        }

        visiting.Remove(parent.Id);
        error = null;
        return true;
    }

    private async Task<bool> HasIncompleteChildrenAsync(
        int workItemId,
        CancellationToken cancellationToken)
    {
        var items = await dbContext.WorkItems
            .Where(item => item.ParentWorkItemId == workItemId && !item.IsArchived)
            .Select(item => new { item.Id, item.Status })
            .ToListAsync(cancellationToken);
        return items.Any(item => item.Status != WbsItemStatus.Completed);
    }

    private static (IReadOnlyList<WbsItemTree> Roots, string? Failure) BuildTree(
        IReadOnlyCollection<WorkItem> items,
        IReadOnlyDictionary<int, int> linkedTaskCounts)
    {
        var byId = items.ToDictionary(item => item.Id);
        var children = items
            .Where(item => item.ParentWorkItemId.HasValue)
            .GroupBy(item => item.ParentWorkItemId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(item => item.SequenceOrder)
                    .ThenBy(item => item.WbsCode, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(item => item.Id)
                    .ToList());
        var visited = new HashSet<int>();
        var roots = items
            .Where(item => !item.ParentWorkItemId.HasValue)
            .OrderBy(item => item.SequenceOrder)
            .ThenBy(item => item.WbsCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id)
            .ToList();
        var rootTrees = new List<WbsItemTree>(roots.Count);
        foreach (var root in roots)
        {
            if (!TryBuildNode(root, 1, out var node))
            {
                return ([], "Không thể hiển thị cây WBS vì dữ liệu có vòng lặp hoặc thiếu hạng mục cha.");
            }

            rootTrees.Add(node!);
        }

        if (visited.Count != items.Count)
        {
            return ([], "Không thể hiển thị cây WBS vì dữ liệu có vòng lặp hoặc thiếu hạng mục cha.");
        }

        return (rootTrees, null);

        bool TryBuildNode(WorkItem item, int depth, out WbsItemTree? node)
        {
            if (!visited.Add(item.Id))
            {
                node = null;
                return false;
            }

            var itemChildren = children.GetValueOrDefault(item.Id, []);
            var childTrees = new List<WbsItemTree>(itemChildren.Count);
            foreach (var child in itemChildren)
            {
                if (!TryBuildNode(child, depth + 1, out var childNode))
                {
                    node = null;
                    return false;
                }

                childTrees.Add(childNode!);
            }

            node = new WbsItemTree(
                item,
                depth,
                linkedTaskCounts.GetValueOrDefault(item.Id),
                childTrees);
            return true;
        }
    }

    private static List<WorkItem>? GetSubtree(int rootId, IReadOnlyCollection<WorkItem> items)
    {
        var byId = items.ToDictionary(item => item.Id);
        if (!byId.ContainsKey(rootId))
        {
            return null;
        }

        var children = items
            .Where(item => item.ParentWorkItemId.HasValue)
            .GroupBy(item => item.ParentWorkItemId!.Value)
            .ToDictionary(group => group.Key, group => group.Select(item => item.Id).ToList());
        var result = new List<WorkItem>();
        var visited = new HashSet<int>();
        var stack = new Stack<int>();
        stack.Push(rootId);
        while (stack.TryPop(out var id))
        {
            if (!visited.Add(id) || !byId.TryGetValue(id, out var current))
            {
                return null;
            }

            result.Add(current);
            if (children.TryGetValue(id, out var childIds))
            {
                foreach (var childId in childIds)
                {
                    stack.Push(childId);
                }
            }
        }

        return result;
    }

    private static void Archive(WorkItem item, string userId, DateTime now, Guid groupId)
    {
        item.StatusBeforeArchive = item.Status;
        item.Status = WbsItemStatus.Archived;
        SetArchived(item, userId, now, groupId);
    }

    private static void Archive(ProjectPhase phase, string userId, DateTime now, Guid groupId)
    {
        phase.StatusBeforeArchive = phase.Status;
        phase.Status = ProjectPhaseStatus.Archived;
        SetArchived(phase, userId, now, groupId);
    }

    private static void SetArchived(
        ProjectPhase phase,
        string userId,
        DateTime now,
        Guid groupId)
    {
        phase.IsArchived = true;
        phase.ArchivedAt = now;
        phase.ArchivedByUserId = userId;
        phase.ArchiveGroupId = groupId;
    }

    private static void SetArchived(
        WorkItem item,
        string userId,
        DateTime now,
        Guid groupId)
    {
        item.IsArchived = true;
        item.ArchivedAt = now;
        item.ArchivedByUserId = userId;
        item.ArchiveGroupId = groupId;
        item.UpdatedAt = now;
        item.UpdatedByUserId = userId;
    }

    private static void Restore(ProjectPhase phase)
    {
        phase.IsArchived = false;
        phase.Status = phase.StatusBeforeArchive ?? ProjectPhaseStatus.NotStarted;
        phase.StatusBeforeArchive = null;
        phase.ArchivedAt = null;
        phase.ArchivedByUserId = null;
        phase.ArchiveGroupId = null;
    }

    private static void Restore(WorkItem item)
    {
        item.IsArchived = false;
        item.Status = item.StatusBeforeArchive ?? WbsItemStatus.NotStarted;
        item.StatusBeforeArchive = null;
        item.ArchivedAt = null;
        item.ArchivedByUserId = null;
        item.ArchiveGroupId = null;
    }

    private static bool IsWbsCodeConflict(DbUpdateException exception) =>
        exception.InnerException is SqliteException
        {
            SqliteErrorCode: 19,
            SqliteExtendedErrorCode: 2067
        } sqliteException &&
        sqliteException.Message.Contains("WorkItems.ProjectId, WorkItems.WbsCode", StringComparison.OrdinalIgnoreCase);

    private static WbsOperationResult<TTarget> CopyFailure<TSource, TTarget>(
        WbsOperationResult<TSource> source) =>
        new(source.Failure, Error: source.Error);

    private enum WbsPermission
    {
        View,
        ManageStructure,
        UpdateStatus,
        ArchiveRestore,
        LinkTask
    }
}

using ConstructionProjectManagement.Models;
using ConstructionProjectManagement.Services;

namespace ConstructionProjectManagement.Services.Interfaces;

public interface IWbsService
{
    Task<WbsOperationResult<IReadOnlyList<WbsProjectTree>>> GetVisibleProjectTreesAsync(
        string userId,
        IReadOnlyCollection<string> roles,
        string? search,
        CancellationToken cancellationToken = default);

    Task<WbsOperationResult<WbsProjectTree>> GetProjectTreeAsync(
        int projectId,
        string userId,
        IReadOnlyCollection<string> roles,
        bool includeArchived = false,
        CancellationToken cancellationToken = default);

    Task<WbsOperationResult<WbsProjectTree>> GetProjectTreeForStructureEditAsync(
        int projectId,
        string userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default);

    Task<WbsOperationResult<ProjectPhase>> GetPhaseForEditAsync(
        int phaseId,
        string userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default);

    Task<WbsOperationResult<ProjectPhase>> CreatePhaseAsync(
        ProjectPhase phase,
        string userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default);

    Task<WbsOperationResult<ProjectPhase>> UpdatePhaseAsync(
        ProjectPhase phase,
        string userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default);

    Task<WbsOperationResult<WorkItem>> GetWorkItemForEditAsync(
        int workItemId,
        string userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default);

    Task<WbsOperationResult<WorkItem>> CreateWorkItemAsync(
        WorkItem workItem,
        string userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default);

    Task<WbsOperationResult<WorkItem>> UpdateWorkItemAsync(
        WorkItem workItem,
        string userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default);

    Task<WbsOperationResult<WorkItem>> UpdateWorkItemStatusAsync(
        int workItemId,
        WbsItemStatus status,
        string userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default);

    Task<WbsOperationResult<bool>> ArchivePhaseAsync(
        int phaseId,
        string userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default);

    Task<WbsOperationResult<bool>> RestorePhaseAsync(
        int phaseId,
        string userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default);

    Task<WbsOperationResult<bool>> ArchiveWorkItemAsync(
        int workItemId,
        string userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default);

    Task<WbsOperationResult<bool>> RestoreWorkItemAsync(
        int workItemId,
        string userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default);

    Task<WbsOperationResult<IReadOnlyCollection<WorkItem>>> GetTaskWorkItemsAsync(
        int projectId,
        int? currentWorkItemId,
        string userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default);

    Task<WbsOperationResult<bool>> CanLinkWorkItemToTaskAsync(
        int projectId,
        int? workItemId,
        int? existingWorkItemId,
        string userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default);
}

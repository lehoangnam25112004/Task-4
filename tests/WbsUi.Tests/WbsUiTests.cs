using ConstructionProjectManagement.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using Task = System.Threading.Tasks.Task;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace WbsUi.Tests;

public sealed class WbsUiTests(WbsUiFixture fixture) : IClassFixture<WbsUiFixture>
{
    public static IEnumerable<object[]> Roles =>
    [
        ["Admin", false, false, true],
        ["Project Director", true, true, true],
        ["Project Manager", true, true, true],
        ["Site Manager", false, true, false],
        ["Supervising Engineer", false, false, false],
        ["Materials Staff", false, false, false],
        ["Worker", false, false, false],
        ["Client Representative", false, false, false]
    ];

    [Theory]
    [MemberData(nameof(Roles))]
    public async Task Role_matrix_on_real_browser(string role, bool structure, bool status, bool archived)
    {
        await RunWithPageAsync(role, async page =>
        {
            await page.GotoAsync($"{fixture.BaseUrl}/Wbs");
            Assert.Equal(1, await page.Locator("#search").CountAsync());
            Assert.Equal(1, await page.GetByRole(AriaRole.Button, new() { Name = "Tìm kiếm" }).CountAsync());
            Assert.Equal(1, await page.GetByRole(AriaRole.Link, new() { Name = "Đặt lại" }).CountAsync());
            Assert.Equal(0, await page.GetByRole(AriaRole.Link, new() { Name = "Thêm hạng mục WBS" }).CountAsync());
            var card = page.Locator("section.card").Filter(new() { HasText = "UI-ACTIVE" });
            Assert.Equal(1, await card.CountAsync());
            Assert.Equal(1, await card.GetByRole(AriaRole.Link, new() { Name = "Chi tiết" }).CountAsync());
            Assert.Equal(0, await card.GetByRole(AriaRole.Link, new() { Name = "Thêm hạng mục" }).CountAsync());
            Assert.Contains("UI-CHILD", await card.InnerTextAsync());
            Assert.Equal(role == "Admin" ? 1 : 0,
                await page.Locator("section.card").Filter(new() { HasText = "UI-OUTSIDE" }).CountAsync());
            Assert.Equal(0, await page.Locator("section.card").Filter(new() { HasText = "UI-ARCHIVED" }).CountAsync());

            await page.Locator("#search").FillAsync("UI-CHILD");
            await page.GetByRole(AriaRole.Button, new() { Name = "Tìm kiếm" }).ClickAsync();
            Assert.Equal(1, await page.Locator("section.card").Filter(new() { HasText = "UI-ACTIVE" }).CountAsync());
            Assert.Equal(0, await page.Locator("section.card").Filter(new() { HasText = "UI-OUTSIDE" }).CountAsync());
            await page.GetByRole(AriaRole.Link, new() { Name = "Đặt lại" }).ClickAsync();
            Assert.Equal("", await page.Locator("#search").InputValueAsync());
            await page.Locator("#search").FillAsync("UI-ACTIVE");
            await page.GetByRole(AriaRole.Button, new() { Name = "Tìm kiếm" }).ClickAsync();
            Assert.Equal(1, await page.Locator("section.card").CountAsync());
            await page.Locator("section.card").Filter(new() { HasText = "UI-ACTIVE" })
                .GetByRole(AriaRole.Link, new() { Name = "Chi tiết" }).ClickAsync();
            Assert.Contains($"projectId={fixture.ActiveId}", page.Url);
            await AssertDetailControlsAsync(page, structure, status, archived, readOnly: false);
            Assert.Contains("Tạm hoãn", await page.Locator("body").InnerTextAsync());
            Assert.Contains("Cấp 2", await page.Locator("body").InnerTextAsync());
            Assert.DoesNotContain("UI-ARCHIVED-ITEM", await page.Locator("body").InnerTextAsync());

            var outsideResponse = await GetAsync(page, $"/Wbs?projectId={fixture.OutsideId}");
            Assert.Equal(role == "Admin" ? 200 : 404, outsideResponse.Status);
            await AssertGetAllowedAsync(page, $"/Wbs/CreatePhase?projectId={fixture.ActiveId}", structure);
            await AssertGetAllowedAsync(page, $"/Wbs/CreateItem?projectId={fixture.ActiveId}", structure);
            await AssertGetAllowedAsync(page, $"/Wbs/EditPhase/{fixture.PhaseId}", structure);
            await AssertGetAllowedAsync(page, $"/Wbs/EditItem/{fixture.ItemId}", structure);
            await AssertGetAllowedAsync(page, $"/Wbs?projectId={fixture.ActiveId}&includeArchived=true", archived);

            if (archived)
            {
                await SetArchivedFilterAsync(page, true);
                Assert.Contains("UI-ARCHIVED-ITEM", await page.Locator("body").InnerTextAsync());
                Assert.Equal(1, await page.Locator($"form[action$='/RestoreItem'] input[name=id][value='{fixture.ArchivedItemId}']").CountAsync());
                await SetArchivedFilterAsync(page, false);
            }

            if (status)
            {
                var row = ItemRow(page, fixture.ItemId);
                await row.Locator("select[name=status]").SelectOptionAsync("InProgress");
                await row.GetByRole(AriaRole.Button, new() { Name = "Cập nhật" }).ClickAsync();
                await using var db = fixture.OpenDb();
                Assert.Equal(WbsItemStatus.InProgress, (await db.WorkItems.FindAsync(fixture.ItemId))!.Status);
                row = ItemRow(page, fixture.ItemId);
                await row.Locator("select[name=status]").SelectOptionAsync("NotStarted");
                await row.GetByRole(AriaRole.Button, new() { Name = "Cập nhật" }).ClickAsync();
            }
            if (archived)
            {
                await ItemRow(page, fixture.ItemId).GetByRole(AriaRole.Button, new() { Name = "Lưu trữ", Exact = true }).ClickAsync();
                await SetArchivedFilterAsync(page, true);
                await ItemRow(page, fixture.ChildId).GetByRole(AriaRole.Button, new() { Name = "Khôi phục", Exact = true }).ClickAsync();
                await using var db = fixture.OpenDb();
                var restoredRoot = (await db.WorkItems.FindAsync(fixture.ItemId))!;
                var restoredChild = (await db.WorkItems.FindAsync(fixture.ChildId))!;
                Assert.False(restoredRoot.IsArchived);
                Assert.Equal(WbsItemStatus.NotStarted, restoredRoot.Status);
                Assert.False(restoredChild.IsArchived);
                Assert.Equal(WbsItemStatus.OnHold, restoredChild.Status);
                Assert.True((await db.WorkItems.FindAsync(fixture.ArchivedItemId))!.IsArchived);
                var phaseCard = page.Locator("section.card").Filter(new() { HasText = "UI phase" });
                await phaseCard.GetByRole(AriaRole.Button, new() { Name = "Lưu trữ giai đoạn" }).ClickAsync();
                await SetArchivedFilterAsync(page, true);
                phaseCard = page.Locator("section.card").Filter(new() { HasText = "UI phase" });
                await phaseCard.GetByRole(AriaRole.Button, new() { Name = "Khôi phục giai đoạn" }).ClickAsync();
                await using var verify = fixture.OpenDb();
                var phaseRoot = (await verify.WorkItems.FindAsync(fixture.ItemId))!;
                var phaseChild = (await verify.WorkItems.FindAsync(fixture.ChildId))!;
                Assert.False(phaseRoot.IsArchived);
                Assert.Equal(WbsItemStatus.NotStarted, phaseRoot.Status);
                Assert.False(phaseChild.IsArchived);
                Assert.Equal(WbsItemStatus.OnHold, phaseChild.Status);
                Assert.True((await verify.WorkItems.FindAsync(fixture.ArchivedItemId))!.IsArchived);
            }
            if (structure) await CreateAndEditAsync(page, role);

            await page.GotoAsync($"{fixture.BaseUrl}/Wbs?projectId={fixture.ActiveId}");
            var before = await SnapshotAsync();
            if (!structure)
            {
                await AssertDeniedPostAsync(page, "/Wbs/CreatePhase", new() { ["ProjectId"] = fixture.ActiveId.ToString(), ["Name"] = "denied", ["SequenceOrder"] = "9" });
                await AssertDeniedPostAsync(page, "/Wbs/CreateItem", new() { ["ProjectId"] = fixture.ActiveId.ToString(), ["ProjectPhaseId"] = fixture.PhaseId.ToString(), ["WbsCode"] = "DENIED", ["Name"] = "denied", ["SequenceOrder"] = "1" });
                await AssertDeniedPostAsync(page, $"/Wbs/EditPhase/{fixture.PhaseId}", new() { ["Id"] = fixture.PhaseId.ToString(), ["ProjectId"] = fixture.ActiveId.ToString(), ["Name"] = "denied", ["SequenceOrder"] = "1" });
                await AssertDeniedPostAsync(page, $"/Wbs/EditItem/{fixture.ItemId}", new() { ["Id"] = fixture.ItemId.ToString(), ["ProjectId"] = fixture.ActiveId.ToString(), ["ProjectPhaseId"] = fixture.PhaseId.ToString(), ["WbsCode"] = "DENIED", ["Name"] = "denied", ["SequenceOrder"] = "1" });
            }
            if (!status) await AssertDeniedPostAsync(page, "/Wbs/UpdateStatus", new() { ["id"] = fixture.ItemId.ToString(), ["status"] = "Completed" });
            if (!archived)
            {
                await AssertDeniedPostAsync(page, "/Wbs/ArchiveItem", new() { ["id"] = fixture.ItemId.ToString(), ["projectId"] = fixture.ActiveId.ToString() });
                await AssertDeniedPostAsync(page, "/Wbs/RestoreItem", new() { ["id"] = fixture.ArchivedItemId.ToString(), ["projectId"] = fixture.ActiveId.ToString() });
            }
            if (role != "Admin")
            {
                var crossCreate = await PostAsync(page, "/Wbs/CreatePhase", new() { ["ProjectId"] = fixture.OutsideId.ToString(), ["Name"] = "denied", ["SequenceOrder"] = "9" });
                if (structure) Assert.Equal(404, crossCreate.Status); else AssertDenied(crossCreate);
                var crossStatus = await PostAsync(page, "/Wbs/UpdateStatus", new() { ["id"] = fixture.OutsideItemId.ToString(), ["status"] = "Completed" });
                if (status) Assert.Equal(404, crossStatus.Status); else AssertDenied(crossStatus);
                var crossArchive = await PostAsync(page, "/Wbs/ArchivePhase", new() { ["id"] = fixture.OutsidePhaseId.ToString(), ["projectId"] = fixture.OutsideId.ToString() });
                if (archived) Assert.Equal(404, crossArchive.Status); else AssertDenied(crossArchive);
            }
            Assert.Equal(before, await SnapshotAsync());

            foreach (var project in fixture.ReadOnlyProjects)
            {
                var response = await page.GotoAsync($"{fixture.BaseUrl}/Wbs?projectId={project.ProjectId}");
                Assert.Equal(200, response!.Status);
                await AssertDetailControlsAsync(page, false, false, archived, readOnly: true);
                if (archived)
                {
                    await SetArchivedFilterAsync(page, true);
                    Assert.Contains("UI readonly archived", await page.Locator("body").InnerTextAsync());
                    Assert.Equal(0, await page.Locator("form[action$='/RestoreItem']").CountAsync());
                    Assert.Equal(0, await page.Locator("form[action$='/RestorePhase']").CountAsync());
                }
                else await AssertGetAllowedAsync(page, $"/Wbs?projectId={project.ProjectId}&includeArchived=true", false);
                await AssertGetAllowedAsync(page, $"/Wbs/CreatePhase?projectId={project.ProjectId}", false);
                await AssertGetAllowedAsync(page, $"/Wbs/CreateItem?projectId={project.ProjectId}", false);
                await AssertGetAllowedAsync(page, $"/Wbs/EditPhase/{project.PhaseId}", false);
                await AssertGetAllowedAsync(page, $"/Wbs/EditItem/{project.ItemId}", false);
                var snapshot = await SnapshotAsync();
                await AssertDeniedPostAsync(page, "/Wbs/CreatePhase", new() { ["ProjectId"] = project.ProjectId.ToString(), ["Name"] = "denied", ["SequenceOrder"] = "9" });
                await AssertDeniedPostAsync(page, "/Wbs/CreateItem", new() { ["ProjectId"] = project.ProjectId.ToString(), ["ProjectPhaseId"] = project.PhaseId.ToString(), ["WbsCode"] = "DENIED", ["Name"] = "denied", ["SequenceOrder"] = "1" });
                await AssertDeniedPostAsync(page, $"/Wbs/EditPhase/{project.PhaseId}", new() { ["Id"] = project.PhaseId.ToString(), ["ProjectId"] = project.ProjectId.ToString(), ["Name"] = "denied", ["SequenceOrder"] = "1" });
                await AssertDeniedPostAsync(page, $"/Wbs/EditItem/{project.ItemId}", new() { ["Id"] = project.ItemId.ToString(), ["ProjectId"] = project.ProjectId.ToString(), ["ProjectPhaseId"] = project.PhaseId.ToString(), ["WbsCode"] = "DENIED", ["Name"] = "denied", ["SequenceOrder"] = "1" });
                await AssertDeniedPostAsync(page, "/Wbs/UpdateStatus", new() { ["id"] = project.ItemId.ToString(), ["status"] = "Completed" });
                await AssertDeniedPostAsync(page, "/Wbs/ArchivePhase", new() { ["id"] = project.PhaseId.ToString(), ["projectId"] = project.ProjectId.ToString() });
                await AssertDeniedPostAsync(page, "/Wbs/ArchiveItem", new() { ["id"] = project.ItemId.ToString(), ["projectId"] = project.ProjectId.ToString() });
                await AssertDeniedPostAsync(page, "/Wbs/RestoreItem", new() { ["id"] = project.ArchivedItemId.ToString(), ["projectId"] = project.ProjectId.ToString() });
                await AssertDeniedPostAsync(page, "/Wbs/RestorePhase", new() { ["id"] = project.ArchivedPhaseId.ToString(), ["projectId"] = project.ProjectId.ToString() });
                Assert.Equal(snapshot, await SnapshotAsync());
            }
        });
    }

    [Fact]
    public async Task Unknown_role_is_default_denied()
    {
        await RunWithPageAsync("NoRole", async page =>
        {
            AssertDenied(await GetAsync(page, "/Wbs"));
            AssertDenied(await GetAsync(page, $"/Wbs?projectId={fixture.ActiveId}"));
            var before = await SnapshotAsync();
            await AssertDeniedPostAsync(page, "/Wbs/CreatePhase", new() { ["ProjectId"] = fixture.ActiveId.ToString(), ["Name"] = "denied", ["SequenceOrder"] = "1" });
            Assert.Equal(before, await SnapshotAsync());
        });
    }

    [Fact]
    public async Task Inactive_user_cannot_access_or_write()
    {
        await RunWithPageAsync("Inactive", async page =>
        {
            AssertDenied(await GetAsync(page, "/Wbs"));
            AssertDenied(await GetAsync(page, $"/Wbs?projectId={fixture.ActiveId}"));
            var before = await SnapshotAsync();
            await AssertDeniedPostAsync(page, "/Wbs/CreatePhase", new() { ["ProjectId"] = fixture.ActiveId.ToString(), ["Name"] = "denied", ["SequenceOrder"] = "1" });
            Assert.Equal(before, await SnapshotAsync());
        });
    }

    [Fact]
    public async Task Inactive_membership_and_invalid_edit_posts_do_not_disclose_data()
    {
        await RunWithPageAsync("InactiveMember", async page =>
        {
            Assert.Equal(404, (await GetAsync(page, $"/Wbs?projectId={fixture.InactiveMemberId}")).Status);
            await page.GotoAsync($"{fixture.BaseUrl}/Wbs?projectId={fixture.ActiveId}");
            await using var db = fixture.OpenDb();
            var member = await db.ProjectMembers.SingleAsync(m => m.ProjectId == fixture.ActiveId && m.UserId == db.Users.Single(u => u.Email == fixture.Emails["InactiveMember"]).Id);
            member.IsActive = false;
            await db.SaveChangesAsync();
            try
            {
                var before = await SnapshotAsync();
                Assert.Equal(404, (await GetAsync(page, $"/Wbs?projectId={fixture.ActiveId}")).Status);
                foreach (var (action, id) in new[] { ("EditPhase", fixture.PhaseId), ("EditItem", fixture.ItemId) })
                {
                    var result = await PostAsync(page, $"/Wbs/{action}/{id}", new()
                    {
                        ["Id"] = id.ToString(), ["ProjectId"] = fixture.ActiveId.ToString(),
                        ["Name"] = "", ["SequenceOrder"] = "not-a-number"
                    });
                    Assert.Equal(404, result.Status);
                    Assert.DoesNotContain("UI phase", await result.TextAsync());
                    Assert.DoesNotContain("UI root", await result.TextAsync());
                    var spoof = await PostAsync(page, $"/Wbs/{action}/{id}", new()
                    {
                        ["Id"] = id.ToString(), ["ProjectId"] = fixture.OutsideId.ToString(),
                        ["Name"] = "", ["SequenceOrder"] = "not-a-number"
                    });
                    Assert.Equal(404, spoof.Status);
                }
                Assert.Equal(before, await SnapshotAsync());
            }
            finally { member.IsActive = true; await db.SaveChangesAsync(); }
        });
    }

    [Fact]
    public async Task Client_organization_mismatch_blocks_active_membership()
    {
        await RunWithPageAsync("Mismatch", async page =>
        {
            await page.GotoAsync($"{fixture.BaseUrl}/Wbs");
            Assert.Equal(0, await page.Locator("section.card").Filter(new() { HasText = "UI-ACTIVE" }).CountAsync());
            Assert.Equal(404, (await GetAsync(page, $"/Wbs?projectId={fixture.ActiveId}")).Status);
            var before = await SnapshotAsync();
            await AssertDeniedPostAsync(page, "/Wbs/UpdateStatus", new() { ["id"] = fixture.ItemId.ToString(), ["status"] = "Completed" });
            Assert.Equal(before, await SnapshotAsync());
        });
    }

    [Theory]
    [InlineData("AdminPM")]
    [InlineData("AdminPD")]
    public async Task Admin_combined_role_keeps_admin_restrictions(string account)
    {
        await RunWithPageAsync(account, async page =>
        {
            Assert.Equal(200, (await page.GotoAsync($"{fixture.BaseUrl}/Wbs?projectId={fixture.ActiveId}"))!.Status);
            await AssertDetailControlsAsync(page, false, false, true, readOnly: false);
            await AssertGetAllowedAsync(page, $"/Wbs/CreatePhase?projectId={fixture.ActiveId}", false);
            await AssertGetAllowedAsync(page, $"/Wbs/CreateItem?projectId={fixture.ActiveId}", false);
            await AssertGetAllowedAsync(page, $"/Wbs/EditPhase/{fixture.PhaseId}", false);
            await AssertGetAllowedAsync(page, $"/Wbs/EditItem/{fixture.ItemId}", false);
            var before = await SnapshotAsync();
            await AssertDeniedPostAsync(page, "/Wbs/CreatePhase", new() { ["ProjectId"] = fixture.ActiveId.ToString(), ["Name"] = "denied", ["SequenceOrder"] = "9" });
            await AssertDeniedPostAsync(page, "/Wbs/CreateItem", new() { ["ProjectId"] = fixture.ActiveId.ToString(), ["ProjectPhaseId"] = fixture.PhaseId.ToString(), ["WbsCode"] = "DENIED", ["Name"] = "denied", ["SequenceOrder"] = "1" });
            await AssertDeniedPostAsync(page, "/Wbs/UpdateStatus", new() { ["id"] = fixture.ItemId.ToString(), ["status"] = "Completed" });
            Assert.Equal(before, await SnapshotAsync());
            await ItemRow(page, fixture.ItemId).GetByRole(AriaRole.Button, new() { Name = "Lưu trữ", Exact = true }).ClickAsync();
            await SetArchivedFilterAsync(page, true);
            await ItemRow(page, fixture.ChildId).GetByRole(AriaRole.Button, new() { Name = "Khôi phục", Exact = true }).ClickAsync();
        });
    }

    private async Task CreateAndEditAsync(IPage page, string role)
    {
        var suffix = role.Replace(" ", "-");
        await page.GetByRole(AriaRole.Link, new() { Name = "Thêm giai đoạn", Exact = true }).ClickAsync();
        await page.Locator("#Name").FillAsync($"UI created {suffix}");
        await page.Locator("#SequenceOrder").FillAsync("20");
        await page.GetByRole(AriaRole.Button, new() { Name = "Lưu", Exact = true }).ClickAsync();
        Assert.Contains($"UI created {suffix}", await page.Locator("body").InnerTextAsync());
        var phase = page.Locator("section.card").Filter(new() { HasText = $"UI created {suffix}" });
        await phase.GetByRole(AriaRole.Link, new() { Name = "Sửa" }).ClickAsync();
        await page.Locator("#Name").FillAsync($"UI edited phase {suffix}");
        await page.GetByRole(AriaRole.Button, new() { Name = "Lưu thay đổi", Exact = true }).ClickAsync();
        Assert.Contains($"UI edited phase {suffix}", await page.Locator("body").InnerTextAsync());
        await page.GetByRole(AriaRole.Link, new() { Name = "Thêm hạng mục WBS" }).ClickAsync();
        await page.Locator("#ProjectPhaseId").SelectOptionAsync(fixture.PhaseId.ToString());
        await page.Locator("#WbsCode").FillAsync($"UI-CREATED-{suffix}");
        await page.Locator("#Name").FillAsync($"UI item {suffix}");
        await page.Locator("#SequenceOrder").FillAsync("20");
        await page.GetByRole(AriaRole.Button, new() { Name = "Lưu", Exact = true }).ClickAsync();
        Assert.Contains($"UI-CREATED-{suffix}", await page.Locator("body").InnerTextAsync());
        await ItemRow(page, fixture.ItemId).GetByRole(AriaRole.Link, new() { Name = "Sửa" }).ClickAsync();
        await page.Locator("#Name").FillAsync("UI root edited");
        await page.GetByRole(AriaRole.Button, new() { Name = "Lưu thay đổi", Exact = true }).ClickAsync();
        Assert.Contains("UI root edited", await page.Locator("body").InnerTextAsync());
        await ItemRow(page, fixture.ItemId).GetByRole(AriaRole.Link, new() { Name = "Sửa" }).ClickAsync();
        await page.Locator("#Name").FillAsync("UI root");
        await page.GetByRole(AriaRole.Button, new() { Name = "Lưu thay đổi", Exact = true }).ClickAsync();
    }

    private static ILocator ItemRow(IPage page, int id) =>
        page.Locator($"li.list-group-item:has(> div input[name=id][value='{id}']) > div:first-child");

    private static async Task SetArchivedFilterAsync(IPage page, bool enabled)
    {
        var navigation = page.WaitForURLAsync(url => url.Contains("includeArchived=true", StringComparison.OrdinalIgnoreCase) == enabled);
        if (enabled) await page.Locator("#includeArchived").CheckAsync();
        else await page.Locator("#includeArchived").UncheckAsync();
        await navigation;
    }

    private static async Task AssertDetailControlsAsync(IPage page, bool structure, bool status, bool archived, bool readOnly)
    {
        Assert.Equal(structure ? 1 : 0, await page.GetByRole(AriaRole.Link, new() { Name = "Thêm giai đoạn", Exact = true }).CountAsync());
        Assert.Equal(structure ? 1 : 0, await page.GetByRole(AriaRole.Link, new() { Name = "Thêm hạng mục WBS", Exact = true }).CountAsync());
        Assert.Equal(status, await page.Locator("select[name=status]").CountAsync() > 0);
        Assert.Equal(archived ? 1 : 0, await page.Locator("#includeArchived").CountAsync());
        Assert.Equal(archived && !readOnly, await page.Locator("form[action$='/ArchiveItem']").CountAsync() > 0);
        Assert.Equal(structure, await page.Locator("a[href*='/Wbs/EditPhase/']").CountAsync() > 0);
        Assert.Equal(structure, await page.Locator("a[href*='/Wbs/EditItem/']").CountAsync() > 0);
        if (readOnly) Assert.Contains("Dự án chỉ đọc", await page.Locator("body").InnerTextAsync());
    }

    private async Task RunWithPageAsync(string name, Func<IPage, Task> test)
    {
        await using var context = await fixture.Browser.NewContextAsync();
        IPage? page = null;
        try { page = await fixture.LoginAsync(context, name); await test(page); }
        catch
        {
            var directory = Path.Combine(fixture.Root, "tests", "WbsUi.Tests", "TestResults");
            Directory.CreateDirectory(directory);
            page ??= context.Pages.LastOrDefault();
            if (page is not null) await page.ScreenshotAsync(new() { Path = Path.Combine(directory, $"{name.Replace(" ", "-")}-failure.png"), FullPage = true });
            fixture.SaveLog(name.Replace(" ", "-"));
            throw;
        }
    }

    private async Task<IAPIResponse> GetAsync(IPage page, string path) =>
        await page.Context.APIRequest.GetAsync(fixture.BaseUrl + path, new() { MaxRedirects = 0 });

    private async Task<IAPIResponse> PostAsync(IPage page, string path, Dictionary<string, string> form)
    {
        var token = await page.Locator("form[action*='/Account/Logout'] input[name='__RequestVerificationToken']").First.GetAttributeAsync("value");
        Assert.False(string.IsNullOrEmpty(token));
        form["__RequestVerificationToken"] = token!;
        var data = page.Context.APIRequest.CreateFormData();
        foreach (var (key, value) in form) data.Append(key, value);
        return await page.Context.APIRequest.PostAsync(fixture.BaseUrl + path, new() { Form = data, MaxRedirects = 0 });
    }

    private async Task AssertDeniedPostAsync(IPage page, string path, Dictionary<string, string> form) =>
        AssertDenied(await PostAsync(page, path, form));

    private async Task AssertGetAllowedAsync(IPage page, string path, bool allowed)
    {
        var response = await GetAsync(page, path);
        if (allowed) Assert.Equal(200, response.Status);
        else AssertDenied(response);
    }

    private static void AssertDenied(IAPIResponse response)
    {
        Assert.Equal(302, response.Status);
        Assert.Contains("/Account/AccessDenied", response.Headers["location"], StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string[]> SnapshotAsync()
    {
        await using var db = fixture.OpenDb();
        var phases = await db.ProjectPhases.AsNoTracking().OrderBy(x => x.Id)
            .Select(x => $"P:{x.Id}:{x.ProjectId}:{x.Name}:{x.SequenceOrder}:{x.Status}:{x.IsArchived}:{x.ArchiveGroupId}:{x.StatusBeforeArchive}").ToArrayAsync();
        var items = await db.WorkItems.AsNoTracking().OrderBy(x => x.Id)
            .Select(x => $"I:{x.Id}:{x.ProjectId}:{x.ProjectPhaseId}:{x.ParentWorkItemId}:{x.WbsCode}:{x.Name}:{x.Description}:{x.SequenceOrder}:{x.Level}:{x.Status}:{x.IsArchived}:{x.ArchiveGroupId}:{x.StatusBeforeArchive}").ToArrayAsync();
        return phases.Concat(items).ToArray();
    }
}

# Báo cáo discovery & audit TASK-05

**Phạm vi:** phân tích tĩnh source và tài liệu tại [Task-2-main](<../..>). Không khởi động ứng dụng, không chạy test và không mở hoặc thay đổi database trong lượt audit này. Vì vậy, báo cáo mô tả **code hiện tại**, không xác nhận schema/dữ liệu thực tế hay kết quả runtime.

**Kết luận:** **READY WITH LIMITATIONS** — đã có nền tảng quản lý công việc, nhưng cần chốt ma trận quyền và xử lý các rủi ro về phạm vi dự án, cập nhật tiến độ và toàn vẹn dữ liệu trước khi mở rộng.

---

## PHẦN 1 – PROJECT STRUCTURE

| Thư mục | Thành phần liên quan |
|---|---|
| [Controllers](<../../Controllers>) | Task, tiến độ, nghiệm thu và các module tiêu thụ Task |
| [Services](<../../Services>) | Nghiệp vụ Task, Progress, WBS, Quality và phụ thuộc khác |
| [Models](<../../Models>) | Task, phân công, phụ thuộc, lịch sử và entity liên quan |
| [ViewModels](<../../ViewModels>) | Dữ liệu form Task, tiến độ, nghiệm thu |
| [Views](<../../Views>) | Razor UI Task và các màn hình tích hợp |
| [Data](<../../Data>) | DbContext, cấu hình quan hệ, khởi tạo |
| [Migrations](<../../Migrations>) | Migration SQLite đang được project biên dịch |
| [Migrations.SqlServer](<../../Migrations.SqlServer>) | Migration SQL Server lịch sử, hiện bị loại khỏi build |
| [docs](<..>) và [tests](<../../tests>) | Tài liệu, sơ đồ và kiểm thử liên quan |

---

## PHẦN 2 – COMPLETE FILE INVENTORY

**Quy ước đếm:** mỗi file trong bảng tính một lần; bao gồm code, tài liệu và metadata migration liên quan trực tiếp đến luồng hoặc schema Task. Không tính thư viện bên thứ ba, output `bin/obj`, dữ liệu database hay các file chỉ chứa chữ “Task” theo nghĩa `System.Threading.Tasks.Task`.

### 2.1. DIRECT — 20 file

| STT | File tương đối từ project root | Layer / component | Vai trò |
|---:|---|---|---|
| 1 | [Controllers/TasksController.cs](<../../Controllers/TasksController.cs>) | Controller | Route Task, quyền role, nạp form |
| 2 | [Services/TaskService.cs](<../../Services/TaskService.cs>) | Service | Đọc, tạo, sửa, validation, assignment |
| 3 | [Services/Interfaces/ITaskService.cs](<../../Services/Interfaces/ITaskService.cs>) | Interface | Hợp đồng TaskService |
| 4 | [ViewModels/TaskViewModels.cs](<../../ViewModels/TaskViewModels.cs>) | ViewModel | Form, danh sách, chi tiết |
| 5 | [Models/Task.cs](<../../Models/Task.cs>) | Entity | Công việc |
| 6 | [Models/TaskAssignment.cs](<../../Models/TaskAssignment.cs>) | Entity | User được phân công |
| 7 | [Models/TaskDependency.cs](<../../Models/TaskDependency.cs>) | Entity | Quan hệ predecessor |
| 8 | [Models/TaskProgressHistory.cs](<../../Models/TaskProgressHistory.cs>) | Entity | Lịch sử cập nhật tiến độ |
| 9 | [Models/TaskStatus.cs](<../../Models/TaskStatus.cs>) | Enum | Trạng thái Task |
| 10 | [Models/TaskPriority.cs](<../../Models/TaskPriority.cs>) | Enum | Ưu tiên Task |
| 11 | [Views/Tasks/Index.cshtml](<../../Views/Tasks/Index.cshtml>) | View | Danh sách |
| 12 | [Views/Tasks/Details.cshtml](<../../Views/Tasks/Details.cshtml>) | View | Chi tiết |
| 13 | [Views/Tasks/Create.cshtml](<../../Views/Tasks/Create.cshtml>) | View | Tạo |
| 14 | [Views/Tasks/Edit.cshtml](<../../Views/Tasks/Edit.cshtml>) | View | Sửa |
| 15 | [Views/Tasks/_TaskForm.cshtml](<../../Views/Tasks/_TaskForm.cshtml>) | Partial/JS | Form và script đổi dự án |
| 16 | [docs/01_Business_Analysis/Business_05_Task_Management.md](<../01_Business_Analysis/Business_05_Task_Management.md>) | Tài liệu | Nghiệp vụ TASK-05 |
| 17–18 | [Migrations.SqlServer/20261111081837_AddTasks.cs](<../../Migrations.SqlServer/20261111081837_AddTasks.cs>), [AddTasks.Designer.cs](<../../Migrations.SqlServer/20261111081837_AddTasks.Designer.cs>) | Migration lịch sử | Schema Task SQL Server |
| 19–20 | [Migrations.SqlServer/20261111082215_AddProgressHistory.cs](<../../Migrations.SqlServer/20261111082215_AddProgressHistory.cs>), [AddProgressHistory.Designer.cs](<../../Migrations.SqlServer/20261111082215_AddProgressHistory.Designer.cs>) | Migration lịch sử | Lịch sử tiến độ SQL Server |

### 2.2. INDIRECT — 74 file

| STT | File tương đối từ project root | Layer / component | Vai trò |
|---:|---|---|---|
| 21–25 | [Controllers/ProgressController.cs](<../../Controllers/ProgressController.cs>), [Services/ProgressService.cs](<../../Services/ProgressService.cs>), [Services/Interfaces/IProgressService.cs](<../../Services/Interfaces/IProgressService.cs>), [ViewModels/ProgressViewModels.cs](<../../ViewModels/ProgressViewModels.cs>), [Views/Progress/Index.cshtml](<../../Views/Progress/Index.cshtml>) | Progress | Đọc/ghi trạng thái, %, lịch sử và biểu đồ |
| 26–33 | [Controllers/QualityController.cs](<../../Controllers/QualityController.cs>), [Services/QualityService.cs](<../../Services/QualityService.cs>), [Services/Interfaces/IQualityService.cs](<../../Services/Interfaces/IQualityService.cs>), [ViewModels/QualityViewModels.cs](<../../ViewModels/QualityViewModels.cs>), [Views/Quality/Index.cshtml](<../../Views/Quality/Index.cshtml>), [Views/Quality/CreateInspection.cshtml](<../../Views/Quality/CreateInspection.cshtml>), [Views/Quality/CreateIncident.cshtml](<../../Views/Quality/CreateIncident.cshtml>), [Views/Quality/CreateRfi.cshtml](<../../Views/Quality/CreateRfi.cshtml>) | Quality | Nghiệm thu, sự cố/RFI gắn Task |
| 34–38 | [Models/InspectionRequest.cs](<../../Models/InspectionRequest.cs>), [Models/InspectionHistory.cs](<../../Models/InspectionHistory.cs>), [Models/InspectionResult.cs](<../../Models/InspectionResult.cs>), [Models/Incident.cs](<../../Models/Incident.cs>), [Models/RfiRequest.cs](<../../Models/RfiRequest.cs>) | Domain | Entity/kết quả nghiệm thu và sự cố/RFI |
| 39–45 | [Controllers/MaterialsController.cs](<../../Controllers/MaterialsController.cs>), [Services/MaterialService.cs](<../../Services/MaterialService.cs>), [Services/Interfaces/IMaterialService.cs](<../../Services/Interfaces/IMaterialService.cs>), [ViewModels/MaterialViewModels.cs](<../../ViewModels/MaterialViewModels.cs>), [Views/Materials/Request.cshtml](<../../Views/Materials/Request.cshtml>), [Models/MaterialRequest.cs](<../../Models/MaterialRequest.cs>), [Models/MaterialTransaction.cs](<../../Models/MaterialTransaction.cs>) | Vật tư | Phiếu và giao dịch có `TaskId` |
| 46–51 | [Controllers/EquipmentController.cs](<../../Controllers/EquipmentController.cs>), [Services/EquipmentService.cs](<../../Services/EquipmentService.cs>), [Services/Interfaces/IEquipmentService.cs](<../../Services/Interfaces/IEquipmentService.cs>), [ViewModels/EquipmentViewModels.cs](<../../ViewModels/EquipmentViewModels.cs>), [Views/Equipment/Assign.cshtml](<../../Views/Equipment/Assign.cshtml>), [Models/EquipmentAssignment.cs](<../../Models/EquipmentAssignment.cs>) | Thiết bị | Bàn giao thiết bị có `TaskId` |
| 52–57 | [Controllers/WbsController.cs](<../../Controllers/WbsController.cs>), [Services/WbsService.cs](<../../Services/WbsService.cs>), [Services/Interfaces/IWbsService.cs](<../../Services/Interfaces/IWbsService.cs>), [ViewModels/WbsViewModels.cs](<../../ViewModels/WbsViewModels.cs>), [Views/Wbs/_WorkItemTree.cshtml](<../../Views/Wbs/_WorkItemTree.cshtml>), [Views/Wbs/Index.cshtml](<../../Views/Wbs/Index.cshtml>) | WBS | Kiểm tra liên kết WBS và đếm Task liên kết |
| 58–61 | [Controllers/DashboardController.cs](<../../Controllers/DashboardController.cs>), [Services/DashboardService.cs](<../../Services/DashboardService.cs>), [ViewModels/DashboardViewModel.cs](<../../ViewModels/DashboardViewModel.cs>), [Views/Dashboard/Index.cshtml](<../../Views/Dashboard/Index.cshtml>) | Dashboard | Đếm Task mở/quá hạn |
| 62–66 | [Controllers/CompletionController.cs](<../../Controllers/CompletionController.cs>), [Services/CompletionService.cs](<../../Services/CompletionService.cs>), [Services/Interfaces/ICompletionService.cs](<../../Services/Interfaces/ICompletionService.cs>), [ViewModels/CompletionViewModels.cs](<../../ViewModels/CompletionViewModels.cs>), [Views/Completion/Index.cshtml](<../../Views/Completion/Index.cshtml>) | Bàn giao | Danh sách Task mở và điều kiện bàn giao |
| 67 | [tests/ConstructionProjectManagement.Tests/WbsServiceTests.cs](<../../tests/ConstructionProjectManagement.Tests/WbsServiceTests.cs>) | Test | Có test giữ liên kết WBS archived khi sửa Task |
| 68–70 | [docs/01_Business_Analysis/Business_06_Schedule_and_Progress.md](<../01_Business_Analysis/Business_06_Schedule_and_Progress.md>), [Business_11_Quality_Rfi_Inspection.md](<../01_Business_Analysis/Business_11_Quality_Rfi_Inspection.md>), [TASK_FUNCTION_INVENTORY.md](<../../TASK_FUNCTION_INVENTORY.md>) | Tài liệu | Tiến độ, nghiệm thu, inventory cũ |
| 71–72 | [docs/06_Diagrams/14_Sequence_Task_Assignment.mmd](<../06_Diagrams/14_Sequence_Task_Assignment.mmd>), [08_Process_02_Task_Execution.mmd](<../06_Diagrams/08_Process_02_Task_Execution.mmd>) | Sơ đồ | Phân công và thực hiện công việc |
| 73–78 | [docs/04_Testing/Test_Cases.md](<../04_Testing/Test_Cases.md>), [Test_Plan.md](<../04_Testing/Test_Plan.md>), [docs/07_Testing/Integration_Test_Matrix.md](<../07_Testing/Integration_Test_Matrix.md>), [docs/03_Database/Data_Dictionary.md](<../03_Database/Data_Dictionary.md>), [PROJECT_STRUCTURE.md](<../../PROJECT_STRUCTURE.md>), [docs/02_System_Design/System_Design.md](<../02_System_Design/System_Design.md>) | Tài liệu | Test, dữ liệu và kiến trúc |
| 79–81 | [Migrations.SqlServer/20261111083936_AddMaterialsManagement.cs](<../../Migrations.SqlServer/20261111083936_AddMaterialsManagement.cs>), [20261111084345_AddEquipmentManagement.cs](<../../Migrations.SqlServer/20261111084345_AddEquipmentManagement.cs>), [20261111085249_AddQualityAndInspection.cs](<../../Migrations.SqlServer/20261111085249_AddQualityAndInspection.cs>) | Migration lịch sử | FK từ module khác đến Task |
| 119–123 | [docs/TASK04/TASK04_WBS_IMPLEMENTATION_REPORT.md](<../TASK04/TASK04_WBS_IMPLEMENTATION_REPORT.md>), [TASK04_WBS_CODEBASE_AUDIT_REPORT.md](<../TASK04/TASK04_WBS_CODEBASE_AUDIT_REPORT.md>), [TASK04_WBS_FAKE_DATA_GUIDE.md](<../TASK04/TASK04_WBS_FAKE_DATA_GUIDE.md>), [TASK03_CODEBASE_DEPENDENCY_AUDIT.md](<../../TASK03_CODEBASE_DEPENDENCY_AUDIT.md>), [Business_04_Phases_and_WBS.md](<../01_Business_Analysis/Business_04_Phases_and_WBS.md>) | Tài liệu | WBS/Task và đối chiếu phụ thuộc |
| 124–128 | [Business_02_Project_Management.md](<../01_Business_Analysis/Business_02_Project_Management.md>), [Business_08_Materials.md](<../01_Business_Analysis/Business_08_Materials.md>), [Business_09_Equipment.md](<../01_Business_Analysis/Business_09_Equipment.md>), [Business_12_Cost_Completion_Handover_Reports.md](<../01_Business_Analysis/Business_12_Cost_Completion_Handover_Reports.md>), [docs/05_Project_Management/Implementation_Progress.md](<../05_Project_Management/Implementation_Progress.md>) | Tài liệu | Bối cảnh dự án và module tiêu thụ Task |
| 129–131 | [docs/05_Project_Management/Initial_Project_Plan.md](<../05_Project_Management/Initial_Project_Plan.md>), [docs/06_Diagrams/README_Diagrams.md](<../06_Diagrams/README_Diagrams.md>), [docs/TASK03_Business_Description.md](<../TASK03_Business_Description.md>) | Tài liệu | Kế hoạch và bối cảnh tích hợp |

### 2.3. SHARED — 37 file cần bảo vệ

| STT | File tương đối từ project root | Component | Vai trò |
|---:|---|---|---|
| 82–87 | [Program.cs](<../../Program.cs>), [ConstructionProjectManagement.csproj](<../../ConstructionProjectManagement.csproj>), [appsettings.json](<../../appsettings.json>), [appsettings.Development.json](<../../appsettings.Development.json>), [Data/ApplicationDbContext.cs](<../../Data/ApplicationDbContext.cs>), [Data/DbInitializer.cs](<../../Data/DbInitializer.cs>) | Infrastructure | DI, Identity, SQLite, migration, DbContext |
| 88–94 | [Models/ApplicationUser.cs](<../../Models/ApplicationUser.cs>), [Models/RoleNames.cs](<../../Models/RoleNames.cs>), [Models/Project.cs](<../../Models/Project.cs>), [Models/ProjectMember.cs](<../../Models/ProjectMember.cs>), [Models/WorkItem.cs](<../../Models/WorkItem.cs>), [Models/ProjectPhase.cs](<../../Models/ProjectPhase.cs>), [Models/ProjectStatus.cs](<../../Models/ProjectStatus.cs>) | Domain chung | User, 8 role, dự án, membership, WBS |
| 95–101 | [Views/Shared/_Layout.cshtml](<../../Views/Shared/_Layout.cshtml>), [Views/Shared/_LoginPartial.cshtml](<../../Views/Shared/_LoginPartial.cshtml>), [Views/Shared/_ValidationScriptsPartial.cshtml](<../../Views/Shared/_ValidationScriptsPartial.cshtml>), [Views/_ViewImports.cshtml](<../../Views/_ViewImports.cshtml>), [Views/_ViewStart.cshtml](<../../Views/_ViewStart.cshtml>), [wwwroot/css/site.css](<../../wwwroot/css/site.css>), [wwwroot/js/site.js](<../../wwwroot/js/site.js>) | UI chung | Layout, Identity UI, validation, style/script chung |
| 102–111 | [InitialSqlite.cs](<../../Migrations/20261111092125_InitialSqlite.cs>), [InitialSqlite.Designer.cs](<../../Migrations/20261111092125_InitialSqlite.Designer.cs>), [Task02Phase1ProjectMemberAssignments.Designer.cs](<../../Migrations/20261114073018_Task02Phase1ProjectMemberAssignments.Designer.cs>), [Task03ContractsAndDocuments.Designer.cs](<../../Migrations/20261121073229_Task03ContractsAndDocuments.Designer.cs>), [Task03ModelSync.Designer.cs](<../../Migrations/20261121073840_Task03ModelSync.Designer.cs>), [Task03VerificationSync.Designer.cs](<../../Migrations/20261121075104_Task03VerificationSync.Designer.cs>), [Task03DocumentManagementCompletion.Designer.cs](<../../Migrations/20261121150000_Task03DocumentManagementCompletion.Designer.cs>), [Task04WbsLifecycle.cs](<../../Migrations/20261122070416_Task04WbsLifecycle.cs>), [Task04WbsLifecycle.Designer.cs](<../../Migrations/20261122070416_Task04WbsLifecycle.Designer.cs>), [ApplicationDbContextModelSnapshot.cs](<../../Migrations/ApplicationDbContextModelSnapshot.cs>) | SQLite migrations | Schema gốc, metadata và thay đổi WBS liên quan |
| 112–118 | [SQL Server snapshot](<../../Migrations.SqlServer/ApplicationDbContextModelSnapshot.cs>), [AddEmployeesAndTeams.Designer.cs](<../../Migrations.SqlServer/20261111082613_AddEmployeesAndTeams.Designer.cs>), [AddMaterialsManagement.Designer.cs](<../../Migrations.SqlServer/20261111083936_AddMaterialsManagement.Designer.cs>), [AddEquipmentManagement.Designer.cs](<../../Migrations.SqlServer/20261111084345_AddEquipmentManagement.Designer.cs>), [AddConstructionLogs.Designer.cs](<../../Migrations.SqlServer/20261111084859_AddConstructionLogs.Designer.cs>), [AddQualityAndInspection.Designer.cs](<../../Migrations.SqlServer/20261111085249_AddQualityAndInspection.Designer.cs>), [AddCompletionAndReporting.Designer.cs](<../../Migrations.SqlServer/20261111085555_AddCompletionAndReporting.Designer.cs>) | Metadata lịch sử | Snapshot có mô hình Task |

---

## PHẦN 3 – DATABASE INVENTORY

### Entity chính

| Entity | Thuộc tính và quan hệ quan trọng |
|---|---|
| [Task](<../../Models/Task.cs>) | `ProjectId`, `WorkItemId?`, `ParentTaskId?`, `Title`, `Description`, `Priority`, ngày kế hoạch/thực tế, `Status`, `PercentComplete`, `RequiresInspection`, timestamps |
| [TaskAssignment](<../../Models/TaskAssignment.cs>) | `TaskId`, `UserId`, `AssignedAt`; liên kết Identity user |
| [TaskDependency](<../../Models/TaskDependency.cs>) | `TaskId`, `PredecessorTaskId`; có schema nhưng chưa thấy luồng quản lý |
| [TaskProgressHistory](<../../Models/TaskProgressHistory.cs>) | Task, %/status trước–sau, lý do chậm, user và thời gian cập nhật |

[ApplicationDbContext](<../../Data/ApplicationDbContext.cs#L43>) khai báo bốn `DbSet`. Cấu hình tại [ApplicationDbContext](<../../Data/ApplicationDbContext.cs#L355>):

- `Task → Project`: **Restrict**; `Task → WorkItem`: **SetNull**; `Task → ParentTask`: **Restrict**.
- `TaskAssignment → Task`: **Cascade**; `→ User`: **Restrict**; unique index `(TaskId, UserId)`.
- `TaskDependency → Task`: **Cascade**; `→ PredecessorTask`: **Restrict**.
- `TaskProgressHistory → Task`: **Cascade**; `→ UpdatedBy`: **Restrict**.
- SQLite migration gốc tạo index riêng cho Project, WBS, parent, assignment, dependency và history: [InitialSqlite](<../../Migrations/20261111092125_InitialSqlite.cs#L33>).

**Entity liên quan trực tiếp qua FK:** `Project`, `ProjectMember`, `ApplicationUser`, `WorkItem`, `ProjectPhase`, `InspectionRequest`, `InspectionHistory`, `Incident`, `RfiRequest`, `MaterialRequest`, `MaterialTransaction`, `EquipmentAssignment`: tổng cộng **12 entity liên quan ngoài 4 entity lõi**.

Không tìm thấy entity bình luận, tệp đính kèm hoặc audit log **riêng cho Task**. `ProjectDocument` gắn Project/Contract, không có `TaskId`: [ProjectDocument](<../../Models/ProjectDocument.cs#L5>).

### Migration và database thực tế

- SQLite: schema Task xuất hiện trong [InitialSqlite](<../../Migrations/20261111092125_InitialSqlite.cs#L33>); TASK-04 thay đổi WBS trong [Task04WbsLifecycle](<../../Migrations/20261122070416_Task04WbsLifecycle.cs>).
- SQL Server: có [AddTasks](<../../Migrations.SqlServer/20261111081837_AddTasks.cs>) và [AddProgressHistory](<../../Migrations.SqlServer/20261111082215_AddProgressHistory.cs>), nhưng [project file](<../../ConstructionProjectManagement.csproj>) loại thư mục này khỏi compile; [Program](<../../Program.cs>) dùng SQLite.
- **Chưa xác nhận** migration đã áp dụng, schema thực tế hoặc tính hợp lệ của dữ liệu trong DB trong lượt này. Không có seed TASK-05 chuyên biệt được xác định.

---

## PHẦN 4 – FUNCTIONALITY INVENTORY

`IMPLEMENTED` dưới đây nghĩa là **có luồng code**, không có nghĩa đã được kiểm thử runtime hoặc nghiệp vụ đã được phê duyệt.

| # | Chức năng / endpoint | Luồng và entity | Trạng thái |
|---:|---|---|---|
| 1 | Danh sách `GET /Tasks` | [Index](<../../Controllers/TasksController.cs#L22>) → `GetVisibleAsync` → `Tasks/Index` | IMPLEMENTED |
| 2 | Chi tiết `GET /Tasks/Details/{id}` | [Details](<../../Controllers/TasksController.cs#L36>) → `GetByIdAsync` → `Tasks/Details`; Task/assignment | IMPLEMENTED |
| 3 | Tạo `GET/POST /Tasks/Create` | [Create](<../../Controllers/TasksController.cs#L53>) → `CreateAsync` → Task/assignment; có validation nhưng giới hạn quyền chưa đầy đủ | PARTIALLY IMPLEMENTED |
| 4 | Sửa `GET/POST /Tasks/Edit/{id}` | [Edit](<../../Controllers/TasksController.cs#L96>) → `UpdateAsync`; có nguy cơ đổi phạm vi nguồn | PARTIALLY IMPLEMENTED |
| 5 | Giao user | Multi-select trong form → `BuildAssignments`; chỉ nhận user active là PM chính hoặc member active; chưa có lịch sử phân công | PARTIALLY IMPLEMENTED |
| 6 | Task cha–con | `ParentTaskId` trong form và service; cùng dự án nhưng chưa kiểm tra chu kỳ | PARTIALLY IMPLEMENTED |
| 7 | Gắn WBS | `TaskService` → `IWbsService.CanLinkWorkItemToTaskAsync`; bảo toàn link archived hiện có, nhưng quyền khác theo role | PARTIALLY IMPLEMENTED |
| 8 | Tiến độ/trạng thái | `GET /Progress`, `POST /Progress/Update`, `GET /Progress/ChartData`; [ProgressService](<../../Services/ProgressService.cs#L24>) ghi Task/history; phạm vi ghi rộng | PARTIALLY IMPLEMENTED |
| 9 | Xác nhận hoàn thành qua nghiệm thu | `POST /Quality/CreateInspection` và `/Quality/RecordInspection`; [QualityService](<../../Services/QualityService.cs#L54>) cập nhật Task | PARTIALLY IMPLEMENTED |
| 10 | Thống kê Task | Dashboard đếm mở/quá hạn; Progress có biểu đồ và JSON; không phải báo cáo Task đầy đủ | PARTIALLY IMPLEMENTED |
| 11 | Xóa/archive Task | Không thấy action/service/UI | NOT FOUND |
| 12 | Quản lý predecessor/dependency | Chỉ có entity/FK/index; không thấy UI/service/action quản lý | NOT FOUND |
| 13 | Bình luận/trao đổi Task | Không thấy implementation riêng | NOT FOUND |
| 14 | Tệp đính kèm Task | Không thấy liên kết Task–file | NOT FOUND |
| 15 | Tìm kiếm/lọc/phân trang danh sách Task | Không thấy trong Task Index/service | NOT FOUND |
| 16 | Xem lịch sử tiến độ Task | Có ghi history, không thấy trang/action truy xuất history | NOT FOUND |
| 17 | Giao trực tiếp cho đội | `TaskAssignment` gắn User, không thấy Task–Team | NOT FOUND |

**UI cần xác minh runtime:** script đổi dự án và validation section nằm trong [partial form](<../../Views/Tasks/_TaskForm.cshtml#L137>), còn parent dùng `<partial>`. Không chạy UI nên chưa kết luận script được render/thực thi đúng.

---

## PHẦN 5 – AUTHORIZATION MATRIX

Quy ước: **C** = conditional theo điều kiện nêu dưới; **A** = role được action cho phép, vẫn chịu validation; **D** = action từ chối role. Không có ma trận TASK-05 8 role chi tiết được tìm thấy trong tài liệu nghiệp vụ.

| Role | Xem Task | Tạo/giao | Sửa Task | Cập nhật Progress | Ghi kết quả nghiệm thu |
|---|---|---|---|---|---|
| Admin | A¹ | C | C | A¹ | A² |
| Project Director | A¹ | C | C | A¹ | A² |
| Project Manager | C³ | C⁴ | C⁴ | C³ | A² |
| Site Manager | C³ | D | D | C³ | A² |
| Supervising Engineer | C³ | D | D | C³ | A² |
| Worker | C³ | D | D | C³ | D |
| Materials Staff | C³ | D | D | C³ | D |
| Client Representative | C³ | D | D | C³ | D |

1. Controller truyền `isAdmin=true` cho **cả Admin và PD**; các query Task/Progress loại dự án `IsArchived`, nhưng không giới hạn membership: [TasksController](<../../Controllers/TasksController.cs#L29>), [ProgressService](<../../Services/ProgressService.cs#L103>).
2. [QualityController](<../../Controllers/QualityController.cs#L13>) cho năm role đầu; đường nghiệm thu được xem không thấy kiểm tra membership/project scope trong controller/service.
3. Task/Progress cho xem khi là `ProjectManagerId` **hoặc** member active **hoặc** được assignment; không yêu cầu đồng thời membership active. Progress dùng cùng điều kiện cho ghi. Không thấy kiểm tra `ClientName` hay `User.IsActive` tại các luồng này.
4. PM được role attribute cho phép; validation kiểm tra dự án **được gửi lên** là PM chính hoặc member active. Chỉnh sửa qua GET và POST có khác biệt quan trọng như rủi ro #1 bên dưới.

**WBS là quyền riêng:** link WBS mới chịu [WbsService](<../../Services/WbsService.cs#L797>) kiểm tra role/scope và dự án chỉ đọc. Vì vậy quyền tạo Task không đồng nghĩa quyền gắn WBS. Xác nhận hoàn thành trực tiếp với Task không yêu cầu nghiệm thu có thể xảy ra qua Edit/Progress; chưa có action “xác nhận hoàn thành” riêng.

---

## PHẦN 6 – DEPENDENCY MAP

```text
Identity/User/Role + Project/ProjectMember
                  │
                  ▼
Task UI → TasksController → TaskService → Task / TaskAssignment
                  │                 └──→ WbsService → WorkItem/ProjectPhase
                  │
                  ├── ProgressController → ProgressService → Task + TaskProgressHistory
                  ├── QualityController → QualityService → Inspection + Task status
                  ├── Dashboard → đếm Task mở/quá hạn
                  ├── Completion → điều kiện bàn giao/lưu trữ
                  └── Materials / Equipment / Incident / RFI → TaskId
```

- **Trực tiếp:** Project, Identity user, assignment, WBS tùy chọn.
- **Module phụ thuộc Task:** Progress, Quality, Materials, Equipment, Dashboard, Completion.
- **Gián tiếp:** Contract/Document gắn Project; chưa thấy FK Task trực tiếp. Nhật ký thi công có `ProjectId`, không thấy FK Task; sơ đồ nghiệp vụ không chứng minh tích hợp tự động.
- **Dùng chung cần bảo vệ:** [ApplicationDbContext](<../../Data/ApplicationDbContext.cs>), [Program](<../../Program.cs>), Identity, `ProjectMember`, WBS và các migration.
- [IWbsService](<../../Services/Interfaces/IWbsService.cs>) đặt tên tham số `currentWorkItemId`/`existingWorkItemId`, trong khi implementation sử dụng giá trị đó như **taskId**: [WbsService](<../../Services/WbsService.cs#L747>). Caller hiện truyền Task ID; tên hợp đồng dễ gây regression cho caller mới.

---

## PHẦN 7 – ISSUES & RISKS

Các mục dưới đây là **kết luận từ code**, chưa được kiểm chứng bằng request/runtime trong lượt này.

| Mức | Vấn đề, bằng chứng | Ảnh hưởng tiềm ẩn |
|---|---|---|
| **Cao** | `Edit POST` gọi `UpdateAsync`; service tải Task theo `id` rồi kiểm tra quyền trên `task.ProjectId` từ request, không kiểm tra phạm vi Task/dự án nguồn trước khi đổi `existing.ProjectId`: [TaskService](<../../Services/TaskService.cs#L74>), [ValidateAsync](<../../Services/TaskService.cs#L167>). | PM có thể gửi ID Task ngoài phạm vi cùng dự án đích thuộc phạm vi; cần kiểm thử cách ly và xác nhận tác động liên kết trước khi sửa. |
| **Cao** | [ProgressService](<../../Services/ProgressService.cs#L38>) dùng query xem để cho ghi; chỉ cần PM chính, member active **hoặc assignment**, kể cả Worker/Client Representative; không yêu cầu Task được giao. | Người nhìn thấy Task có thể đổi status/% ngoài assignment; chưa rõ có đúng nghiệp vụ không. |
| **Cao** | [QualityService](<../../Services/QualityService.cs#L54>) lấy Task/inspection theo ID; chưa thấy kiểm tra scope dự án hoặc trạng thái read-only. [QualityController](<../../Controllers/QualityController.cs#L13>) cho năm role. | Có nguy cơ tạo/ghi nghiệm thu và đổi Task ngoài phạm vi dự án. |
| **Cao** | `LoadOptionsAsync` nạp **mọi** dự án chưa archive, mọi Task và mọi user active: [TasksController](<../../Controllers/TasksController.cs#L183>). | PM có thể nhìn thấy tên dự án, Task và tài khoản ngoài phạm vi qua form. |
| **Cao** | Task/Progress chỉ loại `Project.IsArchived`; không khóa `Completed`/`Cancelled`. `TaskService` chỉ gọi WBS authorization khi thay liên kết WBS: [TaskService](<../../Services/TaskService.cs#L181>). | Có thể tiếp tục sửa Task/tiến độ ở dự án kết thúc; riêng WBS áp dụng read-only chặt hơn. Cần xác nhận chính sách TASK-05. |
| **Cao** | Luồng Task/Progress không tự kiểm tra `ApplicationUser.IsActive` hay tổ chức Client Representative: [TaskService](<../../Services/TaskService.cs#L118>), [ProgressService](<../../Services/ProgressService.cs#L103>). | Cookie còn hiệu lực hoặc dữ liệu membership/assignment có thể cho phép truy cập không phù hợp; hiệu lực cookie thực tế chưa kiểm thử. |
| **Trung bình** | Chỉ kiểm tra Task cha cùng dự án, không kiểm tra tự làm cha hoặc chu kỳ: [TaskService](<../../Services/TaskService.cs#L219>). | Có thể tạo cây cha–con vòng lặp. |
| **Trung bình** | Edit xóa toàn bộ assignment cũ rồi tạo lại: [TaskService](<../../Services/TaskService.cs#L112>). | Mất `AssignedAt` gốc ngay cả với người vẫn được giao; không có lịch sử giao/thu hồi. |
| **Trung bình** | Status/% có thể sửa ở Task Edit, Progress và Quality; chỉ Progress tạo `TaskProgressHistory`: [TaskService](<../../Services/TaskService.cs#L97>), [ProgressService](<../../Services/ProgressService.cs#L57>), [QualityService](<../../Services/QualityService.cs#L95>). | Lịch sử tiến độ không đầy đủ; status, 100% và ngày thực tế có thể không đồng bộ. |
| **Trung bình** | [MaterialService](<../../Services/MaterialService.cs#L55>) và [EquipmentService](<../../Services/EquipmentService.cs#L61>) ghi `TaskId` nhưng không thấy kiểm tra Task thuộc `ProjectId` được chọn. | Liên kết chéo dự án giữa Task và phiếu vật tư/thiết bị có thể tồn tại nếu FK hợp lệ. |
| **Trung bình** | Form model không khai báo `MaxLength` cho title/description dù entity có: [TaskViewModels](<../../ViewModels/TaskViewModels.cs#L20>), [Task](<../../Models/Task.cs#L20>). | Validation form không đồng bộ với giới hạn domain; hành vi lưu phụ thuộc provider. |
| **Trung bình** | Permission/scope query được lặp tại Task và Progress; `isAdmin` thực tế còn bao gồm PD. | Dễ lệch quyền khi thay đổi một phía; cần test regression trước khi thống nhất. |
| **Trung bình** | [CompletionController](<../../Controllers/CompletionController.cs#L27>) truy vấn Task mở không lọc project scope; role PM được vào controller. | Có nguy cơ lộ Task ngoài dự án qua màn bàn giao. |
| **Thấp** | UI Task/Progress hiển thị enum trực tiếp, ví dụ `Pending`, `High`: [Task Index](<../../Views/Tasks/Index.cshtml#L40>), [Progress](<../../Views/Progress/Index.cshtml#L48>). | Chưa đồng bộ tiếng Việt. |

---

## PHẦN 8 – MISSING FUNCTIONALITIES & CẦN CHỐT

**Chưa tìm thấy:** xóa/archive Task; quản lý dependency; bình luận; file đính kèm; tìm/lọc/phân trang Task; giao trực tiếp cho đội; UI xem lịch sử tiến độ.

**Cần xác nhận trước implementation:**

1. Ma trận 8 role cho xem/tạo/giao/sửa/cập nhật tiến độ/nghiệm thu; PD và Admin có được xem toàn hệ thống không?
2. Task có bắt buộc membership active, `User.IsActive` và khớp tổ chức khách hàng như WBS không? Assignment còn hiệu lực khi membership inactive không?
3. `Completed`, `Cancelled`, `IsArchived` của Project có khóa mọi thao tác Task, Progress và Quality không?
4. Có cho chuyển Task sang dự án khác không; nếu có, xử lý WBS, parent, assignment, vật tư, thiết bị và inspection thế nào?
5. Chu kỳ Task cha–con và `TaskDependency`, quy tắc trạng thái, % hoàn thành, deadline và nghiệm thu cần áp dụng thế nào?
6. Assignment cần lịch sử, người giao, thời hạn và phân công đội không?
7. Xóa mềm/archive, comment, attachment, notification và audit log có thuộc phạm vi TASK-05 đã phê duyệt không?
8. Có yêu cầu UI tiếng Việt và tìm kiếm/lọc/phân trang cụ thể nào?

---

## PHẦN 9 – IMPLEMENTATION READINESS

**READY WITH LIMITATIONS.** Có controller/service/view, entity và migration làm nền; tuy nhiên quyền và vòng đời chưa thống nhất giữa Task–Progress–Quality, có đường POST cần kiểm chứng phạm vi nguồn, và chưa có bằng chứng test chuyên biệt toàn diện cho TASK-05. Một test WBS liên quan Task đã tồn tại tại [WbsServiceTests](<../../tests/ConstructionProjectManagement.Tests/WbsServiceTests.cs#L621>); không suy ra toàn bộ TASK-05 đã được kiểm thử.

**Thứ tự đề xuất sau khi được duyệt:**

1. Chốt nghiệp vụ và ma trận quyền.
2. Viết test cách ly tái hiện các rủi ro phạm vi/ghi trước khi thay code.
3. Thống nhất authorization và read-only xuyên Task–Progress–Quality.
4. Chốt invariants trạng thái, cây và assignment; bảo vệ tích hợp WBS và các module tiêu thụ.
5. Sau đó mới triển khai UI/chức năng còn thiếu theo phạm vi được xác nhận.

---

## PHẦN 10 – FINAL SUMMARY

Theo phạm vi kiểm kê ở Phần 2:

- **131 file:** 20 DIRECT, 74 INDIRECT, 37 SHARED.
- **16 entity:** 4 lõi và 12 entity liên quan.
- **8 controller, 15 service/interface, 16 view/partial** trong danh mục.
- **17 nhóm chức năng đánh giá:** 2 IMPLEMENTED, 8 PARTIALLY IMPLEMENTED, 7 NOT FOUND.
- Rủi ro ưu tiên: **Edit POST theo dự án đích**, **Progress dùng quyền xem để ghi**, **Quality thiếu scope**, và **form nạp dữ liệu ngoài phạm vi**.

Không có thay đổi code, migration, cấu hình hoặc database trong lượt audit. Tôi sẽ chờ bạn duyệt báo cáo và chốt nghiệp vụ trước khi thực hiện giai đoạn tiếp theo.

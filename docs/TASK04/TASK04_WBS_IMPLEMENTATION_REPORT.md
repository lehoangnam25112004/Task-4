# Báo cáo triển khai và kiểm thử WBS - TASK-04

## 1. Kết luận

Đã triển khai luồng WBS ở tầng dịch vụ, controller, giao diện Razor và tích hợp kiểm tra liên kết WBS khi tạo/cập nhật Task. Đã bổ sung migration SQLite và kiểm thử chuyên biệt.

| Hạng mục | Kết quả |
|---|---|
| Build ứng dụng | PASS - 0 lỗi |
| Build dự án test | PASS - 0 lỗi |
| Bộ kiểm thử xUnit | PASS - 38/38 |
| Migration SQLite | Đã áp dụng trên DB hiện hữu sau khi người dùng xác nhận đã sao lưu; schema và toàn vẹn khóa ngoại đã được xác minh |
| Kiểm thử giao diện | PASS trên bản sao DB dùng riêng; gồm WBS và gửi cập nhật Task giữ liên kết WBS đã archive |
| DB hiện hữu | Đã có migration mới nhất; không thêm dữ liệu test vào DB này |

Ba đặc tả được đối chiếu là các tài liệu tiếng Việt đã được người dùng xác nhận trong thư mục làm việc là nguồn có hiệu lực; các đường dẫn tên tiếng Anh dưới `docs/TASK04/` nêu trong yêu cầu ban đầu không tồn tại trong repository tại thời điểm kiểm tra.

## 2. Nội dung đã triển khai

### Phân quyền và phạm vi dự án

- Tất cả tám vai trò đã được đưa vào kiểm thử: Admin, Project Director, Project Manager, Site Manager, Supervising Engineer, Materials Staff, Worker và Client Representative.
- Người dùng phải đang hoạt động. Mọi vai trò nghiệp vụ ngoài Admin phải có `ProjectMember.IsActive == true` trong đúng dự án; Project Director không còn được truy cập toàn cục.
- Client Representative chỉ truy cập dự án có `ClientName` khớp `OrganizationName` sau khi trim, không phân biệt hoa thường.
- Không có vai trò hợp lệ thì từ chối mặc định. Admin có thể xem toàn hệ thống và Archive/Restore, nhưng không thể sửa cấu trúc hoặc trạng thái nghiệp vụ, kể cả khi tài khoản có thêm role khác.
- Quyền dịch vụ được áp dụng lại ở mỗi thao tác; không chỉ dựa vào việc ẩn nút trên giao diện.
- Form sửa kiểm tra quyền theo giai đoạn/hạng mục thực tế trước cả khi trả lỗi validation. Form tạo kiểm tra quyền sửa cấu trúc ở service, kể cả tài khoản nhiều role.
- Trong dự án chỉ đọc, Admin/PD/PM vẫn có bộ lọc xem mục WBS đã lưu trữ; mọi nút lưu trữ/khôi phục và thao tác ghi vẫn bị khóa.

| Chức năng | Vai trò được phép |
|---|---|
| Xem WBS dự án trong phạm vi | Tám vai trò; Admin xem toàn hệ thống |
| Tạo/sửa giai đoạn và cấu trúc WorkItem | Project Director, Project Manager |
| Cập nhật trạng thái WorkItem | Project Director, Project Manager, Site Manager |
| Archive/Restore và xem bản lưu trữ | Admin, Project Director, Project Manager |
| Liên kết WBS với Task tại service | Project Manager, Site Manager, Supervising Engineer, Materials Staff |

### Cây WBS và nghiệp vụ

- Hiển thị cây đệ quy theo giai đoạn, cha-con, `SequenceOrder`, mã WBS, cấp và số lượng Task liên kết; trạng thái có nhãn tiếng Việt.
- Bổ sung tạo/sửa giai đoạn và WorkItem, thay đổi thứ tự/trạng thái, kiểm tra trạng thái đầu vào và thông báo khi hoàn thành cha còn con chưa hoàn thành.
- Kiểm tra dự án/giai đoạn/cha cùng phạm vi, không nhận cha đã lưu trữ, phát hiện vòng lặp và tính lại `Level` cho toàn nhánh khi chuyển cha.
- Chặn mã WBS trùng trong cùng dự án theo so sánh không phân biệt hoa thường ở tầng dịch vụ; có unique index SQLite với collation `NOCASE` để bảo vệ các trường hợp cạnh tranh trên mã ASCII.
- Dự án lưu trữ, hoàn thành hoặc hủy được đánh dấu chỉ đọc.

### Archive/Restore và tích hợp Task

- Archive là lưu trữ mềm, không xóa WorkItem và không làm mất `Task.WorkItemId`.
- Archive giai đoạn lưu snapshot trạng thái và archive các WorkItem đang hoạt động theo cùng `ArchiveGroupId`; Restore chỉ khôi phục những mục thuộc batch đó, không tự mở các mục đã archive riêng.
- Restore WorkItem khôi phục giai đoạn/chuỗi cha cần thiết và giữ nguyên trạng thái các nhánh khác.
- Form Task chỉ nạp WorkItem đang hoạt động thuộc cùng dự án; liên kết đã lưu trữ sẵn vẫn hiển thị khi sửa Task, nhưng không được dùng để tạo liên kết mới.
- Không mở rộng role gate hiện có của Task Create/Edit và không tạo endpoint Task-WBS riêng. Vì vậy, dù service có kiểm tra quyền liên kết cho SM/SE/MS, các vai trò này vẫn không có đường UI tạo/sửa Task trong phạm vi role gate hiện tại. Đây là giới hạn được giữ nguyên theo quyết định đã chốt, không phải thay đổi quyền Task.

## 3. Database và migration

Migration mới: `Migrations/20261122070416_Task04WbsLifecycle.cs`.

- Thêm thông tin archive/snapshot trạng thái và audit fields cho WorkItem; thêm thông tin archive/snapshot trạng thái cho ProjectPhase.
- Mở rộng mã WBS tối đa 50 ký tự, đặt collation `NOCASE`, thêm index lọc theo trạng thái lưu trữ và check constraint `Level >= 1`.
- Chuyển `ProjectPhases.Status` từ TEXT sang integer bằng ánh xạ dữ liệu rõ ràng: Planned/NotStarted → 0, In Progress/InProgress/Active → 1, Completed/Done → 2, Archived → 3; giá trị không nhận diện được → 0. Dữ liệu cũ Archived được đánh dấu lưu trữ và có trạng thái khôi phục mặc định NotStarted.
- Phần `Down` chuyển enum trạng thái về giá trị chuỗi tương ứng trước khi đổi kiểu cột.

Migration đã chạy trong test SQLite in-memory, gồm tình huống dữ liệu trạng thái cũ, và sau đó được áp dụng lên `Data/Databases/ConstructionProjectManagement.db` sau khi người dùng xác nhận đã sao lưu. DB thực tế ghi nhận migration `20261122070416_Task04WbsLifecycle`; hai cột trạng thái có kiểu `INTEGER`, và `PRAGMA foreign_key_check` không phát hiện vi phạm. Tại thời điểm xác minh, DB thực tế không có ProjectPhase, WorkItem hoặc Task nào liên kết WBS, nên không có bản ghi lịch sử để đối chiếu trực tiếp việc chuyển đổi trạng thái; ánh xạ vẫn được kiểm tra trong migration test SQLite in-memory.

## 4. File đã tạo/sửa

| File | Thay đổi |
|---|---|
| `Controllers/WbsController.cs` | Thêm/hoàn thiện endpoint UI và kiểm soát role ở các thao tác WBS |
| `Controllers/TasksController.cs` | Nạp lựa chọn WBS theo dự án và giữ liên kết lưu trữ khi sửa |
| `Data/ApplicationDbContext.cs` | Quan hệ, collation, index và constraint WBS |
| `Models/ProjectPhase.cs` | Trạng thái enum và trường lưu trữ |
| `Models/ProjectPhaseStatus.cs` | Mới: trạng thái giai đoạn |
| `Models/WorkItem.cs` | Mô tả, audit, archive và snapshot trạng thái |
| `Models/WbsItemStatus.cs` | Trạng thái thống nhất, giữ `OnHold` |
| `Services/Interfaces/IWbsService.cs` | Hợp đồng nghiệp vụ, authorization và Task-link |
| `Services/WbsService.cs` | Kiểm tra quyền/phạm vi, cây, validation, Archive/Restore |
| `Services/WbsOperationResult.cs` | Mới: kết quả nghiệp vụ có loại lỗi rõ ràng |
| `Services/WbsTreeModels.cs` | Mới: mô hình cây hiển thị |
| `Services/Interfaces/ITaskService.cs` | Truyền role vào kiểm tra liên kết WBS |
| `Services/TaskService.cs` | Kiểm tra server-side khi thêm/thay đổi liên kết WBS |
| `ViewModels/WbsViewModels.cs` | ViewModel giai đoạn/WorkItem và trạng thái |
| `ViewModels/WbsStatusLabels.cs` | Mới: nhãn trạng thái tiếng Việt |
| `Views/Wbs/Index.cshtml` | Trang cây WBS, bộ lọc mục lưu trữ và thao tác |
| `Views/Wbs/_WorkItemTree.cshtml` | Mới: partial hiển thị cây đệ quy |
| `Views/Wbs/EditPhase.cshtml` | Mới: form sửa giai đoạn |
| `Views/Wbs/EditItem.cshtml` | Mới: form sửa WorkItem |
| `Views/Wbs/CreateItem.cshtml` | Cập nhật form tạo WorkItem |
| `Views/Tasks/_TaskForm.cshtml` | Hiển thị lựa chọn WBS theo quyền/phạm vi |
| `Migrations/20261122070416_Task04WbsLifecycle.cs` | Mới: migration SQLite và ánh xạ dữ liệu cũ |
| `Migrations/20261122070416_Task04WbsLifecycle.Designer.cs` | Mới: metadata migration |
| `Migrations/ApplicationDbContextModelSnapshot.cs` | Đồng bộ snapshot EF Core |
| `tests/ConstructionProjectManagement.Tests/WbsServiceTests.cs` | Mới: kiểm thử chuyên biệt cho WBS |

## 5. Kiểm thử đã chạy

### Lệnh

```powershell
dotnet build --no-restore -p:UseAppHost=false
dotnet build .\tests\ConstructionProjectManagement.Tests\ConstructionProjectManagement.Tests.csproj --no-restore -p:UseAppHost=false
dotnet test .\tests\ConstructionProjectManagement.Tests\ConstructionProjectManagement.Tests.csproj --no-build -p:UseAppHost=false
```

### Kết quả

- Build ứng dụng: PASS, 0 lỗi, 3 cảnh báo.
- Build test: PASS, 0 lỗi, 4 cảnh báo.
- Test: PASS, 38 passed, 0 failed, 0 skipped.
- Cảnh báo build còn lại là cảnh báo advisory package SQLite/PKCS và các tham chiếu Contract obsolete đã có từ trước; không thuộc phạm vi TASK-04.
- `dotnet test --no-build -p:UseAppHost=false` khi chạy tại root chỉ nhắm project web, không phát hiện test. Vì vậy, lệnh test nêu trên chỉ định rõ project test.

### Nhóm ca được bao phủ

- Ma trận quyền của tám vai trò, Admin kết hợp role khác, role không hợp lệ.
- Dự án ngoài phạm vi, thành viên inactive, user inactive, Client Representative sai/đúng tổ chức.
- Dự án chỉ đọc.
- Cây đệ quy, thứ tự hiển thị, đếm Task, dữ liệu cây vòng lặp.
- Tái tính cấp khi chuyển cha, chặn vòng lặp và sai lệch dự án/giai đoạn.
- Mã trùng không phân biệt hoa thường.
- Archive/Restore theo batch, khôi phục trạng thái, giữ liên kết Task và ngăn liên kết mới vào mục đã lưu trữ.
- Migration chuyển trạng thái ProjectPhase cũ, gồm trạng thái không nhận diện được.
- Cập nhật Task giữ nguyên liên kết WBS đã lưu trữ (regression test cho lỗi dịch LINQ của EF Core).

### Khởi động smoke test

Ứng dụng được khởi động với SQLite tạm riêng, không dùng DB hiện hữu. `GET /Wbs` trả `302` tới `/Identity/Account/Login?ReturnUrl=%2FWbs`, đúng hành vi khi chưa đăng nhập. DB tạm và các file WAL/SHM đã được dọn sau khi kiểm tra.

## 6. Hướng dẫn kiểm tra UI

1. Sao lưu database trước khi chạy phiên bản mới vì khởi tạo ứng dụng tự áp dụng migration.
2. Khởi chạy ứng dụng và đăng nhập tài khoản thuộc một trong các vai trò được nêu trong ma trận.
3. Mở `/Wbs` để xem các dự án trong phạm vi, tìm theo mã/tên dự án hoặc mã/tên hạng mục WBS và chọn **Chi tiết**. Có thể mở trực tiếp `/Wbs?projectId=<ID>` cho dự án cụ thể. Xem trước và tìm kiếm chỉ bao gồm các hạng mục chưa lưu trữ.
4. Trong **Chi tiết**, với Project Director/Project Manager có quyền ghi, dùng **Thêm giai đoạn** hoặc **Thêm hạng mục WBS**; thử sửa, đổi thứ tự, đổi mã, đổi cha rồi xác nhận cấp cây cập nhật. Trang tổng quan chỉ có nút **Chi tiết** trên mỗi dự án.
5. Với Site Manager, thử đổi trạng thái nhưng xác nhận không thể tạo/sửa cấu trúc.
6. Với Admin/Project Director/Project Manager, bật “Hiển thị mục đã lưu trữ”, Archive rồi Restore một mục và kiểm tra trạng thái/Task liên kết.
7. Thử mở URL của dự án mà user không là thành viên active, dự án hoàn thành/hủy hoặc dự án có ClientName không khớp tổ chức; server phải từ chối tương ứng.
8. Trên form Task, kiểm tra lựa chọn WBS chỉ gồm mục hoạt động của dự án đang chọn; một liên kết đã archive trước đó vẫn được giữ khi sửa Task.

## 7. Giới hạn và lưu ý triển khai

- DB thực tế không có WBS cũ, nên không thể xác nhận trực tiếp từng giá trị lịch sử sau chuyển đổi trên DB này. Kết quả dữ liệu rỗng không được xem là bằng chứng có dữ liệu cũ đã được chuyển đổi.
- Kiểm tra ma trận quyền tám vai trò và phạm vi dự án được chạy qua kiểm thử dịch vụ; thao tác trình duyệt sau đăng nhập tập trung vào tài khoản Project Manager trên bản sao DB. Không tuyên bố đã thao tác UI với cả tám tài khoản.
- Role gate Task Create/Edit hiện có được giữ nguyên. Nếu muốn SM/SE/MS trực tiếp tạo/sửa Task để dùng khả năng liên kết WBS trong UI, cần một quyết định nghiệp vụ và thay đổi riêng ở TASK quản lý Task.
- UI được chạy ở môi trường Development trên DB bản sao cô lập. Chạy source ở Production trong môi trường kiểm thử làm các endpoint static assets của Identity trả 500; chuyển sang Development cho các asset cục bộ trả 200. Không thay đổi cấu hình production của ứng dụng.

## 8. Xác minh sau migration và kiểm thử UI

### DB thực tế

Sau khi người dùng xác nhận đúng DB và xác nhận đã sao lưu, migration được áp dụng bằng `dotnet ef database update --no-build`. Đọc DB bằng kết nối chỉ đọc cho kết quả:

- Migration mới nhất: `20261122070416_Task04WbsLifecycle` (EF Core `10.0.1`).
- `ProjectPhases.Status` và `WorkItems.Status` là `INTEGER`.
- Enum `WorkItem` giữ `OnHold=3` tách biệt với `Archived=4`; nhãn `OnHold` được xác nhận là `Tạm hoãn` trên UI bản sao. Migration chuyển đổi chuỗi cũ chỉ áp dụng cho `ProjectPhases.Status`; `WorkItems.Status` vẫn là số nguyên, và DB thực tế không có WorkItem cũ để kiểm tra giá trị lịch sử.
- Số lượng ProjectPhase: 0; WorkItem: 0; Task có `WorkItemId`: 0.
- `PRAGMA foreign_key_check`: không có dòng vi phạm.
- Do không có bản ghi WBS thực tế, không thể khẳng định dữ liệu lịch sử nào đó đã chuyển trạng thái; không ghi dữ liệu test vào DB thực tế.

### UI và phản hồi lưu trữ

Các thao tác sau được chạy trên bản sao tạm của DB, với tài khoản test và dữ liệu được cô lập, rồi dọn DB cùng các file WAL/SHM:

- Kiểm tra cây WBS và nhãn tiếng Việt, gồm trạng thái `Tạm hoãn`; tạo/sửa giai đoạn và WorkItem, đổi thứ tự, kiểm tra mã trùng và cảnh báo hoàn tất cha khi còn con chưa hoàn thành.
- Kiểm tra Archive/Restore theo batch: khôi phục giai đoạn không mở lại một WorkItem đã archive riêng; đồng thời kiểm tra khôi phục item con và liên kết Task.
- Form tạo Task chỉ liệt kê WBS hoạt động. Form sửa Task vẫn hiển thị lựa chọn WBS lưu trữ hiện tại với nhãn `Đã lưu trữ - liên kết hiện tại`.
- Lần gửi form sửa Task đầu tiên phát hiện EF Core không dịch được biểu thức kiểm tra thành viên trong truy vấn assignee. Sau khi người dùng cho phép sửa đúng phạm vi, truy vấn được chuyển sang lọc trực tiếp `ProjectMembers` active trong DB, giữ nguyên các điều kiện user active và PM/thành viên dự án. Regression test được thêm; chạy lại form sửa Task chuyển tới `/Tasks/Details/1`, không còn 500.
- Đọc lại bản sao DB sau POST xác nhận `Tasks.WorkItemId` vẫn là `1`, WorkItem vẫn `IsArchived=1`, `Status=4 (Archived)` và `StatusBeforeArchive=3 (OnHold)`. Liên kết không bị mất và mục không bị tự khôi phục.

Kiểm thử dịch vụ xUnit bao phủ ma trận tám vai trò, chặn dự án ngoài phạm vi/thành viên inactive, kiểm tra tổ chức Client Representative, hành vi chỉ đọc, Archive/Restore và liên kết Task. Kiểm thử trình duyệt xác minh các luồng UI nêu trên với Project Manager; không thay thế kết quả test dịch vụ bằng suy luận từ việc ẩn/hiện nút.

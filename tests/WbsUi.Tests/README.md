# Kiểm thử UI và phân quyền WBS

Bộ test dùng xUnit, Playwright .NET và Microsoft Edge ở chế độ headless. Cần .NET 10 SDK,
Edge và quyền mở cổng loopback cục bộ. Chạy từ thư mục gốc ứng dụng:

```powershell
dotnet test .\tests\WbsUi.Tests\WbsUi.Tests.csproj -p:UseAppHost=false --logger "trx;LogFileName=wbs-ui.trx" --results-directory .\tests\WbsUi.Tests\TestResults
```

Test tạo SQLite mới trong thư mục tạm, áp dụng migration và tạo tài khoản/dự án giả;
mật khẩu được sinh ngẫu nhiên trong bộ nhớ. Ứng dụng test dùng connection string riêng,
cổng loopback được chọn tự động và các cờ seed TASK-02/TASK-03 được tắt. Không sao chép
hay ghi vào database chính. Trình duyệt có context/cookie riêng cho từng tài khoản.
Fixture dừng đúng tiến trình test và xóa các file SQLite tạm của nó sau khi hoàn tất.
Nếu tiến trình test bị dừng cưỡng bức, file `cpms-wbs-ui-<GUID>.sqlite` cùng WAL/SHM
có thể còn trong thư mục tạm và cần được kiểm tra trước khi xóa thủ công.

Ma trận kiểm tra 8 role gồm tìm kiếm, reset, chi tiết, phạm vi, nút và GET/POST;
thực hiện thao tác được phép, kiểm tra trạng thái SQLite sau các POST bị từ chối,
và thử cả ba trạng thái dự án chỉ đọc. Test riêng kiểm tra tài khoản inactive,
membership inactive với form lỗi và giả mạo ProjectId, Client Representative lệch
tổ chức, default-deny, Admin+PM và Admin+PD. Quyền xem dự án ngoài membership
của Admin là ngoại lệ có chủ ý.

Kết quả TRX được lưu tại `TestResults`; nếu test thất bại, ảnh và log server giả
cũng được lưu tại đó. Thư mục này không được đưa vào git. Nếu chạy lại, tên file
TRX và ảnh/log cùng tên có thể bị ghi đè; file lỗi cũ không chứng minh lượt chạy
gần nhất vẫn lỗi. Chỉ xem log/ảnh đối với dữ liệu test; không dùng fixture này
với connection string hoặc tài khoản thật.

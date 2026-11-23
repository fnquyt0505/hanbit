# RULES — Quy tắc viết code (.NET MVC)

## 1. Nguyên tắc chung

1. **Làm từng task một** theo `TASKS.md`. Xong task → cập nhật `PROGRESS.md` → mới làm task tiếp.
2. **Nhỏ và chạy được**: mỗi thay đổi phải `dotnet build` không warning, `dotnet test` xanh.
3. **Đọc trước khi sửa**: đọc file liên quan + docs trước khi viết code.
4. **Đơn giản trước**: chọn cách đơn giản nhất đáp ứng yêu cầu; không thêm tính năng ngoài task. Không thêm MediatR/AutoMapper/Repository chung chung khi chưa cần.

## 2. C#

- `Nullable` enable, `TreatWarningsAsErrors` = true (trong `Directory.Build.props`).
- File-scoped namespace, namespace khớp thư mục (`Hanbit.Domain.Srs`).
- Đặt tên: `PascalCase` cho type/method/property, `_camelCase` cho field private, `camelCase` cho biến cục bộ, interface bắt đầu bằng `I`, method async kết thúc bằng `Async`.
- Ưu tiên `sealed record` cho model bất biến; `class` cho entity EF.
- Mọi I/O là async, nhận `CancellationToken`. **Cấm** `.Result`, `.Wait()`, `async void` (trừ event handler).
- Không bắt `catch (Exception)` rồi nuốt lỗi; log bằng `ILogger` rồi ném lại hoặc trả lỗi rõ ràng.
- Không dùng `DateTime.Now`/`UtcNow` trực tiếp — dùng `IClock` (để test được).
- Dependency Injection qua constructor (primary constructor được phép). Không dùng service locator.
- `.editorconfig` + `dotnet format` là chuẩn định dạng.

## 3. Kiến trúc

- Tôn trọng hướng phụ thuộc trong ARCHITECTURE.md: **Domain không tham chiếu** EF Core, ASP.NET hay bất kỳ project nào.
- Logic nghiệp vụ (SRS, chấm bài, streak, XP, sinh bài tập) nằm trong `Hanbit.Domain`, **không** viết trong Controller, View hay JavaScript.
- **Server tính toán – trình duyệt lưu trữ** (ARCHITECTURE §2). JS không tự tính điểm/SRS/streak.

## 4. ASP.NET Core MVC

- Controller **mỏng**: nhận request → gọi Application/Domain → trả View/JSON. Không truy cập file/JSON trực tiếp.
- Mỗi View có **ViewModel** riêng (`Models/<Feature>/<Name>ViewModel.cs`). Không truyền entity/record Domain thẳng vào View, không dùng `ViewBag`/`ViewData` cho dữ liệu chính.
- Route rõ ràng bằng attribute routing (`[Route("learn/{courseSlug}")]`), slug tiếng Việt không dấu.
- Tái sử dụng UI theo thứ tự ưu tiên: **Tag Helper** (phần tử nhỏ) → **Partial View** (khối hiển thị) → **View Component** (khối có logic lấy dữ liệu).
- API: `[ApiController]`, route `api/...`, DTO request/response riêng, validate bằng FluentValidation, lỗi trả `ProblemDetails`. API **stateless**, không dùng Session/TempData.
- DTO bài tập gửi xuống trình duyệt **không chứa đáp án**.
- Trả `NotFound()` cho slug không tồn tại; có trang lỗi 404/500 thân thiện tiếng Việt.

## 5. JavaScript & Alpine.js

- ES modules trong `wwwroot/js/`, một module một trách nhiệm. Không inline `<script>` dài trong View (chỉ khởi tạo).
- Tương tác UI dùng Alpine.js (`x-data` trỏ tới component đăng ký trong module). Không thêm jQuery, React, Vue, htmx…
- Gọi API chỉ qua `api.js`; xử lý lỗi mạng (thông báo + cho thử lại).
- localStorage chỉ truy cập qua `progress-store.js`.
- Không cần Node/npm. Thư viện JS (chỉ Alpine) được vendored trong `wwwroot/lib` có ghi phiên bản.
- Script dùng `asp-append-version="true"` để tránh cache cũ.

## 6. Styling

- Chỉ dùng Tailwind + design tokens trong `DESIGN.md`. **Không hard-code mã màu**.
- Dùng Tag Helper/Partial UI có sẵn trước khi viết thêm.
- Mobile-first: class mobile trước rồi `sm:` `md:` `lg:`.
- Không thêm Bootstrap hay thư viện CSS khác.

## 7. Dữ liệu nội dung

- Mọi truy cập nội dung **phải đi qua `IContentRepository`**. Không đọc file JSON trực tiếp trong Controller/View.
- Mọi file trong `content/` phải pass `Hanbit.ContentValidator`.
- Chuỗi tiếng Hàn chuẩn hóa NFC (`string.Normalize(NormalizationForm.FormC)`) khi so sánh đáp án.
- File `content/vocab/krdict-*.json` **chỉ sinh bởi `Hanbit.ContentImporter`** — không sửa tay. Sửa nghĩa tiếng Việt → `content/vocab-reviews.json`.
- Mọi `VocabItem` từ krdict phải giữ `SourceRef`. Bài học chỉ dùng từ có `reviewed = true`.
- Không xóa/ẩn footer ghi công và trang `/nguon-du-lieu` (yêu cầu giấy phép CC BY-SA).

## 8. Tiến độ người học (chế độ khách) & bảo mật

- Tiến độ chỉ đọc/ghi qua `progress-store.js`; xử lý dữ liệu hỏng/khác `SchemaVersion` mà không làm hỏng trang.
- Dữ liệu tiến độ client gửi lên luôn được **validate giới hạn** ở server.
- Đổi cấu trúc `LearnerProgress` → tăng `SchemaVersion` + hàm migrate (C#) + test.
- Ngày "hôm nay" lấy từ server (`IClock`), không tin đồng hồ trình duyệt.
- Secret (krdict API key…) chỉ ở User Secrets / biến môi trường. Không bao giờ ở `appsettings*.json` hay `render.yaml`.
- *(Giai đoạn 8)* Thay đổi schema DB = migration EF Core mới; không sửa migration đã apply; query lọc theo `UserId`; `[Authorize]` cho trang cần đăng nhập.

## 9. Test

- Bắt buộc unit test (xUnit + Shouldly) cho: `Srs`, `Exercises` (generator, checker), `Progress` (streak/XP), `JsonContentRepository`, validators.
- Integration test (`WebApplicationFactory`) cho **mọi endpoint `/api/*`** và các trang chính trả 200/404 đúng.
- E2E (Playwright) cho: học Hangul bài 1, làm 1 bài học, 1 phiên ôn tập (bao gồm localStorage).
- Tên test: `MethodName_Condition_ExpectedResult`.

## 10. Git

- Conventional Commits: `feat:`, `fix:`, `refactor:`, `docs:`, `test:`, `chore:`.
- Mỗi commit gắn với 1 task, VD: `feat(srs): implement Sm2Scheduler [T6.1]`.
- `.gitignore` chuẩn VisualStudio + `bin/`, `obj/`, `wwwroot/css/app.css` (file build).

---

## 🚫 AI TUYỆT ĐỐI KHÔNG ĐƯỢC

1. **Không bịa dữ liệu tiếng Hàn.** Từ vựng lấy từ 한국어기초사전 qua importer; không tự thêm/sửa nghĩa rồi đánh dấu `reviewed`. Chỉ người dùng mới duyệt. Nếu không chắc → `TODO(verify)` và ghi vào PROGRESS.md.
2. **Không xóa hoặc ghi đè file trong `content/`, `docs/`, `Migrations/`** khi task không yêu cầu. Đặc biệt không ghi đè `vocab-reviews.json`.
3. **Không cài thêm NuGet package** ngoài stack trong `ARCHITECTURE.md` mà không ghi lý do và hỏi người dùng.
4. **Không đổi tech stack, cấu trúc solution, hướng phụ thuộc hay design tokens** khi chưa được đồng ý.
5. **Không commit secret** (connection string, ClientSecret, `appsettings.*.json` chứa key). Không in secret ra log.
6. **Không tắt cảnh báo để "cho build được"** (`#pragma warning disable`, `!` null-forgiving bừa bãi, `<NoWarn>`).
7. **Không xóa/skip test đang fail** (`[Fact(Skip=…)]`) để pass — phải sửa nguyên nhân.
8. **Không làm nhiều task cùng lúc** hay "tiện tay refactor" phần không liên quan.
9. **Không dùng nội dung có bản quyền** (giáo trình, đề thi TOPIK chính thức, dữ liệu Naver, lời bài hát) làm dữ liệu. Nguồn ngoài duy nhất được phép: 한국어기초사전.
10. **Không báo "đã xong"** khi chưa chạy `dotnet build` + `dotnet test` thành công.
11. **Không chạy lệnh phá hủy** (`dotnet ef database drop`, `rm -rf`, xóa migration, `git push --force`) khi chưa được xác nhận.

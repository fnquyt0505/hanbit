# PROGRESS — Nhật ký tiến độ

> AI/dev cập nhật file này **sau mỗi task**. Mục mới nhất ở trên cùng.

## 📍 Trạng thái hiện tại

| | |
|---|---|
| Giai đoạn | 0 — Khởi tạo solution |
| Task đang làm | — |
| Task tiếp theo | **T0.3** 4 project test (xUnit + Shouldly, WebApplicationFactory, Playwright) |
| Việc song song (người dùng) | Đăng ký API key 한국어기초사전 + tải file dữ liệu (cần cho T1.5) |
| Blocker | — |

## 💻 Môi trường máy dev

| | |
|---|---|
| OS | Windows 11 Home (ASUS) |
| Ổ đĩa | C: ~177 GB (ít chỗ trống — **không** đặt cache/dữ liệu lớn ở C) · D: ~297 GB |
| Docker | Docker Desktop (per-user, WSL 2), chạy `hello-world` OK · data: `D:\DockerData` |
| Cache dev | `NUGET_PACKAGES=D:\DevCache\nuget\packages` · `PLAYWRIGHT_BROWSERS_PATH=D:\DevCache\ms-playwright` · npm cache `D:\DevCache\npm-cache` |
| Quy ước | Repo, tool, file tạm lớn → đặt trên ổ **D** |

## ✅ Đã hoàn thành

### 2026-11-23 — T0.2a CI GitHub Actions + auto-merge
- Đã làm: `.github/workflows/ci.yml`, job `build-test` (ubuntu, SDK theo `global.json`: restore → build Release → `dotnet format --verify-no-changes` → test), chạy khi có PR vào `main` và push `main`. Ruleset `main` (tạo qua `gh api`): bắt buộc check `build-test`, chặn force-push và xóa nhánh. Người dùng: chuyển repo sang public (gói Free không có auto-merge/ruleset cho repo private), bật *Allow auto-merge* + *Automatically delete head branches*, đăng nhập `gh` bằng PAT classic (`repo`, `workflow`, `read:org`). Cập nhật quy trình git trong AGENT.md §2.
- File thay đổi: `.github/workflows/ci.yml`, `AGENT.md`, `docs/TASKS.md`, `docs/PROGRESS.md`
- Kiểm tra: build ✅ (0 warning) · format ✅ · CI trên PR này ✅ → tự merge
- Vấn đề / ghi chú: `dotnet test` chưa chạy test nào cho tới T0.3. Ruleset không có bypass → kể cả chủ repo cũng phải đi qua PR. PAT hết hạn sau 90 ngày → tạo lại + `gh auth login`.
- Tiếp theo: T0.3

### 2026-11-23 — T0.2 Cấu hình build chung
- Đã làm: `Directory.Build.props` (net10.0, Nullable, ImplicitUsings, TreatWarningsAsErrors, AnalysisLevel latest, EnforceCodeStyleInBuild) và gỡ các thuộc tính trùng khỏi 4 csproj; `global.json` (SDK 10.0.401, rollForward latestFeature); `.gitignore` (template VisualStudio + `data-raw/`, `wwwroot/css/app.css`); `.editorconfig` (template `dotnet new editorconfig`, nâng lên `warning`: file-scoped namespace IDE0161, interface `I…`, field private `_camelCase`; UTF-8, LF, newline cuối file). Thêm `.gitattributes` (`eol=lf`) để Windows và CI Linux thống nhất line ending, `dotnet format --verify-no-changes` không lệch. Thêm task T0.2a (CI + auto-merge) vào TASKS.md theo đồng ý của người dùng.
- File thay đổi: `Directory.Build.props`, `global.json`, `.gitignore`, `.gitattributes`, `.editorconfig`, `src/*/*.csproj`, `HomeController.cs` (chỉ line ending)
- Kiểm tra: build ✅ (0 warning, `--no-incremental`) · `dotnet format --verify-no-changes` ✅ · test: chưa có project test (T0.3)
- Vấn đề / ghi chú: quy trình git mới: mỗi task một nhánh `NN-type-T##-slug`, AI push, người dùng mở PR và merge (sẽ tự động hóa ở T0.2a).
- Tiếp theo: T0.2a

### 2026-11-23 — T0.1 Tạo solution + 4 project
- Đã làm: `Hanbit.sln` (ép định dạng `.sln` vì SDK 10 mặc định `.slnx`); `Hanbit.Domain`, `Hanbit.Application`, `Hanbit.Infrastructure` (classlib, net10.0); `Hanbit.Web` (`dotnet new mvc --auth None`). Reference: Application → Domain, Infrastructure → Application, Web → Application + Infrastructure (Web là composition root để gọi DI của Infrastructure). Gỡ `wwwroot/lib` (Bootstrap, jQuery, jquery-validation), `_ValidationScriptsPartial`, `_Layout.cshtml.css`, `site.css`, `site.js`, trang Privacy; layout/Index/Error tối giản, tiếng Việt.
- File thay đổi: `Hanbit.sln`, `src/**`
- Kiểm tra: build ✅ (0 warning) · test — chưa có project test (T0.3) · `dotnet run` → `/` trả 200, route lạ trả 404
- Vấn đề / ghi chú: chưa có `.gitignore` (thuộc T0.2) nên commit chỉ stage file nguồn, `bin/`/`obj/` còn untracked.
- Tiếp theo: T0.2

- [x] 2026-11-23 — Cài Docker Desktop (WSL 2, dữ liệu ở D), dọn ổ C (còn trống ~12 GB), chuyển cache NuGet/npm/Playwright sang D.
- [x] 2026-11-23 — Chuyển từ Blazor sang **ASP.NET Core MVC** + Alpine.js. Cập nhật ARCHITECTURE, TASKS, RULES, AGENT, PRD.
- [x] 2026-11-23 — Chốt: tên tạm Hanbit, dữ liệu 한국어기초사전, MVP chế độ khách, deploy Render. Cập nhật PRD, ARCHITECTURE, TASKS, RULES, AGENT.
- [x] 2026-11-23 — Chuyển tài liệu sang stack .NET 10 + Blazor.
- [x] 2026-11-23 — Soạn bộ tài liệu: PRD, ARCHITECTURE, DESIGN, RULES, TASKS, PROGRESS, AGENT.

## 🐞 Lỗi / vấn đề đang mở

| ID | Mô tả | Task liên quan | Trạng thái |
|---|---|---|---|
| — | Chưa có | | |

## ❓ Câu hỏi / việc cần xác minh

1. Định dạng file tải về & mã ngôn ngữ tiếng Việt của krdict API → xác minh khi làm T1.5.
2. Điều khoản bản quyền chính thức của krdict (CC BY-SA phiên bản nào, điều kiện ghi công) → xác minh trước T7.4.
3. Ai duyệt nghĩa tiếng Việt (T1.7)? Mặc định: chủ dự án.

## 📝 Quyết định đã chốt

| Ngày | Quyết định | Lý do |
|---|---|---|
| 2026-11-23 | Tên tạm **Hanbit (한빛)** | |
| 2026-11-23 | Dữ liệu từ vựng: **한국어기초사전 (국립국어원)**, CC BY-SA, ghi công bắt buộc | Nguồn chính thống, có tiếng Việt, có cấp độ |
| 2026-11-23 | Chỉ từ đã duyệt (`reviewed`) mới vào bài học | Nghĩa tiếng Việt sơ cấp có thể có lỗi dịch |
| 2026-11-23 | MVP **chế độ khách**, tiến độ lưu **localStorage** + xuất/nhập JSON | Ra mắt nhanh, không cần DB |
| 2026-11-23 | **ASP.NET Core MVC** (Controllers + Razor Views) thay cho Blazor | Lựa chọn của người dùng; HTTP thuần, không phụ thuộc WebSocket trên Render |
| 2026-11-23 | Tương tác phía trình duyệt: **Alpine.js** vendored + ES modules, không Node | Nhẹ, hợp với Razor View |
| 2026-11-23 | **Server tính toán – trình duyệt lưu trữ**: API stateless `/api/*`, logic ở C# Domain | Chế độ khách lưu localStorage; không viết logic 2 lần |
| 2026-11-23 | Đáp án không gửi xuống HTML; chấm qua API | |
| 2026-11-23 | Deploy **Render** (Docker Web Service), deploy sớm từ T0.7 | Phát hiện lỗi môi trường sớm |
| 2026-11-23 | Tài khoản + PostgreSQL dời sang giai đoạn 8 (P1) | |
| 2026-11-23 | .NET 10 | Chuyên môn của người dùng |
| 2026-11-23 | Tailwind v4 standalone CLI, Tag Helper/Partial/View Component tự viết | |
| ~~2026-11-23~~ | ~~Blazor Web App, InteractiveServer, không dùng ProtectedLocalStorage~~ | Thay bằng MVC |
| 2026-11-23 | Kiến trúc Domain / Application / Infrastructure / Web | |
| 2026-11-23 | SRS SM-2 rút gọn, 4 mức đánh giá | |

---

## Mẫu ghi nhật ký

```md
### YYYY-MM-DD — T#.# Tên task
- Đã làm: …
- File thay đổi: …
- Kiểm tra: build ✅ (0 warning) · test ✅
- Vấn đề / ghi chú: …
- Tiếp theo: T#.#
```

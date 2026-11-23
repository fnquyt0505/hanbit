# PROGRESS — Nhật ký tiến độ

> AI/dev cập nhật file này **sau mỗi task**. Mục mới nhất ở trên cùng.

## 📍 Trạng thái hiện tại

|                                 |                                                                                                                              |
| ------------------------------- | ---------------------------------------------------------------------------------------------------------------------------- |
| Giai đoạn                     | 0 — Khởi tạo solution                                                                                                     |
| Task đang làm                 | —                                                                                                                           |
| Production                      | https://hanbit.onrender.com (Render free, Singapore — ngủ khi không có truy cập, request đầu chậm) |
| Task tiếp theo                 | **T1.1** Records nội dung trong `Domain/Content` + `JsonSerializerOptions` |
| Việc song song (người dùng) | Đăng ký API key 한국어기초사전 + tải file dữ liệu (cần cho T1.5)                                                      |
| Blocker                         | —                                                                                                                           |

## 💻 Môi trường máy dev

|           |                                                                                                                                                |
| --------- | ---------------------------------------------------------------------------------------------------------------------------------------------- |
| OS        | Windows 11 Home (ASUS)                                                                                                                         |
| Ổ đĩa  | C: ~177 GB (ít chỗ trống — **không** đặt cache/dữ liệu lớn ở C) · D: ~297 GB                                                 |
| Docker    | Docker Desktop (per-user, WSL 2), chạy `hello-world` OK · data: `D:\DockerData`                                                          |
| Cache dev | `NUGET_PACKAGES=D:\DevCache\nuget\packages` · `PLAYWRIGHT_BROWSERS_PATH=D:\DevCache\ms-playwright` · npm cache `D:\DevCache\npm-cache` |
| Quy ước | Repo, tool, file tạm lớn → đặt trên ổ **D**                                                                                       |

## ✅ Đã hoàn thành

### 2026-11-24 — T0.7 Deploy sớm (hoàn tất trên Render)
- Đã làm: người dùng tạo tài khoản Render (đăng nhập GitHub), cài GitHub App Render, tạo Blueprint `hanbit` từ `render.yaml` (branch `main`) → service web `hanbit` **Live** tại https://hanbit.onrender.com.
- Kiểm tra trên production: `/health` → 200 `Healthy` · `/`, `/styleguide` → 200 (`lang="vi"`) · `/css/app.css` → 200, nén Brotli, link có fingerprint (static web assets đúng ở Production) · Alpine → 200 · đường dẫn lạ → 404 · HSTS có mặt (forwarded headers hoạt động sau proxy) · HTTP → 301 HTTPS. Auto-deploy: kiểm chứng bằng commit của PR này (Render tự deploy sau khi CI xanh — `autoDeployTrigger: checksPass`).
- Tiếp theo: T1.1

### 2026-11-23 — T0.7 Deploy sớm (phần code)
- Đã làm: `/health` (`AddHealthChecks` + `MapHealthChecks`, có sẵn). `Program.cs`: bỏ `UseHttpsRedirection` (Render ép HTTPS ở proxy; health check nội bộ đi HTTP), bật forwarded headers qua `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`. `Dockerfile` multi-stage: `sdk:10.0.401` (khớp `global.json`; Tailwind CLI linux tự tải + kiểm SHA-256) → `aspnet:10.0` (nhận bản vá runtime), cổng 10000, user không phải root; `.dockerignore`. `render.yaml`: Web Service Docker, plan free, region Singapore, `healthCheckPath: /health`, `autoDeployTrigger: checksPass` (chỉ deploy commit `main` khi CI xanh). CI thêm job `docker`: build image + chạy container (Production) + smoke test `/health`, `/`, `app.css`, Alpine, kiểm tra không chạy root.
- File thay đổi: `Dockerfile`, `.dockerignore`, `render.yaml`, `src/Hanbit.Web/Program.cs`, `.github/workflows/ci.yml`, `tests/Hanbit.Web.Tests/HealthTests.cs`
- Kiểm tra: build ✅ (0 warning) · format ✅ · test ✅ (45/45) · CI `build-test` ✅ + `docker` ✅ (image build + container chạy đúng ở Production)
- Vấn đề / ghi chú: **Docker Desktop trên máy dev không phân giải DNS** (registry Docker Hub & MCR: `no such host`, trong khi Windows phân giải bình thường — nghi Cloudflare WARP) → chưa build được image local; đã kiểm chứng bằng job `docker` trên CI thay thế. Còn chờ: người dùng tạo Blueprint trên Render → AI kiểm tra URL `*.onrender.com` + auto-deploy → đánh dấu T0.7 `[x]`.
- Tiếp theo: hoàn tất T0.7 trên Render, rồi T1.1

### 2026-11-23 — T0.6 UI cơ bản
- Đã làm: token chữ `--success-ink #297C4F`, `--warning-ink #8F6614`, `--xp-ink #90650E` (light; dark = màu gốc) đạt ≥ 4.5:1 — cập nhật DESIGN.md §2 (quyết định câu 4). Tag Helper `<hb-button>` (variant `primary/secondary/outline/ghost`, size `sm/md/lg` — `lg` cao 48px, `href` → `<a>`, `disabled`, `block` full-width mobile; chữ trên nút dùng `--bg` vì chữ trắng trên `--primary` dark chỉ 3.7:1; variant/size sai → exception) và `<ko-text>` (`lang="ko"` + `font-ko` lớn hơn 1 bậc, `display` → `font-hangul text-hangul-display`, `romanization` → span `lang="ko-Latn"` nghiêng, màu phụ). Partial `_Card` (`CardViewModel` + Razor template), `_ProgressBar` (`role="progressbar"`, aria-value*), `_Dialog` (`<dialog>` gốc: khóa focus, Esc/nút/bấm nền để đóng; mở bằng `$dispatch('hb-dialog-open', id)`), `_ToastRegion` trong `_Layout` (`aria-live`, icon cho từng loại). `wwwroot/js/ui.js`: Alpine store `toast` (`show(message, kind, timeoutMs)`, `dismiss`) + component `hbDialog`; export `toast()` cho module khác. `/styleguide` thêm mục Components.
- File thay đổi: `src/Hanbit.Web/{TagHelpers/*, Models/Shared/*, Views/Shared/{_Card,_Dialog,_ProgressBar,_ToastRegion,_Layout}.cshtml, Views/_ViewImports.cshtml, Views/Styleguide/Index.cshtml, Models/Styleguide/StyleguideViewModel.cs, Styles/input.css, wwwroot/js/ui.js}`, `docs/DESIGN.md`, `tests/Hanbit.Web.Tests/{TagHelpers/*, StyleguideTests.cs}`, `tests/Hanbit.E2E/ComponentsTests.cs`
- Kiểm tra: build ✅ (0 warning, `--no-incremental`) · format ✅ · test ✅ (43/43: 17 unit test render Tag Helper; E2E mở/đóng dialog bằng Esc và nút, toast đúng/sai có icon, đóng toast, progress bar aria)
- Vấn đề / ghi chú: (1) Warning lẻ không tái hiện xuất hiện lần 2 (nghi VS Code build song song khóa DLL) — build sạch 0 warning. (2) MVC mặc định mã hóa mọi ký tự ngoài ASCII (`Trang ch&#x1EE7;`) → HTML tiếng Việt/Hàn phình to; đề xuất cấu hình `WebEncoderOptions` cho phép `UnicodeRanges.All` (task riêng / T7.3). Câu hỏi 4–6 đã chốt (xem Quyết định).
- Tiếp theo: T0.7

### 2026-11-23 — T0.5 Design tokens + /styleguide

- Đã làm: `Styles/input.css`: 11 token màu đúng tên/giá trị DESIGN.md §2 (`--bg`, `--ink`…), dark theo `prefers-color-scheme` hoặc ép bằng `<html data-theme="light|dark">`; `@theme inline` map sang Tailwind (`bg-surface`, `text-ink-muted`, `border-border`…) và **tắt bảng màu mặc định** (`--color-*: initial`) → `bg-red-500` không sinh CSS; font `font-sans` (Be Vietnam Pro), `font-ko` (Pretendard → Noto Sans KR), `font-hangul` (Noto Serif KR), `text-hangul-display` (clamp 4–6rem); base: nền/chữ theo token, line-height 1.6/1.2, focus ring `--secondary`. Font: Google Fonts + Pretendard 1.3.9 qua jsDelivr. `wwwroot/js/theme.js` (component Alpine `themeSwitcher`: Hệ thống/Sáng/Tối), `styleguide.js` (`tokenValue` đọc giá trị thực của token). `StyleguideController` (`/styleguide`, noindex) + `StyleguideViewModel`. `_Layout`: section `Head` nạp module **trước** Alpine (để đăng ký component kịp `alpine:init`). E2E fixture `NewPageAsync(colorScheme)` chặn request ra ngoài (font CDN) → test không phụ thuộc mạng.
- File thay đổi: `src/Hanbit.Web/{Styles/input.css, Views/Shared/_Layout.cshtml, Views/Styleguide/Index.cshtml, Views/Home/Index.cshtml, Controllers/StyleguideController.cs, Models/Styleguide/StyleguideViewModel.cs, wwwroot/js/theme.js, wwwroot/js/styleguide.js}`, `tests/Hanbit.Web.Tests/{StyleguideTests.cs, StaticAssetsTests.cs}`, `tests/Hanbit.E2E/{StyleguideTests.cs, WebServerFixture.cs, HomePageTests.cs}`
- Kiểm tra: build ✅ (0 warning, `--no-incremental` Debug + Release) · format ✅ · test ✅ (20/20: nền sáng/tối theo hệ thống, nút chuyển Sáng↔Tối, 3 font, CSS không có màu mặc định)
- Vấn đề / ghi chú: một lần build báo 1 warning không tái hiện được (nghi MSB3026: file DLL bị testhost lần trước giữ) — build lại sạch 0 warning. Lựa chọn sáng/tối **chưa được lưu** giữa các lần tải trang (xem câu hỏi 4–5).
- Tiếp theo: T0.6

### 2026-11-23 — T0.4 Tailwind v4 + Alpine.js

- Đã làm: `src/Hanbit.Web/Tailwind.targets` (import trong csproj): tự tải Tailwind CLI **v4.3.5** standalone đúng OS/CPU (win-x64, linux-x64/arm64, macOS) vào `.tools/tailwindcss/<ver>/` (gitignored), **kiểm SHA-256** theo `sha256sums.txt` của release; build `Styles/input.css` → `wwwroot/css/app.css` trước mỗi build, incremental (bỏ qua khi View/JS/CSS không đổi), luôn `--minify` (chỉ minify ở Release thì bản Debug cũ lọt vào publish do incremental không phân biệt Configuration); đưa `app.css` vào static web assets bằng đường dẫn tương đối. `Styles/input.css`: `source(none)` + chỉ quét `Views/`, `TagHelpers/`, `wwwroot/js/`; `[x-cloak]`. Vendor **Alpine.js 3.17.5** (`wwwroot/lib/alpinejs/alpine.min.js`, ghi phiên bản trong `wwwroot/lib/README.md`), nạp `defer` + `asp-append-version` trong `_Layout`. Trang chủ tạm có demo Tailwind + `x-data` (thay ở T2.2). AGENT.md: sửa lệnh cài Playwright (không có `pwsh`), thêm ghi chú Tailwind/Alpine và lệnh lọc E2E.
- File thay đổi: `src/Hanbit.Web/{Tailwind.targets, Hanbit.Web.csproj, Styles/input.css, Views/Shared/_Layout.cshtml, Views/Home/Index.cshtml, wwwroot/lib/**}`, `tests/Hanbit.Web.Tests/StaticAssetsTests.cs`, `tests/Hanbit.E2E/HomePageTests.cs`, `.gitignore`, `AGENT.md`
- Kiểm tra: build ✅ (0 warning, cả Release từ trạng thái sạch) · format ✅ · test ✅ (11/11: E2E kiểm tra `font-bold` → `font-weight: 700` và click `x-data` hiện panel) · `dotnet publish` có `app.css` đã minify + bản nén `.gz/.br`
- Vấn đề / ghi chú: lỗi tìm thấy trong lúc làm: thêm `app.css` vào `Content` bằng đường dẫn tuyệt đối → không có trong manifest static web assets → Production (`MapStaticAssets`) sẽ trả 404, Development thì vẫn chạy nên khó thấy. Đã sửa + test `EndpointsManifest_BuiltAsset_IsRegisteredForMapStaticAssets` (đã xác nhận test đỏ khi chưa sửa). Dockerfile (T0.7) cần mạng tới github.com để tải CLI lúc build.
- Tiếp theo: T0.5

### 2026-11-23 — T0.3 Project test

- Đã làm: `tests/Hanbit.Domain.Tests`, `Hanbit.Infrastructure.Tests` (test kiến trúc: assembly không tham chiếu sai hướng), `Hanbit.Web.Tests` (`WebApplicationFactory`: `/` trả 200 HTML `lang="vi"`, đường dẫn lạ trả 404), `Hanbit.E2E` (`WebServerFixture`: app chạy Kestrel cổng ngẫu nhiên qua `UseKestrel(0)` của .NET 10 + Chromium dùng chung; test mở trang chủ, kiểm tra title và chữ `한빛` có `lang="ko"`; `[Trait("Category","E2E")]`). Package: xunit 2.9.3 (template), Shouldly 4.3.0, Microsoft.AspNetCore.Mvc.Testing 10.0.12 (khớp runtime 10.0.12 của SDK 10.0.401; bản 11.0 chỉ hỗ trợ net11), Microsoft.Playwright 1.65.1. CI thêm bước cài Chromium (`--with-deps`). `Hanbit.Tools.Tests` để tới T1.3 (khi có tool).
- File thay đổi: `tests/**`, `Hanbit.sln`, `.github/workflows/ci.yml`
- Kiểm tra: build ✅ (0 warning) · format ✅ · test ✅ (5/5)
- Vấn đề / ghi chú: biến môi trường `PLAYWRIGHT_BROWSERS_PATH` **chưa được đặt** ở cấp User (bảng môi trường ghi đã đặt). Chromium đã cài vào `D:\DevCache\ms-playwright` bằng cách đặt biến tạm thời → cần `setx PLAYWRIGHT_BROWSERS_PATH D:\DevCache\ms-playwright` để chạy E2E từ terminal/VS Code. Máy không có `pwsh` (PowerShell 7) → dùng `powershell -File …\playwright.ps1 install chromium`.
- Tiếp theo: T0.4

### 2026-11-23 — T0.2a CI GitHub Actions + auto-merge

- Đã làm: `.github/workflows/ci.yml`, job `build-test` (ubuntu, SDK theo `global.json`: restore → build Release → `dotnet format --verify-no-changes` → test), chạy khi có PR vào `main` và push `main`. Ruleset `main-protection` (người dùng tạo trên UI, vì quyền đổi cài đặt repo không giao cho AI): bắt buộc check `build-test`, chặn force-push và xóa nhánh. Người dùng: chuyển repo sang public (gói Free không có auto-merge/ruleset cho repo private), bật *Allow auto-merge* + *Automatically delete head branches*, đăng nhập `gh` bằng PAT classic (`repo`, `workflow`, `read:org`). Cập nhật quy trình git trong AGENT.md §2.
- File thay đổi: `.github/workflows/ci.yml`, `AGENT.md`, `docs/TASKS.md`, `docs/PROGRESS.md`
- Kiểm tra: build ✅ (0 warning) · format ✅ · CI `build-test` trên PR #3 ✅ (29s). PR #3 merge thủ công vì ruleset tạo sau khi CI đã chạy; từ T0.3 dùng auto-merge.
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

- [X] 2026-11-23 — Cài Docker Desktop (WSL 2, dữ liệu ở D), dọn ổ C (còn trống ~12 GB), chuyển cache NuGet/npm/Playwright sang D.
- [X] 2026-11-23 — Chuyển từ Blazor sang **ASP.NET Core MVC** + Alpine.js. Cập nhật ARCHITECTURE, TASKS, RULES, AGENT, PRD.
- [X] 2026-11-23 — Chốt: tên tạm Hanbit, dữ liệu 한국어기초사전, MVP chế độ khách, deploy Render. Cập nhật PRD, ARCHITECTURE, TASKS, RULES, AGENT.
- [X] 2026-11-23 — Chuyển tài liệu sang stack .NET 10 + Blazor.
- [X] 2026-11-23 — Soạn bộ tài liệu: PRD, ARCHITECTURE, DESIGN, RULES, TASKS, PROGRESS, AGENT.

## 🐞 Lỗi / vấn đề đang mở

| ID | Mô tả   | Task liên quan | Trạng thái |
| -- | --------- | --------------- | ------------ |
| E1 | Docker Desktop (WSL 2) không phân giải DNS registry (`lookup …: no such host`), Windows thì bình thường. Nghi Cloudflare WARP. Cách thử: khởi động lại Docker Desktop; tắt WARP; hoặc Docker Desktop → Settings → Docker Engine thêm `"dns": ["1.1.1.1", "8.8.8.8"]` | T0.7 | Mở — CI job `docker` thay thế |

## ❓ Câu hỏi / việc cần xác minh

1. Định dạng file tải về & mã ngôn ngữ tiếng Việt của krdict API → xác minh khi làm T1.5.
2. Điều khoản bản quyền chính thức của krdict (CC BY-SA phiên bản nào, điều kiện ghi công) → xác minh trước T7.4.
3. Ai duyệt nghĩa tiếng Việt (T1.7)? Mặc định: chủ dự án.
4. ~~Tương phản màu~~ → đã chốt: thêm token `-ink` (xem Quyết định).
5. ~~Lưu chế độ sáng/tối ở đâu~~ → đã chốt: qua `progress-store.js` ở T5.3.
6. ~~Font qua CDN hay tự host~~ → đã chốt: trước mắt giữ CDN.

## 📝 Quyết định đã chốt

| Ngày           | Quyết định                                                                                         | Lý do                                                                                |
| --------------- | ----------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------- |
| 2026-11-23      | Tên tạm **Hanbit (한빛)**                                                                     |                                                                                       |
| 2026-11-23      | Dữ liệu từ vựng: **한국어기초사전 (국립국어원)**, CC BY-SA, ghi công bắt buộc            | Nguồn chính thống, có tiếng Việt, có cấp độ                                 |
| 2026-11-23      | Chỉ từ đã duyệt (`reviewed`) mới vào bài học                                               | Nghĩa tiếng Việt sơ cấp có thể có lỗi dịch                                  |
| 2026-11-23      | MVP **chế độ khách**, tiến độ lưu **localStorage** + xuất/nhập JSON             | Ra mắt nhanh, không cần DB                                                         |
| 2026-11-23      | **ASP.NET Core MVC** (Controllers + Razor Views) thay cho Blazor                                | Lựa chọn của người dùng; HTTP thuần, không phụ thuộc WebSocket trên Render |
| 2026-11-23      | Tương tác phía trình duyệt: **Alpine.js** vendored + ES modules, không Node              | Nhẹ, hợp với Razor View                                                            |
| 2026-11-23      | **Server tính toán – trình duyệt lưu trữ**: API stateless `/api/*`, logic ở C# Domain | Chế độ khách lưu localStorage; không viết logic 2 lần                         |
| 2026-11-23      | Đáp án không gửi xuống HTML; chấm qua API                                                      |                                                                                       |
| 2026-11-23      | Deploy **Render** (Docker Web Service), deploy sớm từ T0.7                                    | Phát hiện lỗi môi trường sớm                                                   |
| 2026-11-23      | Tài khoản + PostgreSQL dời sang giai đoạn 8 (P1)                                                 |                                                                                       |
| 2026-11-23      | .NET 10                                                                                               | Chuyên môn của người dùng                                                       |
| 2026-11-23      | Tailwind v4 standalone CLI, Tag Helper/Partial/View Component tự viết                               |                                                                                       |
| ~~2026-11-23~~ | ~~Blazor Web App, InteractiveServer, không dùng ProtectedLocalStorage~~                            | Thay bằng MVC                                                                        |
| 2026-11-23      | Kiến trúc Domain / Application / Infrastructure / Web                                               |                                                                                       |
| 2026-11-23      | SRS SM-2 rút gọn, 4 mức đánh giá                                                                |                                                                                       |
| 2026-11-23      | Thêm token chữ `--success-ink`, `--warning-ink`, `--xp-ink`; màu gốc chỉ làm nền/viền/icon | Màu gốc ở light mode không đạt 4.5:1 khi làm chữ (DESIGN.md §8) |
| 2026-11-23      | Lưu chế độ sáng/tối qua `progress-store.js` (T5.3) + script `<head>` đặt `data-theme` trước khi vẽ | Giữ RULES §5 (localStorage chỉ qua progress-store.js) |
| 2026-11-23      | Font qua CDN (Google Fonts, jsDelivr) — trước mắt | Nhanh, tự subset tiếng Hàn; mở CSP cho 3 domain ở T7.3 |
| 2026-11-23      | Mỗi task 1 nhánh `NN-type-T##-slug` → PR → auto-merge khi CI `build-test` xanh; repo public | Ruleset/auto-merge miễn phí cho repo public |

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

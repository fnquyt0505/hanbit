# TASKS — Các bước nhỏ, làm từng bước một (.NET MVC · chế độ khách · Render)

> Quy ước: `[ ]` chưa làm · `[~]` đang làm · `[x]` xong. Mỗi task có **Tiêu chí hoàn thành (DoD)**.
> Làm theo thứ tự. Không nhảy giai đoạn khi giai đoạn trước chưa xong.

## Giai đoạn 0 — Khởi tạo solution

- [x] **T0.1** Tạo `Hanbit.sln` + 4 project (`Domain`, `Application`, `Infrastructure` là classlib; `Web` từ `dotnet new mvc`, **không auth**), reference đúng hướng phụ thuộc; xóa Bootstrap/jQuery mặc định của template. DoD: `dotnet run --project src/Hanbit.Web` mở được trang.
- [x] **T0.2** `Directory.Build.props` (net10.0, Nullable, TreatWarningsAsErrors), `.editorconfig`, `.gitignore` (thêm `data-raw/`, `wwwroot/css/app.css`), `global.json`. DoD: `dotnet build` 0 warning.
- [x] **T0.2a** CI GitHub Actions (`.github/workflows/ci.yml`: restore → build → `dotnet format --verify-no-changes` → test, chạy khi có PR vào `main` và push `main`) + auto-merge (`gh pr create` + `gh pr merge --auto --squash`). DoD: PR mở bằng `gh` tự merge khi CI xanh; CI đỏ thì không merge. 👤 Người dùng: cài/đăng nhập `gh`, bật *Allow auto-merge* + *Automatically delete head branches*, rule bảo vệ `main` bắt buộc check CI.
- [x] **T0.3** 4 project test (xUnit + Shouldly; `Hanbit.Web.Tests` dùng `WebApplicationFactory`; Playwright), mỗi project 1 test mẫu. DoD: `dotnet test` xanh.
- [x] **T0.4** Tailwind v4 standalone CLI (`Styles/input.css` → `wwwroot/css/app.css`, MSBuild target trước build) + vendor Alpine.js vào `wwwroot/lib`. DoD: class Tailwind và `x-data` hoạt động.
- [x] **T0.5** Design tokens (màu light/dark, font) theo DESIGN.md; trang `/styleguide`. DoD: đủ màu & font, chuyển dark mode được.
- [x] **T0.6** UI cơ bản: Tag Helper `<hb-button>`, `<ko-text>`; partial `_Card`, `_Dialog`, `_ProgressBar`; toast bằng Alpine. DoD: hiển thị trên `/styleguide`, test render Tag Helper.
- [x] **T0.7** Deploy sớm: `Dockerfile` multi-stage + `/health` + `render.yaml`, deploy lên Render. DoD: URL `*.onrender.com` chạy, auto-deploy khi push `main`.

## Giai đoạn 1 — Dữ liệu nội dung (한국어기초사전)

- [x] **T1.1** Records nội dung trong `Domain/Content` + `JsonSerializerOptions` (snake_case, polymorphic Exercise). DoD: test deserialize.
- [x] **T1.2** `content/hangul.json` (40 chữ, tự biên soạn) + `content/LICENSE-DATA.md`. DoD: đủ 14 phụ âm, 5 phụ âm kép, 10 nguyên âm, 11 nguyên âm ghép.
- [x] **T1.3** FluentValidation validators + tool `Hanbit.ContentValidator`. DoD: báo lỗi rõ file/trường.
- [x] **T1.4** `IContentRepository` + `JsonContentRepository` (IMemoryCache, merge `vocab-reviews.json`). DoD: unit test.
- [x] **T1.5** Tool `Hanbit.ContentImporter` (krdict → `content/vocab/krdict-beginner.json`, lọc 초급, nghĩa tiếng Việt, `SourceRef`). Xác minh định dạng & mã ngôn ngữ trước. DoD: test với file mẫu nhỏ; không ghi đè `vocab-reviews.json`; báo cáo số từ.
- [x] **T1.6** `RomanizationService` (Revised Romanization). DoD: test ≥ 30 từ.
- [x] **T1.7** 👤 **Người dùng** duyệt nghĩa ~150 từ đầu (AI hỗ trợ xuất CSV + tool nhập lại).
- [x] **T1.8** Soạn khóa `beginner`: 5 bài đầu, chỉ dùng từ đã duyệt + ngữ pháp tự viết giải thích. DoD: pass validator.

## Giai đoạn 2 — Layout & điều hướng

- [x] **T2.1** `_Layout.cshtml`: `_BottomNav` (mobile), `_Sidebar` (desktop), `_Footer` ghi công. 
- [x] **T2.2** Trang chủ `/`: hero, 3 lợi ích, CTA "Bắt đầu miễn phí". DoD: responsive, Lighthouse ≥ 90. *(Lighthouse mobile trên production: 95 · 96 · 100 · 100.)*
- [x] **T2.2a** Trang lỗi thân thiện: `UseStatusCodePagesWithReExecute("/loi/{0}")` (trừ `/api/*`) → trang 404/lỗi trong layout, giữ mã HTTP gốc, nút "Về trang chủ". DoD: test Web + E2E.
- [x] **T2.3** `/nguon-du-lieu`: ghi công 국립국어원 · 한국어기초사전, giấy phép, link gốc.
- [x] **T2.4** `/dashboard` với skeleton (dữ liệu điền bằng JS ở giai đoạn 5).

## Giai đoạn 3 — Hangul & âm thanh

- [x] **T3.1** `speech.js` (speak, tốc độ thường/chậm, kiểm tra giọng `ko-KR`) + Tag Helper `<audio-button>`. DoD: test thủ công Chrome/Safari/Android. *(Người dùng nghe thử trên máy tính 2026-11-24 — câu hỏi 12; Android/Safari chưa kiểm.)*
- [x] **T3.2** `/hangul`: lưới chữ theo nhóm (partial `_HangulTile`).
- [x] **T3.3** `HangulComposer` (Domain, công thức Unicode) + `GET /api/hangul/compose` + bảng ghép âm (Alpine). DoD: unit test + integration test API.
- [x] **T3.4** `/hangul/{group}`: học từng chữ + quiz 10 câu (`hangul-quiz.js`, chấm qua API).

## Giai đoạn 4 — Bài học & bài tập

- [x] **T4.1** `/learn/{course}`: View Component `CourseMap` (trạng thái khóa/mở điền bằng JS).
- [x] **T4.2** `ExerciseGenerator` + `AnswerChecker` (Domain) — 5 loại, NFC, chuẩn hóa khoảng trắng. DoD: unit test.
- [x] **T4.3** `GET /api/lessons/{c}/{l}` (DTO không đáp án) + `POST /api/exercises/check`. DoD: integration test; HTML/JSON không lộ đáp án.
- [x] **T4.4** `/learn/{c}/{l}` + `lesson-runner.js`: từ vựng → ngữ pháp → bài tập (`multiple_choice`, `listen_choose`).
- [x] **T4.5** UI `match_pairs`, `reorder`, `type_answer`.
- [x] **T4.6** Màn tổng kết: điểm, từ sai (XP/lưu ở giai đoạn 5).

## Giai đoạn 5 — Tiến độ chế độ khách

- [x] **T5.1** Model `LearnerProgress` + `IClock` + `XpRules` + `StreakCalculator` (Asia/Ho_Chi_Minh, qua nửa đêm). DoD: unit test.
- [x] **T5.2** `SubmitLessonHandler` + `POST /api/lessons/{c}/{l}/complete` (trả LessonRecord, card mới, activity, XP). DoD: integration test.
- [x] **T5.3** `progress-store.js`: load/save localStorage, xử lý JSON hỏng/khác version, merge kết quả từ API. DoD: E2E reload trang vẫn giữ tiến độ.
- [x] **T5.4** Dashboard, CourseMap, StreakBadge đọc tiến độ thật.
- [x] **T5.5** Xuất/nhập JSON ở `/profile` + `POST /api/progress/validate` (validate + migrate). DoD: E2E xuất → xóa → nhập → khôi phục đúng.

## Giai đoạn 6 — Ôn tập SRS

- [x] **T6.1** `Sm2Scheduler` thuần + test 4 grade và biên.
- [x] **T6.2** `POST /api/reviews/due`, `POST /api/reviews/grade`. DoD: integration test.
- [x] **T6.3** `/review` + `review-session.js`: flashcard lật, 4 nút đánh giá, phím 1–4 / Space.
- [x] **T6.4** Dashboard hiển thị số thẻ đến hạn.

## Giai đoạn 7 — Hoàn thiện & ra mắt MVP

- [x] **T7.1** `/profile`: thống kê từ đã thuộc, lịch streak.
- [x] **T7.2** E2E 3 luồng: Hangul bài 1, 1 bài học, 1 phiên ôn tập.
- [x] **T7.3** Rate limiting cho `/api/*`, security headers, `Cache-Control` cho nội dung tĩnh.
- [x] **T7.4** Accessibility (axe), SEO meta, OG image, sitemap.
- [x] **T7.5** Kiểm tra điều khoản bản quyền krdict, trang ghi công, `LICENSE-DATA.md`.
- [x] **T7.6** Render: domain riêng (nếu có), kiểm tra cold start trên 4G. *(Giữ hanbit.onrender.com; tối ưu nén + warmup + ReadyToRun; UptimeRobot ping /health 5 phút/lần — người dùng cài.)*

## Giai đoạn 8 — Tài khoản (P1, sau MVP)

- [~] **T8.1** PostgreSQL (**Neon free** — Render free DB hết hạn 30 ngày) + EF Core + migration `Initial`. *(Code xong; chờ người dùng tạo Neon + đặt `ConnectionStrings__Hanbit` trên Render.)*
- [ ] **T8.2** ASP.NET Core Identity (cookie) + Google login; persist Data Protection keys vào DB; anti-forgery cho API có cookie.
- [ ] **T8.3** Lưu tiến độ trên server + gộp dữ liệu localStorage khi đăng nhập.

## Backlog

- Tra từ Hàn–Việt · Khóa 중급 · Luyện dạng đề TOPIK (tự soạn) · Nhắc học · MP3 tạo trước (Azure AI Speech) · PWA offline · Admin duyệt nội dung · AI giải thích lỗi.

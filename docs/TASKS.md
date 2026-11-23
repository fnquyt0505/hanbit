# TASKS — Các bước nhỏ, làm từng bước một (.NET MVC · chế độ khách · Render)

> Quy ước: `[ ]` chưa làm · `[~]` đang làm · `[x]` xong. Mỗi task có **Tiêu chí hoàn thành (DoD)**.
> Làm theo thứ tự. Không nhảy giai đoạn khi giai đoạn trước chưa xong.

## Giai đoạn 0 — Khởi tạo solution

- [x] **T0.1** Tạo `Hanbit.sln` + 4 project (`Domain`, `Application`, `Infrastructure` là classlib; `Web` từ `dotnet new mvc`, **không auth**), reference đúng hướng phụ thuộc; xóa Bootstrap/jQuery mặc định của template. DoD: `dotnet run --project src/Hanbit.Web` mở được trang.
- [x] **T0.2** `Directory.Build.props` (net10.0, Nullable, TreatWarningsAsErrors), `.editorconfig`, `.gitignore` (thêm `data-raw/`, `wwwroot/css/app.css`), `global.json`. DoD: `dotnet build` 0 warning.
- [x] **T0.2a** CI GitHub Actions (`.github/workflows/ci.yml`: restore → build → `dotnet format --verify-no-changes` → test, chạy khi có PR vào `main` và push `main`) + auto-merge (`gh pr create` + `gh pr merge --auto --squash`). DoD: PR mở bằng `gh` tự merge khi CI xanh; CI đỏ thì không merge. 👤 Người dùng: cài/đăng nhập `gh`, bật *Allow auto-merge* + *Automatically delete head branches*, rule bảo vệ `main` bắt buộc check CI.
- [ ] **T0.3** 4 project test (xUnit + Shouldly; `Hanbit.Web.Tests` dùng `WebApplicationFactory`; Playwright), mỗi project 1 test mẫu. DoD: `dotnet test` xanh.
- [ ] **T0.4** Tailwind v4 standalone CLI (`Styles/input.css` → `wwwroot/css/app.css`, MSBuild target trước build) + vendor Alpine.js vào `wwwroot/lib`. DoD: class Tailwind và `x-data` hoạt động.
- [ ] **T0.5** Design tokens (màu light/dark, font) theo DESIGN.md; trang `/styleguide`. DoD: đủ màu & font, chuyển dark mode được.
- [ ] **T0.6** UI cơ bản: Tag Helper `<hb-button>`, `<ko-text>`; partial `_Card`, `_Dialog`, `_ProgressBar`; toast bằng Alpine. DoD: hiển thị trên `/styleguide`, test render Tag Helper.
- [ ] **T0.7** Deploy sớm: `Dockerfile` multi-stage + `/health` + `render.yaml`, deploy lên Render. DoD: URL `*.onrender.com` chạy, auto-deploy khi push `main`.

## Giai đoạn 1 — Dữ liệu nội dung (한국어기초사전)

- [ ] **T1.1** Records nội dung trong `Domain/Content` + `JsonSerializerOptions` (snake_case, polymorphic Exercise). DoD: test deserialize.
- [ ] **T1.2** `content/hangul.json` (40 chữ, tự biên soạn) + `content/LICENSE-DATA.md`. DoD: đủ 14 phụ âm, 5 phụ âm kép, 10 nguyên âm, 11 nguyên âm ghép.
- [ ] **T1.3** FluentValidation validators + tool `Hanbit.ContentValidator`. DoD: báo lỗi rõ file/trường.
- [ ] **T1.4** `IContentRepository` + `JsonContentRepository` (IMemoryCache, merge `vocab-reviews.json`). DoD: unit test.
- [ ] **T1.5** Tool `Hanbit.ContentImporter` (krdict → `content/vocab/krdict-beginner.json`, lọc 초급, nghĩa tiếng Việt, `SourceRef`). Xác minh định dạng & mã ngôn ngữ trước. DoD: test với file mẫu nhỏ; không ghi đè `vocab-reviews.json`; báo cáo số từ.
- [ ] **T1.6** `RomanizationService` (Revised Romanization). DoD: test ≥ 30 từ.
- [ ] **T1.7** 👤 **Người dùng** duyệt nghĩa ~150 từ đầu (AI hỗ trợ xuất CSV + tool nhập lại).
- [ ] **T1.8** Soạn khóa `beginner`: 5 bài đầu, chỉ dùng từ đã duyệt + ngữ pháp tự viết giải thích. DoD: pass validator.

## Giai đoạn 2 — Layout & điều hướng

- [ ] **T2.1** `_Layout.cshtml`: `_BottomNav` (mobile), `_Sidebar` (desktop), `_Footer` ghi công. 
- [ ] **T2.2** Trang chủ `/`: hero, 3 lợi ích, CTA "Bắt đầu miễn phí". DoD: responsive, Lighthouse ≥ 90.
- [ ] **T2.3** `/nguon-du-lieu`: ghi công 국립국어원 · 한국어기초사전, giấy phép, link gốc.
- [ ] **T2.4** `/dashboard` với skeleton (dữ liệu điền bằng JS ở giai đoạn 5).

## Giai đoạn 3 — Hangul & âm thanh

- [ ] **T3.1** `speech.js` (speak, tốc độ thường/chậm, kiểm tra giọng `ko-KR`) + Tag Helper `<audio-button>`. DoD: test thủ công Chrome/Safari/Android.
- [ ] **T3.2** `/hangul`: lưới chữ theo nhóm (partial `_HangulTile`).
- [ ] **T3.3** `HangulComposer` (Domain, công thức Unicode) + `GET /api/hangul/compose` + bảng ghép âm (Alpine). DoD: unit test + integration test API.
- [ ] **T3.4** `/hangul/{group}`: học từng chữ + quiz 10 câu (`hangul-quiz.js`, chấm qua API).

## Giai đoạn 4 — Bài học & bài tập

- [ ] **T4.1** `/learn/{course}`: View Component `CourseMap` (trạng thái khóa/mở điền bằng JS).
- [ ] **T4.2** `ExerciseGenerator` + `AnswerChecker` (Domain) — 5 loại, NFC, chuẩn hóa khoảng trắng. DoD: unit test.
- [ ] **T4.3** `GET /api/lessons/{c}/{l}` (DTO không đáp án) + `POST /api/exercises/check`. DoD: integration test; HTML/JSON không lộ đáp án.
- [ ] **T4.4** `/learn/{c}/{l}` + `lesson-runner.js`: từ vựng → ngữ pháp → bài tập (`multiple_choice`, `listen_choose`).
- [ ] **T4.5** UI `match_pairs`, `reorder`, `type_answer`.
- [ ] **T4.6** Màn tổng kết: điểm, từ sai (XP/lưu ở giai đoạn 5).

## Giai đoạn 5 — Tiến độ chế độ khách

- [ ] **T5.1** Model `LearnerProgress` + `IClock` + `XpRules` + `StreakCalculator` (Asia/Ho_Chi_Minh, qua nửa đêm). DoD: unit test.
- [ ] **T5.2** `SubmitLessonHandler` + `POST /api/lessons/{c}/{l}/complete` (trả LessonRecord, card mới, activity, XP). DoD: integration test.
- [ ] **T5.3** `progress-store.js`: load/save localStorage, xử lý JSON hỏng/khác version, merge kết quả từ API. DoD: E2E reload trang vẫn giữ tiến độ.
- [ ] **T5.4** Dashboard, CourseMap, StreakBadge đọc tiến độ thật.
- [ ] **T5.5** Xuất/nhập JSON ở `/profile` + `POST /api/progress/validate` (validate + migrate). DoD: E2E xuất → xóa → nhập → khôi phục đúng.

## Giai đoạn 6 — Ôn tập SRS

- [ ] **T6.1** `Sm2Scheduler` thuần + test 4 grade và biên.
- [ ] **T6.2** `POST /api/reviews/due`, `POST /api/reviews/grade`. DoD: integration test.
- [ ] **T6.3** `/review` + `review-session.js`: flashcard lật, 4 nút đánh giá, phím 1–4 / Space.
- [ ] **T6.4** Dashboard hiển thị số thẻ đến hạn.

## Giai đoạn 7 — Hoàn thiện & ra mắt MVP

- [ ] **T7.1** `/profile`: thống kê từ đã thuộc, lịch streak.
- [ ] **T7.2** E2E 3 luồng: Hangul bài 1, 1 bài học, 1 phiên ôn tập.
- [ ] **T7.3** Rate limiting cho `/api/*`, security headers, `Cache-Control` cho nội dung tĩnh.
- [ ] **T7.4** Accessibility (axe), SEO meta, OG image, sitemap.
- [ ] **T7.5** Kiểm tra điều khoản bản quyền krdict, trang ghi công, `LICENSE-DATA.md`.
- [ ] **T7.6** Render: domain riêng (nếu có), kiểm tra cold start trên 4G.

## Giai đoạn 8 — Tài khoản (P1, sau MVP)

- [ ] **T8.1** PostgreSQL (Render) + EF Core + migration `Initial`.
- [ ] **T8.2** ASP.NET Core Identity (cookie) + Google login; persist Data Protection keys vào DB; anti-forgery cho API có cookie.
- [ ] **T8.3** Lưu tiến độ trên server + gộp dữ liệu localStorage khi đăng nhập.

## Backlog

- Tra từ Hàn–Việt · Khóa 중급 · Luyện dạng đề TOPIK (tự soạn) · Nhắc học · MP3 tạo trước (Azure AI Speech) · PWA offline · Admin duyệt nội dung · AI giải thích lỗi.

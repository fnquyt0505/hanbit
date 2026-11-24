# PROGRESS — Nhật ký tiến độ

> AI/dev cập nhật file này **sau mỗi task**. Mục mới nhất ở trên cùng.

## 📍 Trạng thái hiện tại

|                                 |                                                                                                                              |
| ------------------------------- | ---------------------------------------------------------------------------------------------------------------------------- |
| Giai đoạn                     | 2 — Layout & điều hướng                                                                                                   |
| Task đang làm                 | **T2.2** — code xong, chờ người dùng đo Lighthouse (câu hỏi 11) |
| Production                      | https://hanbit.onrender.com (Render free, Singapore — ngủ khi không có truy cập, request đầu chậm) |
| Task tiếp theo                 | **T2.3** `/nguon-du-lieu`: ghi công, giấy phép, link gốc |
| Việc song song (người dùng) | Duyệt ngữ pháp + bài tập AI soạn ở T1.8 (câu hỏi 10) · (tùy chọn) sửa nghĩa krdict chưa tự nhiên (câu hỏi 9) |
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

### 2026-11-24 — T2.2a Trang lỗi thân thiện (thêm theo yêu cầu người dùng)
- Lý do: người dùng bấm CTA → `/hangul` gặp trang 404 trống của trình duyệt (mất menu, không có đường về) — nhiều link điều hướng còn 404 tới các giai đoạn sau.
- Đã làm: `Program.cs` thêm `UseStatusCodePagesWithReExecute("/loi/{0}")` qua `UseWhen` (bỏ qua `/api/*` để API sau này trả lỗi dạng JSON). `HomeController.StatusCodePage` (`[Route("/loi/{statusCode:int}")]`, giữ mã gốc; truy cập trực tiếp mã ngoài 400–599 → 404, `no-store`) + `StatusCodeViewModel` + `Views/Home/StatusCode.cshtml` (trong layout: "Không tìm thấy trang — Trang này chưa có hoặc đang được xây dựng…", nút "Về trang chủ"). `Error.cshtml` (500) đổi sang cùng kiểu.
- Kiểm tra: build ✅ 0 warning · format ✅ · test ✅ 307/307 (Web +7: 404 có layout/footer, `/api` không trả HTML, mã trực tiếp, trang có thật vẫn 200; E2E +2: 404 có CSS, CTA → 404 → về trang chủ).
- Tiếp theo: chờ điểm Lighthouse (câu hỏi 11) → đóng T2.2 → T2.3

### 2026-11-24 — T2.2 Trang chủ (chờ đo Lighthouse)
- Đã làm: `Views/Home/Index.cshtml` — hero (tiêu đề, mô tả, CTA "Bắt đầu miễn phí" → `/hangul` theo user flow PRD §6, nút phụ "Xem lộ trình" → `/learn/beginner`, thẻ minh họa 안녕하세요), 3 lợi ích (Hangul từ con số 0 · Giải thích bằng tiếng Việt · Ôn đúng lúc sắp quên), CTA cuối trang. `_Layout` thêm `<meta name="description">` (đặt qua `ViewData["Description"]`). Bỏ khối thử Alpine tạm thời (Alpine vẫn được E2E kiểm qua bộ chuyển theme ở `/styleguide`).
- Kiểm tra: build ✅ 0 warning · format ✅ · test ✅ Web 44/44, E2E 25/25 (CTA mobile full-width & cao ≥ 48px, không cuộn ngang ở 320/390/1280px) · ảnh chụp mobile/desktop OK.
- Vấn đề / ghi chú: **chưa đo Lighthouse** — AI không được phép tải/chạy `npx lighthouse`; người dùng đo bằng Chrome DevTools trên https://hanbit.onrender.com sau deploy (câu hỏi 11). Link CTA tới `/hangul` 404 cho tới T3.2.
- Tiếp theo: người dùng gửi điểm Lighthouse → đóng T2.2 → T2.3

### 2026-11-24 — T2.1 Layout: sidebar, bottom nav, footer ghi công
- Đã làm: `Models/Shared/Navigation.cs` (`MainNavigation.Items` dùng chung: Tổng quan `/dashboard`, Hangul `/hangul`, Bài học `/learn/beginner`, Ôn tập `/review`, Hồ sơ `/profile`; `NavItem.IsActive` so tiền tố theo segment, không phân biệt hoa thường). Partial `_Sidebar` (desktop ≥ 1024px, `lg`), `_BottomNav` (mobile + tablet, 5 cột, chừa `safe-area-inset-bottom`), `_NavIcon` (SVG nội tuyến tự vẽ + chữ 한 cho Hangul, `aria-hidden`), `_Footer` (ghi công *한국어기초사전 © 국립국어원, CC BY-SA 2.0 KR* + link krdict, giấy phép, `/nguon-du-lieu`). `_Layout`: link "Bỏ qua điều hướng", header thương hiệu trên mobile, `aria-current="page"` cho mục đang ở, nội dung chừa chỗ cho bottom nav. Toast nâng lên trên bottom nav tới `lg`. Fixture E2E nhận thêm `viewport`.
- Kiểm tra: build ✅ 0 warning · format ✅ · test ✅ 293/293 (Web +13, E2E +6: hiện/ẩn nav theo mobile/tablet/desktop, footer không bị bottom nav che, không cuộn ngang ở 320px, ghi công hiển thị) · xem ảnh chụp mobile/desktop OK.
- Vấn đề / ghi chú: các link điều hướng (trừ `/`) **chưa có trang** → 404 cho tới T2.3 (`/nguon-du-lieu`), T2.4 (`/dashboard`), T3.2, T4.1, T6.3, T7.1. Chưa có nút đổi sáng/tối trên layout (lưu lựa chọn thuộc T5.3).
- Tiếp theo: T2.2

### 2026-11-24 — T1.8 Khóa `beginner`: 5 bài đầu
- Đã làm: dàn bài được người dùng duyệt trước. `content/grammar/beginner.json` — 8 điểm ngữ pháp tự viết giải thích (-ㅂ니다/-습니다, N은/는, N입니다/입니까, N이에요/예요, N이/가 아니에요, N의, 무슨 N이에요?, N에 thời gian). `content/courses/beginner/course.json` + 5 bài `lessons/`: chào hỏi (14 từ), giới thiệu bản thân (15), nghề nghiệp (14), gia đình (18), thứ trong tuần (10) — **71 từ, toàn bộ đã duyệt**; mỗi bài 7 bài tập tự soạn đủ 5 loại (vị trí đáp án đúng đã xáo tay). Đầu mỗi file có comment "AI soạn — chờ người dùng duyệt". Test `BeginnerCourseDataTests` (dữ liệu thật qua `JsonContentRepository`: validator sạch, 5 bài đúng thứ tự, từ đều `reviewed`, ngữ pháp tồn tại, đủ 5 loại bài tập).
- Kiểm tra: validator ✅ (10 file) · build ✅ 0 warning · test ✅ 258/258 (không tính E2E)
- Vấn đề / ghi chú: ngữ pháp + câu ví dụ + bài tập do AI viết → câu hỏi 10. Nghĩa krdict gặp khi soạn cần người dùng cân nhắc → bổ sung câu hỏi 9.
- Tiếp theo: T2.1

### 2026-11-24 — T1.7 Duyệt nghĩa đợt 1 (150 từ)
- Đã làm: người dùng **quyết định duyệt cả 150 từ bộ chủ đề A với nghĩa krdict nguyên bản** (không sửa nghĩa). AI tạo `data-raw/review/dot-01-duyet-tat-ca.csv` (`duyet = x`, ghi chú `duyệt hàng loạt theo nghĩa krdict (người dùng, 2026-11-24)`) và nhập qua `review-import` → `content/vocab-reviews.json` 150 bản duyệt. File CSV gốc giữ nguyên.
- Kiểm tra: validator ✅ · sau gộp: **150/2.501 từ `reviewed`** (VD 가족 = gia đình, 가수 = ca sĩ, 간호사 = y tá)
- Vấn đề / ghi chú: một số nghĩa krdict hơi văn vẻ/dài so với cách dạy thông thường (VD 안녕히 = "một cách an lành", 경찰 = "(cơ quan) cảnh sát; (viên) cảnh sát") — xem câu hỏi 9.
- Tiếp theo: T1.8

### 2026-11-24 — T1.7 (phần AI) Công cụ duyệt nghĩa CSV
- Đã làm: `KrdictLmfReader` đọc thêm chủ đề tình huống (`subjectCategiory` — dữ liệu gốc viết sai tên, đọc cả 2 cách viết). `tools/Hanbit.ContentImporter/Review/`: `Csv` (RFC 4180 tự viết, ghi UTF-8 BOM, đọc tự nhận `,`/`;`/tab), `ReviewExporter` (chọn từ chưa duyệt theo chủ đề, giữ thứ tự chủ đề, bỏ trùng; CSV có phiên âm RR, từ loại tiếng Việt, Hán tự, nghĩa/định nghĩa krdict, cột trống `nghia_moi`/`dinh_nghia_moi`/`duyet`/`ghi_chu`, link krdict), `ReviewImporter` (gộp vào `vocab-reviews.json`: `x`/`có`/`1`… = duyệt, `loai`/`không`/`0`… = loại, trống = chưa quyết; id lạ/trùng/giá trị không hiểu → báo lỗi theo dòng và **không ghi file**; giữ bản duyệt cũ; file không phải UTF-8 → hướng dẫn lưu "CSV UTF-8"). CLI `review-export` / `review-import` (chạy validator sau khi ghi). Xuất đợt 1: **bộ A = 150 từ** → `data-raw/review/dot-01.csv` (không commit).
- File thay đổi: `tools/Hanbit.ContentImporter/{Krdict/KrdictEntry.cs, Krdict/KrdictLmfReader.cs, Review/*, Program.cs}`, `tests/Hanbit.Tools.Tests/Review/ReviewToolsTests.cs`, `AGENT.md`
- Kiểm tra: build ✅ (0 warning) · format ✅ · test ✅ (Tools 61)
- Tiếp theo: người dùng duyệt CSV → `review-import` → đóng T1.7

### 2026-11-24 — T1.5 hoàn tất: import từ file "사전 전체 내려받기"
- Đã làm: import qua Open API thất bại (E2 — krdict chặn IP khi gọi liên tục, kể cả tuần tự + nghỉ 300 ms qua WARP). Người dùng tải **11 file JSON LMF** (~1 GB, 53.671 mục, 2026-11-19) vào `data-raw/`. Khảo sát cấu trúc LMF bằng script (thuộc tính `feat`, `Lemma` object/mảng có `variant`, `WordForm` object/mảng/vắng, 2.884 mục trùng mã, `Equivalent` 11 ngôn ngữ). Thay phần import qua API bằng `KrdictLmfReader` (đọc phòng thủ) + `KrdictImporter` (lọc cấp + chỉ `단어`, gộp theo mã, báo cáo); **gỡ** parser XML / nguồn HTTP có cache (không dùng nữa); giữ lệnh `probe` tra lẻ API. Chất lượng dữ liệu: bỏ placeholder `(Không có từ tương ứng)` (13 từ; 2 từ chỉ có placeholder → dùng định nghĩa tiếng Việt), bỏ phần nghĩa lặp giữa các sense (272 → 0), giải mã HTML entity (`&apos;`). Fixture test = 7 mục thật trích từ file (phủ các trường hợp đặc biệt). Cập nhật ARCHITECTURE §8, LICENSE-DATA, AGENT.md (`import --input data-raw`).
- **Báo cáo:** 11 file · 56.555 mục quét · 2.545 mục 초급 · loại 44 cụm (구) · **đã nhập 2.501 từ**, 0 từ thiếu nghĩa tiếng Việt · Danh từ 1.401, Động từ 527, Tính từ 168, Phó từ 127, Định từ 74, Danh từ phụ thuộc 53, Số từ 53, Đại từ 38, Thán từ 31, Trợ động từ 9, Phụ tố 8, Không từ loại 8, Trợ tính từ 3, Trợ từ 1. File `content/vocab/krdict-beginner.json` (1,3 MB, mọi từ `reviewed = false`). Validator ✅.
- Kiểm tra: build ✅ (0 warning) · format ✅ · test ✅ (Tools 45 + toàn bộ) · đối chiếu ngẫu nhiên với API (학교 → trường học 學校, 가격 → giá cả, giá 價格) khớp
- Vấn đề / ghi chú: nghĩa đơn vị đếm có phiên âm Latin do krdict ghi (접시 → "jeopsi, đĩa", 층 → "cheung, lớp, tầng") — để người duyệt quyết ở T1.7.
- Tiếp theo: T1.7

### 2026-11-24 — T1.6 RomanizationService
- Đã làm: `Domain/Hangul/HangulSyllable` (tách/ghép âm tiết theo công thức Unicode — dùng lại cho `HangulComposer` T3.3) và `RomanizationService.Romanize(text)` theo **Revised Romanization**: ghi theo cách đọc với biến âm giữa các âm tiết trong một từ — nối âm (kể cả phụ âm cuối kép, ㅎ câm), mũi hóa, ㄹ (ㄴ+ㄹ→ll, ㄹ+ㄴ→ll, ㅁㅇ/ㄱㅂ+ㄹ→ㄴ), bật hơi với ㅎ (cả hai chiều), vòm hóa; không ghi âm căng; biến âm không vượt qua khoảng trắng; ký tự khác giữ nguyên. Giới hạn (ghi trong XML doc): chèn ㄴ, ㄴ+ㄹ đọc ㄴㄴ trong từ ghép (신문로), ngoại lệ danh từ giữ "h" (묵호), 밟-. Importer chưa điền `romanization` (để null) — tính khi hiển thị bằng service.
- File thay đổi: `src/Hanbit.Domain/Hangul/{HangulSyllable.cs, RomanizationService.cs}`, `tests/Hanbit.Domain.Tests/Hangul/RomanizationServiceTests.cs`, `tests/Hanbit.Infrastructure.Tests/Content/HangulDataTests.cs`
- Kiểm tra: build ✅ (0 warning) · format ✅ · test ✅ — **60 từ** theo RR (phần lớn là ví dụ chính thức: 백암 baegam, 종로 jongno, 왕십리 wangsimni, 독립 dongnip, 좋고 joko, 같이 gachi…), câu có dấu câu, 11.172 âm tiết đều ra chữ Latin thường, và **40 ví dụ trong `hangul.json` khớp 100%** với service (đối chiếu chéo dữ liệu viết tay ↔ thuật toán).
- Vấn đề / ghi chú: đã bắt được nguồn gốc warning "không tái hiện" ở T0.5/T0.6: `MSB3101 … file is being used by another process` — VS Code (C# Dev Kit) build song song khóa file `obj/`. Không phải lỗi code; build lại là sạch.

### 2026-11-24 — T1.5 Hanbit.ContentImporter (phần code — chưa chạy import đầy đủ)
- Đã làm: người dùng đăng ký API key krdict, lưu bằng `dotnet user-secrets` (`Krdict:ApiKey`, không vào repo/chat). Xác minh tài liệu Open API + **gọi thử API thật** (lệnh `probe`, che key): `trans_lang=7` = tiếng Việt, `level=level1` = 초급, `start` = số trang, `num` ≤ 100, XML thật (ARCHITECTURE §8); API **không** hỗ trợ từ khóa rỗng/ký tự đại diện → quét 11.172 âm tiết `가…힣` (`method=start`). Xác minh chính sách bản quyền krdict: **CC BY-SA 2.0 KR** (câu hỏi 1–2 đã đóng; `LICENSE-DATA.md`, ARCHITECTURE §9). `tools/Hanbit.ContentImporter`: `KrdictXmlParser` (đọc phòng thủ, lỗi `<error>` → `KrdictApiException`, 010 = hết lượt ngày), `KrdictVocabMapper` (id `kd-<target_code>`, `vi` ≤ 3 nghĩa, `definition_vi`, `hanja` từ `origin`, 15 từ loại, NFC + gộp khoảng trắng, bỏ mục không có nghĩa Việt; ví dụ để trống vì krdict chỉ có ví dụ tiếng Hàn), `KrdictHttpSearchSource` (cache XML thô `data-raw/krdict-cache/`, không cache trang lỗi, không ghi key; tuần tự + nghỉ 300 ms + thử lại 5→120 s), `KrdictImporter` (gộp theo target_code, lọc đúng cấp, sắp xếp ổn định, **từ chối ghi `vocab-reviews.json`**, ghi file tạm rồi đổi tên), CLI `import`/`probe` + chạy validator sau khi ghi + báo cáo số từ. `ContentJson` bỏ trường null khi ghi. Project `tests/Hanbit.Tools.Tests` (fixture XML thật nhỏ, CC BY-SA ghi công trong `Fixtures/README.md`). Package: `Microsoft.Extensions.Configuration.UserSecrets` + `EnvironmentVariables` 10.0.12 (đọc key).
- Chạy thử 3 âm tiết (가, 학, 나): 66 từ 초급, 0 bỏ qua, file hợp lệ. **Chạy đầy đủ thất bại** (E2) — xem "Lỗi đang mở".
- Kiểm tra: build ✅ (0 warning) · format ✅ · test ✅ (Tools 47 + toàn bộ)
- Tiếp theo: chạy lại import khi krdict truy cập được → commit `content/vocab/krdict-beginner.json` + báo cáo số từ → đóng T1.5. Trong lúc chờ: T1.6.

### 2026-11-24 — T1.4 IContentRepository + JsonContentRepository
- Đã làm: `Application/Abstractions/IContentRepository` (Hangul, khóa học, bài học, từ vựng/ngữ pháp theo id — async + `CancellationToken`). `Infrastructure/Content/JsonContentRepository`: đọc bằng `ContentJson.Options`, cache `IMemoryCache` (hết hạn theo `Content:CacheDuration`, null = không hết hạn), **slug kiểm tra regex trước khi ghép đường dẫn** (chặn path traversal), từ vựng = mọi `vocab/*.json` gộp `vocab-reviews.json`. Chốt định dạng `vocab-reviews.json` + `VocabReview`/`VocabReviewMerger` (Domain, hàm thuần) — validator T1.3 giờ kiểm tra file review (id tồn tại, không trùng, trường không rỗng) và tính "đã duyệt" trên dữ liệu sau gộp. `ContentPaths` dùng chung. `AddInfrastructure()` + Web cấu hình `Content` (dev `../../content`, cache 2 giây). `ContentHealthCheck`: `/health` chỉ Healthy khi đọc được `hangul.json`. Docker copy `content/` (kèm `LICENSE-DATA.md`) vào `/app/content`; CI smoke test kiểm tra file trong container. Package mới: `Microsoft.Extensions.Caching.Memory` 10.0.12 (cho `IMemoryCache` — ARCHITECTURE đã định).
- File thay đổi: `src/Hanbit.Domain/Content/VocabReview.cs`, `src/Hanbit.Application/Abstractions/IContentRepository.cs`, `src/Hanbit.Infrastructure/{Content/*, DependencyInjection.cs, Hanbit.Infrastructure.csproj}`, `src/Hanbit.Web/{Program.cs, appsettings*.json, HealthChecks/ContentHealthCheck.cs}`, `Dockerfile`, `.dockerignore`, `.github/workflows/ci.yml`, `docs/ARCHITECTURE.md`, tests
- Kiểm tra: build ✅ (0 warning) · format ✅ · test ✅ (140/140: Domain merger 6, repository 19 gồm cache/hết hạn/path traversal/gộp review, validator review 4, Web: DI đọc `content/` thật = 40 chữ, `/health` 503 khi sai đường dẫn content)
- Tiếp theo: T1.5 (cần người dùng: file dữ liệu tải từ krdict hoặc API key)

### 2026-11-24 — T1.3 Validators + Hanbit.ContentValidator
- Đã làm: package **FluentValidation 12.2.0** (Infrastructure — có trong stack ARCHITECTURE §1). `Infrastructure/Content/Validation/`: validator cho `HangulLetter`, `VocabItem` (+ `SourceRef` URL http(s)), `GrammarPoint`, `Course`, `Lesson` + 5 loại bài tập qua `SetInheritanceValidator` (VD `answer_index` trong khoảng, lựa chọn ≥ 2 & không trùng, `audio_ko` có Hangul, cặp nối không trùng vế), quy tắc chung slug/Hangul/không rỗng, thông báo tiếng Việt. `ContentValidationService`: quét `content/` theo cấu trúc ARCHITECTURE §3, đọc bằng `ContentJson.Options` (lỗi JSON kèm **số dòng**), chạy validator, **kiểm tra chéo** (id trùng giữa file; bài học chỉ dùng từ vựng `reviewed` + ngữ pháp tồn tại; mục lục khóa học ↔ file bài học: thiếu file / file thừa / title, order lệch; slug trùng tên file/thư mục; file JSON lạ; thiếu `hangul.json`), tránh báo lỗi dây chuyền khi file từ vựng/ngữ pháp hỏng. Lỗi dạng `content/<file>: <json path snake_case>: <lý do>`. Tool `tools/Hanbit.ContentValidator` (exit 1 khi có lỗi). CI `build-test` chạy validator trên `content/`. `vocab-reviews.json` được chấp nhận nhưng chưa kiểm tra (định dạng chốt ở T1.4). Áp dụng quyết định câu 7–8: `LICENSE-DATA.md` §2 → giữ mọi quyền.
- File thay đổi: `src/Hanbit.Infrastructure/{Hanbit.Infrastructure.csproj, Content/Validation/*}`, `tools/Hanbit.ContentValidator/*`, `Hanbit.sln`, `.github/workflows/ci.yml`, `content/LICENSE-DATA.md`, `tests/Hanbit.Infrastructure.Tests/Content/Validation/*`
- Kiểm tra: build ✅ (0 warning) · format ✅ · test ✅ (109/109; Infrastructure 49: 18 unit test validator + 15 test service với thư mục tạm, gồm `content/` thật hợp lệ) · chạy tool trên dữ liệu hỏng mẫu → 4 lỗi rõ file/trường, exit 1
- Tiếp theo: T1.4

### 2026-11-24 — T1.2 hangul.json + LICENSE-DATA.md
- Đã làm: `content/hangul.json` — mảng 40 `HangulLetter` (14 phụ âm, 5 phụ âm kép, 10 nguyên âm, 11 nguyên âm ghép): `id` (`c-giyeok`, `dc-ssanggiyeok`, `v-a`, `cv-ae`…), tên chữ tiếng Hàn (chỉ phụ âm), Revised Romanization, gợi ý đọc cho người Việt, 1 từ ví dụ thông dụng (Hangul + RR + nghĩa Việt). `content/LICENSE-DATA.md`: ghi công 한국어기초사전 © 국립국어원 (CC BY-SA, phiên bản `TODO(verify)`), phạm vi áp dụng, dữ liệu tự biên soạn (giấy phép `TODO(owner)`).
- File thay đổi: `content/hangul.json`, `content/LICENSE-DATA.md`, `tests/Hanbit.Infrastructure.Tests/{Content/HangulDataTests.cs, RepoRoot.cs}`
- Kiểm tra: build ✅ (0 warning) · format ✅ · test ✅ (Infrastructure 14/14: đủ 14/5/10/11, tổng 40, id/ký tự không trùng, mỗi ký tự là 1 chữ cái tương thích, phụ âm có tên/nguyên âm không, có gợi ý + ví dụ, **mỗi từ ví dụ thật sự chứa chữ cái đó** — tách âm tiết theo công thức Unicode)
- Vấn đề / ghi chú: dữ liệu tự biên soạn bằng tiếng Hàn → cần người dùng duyệt (câu hỏi 7). File JSON là mảng ở gốc (repository T1.4 đọc `List<HangulLetter>`).
- Tiếp theo: T1.3

### 2026-11-24 — T1.1 Records nội dung + ContentJson
- Đã làm: `src/Hanbit.Domain/Content/`: `Hangul.cs` (`HangulType`, `HangulLetter`, `HangulExample`), `Vocabulary.cs` (`VocabGrade`, `PartOfSpeech` — 15 từ loại theo 품사 krdict, `TODO(verify)` ở T1.5; `SourceRef`, `Example`, `VocabItem`, `GrammarPoint`), `Exercises.cs` (`Exercise` đa hình qua `type`: `multiple_choice`, `listen_choose`, `match_pairs` + `MatchPair`, `reorder`, `type_answer`), `Courses.cs` (`Lesson`, `Course`, `LessonSummary`), `ContentJson.Options` (snake_case tên trường + enum, `RespectNullableAnnotations`, `RespectRequiredConstructorParameters`, `UnmappedMemberHandling.Disallow`, `AllowOutOfOrderMetadataProperties`, comment + dấu phẩy cuối, ghi không escape chữ Hàn/Việt, read-only). Trường tùy chọn đưa xuống cuối record với `= null` (khác thứ tự sketch ARCHITECTURE §5 — tên/kiểu giữ nguyên) để JSON không phải ghi `"audio": null`. ARCHITECTURE §5 thêm bảng cấu trúc JSON 5 loại bài tập.
- File thay đổi: `src/Hanbit.Domain/Content/*.cs`, `tests/Hanbit.Domain.Tests/Content/ContentJsonTests.cs`, `docs/ARCHITECTURE.md`
- Kiểm tra: build ✅ (0 warning) · format ✅ · test ✅ (Domain 17/17: deserialize mọi record + 5 loại bài tập, `type` không đứng đầu, round-trip, lỗi khi thiếu trường / null sai / trường lạ / `type` lạ / enum dạng số)
- Vấn đề / ghi chú: Domain dùng `System.Text.Json` (BCL, không phải package) cho `[JsonPolymorphic]` như ARCHITECTURE đã định; test kiến trúc vẫn xanh.
- Tiếp theo: T1.2

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
| E2 | 2026-11-24: chạy import với 4 kết nối song song → sau 35 trang, krdict.korean.go.kr **timeout mọi kết nối** từ máy dev (cả trang chủ) — nghi IP bị chặn tạm vì gọi dồn dập. Đã sửa importer: mặc định 1 kết nối, nghỉ 300 ms/request, thử lại 5 lần chờ 5→120 giây. 35 trang đã cache trong `data-raw/krdict-cache/`. Chờ truy cập lại rồi chạy lại (tiếp tục từ cache). Lần 2 (qua Cloudflare WARP, tuần tự + nghỉ 300 ms): bị chặn lại sau ~54 trang → API không khả thi cho import hàng loạt. **Giải quyết:** chuyển sang file "사전 전체 내려받기" (LMF JSON) — import trên máy, không gọi krdict. | T1.5 | Đóng (2026-11-24) |

## ❓ Câu hỏi / việc cần xác minh

1. ~~Định dạng & mã ngôn ngữ tiếng Việt của krdict API~~ → đã xác minh (T1.5): `trans_lang=7`, chi tiết ARCHITECTURE §8.
2. ~~Điều khoản bản quyền krdict~~ → đã xác minh (2026-11-24): CC BY-SA 2.0 KR, cho phép thương mại, ghi tác giả + cùng giấy phép; đa phương tiện giấy phép riêng (`content/LICENSE-DATA.md`).
3. Ai duyệt nghĩa tiếng Việt (T1.7)? Mặc định: chủ dự án.
4. ~~Tương phản màu~~ → đã chốt: thêm token `-ink` (xem Quyết định).
5. ~~Lưu chế độ sáng/tối ở đâu~~ → đã chốt: qua `progress-store.js` ở T5.3.
6. ~~Font qua CDN hay tự host~~ → đã chốt: trước mắt giữ CDN.
7. ~~Duyệt `content/hangul.json`~~ → đã duyệt (2026-11-24); tinh chỉnh `vi_hint` khi làm trang `/hangul` (T3.2) nếu cần.
8. ~~Giấy phép phần tự biên soạn~~ → đã chốt: giữ mọi quyền.
9. **Nghĩa duyệt hàng loạt (T1.7):** 150 từ đợt 1 dùng nghĩa krdict nguyên bản. Khi soạn bài (T1.8) nếu gặp nghĩa chưa tự nhiên, AI sẽ liệt kê để người dùng quyết sửa qua `nghia_moi` (xuất lại bằng `review-export` không lấy từ đã duyệt → cần sửa trực tiếp `vocab-reviews.json` hoặc một CSV mới chỉ chứa các id cần sửa).
   Gặp khi soạn T1.8 (đề xuất, chờ người dùng quyết): 저 (kd-44506) = "em, con, cháu" → thiếu "tôi (khiêm nhường)" · 안녕히 (kd-28415) = "một cách an lành" → thêm "(안녕히 가세요/계세요: tạm biệt)" · 다니다 (kd-29702) = "lui tới; ghé qua; **l**đi làm, đi học" — lỗi gõ từ krdict · 나라 (kd-29699) = "…; quốc gia." — trùng + dấu chấm · 반갑다 (kd-70861) = "hân hoan, vui sướng, hân hạnh" → "vui mừng (được gặp)" · 경찰 (kd-25949) = "(cơ quan) cảnh sát; (viên) cảnh sát" → "cảnh sát".
10. **Duyệt nội dung AI soạn ở T1.8:** `content/grammar/beginner.json` (giải thích + câu ví dụ) và bài tập trong `content/courses/beginner/lessons/*.json`. Duyệt xong → xóa comment "chờ người dùng duyệt" ở đầu file.
11. **Lighthouse trang chủ (DoD T2.2):** mở https://hanbit.onrender.com (đợi hết cold start) → DevTools → Lighthouse → Mobile → Analyze; gửi 4 điểm Performance / Accessibility / Best Practices / SEO (mục nào < 90 thì gửi kèm danh sách gợi ý).

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
| 2026-11-24      | Đợt duyệt 1 = bộ chủ đề A (150 từ): 인사하기, 감사하기, 사과하기, 소개하기(자기소개), 개인 정보 교환하기, 소개하기(가족 소개), 요일 표현하기 | Chọn theo chủ đề tình huống của krdict (dữ liệu thật); khớp 5 bài đầu T1.8 |
| 2026-11-24      | `content/hangul.json` đã duyệt (AI biên soạn, người dùng duyệt) | Gợi ý đọc chỉ là phụ — người học nghe audio thật |
| 2026-11-24      | Nội dung tự biên soạn (`hangul.json`, `grammar/`, `courses/`): **giữ mọi quyền** | Có thể mở giấy phép sau; CC thì không rút lại được |

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

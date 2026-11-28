# ARCHITECTURE — Hanbit (.NET MVC)

## 1. Tech stack

| Lớp | Công nghệ | Ghi chú |
|---|---|---|
| Runtime | **.NET 10 (LTS)** · C# 14 | |
| Web | **ASP.NET Core MVC** (Controllers + Razor Views) | Server render HTML; không SignalR, không SPA |
| API nội bộ | `[ApiController]` trong cùng project (`/api/...`) | **Không trạng thái** — chỉ tính toán, không lưu gì |
| Tương tác phía trình duyệt | **Alpine.js** (vendored trong `wwwroot/lib`) + **ES modules JS thuần** | Không cần Node/npm |
| Styling | **Tailwind CSS v4** (standalone CLI) + Razor Partial/Tag Helper/View Component | |
| Lưu tiến độ (MVP) | **localStorage** trên trình duyệt | Chế độ khách, không DB |
| Validation | **FluentValidation** | Content + request API |
| JSON | `System.Text.Json` | |
| Audio | Web Speech API (`ko-KR`) → fallback MP3 | |
| Bảo vệ API | Rate limiting middleware có sẵn của ASP.NET Core | |
| Logging | `ILogger` + Serilog (console) | |
| Test | **xUnit** + **Shouldly**, **WebApplicationFactory** (integration), **Playwright for .NET** (e2e) | |
| Deploy | **Docker → Render** (Web Service) | |
| *(P1)* Database / Auth | PostgreSQL + EF Core 10 · ASP.NET Core Identity + Google | Giai đoạn 8 |

## 2. Nguyên tắc cốt lõi: "Server tính toán – Trình duyệt lưu trữ"

Ở chế độ khách, tiến độ nằm trong localStorage nên server không đọc được. Vì vậy:

- **Server (C#)** giữ toàn bộ logic nghiệp vụ: chấm bài, SRS, XP, streak, sinh bài tập — viết trong `Hanbit.Domain`, test bằng xUnit.
- **Trình duyệt (JS)** chỉ: hiển thị tương tác, gọi API, **lưu kết quả** server trả về vào localStorage.
- API **stateless**: nhận dữ liệu cần thiết trong request → trả kết quả. Không session, không cookie.
- JS **không** tự tính SRS/streak/điểm (tránh viết logic 2 lần).

## 3. Cấu trúc solution

```
hanbit/
├── AGENT.md
├── Dockerfile
├── render.yaml
├── docs/
├── content/                               # Dữ liệu học (JSON) — đóng gói vào image
│   ├── LICENSE-DATA.md
│   ├── hangul.json
│   ├── vocab/krdict-beginner.json         # sinh bởi ContentImporter — KHÔNG sửa tay
│   ├── vocab-reviews.json                 # duyệt / sửa nghĩa tiếng Việt
│   ├── grammar/beginner.json
│   └── courses/beginner/course.json, lessons/*.json
├── data-raw/                              # file tải từ krdict (gitignored)
├── Hanbit.sln
├── Directory.Build.props
├── src/
│   ├── Hanbit.Domain/                     # C# thuần
│   │   ├── Content/  Learning/  Srs/  Exercises/  Hangul/  Progress/
│   ├── Hanbit.Application/
│   │   ├── Abstractions/                  # IContentRepository, IClock
│   │   ├── Lessons/                       # GetLessonQuery, SubmitLessonHandler
│   │   ├── Reviews/                       # GradeCardHandler, GetDueCardsHandler
│   │   ├── Progress/                      # ProgressSnapshotValidator, MigrateProgress
│   │   └── DependencyInjection.cs
│   ├── Hanbit.Infrastructure/
│   │   ├── Content/                       # JsonContentRepository, validators
│   │   └── DependencyInjection.cs
│   └── Hanbit.Web/                        # ASP.NET Core MVC
│       ├── Controllers/
│       │   ├── HomeController.cs          # /            , /dashboard
│       │   ├── HangulController.cs        # /hangul      , /hangul/{group}
│       │   ├── LearnController.cs         # /learn/{course} , /learn/{course}/{lesson}
│       │   ├── ReviewController.cs        # /review
│       │   ├── ProfileController.cs       # /profile
│       │   ├── CreditsController.cs       # /nguon-du-lieu
│       │   └── Api/
│       │       ├── ExercisesApiController.cs   # POST /api/exercises/check
│       │       ├── LessonsApiController.cs     # GET  /api/lessons/{c}/{l} · POST /api/lessons/{c}/{l}/complete
│       │       ├── ReviewsApiController.cs     # POST /api/reviews/grade · POST /api/reviews/due
│       │       ├── ProgressApiController.cs    # POST /api/progress/summary · /validate (nhập file)
│       │       └── HangulApiController.cs      # GET  /api/hangul/compose?c=ㄱ&v=ㅏ
│       ├── Models/                        # ViewModels + DTO request/response API
│       ├── Views/
│       │   ├── Shared/_Layout.cshtml, _BottomNav.cshtml, _Sidebar.cshtml, _Footer.cshtml (ghi công)
│       │   ├── Shared/Components/         # View Components (StreakBadge, CourseMap…)
│       │   ├── Home/  Hangul/  Learn/  Review/  Profile/  Credits/
│       │   └── _ViewImports.cshtml, _ViewStart.cshtml
│       ├── TagHelpers/                    # <hb-button>, <ko-text>, <audio-button>
│       ├── wwwroot/
│       │   ├── css/app.css                # output Tailwind (gitignored)
│       │   ├── lib/alpinejs/alpine.min.js # vendored, ghi phiên bản trong README
│       │   └── js/
│       │       ├── api.js                 # fetch wrapper
│       │       ├── progress-store.js      # localStorage "hanbit.progress.v1" + export/import
│       │       ├── speech.js              # Web Speech API
│       │       ├── lesson-runner.js       # Alpine component cho màn học
│       │       ├── review-session.js      # Alpine component flashcard
│       │       └── hangul-quiz.js
│       ├── Styles/input.css
│       └── Program.cs
├── tools/
│   ├── Hanbit.ContentImporter/
│   └── Hanbit.ContentValidator/
└── tests/
    ├── Hanbit.Domain.Tests/
    ├── Hanbit.Infrastructure.Tests/
    ├── Hanbit.Tools.Tests/
    ├── Hanbit.Web.Tests/                  # WebApplicationFactory: controller + API
    └── Hanbit.E2E/                        # Playwright: luồng học có JS + localStorage
```

**Hướng phụ thuộc:** `Web → Application → Domain` · `Infrastructure → Application → Domain`. Domain không tham chiếu project nào.

## 4. Luồng dữ liệu

```
 [Chuẩn bị dữ liệu]
 krdict ──► ContentImporter ──► content/vocab/*.json ◄── vocab-reviews.json (người duyệt)

 [Trang] Trình duyệt ──GET /learn/beginner/01──► LearnController ──► IContentRepository ──► Razor View (HTML)

 [Làm bài]
   Alpine (lesson-runner.js) ──POST /api/exercises/check {exerciseRef, answer}──► AnswerChecker ──► {correct, explanation}
   Kết thúc bài ──POST /api/lessons/{c}/{l}/complete {answers, cards, activity, today}──►
        SubmitLessonHandler (điểm, XP, streak, tạo ReviewCard mới) ──► {lessonRecord, newCards, activity, xp}
   progress-store.js ──► merge vào localStorage

 [Ôn tập]
   progress-store.js lấy cards ──POST /api/reviews/due {cards}──► danh sách đến hạn
   Mỗi thẻ ──POST /api/reviews/grade {card, grade}──► Sm2Scheduler ──► card mới ──► localStorage
```

- Trang HTML render đầy đủ ở server (SEO, nhanh). Phần cần tiến độ (dashboard, streak, CourseMap trạng thái mở khóa) hiển thị **skeleton** rồi JS điền sau khi đọc localStorage.
- Đáp án **không** nhúng trong HTML; kiểm tra qua API.
- "Hôm nay" do **server** xác định qua `IClock` (Asia/Ho_Chi_Minh), không tin ngày của trình duyệt.

## 5. Model nội dung (Domain/Content)

```csharp
public enum HangulType { Consonant, Vowel, DoubleConsonant, CompoundVowel }
public sealed record HangulLetter(string Id, string Char, HangulType Type, string Romanization,
    string? NameKo, string ViHint, IReadOnlyList<HangulExample> Examples, string? Audio);

public enum VocabGrade { Beginner, Intermediate, Advanced, None }      // 초급 / 중급 / 고급
public sealed record SourceRef(string Name, string Id, string Url, string License);
public sealed record VocabItem(string Id, string Ko, string? Pronunciation, string? Romanization,
    string Vi, string? DefinitionVi, PartOfSpeech Pos, VocabGrade Grade, string? Hanja,
    IReadOnlyList<Example> Examples, bool Reviewed, SourceRef Source, string? Audio);

public sealed record GrammarPoint(string Id, string Pattern, string MeaningVi,
    string ExplanationVi, IReadOnlyList<Example> Examples, VocabGrade Grade);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]   // multiple_choice | listen_choose | match_pairs | reorder | type_answer
public abstract record Exercise(string Id);

public sealed record Lesson(string Slug, string Title, int Order, IReadOnlyList<string> VocabIds,
    IReadOnlyList<string> GrammarIds, IReadOnlyList<Exercise> Exercises);
public sealed record Course(string Slug, string Title, VocabGrade Level, string Description,
    IReadOnlyList<LessonSummary> Lessons);
```

- Bài tập = `ExerciseGenerator` (từ VocabIds, seed = hash(course/lesson) → ổn định) + bài tự soạn.
- Khi gửi bài tập xuống trình duyệt, dùng DTO **không có đáp án**.
- JSON dùng `snake_case`.
- Định nghĩa đầy đủ: `src/Hanbit.Domain/Content/*.cs` (T1.1). Trường tùy chọn (`= null`) đặt cuối record và được bỏ trống trong JSON.
- Đọc/ghi chỉ qua `ContentJson.Options`: enum là chuỗi snake_case; **chặt** — thiếu trường bắt buộc, `null` ở trường không-null, trường lạ, `type` lạ → lỗi; cho phép comment + dấu phẩy cuối.
- Bài tập tự soạn (trường `type` chọn loại; mọi loại có `id`, tùy chọn `explanation_vi`):

| `type` | Trường | Đáp án nằm ở |
|---|---|---|
| `multiple_choice` | `prompt`, `choices[]`, `answer_index` | `answer_index` |
| `listen_choose` | `audio_ko`, `choices[]`, `answer_index` | `answer_index` |
| `match_pairs` | `prompt`, `pairs[{left, right}]` | cặp `left`–`right` |
| `reorder` | `prompt`, `tokens[]` (theo thứ tự đúng) | thứ tự `tokens` |
| `type_answer` | `prompt`, `accepted_answers[]` | `accepted_answers` |

## 6. Model tiến độ (lưu localStorage, server chỉ xử lý)

```csharp
public sealed record LearnerProgress(int SchemaVersion, DateTimeOffset CreatedAt,
    IReadOnlyDictionary<string, LessonRecord> Lessons,   // key "course/lesson"
    IReadOnlyDictionary<string, ReviewCard> Cards,       // key itemId
    IReadOnlyList<DailyActivity> Activity,               // 365 ngày gần nhất
    int TotalXp);
public sealed record LessonRecord(LessonStatus Status, int BestScore, DateTimeOffset? CompletedAt);
public sealed record ReviewCard(string ItemId, ItemType ItemType, double Ease, int IntervalDays,
    int Repetitions, DateTimeOffset DueAt, DateTimeOffset? LastReviewedAt);
public sealed record DailyActivity(DateOnly Date, int Xp, int Reviews);
```

- Key localStorage: `hanbit.progress.v1`. File sao lưu `hanbit-backup-YYYYMMDD.json`.
- Nhập file: gửi `POST /api/progress/validate` → server validate + migrate schema → trả bản sạch → JS ghi.
- Vì dữ liệu do client gửi lên có thể bị sửa: server **validate giới hạn** (ease 1.3–5, interval ≤ 3650, XP ≥ 0…) nhưng không cần chống gian lận ở MVP (không có bảng xếp hạng).

## 7. SRS (SM-2 rút gọn)

- Grade: `Again=0`, `Hard=1`, `Good=2`, `Easy=3`. Again → Repetitions = 0, ôn lại trong phiên.
- interval 1 → 3 ngày → `interval × ease`; ease ±, tối thiểu 1.3.
- `ReviewCard Schedule(ReviewCard card, ReviewGrade grade, DateTimeOffset now)` — hàm thuần, có test.

## 8. Content Importer (krdict → JSON)

- **Đầu vào (T1.5): file "사전 전체 내려받기"** (chân trang krdict) — 11 file JSON định dạng **LMF** (~1 GB, 53.671 mục, tải 2026-11-19) đặt trong `data-raw/` (gitignored). Import chạy trên máy (~5 giây), lặp lại được.
- Cấu trúc LMF (đã xác minh): `LexicalResource.Lexicon.LexicalEntry[]`; mục: `val` = mã mục từ (= `target_code` của API), `feat[{att,val}]` gồm `homonym_number`, `lexicalUnit` (단어/구/관용구/속담/문법‧표현), `partOfSpeech`, `origin` (Hán tự), `vocabularyLevel` (초급/중급/고급/없음); `Lemma` (object hoặc mảng `writtenForm` + `variant`); `WordForm` (object/mảng/vắng; `pronunciation`); `Sense[]` (`val` = thứ tự, `definition`, `Equivalent[]` theo `language` — tiếng Việt = `베트남어`: `lemma` + `definition`). Có **mục trùng mã** (mục con quán ngữ) → gộp theo mã, giữ mục đúng cấp.
- Lọc: đúng cấp + chỉ `lexicalUnit = 단어` (cụm/quán ngữ/tục ngữ/mẫu ngữ pháp soạn riêng). 초급: 2.545 mục → **2.501 từ** (44 cụm 구 bị loại), 100% có nghĩa tiếng Việt.
- Map → `VocabItem`: `id = kd-<mã>`, `vi` = nghĩa tối đa 3 sense nối "; " — bỏ placeholder `(Không có từ tương ứng)`, bỏ phần nghĩa (tách theo `,`/`;`) đã có ở sense trước; không còn nghĩa → dùng định nghĩa tiếng Việt; `definition_vi` = định nghĩa sense đầu, `hanja` = `origin` nếu là Hán tự, `pronunciation` (nhiều cách đọc nối "/"), `pos`/`grade` từ chữ Hàn, `source` = 한국어기초사전 + link `SearchView?ParaWordNo=<mã>` + `CC BY-SA 2.0 KR`, `reviewed = false`; giải mã HTML entity + gộp khoảng trắng + NFC. **Ví dụ để trống** (ví dụ krdict chỉ có tiếng Hàn, `Example` cần cả nghĩa Việt). Không lấy tệp đa phương tiện (giấy phép riêng từng tệp).
- Open API (`/api/search`, key user-secrets `Krdict:ApiKey`; tiếng Việt `trans_lang=7`, cấp `level=level1…3`) chỉ dùng tra cứu lẻ bằng lệnh `probe`. **Không dùng để import hàng loạt**: không hỗ trợ từ khóa rỗng và server **chặn IP khi gọi ~2–3 request/giây** (PROGRESS E2).
- Không ghi đè `vocab-reviews.json` (importer từ chối ghi vào file này).
- `vocab-reviews.json` (T1.4) — mảng bản duyệt, **chỉ người duyệt sửa**:
  `[ { "id": "kd-123", "reviewed": true, "vi": "…", "definition_vi": "…", "note": "…" } ]` (`vi`, `definition_vi`, `note` tùy chọn).
  `VocabReviewMerger` (Domain) gộp vào từ krdict: `reviewed` lấy theo bản duyệt, `vi`/`definition_vi` thay nếu có. Repository và validator dùng chung hàm này → "chỉ dùng từ đã duyệt" luôn tính trên dữ liệu sau gộp.

## 9. Giấy phép & ghi công

- 한국어기초사전 theo **CC BY-SA 2.0 KR** (đã xác minh chính sách 저작권 정책 của krdict 2026-11-24: cho phép cả thương mại; bắt buộc ghi tác giả; tác phẩm dẫn xuất cùng giấy phép; đa phương tiện có giấy phép riêng) → `content/LICENSE-DATA.md`, trang `/nguon-du-lieu`, `_Footer.cshtml`: *"Dữ liệu từ vựng: 한국어기초사전 © 국립국어원, CC BY-SA 2.0 KR"*.
- Dữ liệu dẫn xuất cũng CC BY-SA; mã nguồn không bị ảnh hưởng.

## 10. Deploy — Render

- Web Service, runtime Docker; `Dockerfile` multi-stage (`sdk:10.0` → `aspnet:10.0`): chạy Tailwind CLI → `dotnet publish` → copy `content/`.
- `ASPNETCORE_HTTP_PORTS=10000`; health check `/health`; `render.yaml`; auto-deploy nhánh `main`.
- MVC + HTTP thuần → không phụ thuộc WebSocket. Free tier vẫn "ngủ" khi không có truy cập (request đầu chậm).
- Response caching/`Cache-Control` cho trang nội dung tĩnh và file trong `wwwroot` (`asp-append-version`).

## 11. Cấu hình & secrets

`appsettings.json`: `{ "Content": { "RootPath": "content" } }` — đường dẫn tương đối tính từ `ContentRootPath` (Docker: `/app/content`).
`appsettings.Development.json`: `RootPath: "../../content"` (repo root), `CacheDuration: "00:00:02"` (sửa content thấy ngay). Production: cache không hết hạn.

**Database (giai đoạn 8 — T8.1):** PostgreSQL trên **Neon free** (vùng Singapore; Render free DB hết hạn sau 30 ngày và bị xóa). Chuỗi kết nối `ConnectionStrings:Hanbit` — Render: biến môi trường `ConnectionStrings__Hanbit`; máy dev: user-secrets của `Hanbit.Web`. **Tùy chọn**: không có → không đăng ký DbContext, app chạy chế độ khách. Có → `HanbitDbContext` (Infrastructure/Persistence, Npgsql, retry khi lỗi tạm thời), `DatabaseMigrator` áp migration khi khởi động (lỗi chỉ ghi log), `/health` thêm mục `database` (lỗi → Degraded, tối đa 3 s — Render không khởi động lại app). Bảng `learner_progress(user_id PK, schema_version, data jsonb, created_at, updated_at)` lưu nguyên `LearnerProgress` (T8.3). Migration trong `Infrastructure/Persistence/Migrations` (bỏ qua analyzer/format).

**Tài khoản (giai đoạn 8 — T8.2):** chỉ đăng nhập **Google** (ASP.NET Core Identity, không mật khẩu). Bật khi có **cả** DB và `Authentication:Google:ClientId/ClientSecret` (Render: `Authentication__Google__ClientId/ClientSecret`, `sync: false`); thiếu → chế độ khách, `/tai-khoan/*` 404, không hiện nút đăng nhập. Cookie `hanbit.auth` (HttpOnly, SameSite=Lax, Secure khi HTTPS, 30 ngày trượt); API chưa đăng nhập → 401 (không chuyển hướng). Khóa Data Protection lưu bảng `data_protection_keys` (giữ phiên khi Render khởi động lại). **CSRF**: form dùng token ẩn (`[ValidateAntiForgeryToken]`); API ghi của người đã đăng nhập phải có header `X-CSRF-TOKEN` (`AuthenticatedApiAntiforgeryFilter`; token ở `<meta name="csrf-token">` khi đã đăng nhập; JS gọi qua `wwwroot/js/api.js` → `apiFetch`). CSP `form-action` thêm `https://accounts.google.com`. Callback Google: `/signin-google`. Retry Npgsql: 3 lần, tối đa 2 s (khóa Data Protection nạp khi khởi động).

**Đồng bộ tiến độ (giai đoạn 8 — T8.3):** localStorage vẫn là bản làm việc (mọi API tính toán giữ nguyên, không trạng thái); khi đã đăng nhập, `progress-sync.js` (chỉ nạp khi có cookie) gửi **toàn bộ** tiến độ tới `PUT /api/progress/sync` { progress, baseXp, mode } lúc mở trang và sau mỗi thay đổi (debounce 1 s). Server: `ProgressImporter` (migrate + làm sạch) → `ProgressMerger` (Domain, thuần) gộp với bản trong `learner_progress` (khóa dòng `FOR UPDATE`, thử lại khi hai máy cùng tạo dòng) → trả bản gộp, máy ghi đè localStorage. Quy tắc gộp: bài — đã hoàn thành thắng, điểm cao nhất, ngày hoàn thành sớm nhất; thẻ — bản ôn gần nhất (rồi nhiều lần lặp hơn); hoạt động — max theo ngày (giữ 365 ngày); **XP = server + max(0, XP máy − baseXp)** (`baseXp` = XP máy sau lần đồng bộ trước; dữ liệu khách → 0) nên gửi lại không cộng hai lần. Trạng thái `hanbit.sync.v1` { userId, baseXp, syncedAt, replace } (qua `progress-store.js`): máy giữ tiến độ của **tài khoản khác** → sao lưu sang khóa backup rồi lấy tiến độ của tài khoản đang đăng nhập; nhập file sao lưu / xóa tiến độ khi đã đăng nhập → `mode: replace`; **đăng xuất** → đồng bộ lần cuối (≤ 4 s) rồi xóa tiến độ trên máy (lỗi mạng → giữ, lần sau gửi tiếp). `GET /api/progress/sync` đọc bản trên tài khoản. Tắt tài khoản → 404; chưa đăng nhập → 401; PUT cần `X-CSRF-TOKEN`. Giới hạn đã biết: mất phản hồi sau khi server đã ghi → lần gửi lại có thể cộng XP phần đó hai lần (XP không dùng xếp hạng).

**Nhắc học (giai đoạn 9):** Web Push, cần đăng nhập. Bật khi có tài khoản + khóa VAPID `Push:PublicKey`/`Push:PrivateKey` (Render `Push__*`, `sync: false`; tạo bằng `npx web-push generate-vapid-keys`). Mã hóa tự cài đặt theo **RFC 8291** (`aes128gcm`: ECDH P-256 → HKDF-SHA-256 → AES-128-GCM, 1 record 4096) + **RFC 8292** (JWT ES256, `Authorization: vapid t=…, k=…`) trong `Infrastructure/Reminders/WebPushCrypto.cs` — có test vector RFC (package `WebPush` dùng bản nháp `aesgcm`, Apple không nhận). Bảng `reminder_settings` (giờ VN, `last_sent_on`) và `push_subscriptions` (endpoint duy nhất; cùng trình duyệt đổi tài khoản → chuyển thiết bị). Endpoint thiết bị chỉ nhận host của FCM / Mozilla / WNS / Apple (server POST tới đó — chống SSRF). Thiết bị trả 404/410 hoặc khóa hỏng → xóa. Trình duyệt: `/sw.js` (phạm vi `/`, không cache — chỉ `push` + `notificationclick` mở đường dẫn trong site), `/manifest.webmanifest` + icon (iPhone chỉ nhận push khi đã "Thêm vào Màn hình chính", iOS 16.4+), `reminders.js` trên /profile. **Lịch gửi (T9.2):** `.github/workflows/reminders.yml` (cron 15 phút, 22:00–18:00 UTC = 05:00–01:00 giờ VN; repo public nên miễn phí) gọi `POST /internal/reminders/run` với `Authorization: Bearer` = secret `REMINDER_RUN_TOKEN` = Render `Reminders__RunToken` (≥ 32 ký tự, so sánh thời gian hằng; thiếu → 404, sai → 401; ngoài `/api` nên không rate limit/CSRF; robots `Disallow: /internal/`). `ReminderJob`: lấy ứng viên (bật, giờ nhắc trong 2 giờ gần nhất, hôm nay chưa nhắc, có thiết bị) → `ReminderPolicy` (Domain: đã tới giờ, chưa quá 2 giờ, **hôm nay chưa có XP** theo tiến độ trên tài khoản) → giữ chỗ nguyên tử `last_sent_on = hôm nay` (hai lần chạy chồng nhau không gửi đôi) → gửi tới mọi thiết bị (nội dung: chuỗi ngày + số thẻ đến hạn; bấm → /review nếu có thẻ, không thì /dashboard) → không thiết bị nào nhận → trả lại `last_sent_on` để lần sau thử lại.

**PWA offline (giai đoạn 10):** chỉ **xem** offline — bài tập, ôn tập, chấm điểm, lưu tiến độ vẫn cần mạng (server chấm). `/sw.js` (đăng ký trên mọi trang qua `offline.js`): khi cài lưu sẵn **vỏ app** = mọi file `/css`, `/js`, `/lib` + icon/manifest theo danh sách `GET /offline/assets.json` (server liệt kê wwwroot) và trang `/offline`; **trang HTML** mạng trước (chờ ≤ 4 s) → lưu vào `hanbit-pages-v1` (tối đa 150 trang) → mất mạng: bản đã lưu → `/offline` (liệt kê trang đã lưu); **file tĩnh** mạng trước → lưu theo đường dẫn bỏ `?v=` (module import không phiên bản vẫn tìm được) → mất mạng: bản đã lưu. Không can thiệp `/api`, `/internal`, `/tai-khoan`, request không phải GET, khác nguồn (font CDN offline → font hệ thống). Trang khóa học có nút **"Tải để xem offline"** (lưu trang khóa + mọi bài, `offline-cache.js`). Dashboard offline: số liệu `/api/progress/summary` lần trước (`hanbit.summary.v1` qua `progress-store.js`) + ghi chú "số liệu lần cập nhật trước". Thanh báo offline (store Alpine `network`). **Đăng xuất** → xóa `hanbit-pages-v1` + số liệu lần trước (trang có tên/email). Đổi chiến lược cache → tăng `VERSION` trong `sw.js` (và `PAGES_CACHE` trong `offline-cache.js`). E2E: service worker mặc định bị chặn (`WebServerFixture`), test PWA bật riêng.

`/health` gồm check `content` (đọc được `hangul.json`) → Render phát hiện image thiếu/sai `content/`.
Importer: `dotnet user-secrets set "Krdict:ApiKey" "..."` — không commit, không đưa lên Render.

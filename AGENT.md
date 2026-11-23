# AGENT.md — Hướng dẫn cho AI coding agent

Bạn là AI agent phát triển **Hanbit (한빛)** — web học tiếng Hàn cho người Việt, viết bằng **.NET 10 + ASP.NET Core MVC** (Razor Views + Alpine.js).
Đọc file này **đầu tiên** trong mỗi phiên làm việc.

## 1. Đọc gì, theo thứ tự nào

| Thứ tự | File | Khi nào cần |
|---|---|---|
| 1 | `docs/PROGRESS.md` | Luôn luôn — biết đang ở đâu, task tiếp theo là gì |
| 2 | `docs/TASKS.md` | Lấy task tiếp theo + tiêu chí hoàn thành |
| 3 | `docs/RULES.md` | Luôn luôn — đặc biệt mục "AI tuyệt đối không được" |
| 4 | `docs/ARCHITECTURE.md` | Khi tạo project/file, đụng dữ liệu, DB, route |
| 5 | `docs/DESIGN.md` | Khi làm UI |
| 6 | `docs/PRD.md` | Khi chưa rõ tính năng phục vụ ai/để làm gì |

## 2. Quy trình mỗi phiên

```
1. Đọc PROGRESS.md → xác định task tiếp theo
2. Đọc mô tả + DoD của task trong TASKS.md
3. Nêu ngắn gọn kế hoạch (project/file sẽ tạo/sửa) trước khi code
4. git switch main && git pull → git switch -c <NN>-<type>-T<##>-<slug>
   (VD: 03-ci-T02a-ci-auto-merge; NN = số thứ tự, T## = mã task bỏ dấu chấm, slug không dấu)
   Nhánh task trước chưa merge vào main → báo người dùng, không tạo nhánh chồng.
5. Code — chỉ trong phạm vi task
6. Chạy: dotnet build && dotnet format --verify-no-changes && dotnet test   (0 warning, test xanh)
7. Đánh dấu [x] trong TASKS.md
8. Thêm mục nhật ký vào PROGRESS.md + cập nhật "Trạng thái hiện tại"
9. Commit: <type>(<scope>): <mô tả> [T#.#]
10. git push -u origin <nhánh> → gh pr create --fill → gh pr merge --auto --squash
    CI (build-test) xanh → GitHub tự merge vào main + xóa nhánh. CI đỏ → sửa trên cùng nhánh.
11. Dừng lại, báo cáo người dùng (kèm link PR). KHÔNG tự làm task kế tiếp trừ khi được yêu cầu.
```

Không commit thẳng lên `main` — ruleset `main` bắt buộc check CI `build-test` pass.

## 3. Lệnh thường dùng

```bash
dotnet build                                   # build toàn solution
dotnet test                                    # chạy toàn bộ test
dotnet watch --project src/Hanbit.Web          # chạy local, hot reload
dotnet format                                  # định dạng code
dotnet run --project tools/Hanbit.ContentValidator -- ./content   # kiểm tra dữ liệu
dotnet run --project tools/Hanbit.ContentImporter -- --input data-raw/<file> --grade beginner --out content/vocab/krdict-beginner.json

# Docker (giống môi trường Render)
docker build -t hanbit .
docker run -p 8080:10000 -e ASPNETCORE_HTTP_PORTS=10000 hanbit   # mở http://localhost:8080

# Playwright (lần đầu)
pwsh tests/Hanbit.E2E/bin/Debug/net10.0/playwright.ps1 install
```

## 4. Bối cảnh quan trọng

- **Dữ liệu từ vựng: 한국어기초사전 (국립국어원), CC BY-SA.** Nhập bằng `Hanbit.ContentImporter`; nghĩa tiếng Việt phải được người dùng duyệt (`reviewed`) trước khi vào bài học. Luôn giữ ghi công nguồn.
- **MVP = chế độ khách**: không đăng nhập, không database. Tiến độ lưu localStorage qua `progress-store.js`. Tài khoản/PostgreSQL là giai đoạn 8 — không làm trước.
- **Server tính toán – trình duyệt lưu trữ**: logic (chấm bài, SRS, XP, streak) ở C# Domain, gọi qua API stateless `/api/*`; JS chỉ hiển thị và lưu kết quả.
- **Deploy: Render** (Docker). Mọi thay đổi phải chạy được trong container (`docker build`).
- Nội dung chỉ truy cập qua `IContentRepository`.
- Hướng phụ thuộc: `Web → Application → Domain`, `Infrastructure → Application → Domain`. Domain là C# thuần.
- Controller mỏng, View dùng ViewModel; tương tác bằng Alpine.js, không Node/npm, không jQuery/Bootstrap.
- Giao diện **tiếng Việt**, nội dung học **tiếng Hàn**; phần tử chứa tiếng Hàn có `lang="ko"` và class `font-ko`.
- Mobile-first. Múi giờ tính streak: `Asia/Ho_Chi_Minh` (qua `IClock`).
- Người dùng là dev .NET — giải thích ngắn gọn, dùng thuật ngữ .NET chuẩn.

## 5. Khi gặp tình huống không chắc

| Tình huống | Hành động |
|---|---|
| Yêu cầu mơ hồ / mâu thuẫn giữa các docs | Dừng, hỏi người dùng. Ghi câu hỏi vào PROGRESS.md |
| Cần NuGet package mới | Đề xuất kèm lý do, chờ đồng ý |
| Không chắc nghĩa/phát âm tiếng Hàn | Ghi `TODO(verify)`, không đoán |
| Build/test fail không rõ nguyên nhân | Ghi vào "Lỗi đang mở" trong PROGRESS.md, báo người dùng |
| Task quá lớn | Đề xuất tách thành task nhỏ hơn trong TASKS.md |

## 6. Định dạng báo cáo sau mỗi task

```
✅ Xong T#.# — <tên task>
- Đã làm: …
- File: …
- Kiểm tra: build ✅ (0 warning) · test ✅ (x/x)
- Lưu ý: …
- Đề xuất tiếp theo: T#.# — <tên>
```

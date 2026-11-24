# Giấy phép dữ liệu nội dung (`content/`)

Mã nguồn Hanbit và dữ liệu trong `content/` có giấy phép **riêng biệt**. File này chỉ áp dụng cho dữ liệu.

## 1. Dữ liệu từ vựng — 한국어기초사전 (Từ điển tiếng Hàn cơ bản)

| | |
|---|---|
| Nguồn | 한국어기초사전 (Basic Korean Dictionary) — https://krdict.korean.go.kr |
| Chủ sở hữu | © 국립국어원 (Viện Quốc ngữ Quốc gia Hàn Quốc — National Institute of Korean Language) |
| Giấy phép | **Creative Commons Attribution-ShareAlike 2.0 Korea (CC BY-SA 2.0 KR)** — https://creativecommons.org/licenses/by-sa/2.0/kr/ |
| Xác minh | Chính sách 저작권 정책 trên krdict (trang đăng ký Open API), 2026-11-24: mọi tài liệu không ghi chú riêng theo CC BY-SA 2.0 KR; **được dùng cả cho mục đích thương mại**, không cần xin phép riêng; điều kiện: **ghi tác giả** (저작자 표시) và **tác phẩm dẫn xuất cùng giấy phép** (동일조건변경허락). Tệp đa phương tiện (ảnh, video, âm thanh, phát âm) có **giấy phép riêng từng tệp** → Hanbit **không** nhập các tệp này. |
| Cách lấy | File "사전 전체 내려받기" (LMF JSON, tải 2026-11-19) → `tools/Hanbit.ContentImporter` |
| Áp dụng cho | `vocab/krdict-*.json` (sinh bởi `Hanbit.ContentImporter`), `vocab-reviews.json` (bản sửa/duyệt nghĩa tiếng Việt — **dữ liệu dẫn xuất**) |

- Mỗi mục từ giữ trường `source` (`SourceRef`: tên nguồn, mã mục từ, link gốc, giấy phép).
- Dữ liệu dẫn xuất (nghĩa tiếng Việt đã sửa/duyệt) được chia sẻ **cùng giấy phép CC BY-SA**.
- Ghi công hiển thị trên site: footer mọi trang + trang `/nguon-du-lieu`:
  > Dữ liệu từ vựng: 한국어기초사전 © 국립국어원, CC BY-SA 2.0 KR
- Mỗi từ trong màn học và màn ôn tập có link về mục từ gốc trên 한국어기초사전.
- **Tải về** (chia sẻ dữ liệu dẫn xuất cùng giấy phép): trang `/nguon-du-lieu` → `/nguon-du-lieu/tai-xuong/krdict-beginner.json`, `/nguon-du-lieu/tai-xuong/vocab-reviews.json`, `/nguon-du-lieu/tai-xuong/LICENSE-DATA.md`.
- Rà soát điều khoản lần cuối: 2026-11-24 (T7.5) — ghi công tác giả/nguồn/giấy phép, nêu thay đổi, chia sẻ cùng giấy phép, không dùng tệp đa phương tiện.

## 2. Dữ liệu tự biên soạn

| File | Nội dung |
|---|---|
| `hangul.json` | Bảng chữ cái Hangul: phiên âm, gợi ý đọc cho người Việt, từ ví dụ |
| `grammar/*.json` | Giải thích ngữ pháp |
| `courses/**` | Cấu trúc khóa học, bài học, bài tập tự soạn |

- Do dự án Hanbit tự biên soạn; **không** sao chép từ giáo trình, đề thi TOPIK hay nguồn có bản quyền khác (RULES — mục "AI tuyệt đối không được" #9).
- Giấy phép: **© Hanbit — giữ mọi quyền (All rights reserved)**. Không được sao chép, phân phối lại hay dùng cho mục đích khác khi chưa có sự đồng ý của chủ dự án. *(Chốt 2026-11-24; có thể chuyển sang giấy phép mở sau này.)*
- Bài học tham chiếu từ vựng bằng `id` (không sao chép), nên phần từ vựng trong bài học vẫn thuộc mục 1.
- ⚠️ Nếu bài tập **chép nguyên văn** nghĩa/định nghĩa lấy từ krdict (VD lựa chọn "nước" trong `multiple_choice`), phần chữ đó vẫn là dữ liệu CC BY-SA 2.0 KR của mục 1 (ghi công như mục 1); phần còn lại của bài học giữ mọi quyền.

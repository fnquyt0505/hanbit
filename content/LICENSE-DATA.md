# Giấy phép dữ liệu nội dung (`content/`)

Mã nguồn Hanbit và dữ liệu trong `content/` có giấy phép **riêng biệt**. File này chỉ áp dụng cho dữ liệu.

## 1. Dữ liệu từ vựng — 한국어기초사전 (Từ điển tiếng Hàn cơ bản)

| | |
|---|---|
| Nguồn | 한국어기초사전 (Basic Korean Dictionary) — https://krdict.korean.go.kr |
| Chủ sở hữu | © 국립국어원 (Viện Quốc ngữ Quốc gia Hàn Quốc — National Institute of Korean Language) |
| Giấy phép | Creative Commons **BY-SA** — `TODO(verify)`: phiên bản chính xác (dự kiến CC BY-SA 2.0 KR) và điều kiện ghi công, xác minh trước T7.5 |
| Áp dụng cho | `vocab/krdict-*.json` (sinh bởi `Hanbit.ContentImporter`), `vocab-reviews.json` (bản sửa/duyệt nghĩa tiếng Việt — **dữ liệu dẫn xuất**) |

- Mỗi mục từ giữ trường `source` (`SourceRef`: tên nguồn, mã mục từ, link gốc, giấy phép).
- Dữ liệu dẫn xuất (nghĩa tiếng Việt đã sửa/duyệt) được chia sẻ **cùng giấy phép CC BY-SA**.
- Ghi công hiển thị trên site: footer mọi trang + trang `/nguon-du-lieu`:
  > Dữ liệu từ vựng: 한국어기초사전 © 국립국어원, CC BY-SA 2.0 KR

## 2. Dữ liệu tự biên soạn

| File | Nội dung |
|---|---|
| `hangul.json` | Bảng chữ cái Hangul: phiên âm, gợi ý đọc cho người Việt, từ ví dụ |
| `grammar/*.json` | Giải thích ngữ pháp |
| `courses/**` | Cấu trúc khóa học, bài học, bài tập tự soạn |

- Do dự án Hanbit tự biên soạn; **không** sao chép từ giáo trình, đề thi TOPIK hay nguồn có bản quyền khác (RULES — mục "AI tuyệt đối không được" #9).
- Giấy phép: `TODO(owner)` — chủ dự án chọn (VD giữ mọi quyền, hoặc CC BY-SA 4.0 để đồng bộ với dữ liệu từ vựng).
- Bài học tham chiếu từ vựng bằng `id` (không sao chép), nên phần từ vựng trong bài học vẫn thuộc mục 1.

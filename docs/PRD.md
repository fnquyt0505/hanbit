# PRD — Hanbit (한빛): Web học tiếng Hàn cho người Việt

> Phiên bản: 0.2 (MVP) · Cập nhật: 2026-11-23 · Trạng thái: Draft

## 1. Tóm tắt

**Hanbit** là web app giúp người Việt học tiếng Hàn từ con số 0 (bảng chữ Hangul) đến trình độ TOPIK I–II, theo hướng **học ít mỗi ngày, ôn đúng lúc** (lặp lại ngắt quãng – SRS).

MVP chạy ở **chế độ khách**: không cần đăng ký, mở web là học, tiến độ lưu trên trình duyệt.

## 2. Vấn đề

- Tài liệu tiếng Hàn cho người Việt rời rạc (video, PDF, nhóm Facebook), thiếu lộ trình rõ ràng.
- App quốc tế (Duolingo…) giải thích bằng tiếng Anh, không sát ngữ pháp với tiếng Việt.
- Người học quên từ nhanh vì không có cơ chế ôn tập có hệ thống.

## 3. Người dùng mục tiêu

| Persona | Mô tả | Nhu cầu chính |
|---|---|---|
| **Linh – sinh viên, 20 tuổi** | Mê K-pop/K-drama, chưa biết gì | Học Hangul nhanh, vui, có phát âm |
| **Tuấn – người đi làm, 27 tuổi** | Chuẩn bị thi TOPIK để xin việc công ty Hàn | Lộ trình theo cấp, theo dõi tiến độ |
| **Hà – chuẩn bị du học/XKLĐ, 23 tuổi** | Cần giao tiếp cơ bản trong 3–6 tháng | Từ vựng theo chủ đề, mẫu câu thực tế |

Ngôn ngữ giao diện: **tiếng Việt**. Thiết bị chính: **điện thoại** (mobile-first), sau đó desktop.

## 4. Mục tiêu & chỉ số

| Mục tiêu | Chỉ số (sau 3 tháng ra mắt) |
|---|---|
| Người học quay lại thường xuyên | D7 retention ≥ 25% |
| Học có hiệu quả | ≥ 60% người hoàn thành Hangul trong 7 ngày |
| Thói quen | Streak trung bình ≥ 4 ngày |

**Không phải mục tiêu (MVP):** tài khoản/đăng nhập, đồng bộ nhiều thiết bị, app native, cộng đồng, thanh toán.

## 5. Tính năng

### MVP (P0) — chế độ khách
1. **Học Hangul** — 40 chữ cái (nguyên âm, phụ âm, phụ âm kép, nguyên âm ghép), bảng ghép âm, nghe phát âm, quiz nhận diện.
2. **Bài học theo lộ trình** — Khóa → Chương → Bài. Mỗi bài: từ vựng, ngữ pháp, ví dụ, bài tập. Khóa đầu tiên: **Sơ cấp (초급)** dựa trên từ vựng cấp sơ cấp của 한국어기초사전.
3. **Flashcard + SRS** — Từ đã học tự vào hàng ôn (SM-2). Tự đánh giá: Quên / Khó / Được / Dễ.
4. **Bài tập** — Trắc nghiệm, nghe chọn đáp án, ghép cặp, sắp xếp câu, gõ Hangul.
5. **Phát âm** — Web Speech API (`ko-KR`), fallback MP3 khi có.
6. **Tiến độ cục bộ** — Bài đã học, thẻ SRS, XP, streak lưu trong **localStorage**.
7. **Sao lưu/khôi phục** — Xuất tiến độ ra file JSON và nhập lại (tránh mất dữ liệu khi xóa trình duyệt / đổi máy).
8. **Trang nguồn dữ liệu** — Ghi công 국립국어원 · 한국어기초사전 theo giấy phép CC BY-SA.

### P1 (sau MVP)
- **Tài khoản** (đăng nhập Google/email) + đồng bộ tiến độ lên server, gộp dữ liệu khách.
- Tra từ nhanh Hàn–Việt (từ dữ liệu 한국어기초사전 đã nhập).
- Khóa Trung cấp (중급).
- Luyện dạng đề TOPIK (câu hỏi tự soạn).
- Nhắc học hằng ngày (Web Push).

### P2
- Admin nhập/duyệt bài học · PWA offline · Ghi âm phát âm · AI giải thích lỗi sai.

## 6. User flows chính

1. **Người mới:** Trang chủ → "Bắt đầu miễn phí" → Hangul bài 1 → hoàn thành → nhận XP, streak ngày 1.
2. **Học hằng ngày:** Dashboard → "Ôn tập (12 thẻ)" → flashcard → "Học bài mới" → bài tập → XP, streak.
3. **Đổi máy:** Hồ sơ → "Xuất tiến độ" (file .json) → máy mới → "Nhập tiến độ".

## 7. Dữ liệu nội dung

| Loại | Nguồn | Ghi chú |
|---|---|---|
| Từ vựng, nghĩa tiếng Việt, ví dụ, cấp độ | **한국어기초사전 (국립국어원)** | Giấy phép CC BY-SA — bắt buộc ghi nguồn; phần dữ liệu chỉnh sửa chia sẻ cùng giấy phép |
| Bảng chữ Hangul, quy tắc ghép âm | Tự biên soạn (kiến thức phổ thông) | |
| Danh sách ngữ pháp theo cấp | Tham chiếu khung 국제 통용 한국어 표준 교육과정 | Giải thích tiếng Việt **tự viết** |
| Bài tập | Sinh tự động từ từ vựng + tự soạn | |
| Âm thanh | TTS trình duyệt; sau có thể tạo MP3 trước | |

**Không dùng:** đề thi TOPIK chính thức, giáo trình in, dữ liệu Naver, video YouTube.
**Lưu ý chất lượng:** nghĩa tiếng Việt của từ sơ cấp cần được người duyệt lại trước khi đưa vào bài học.

## 8. Yêu cầu phi chức năng

- Lighthouse mobile ≥ 90 (Performance, Accessibility).
- Trang bài học tải < 2s trên 4G (lưu ý Render free tier "ngủ" khi không có truy cập → lần đầu có thể chậm).
- Hiển thị đúng font Hangul trên mọi thiết bị.
- WCAG 2.1 AA.
- MVP không thu thập dữ liệu cá nhân.

## 9. Rủi ro

| Rủi ro | Giảm thiểu |
|---|---|
| Mất tiến độ khi xóa dữ liệu trình duyệt | Nút xuất/nhập JSON; nhắc sao lưu; tài khoản ở P1 |
| Nghĩa tiếng Việt trong từ điển có lỗi | Trạng thái `reviewed` cho từng từ; chỉ từ đã duyệt vào bài học |
| TTS trình duyệt thiếu giọng Hàn | Phát hiện & thông báo; fallback MP3 |
| Render free "ngủ" khi không có truy cập (request đầu chậm) | Trang render server nhẹ, cache tĩnh; cân nhắc gói trả phí khi có người dùng |
| Vi phạm giấy phép | Trang nguồn dữ liệu + footer ghi công; giữ CC BY-SA cho dữ liệu dẫn xuất |

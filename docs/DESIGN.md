# DESIGN — Hanbit

## 1. Phong cách

**"Giấy Hanji & mực tàu, hiện đại."** Nền ấm như giấy truyền thống Hàn Quốc, chữ đậm như mực, điểm nhấn đỏ/xanh lấy cảm hứng từ Taegeuk. Gọn gàng, nhiều khoảng trắng, chữ Hangul là nhân vật chính — luôn to và rõ.

Từ khóa: *tối giản · ấm áp · tập trung · khích lệ (không trẻ con)*.

## 2. Màu sắc

| Token | Light | Dark | Dùng cho |
|---|---|---|---|
| `--bg` | `#FAF7F2` (hanji) | `#14120F` | Nền trang |
| `--surface` | `#FFFFFF` | `#1E1B17` | Card, modal |
| `--ink` | `#1C1917` | `#F5F1EA` | Chữ chính |
| `--ink-muted` | `#6B645C` | `#A8A097` | Chữ phụ, romanization |
| `--border` | `#E7E0D6` | `#2E2A25` | Viền |
| `--primary` | `#C8323C` (đỏ Taegeuk) | `#E0565F` | Nút chính, CTA, streak |
| `--secondary` | `#1F4E9A` (xanh Taegeuk) | `#5B8BD9` | Link, tiến độ, thông tin |
| `--success` | `#2F8F5B` | `#4CB97E` | Đáp án đúng |
| `--warning` | `#D99A1E` | `#F0B94A` | "Khó", sắp hết hạn |
| `--danger` | `#C8323C` | `#E0565F` | Đáp án sai |
| `--xp` | `#E8A317` | `#F3C04F` | Điểm XP, huy hiệu |

Quy tắc: không dùng quá 1 màu nhấn mạnh trên một màn hình; đúng/sai luôn đi kèm icon (không chỉ dựa vào màu).

## 3. Typography

| Vai trò | Font | Ghi chú |
|---|---|---|
| UI tiếng Việt | **Be Vietnam Pro** | Hỗ trợ dấu tiếng Việt tốt |
| Tiếng Hàn | **Pretendard** → fallback `Noto Sans KR` | Dùng class `font-ko` |
| Hangul hiển thị lớn | **Noto Serif KR** | Thẻ chữ cái, tiêu đề tiếng Hàn |

Thang chữ (rem): `xs .75 · sm .875 · base 1 · lg 1.125 · xl 1.25 · 2xl 1.5 · 3xl 1.875 · 4xl 2.25 · hangul-display 4–6`.

- Chữ Hàn trong câu: lớn hơn chữ Việt xung quanh 1 bậc.
- Romanization: `text-sm`, màu `--ink-muted`, nghiêng nhẹ.
- line-height: 1.6 cho đoạn văn, 1.2 cho tiêu đề.

## 4. Lưới, khoảng cách, bo góc

- Spacing theo bội số 4px (Tailwind mặc định).
- Container tối đa `max-w-2xl` cho màn học (tập trung), `max-w-6xl` cho dashboard/landing.
- Bo góc: `rounded-xl` (12px) cho card, `rounded-full` cho nút pill, `rounded-2xl` cho flashcard.
- Bóng: rất nhẹ (`shadow-sm`), ưu tiên viền 1px.
- Breakpoints: mobile < 640, tablet 640–1024, desktop > 1024. Mobile dùng **bottom nav**, desktop dùng **sidebar**.

## 5. Components

| Component | Mô tả |
|---|---|
| `Button` | Variants: `primary`, `secondary`, `ghost`, `outline`. Size `sm/md/lg`. Nút chính trên màn học luôn full-width ở mobile, cao ≥ 48px. |
| `HangulTile` | Ô vuông chứa 1 chữ cái lớn + romanization; trạng thái `new / learning / mastered`. Bấm để nghe. |
| `Flashcard` | Mặt trước: tiếng Hàn + nút loa. Mặt sau: nghĩa Việt, romanization, ví dụ. Lật bằng click/Space, animation 3D ≤ 300ms. |
| `GradeButtons` | 4 nút Quên/Khó/Được/Dễ, phím tắt 1–4, hiển thị khoảng ôn tiếp theo. |
| `QuizCard` | Câu hỏi + đáp án; phản hồi tức thì (viền xanh/đỏ + icon + giải thích). |
| `AudioButton` | Icon loa, trạng thái đang phát; hỗ trợ tốc độ chậm (0.7x). |
| `LessonProgressBar` | Thanh tiến độ trên cùng màn học + nút thoát. |
| `StreakBadge` | Ngọn lửa + số ngày. |
| `XPToast` | "+10 XP" bay lên khi đúng. |
| `CourseMap` | Lộ trình bài học dạng danh sách dọc có trạng thái khóa/mở/hoàn thành. |
| `EmptyState` | Minh họa đơn giản + 1 câu + 1 CTA. |

## 6. Chuyển động

- Thời lượng 150–300ms, easing `ease-out`.
- Đúng: rung nhẹ scale 1.02 + màu xanh. Sai: lắc ngang 2 lần.
- Tôn trọng `prefers-reduced-motion`: tắt lật 3D, chỉ fade.

## 7. Giọng văn (UI copy)

- Xưng "bạn", thân thiện, ngắn gọn. VD: "Tuyệt! Bạn đã nhớ 8/10 từ 🎉", "Hôm nay ôn 12 thẻ nhé".
- Không trách khi sai: "Gần đúng rồi! Đáp án là **감사합니다**."
- Nút hành động là động từ: "Bắt đầu", "Kiểm tra", "Tiếp tục".

## 8. Accessibility

- Tương phản chữ ≥ 4.5:1.
- Mọi thao tác dùng được bằng bàn phím; focus ring rõ (`ring-2 ring-secondary`).
- Thẻ tiếng Hàn có `lang="ko"`.
- Nút chỉ có icon phải có `aria-label` tiếng Việt.

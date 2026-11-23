# Fixtures — dữ liệu mẫu cho test importer

- `krdict-lmf-sample.json`: 7 mục trích từ file "사전 전체 내려받기" (định dạng LMF) của 한국어기초사전, tải 2026-11-19;
  chỉ giữ bản dịch tiếng Việt + tiếng Anh và 1 câu ví dụ mỗi nghĩa cho gọn. Chọn để phủ các trường hợp:
  초급 단어 có `WordForm` là object (청년) / mảng (청소되다) / vắng + `Lemma` dạng mảng có variant (초콜릿),
  초급 구 (체크 카드), 중급 단어 (첫가을), và 2 mục trùng mã 77610 (첫 — 초급; 첫 단추를 끼우다 — 관용구).
  Nội dung: 한국어기초사전 © 국립국어원, **CC BY-SA 2.0 KR** (https://krdict.korean.go.kr).

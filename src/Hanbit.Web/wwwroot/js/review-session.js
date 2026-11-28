// Phiên ôn tập /review (T6.3): flashcard lật (Space), 4 mức Quên/Khó/Được/Dễ (phím 1–4).
// Server lọc thẻ đến hạn + lập lịch SM-2 (POST /api/reviews/due, /grade); JS chỉ hiển thị và lưu qua progress-store.js.
import { apiFetch } from './api.js';
import { applyReviewResult, load } from './progress-store.js';
import { speak } from './speech.js';

export const GRADES = ['again', 'hard', 'good', 'easy'];
export const SESSION_LIMIT = 50;

/** Nhãn khoảng ôn trên nút đánh giá: 0 → "Ngay", 1–29 ngày, rồi tháng/năm. */
export function intervalLabel(days) {
  if (days <= 0) return 'Ngay';
  if (days < 30) return `${days} ngày`;
  if (days < 365) return `${Math.round(days / 30)} tháng`;
  return `${Math.round((days / 365) * 10) / 10} năm`;
}

document.addEventListener('alpine:init', () => {
  window.Alpine.data('reviewSession', () => ({
    status: 'loading', // loading | empty | reviewing | done | error
    queue: [], // [{ card, word, previews }]
    total: 0, // tổng thẻ đến hạn lúc bắt đầu
    flipped: false,
    grading: false,
    reviewed: 0,
    xp: 0,
    streak: null,
    error: null,

    get current() {
      return this.queue[0] ?? null;
    },

    label(days) {
      return intervalLabel(days);
    },

    async init() {
      await this.loadDue();
    },

    async loadDue() {
      this.status = 'loading';
      try {
        const progress = load();
        const response = await apiFetch('/api/reviews/due', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ cards: Object.values(progress.cards), limit: SESSION_LIMIT }),
        });
        if (!response.ok) {
          throw new Error(String(response.status));
        }
        const body = await response.json();
        this.queue = body.items;
        this.total = body.total;
        this.flipped = false;
        this.status = this.queue.length > 0 ? 'reviewing' : 'empty';
      } catch {
        this.status = 'error';
      }
    },

    flip() {
      if (this.status === 'reviewing' && !this.flipped) {
        this.flipped = true;
      }
    },

    listen() {
      if (this.current) {
        speak(this.current.word.ko).catch(() =>
          window.Alpine.store('toast')?.show('Thiết bị chưa đọc được tiếng Hàn.', 'danger'));
      }
    },

    async grade(grade) {
      if (!this.flipped || this.grading || !this.current) {
        return;
      }
      this.grading = true;
      this.error = null;
      const item = this.current;
      try {
        const response = await apiFetch('/api/reviews/grade', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ card: item.card, grade, activity: load().activity }),
        });
        if (!response.ok) {
          throw new Error(String(response.status));
        }
        const result = await response.json();
        applyReviewResult(result);
        this.reviewed++;
        this.xp += result.xp;
        this.streak = result.streak;

        this.queue.shift();
        if (grade === 'again') {
          // "Quên" → ôn lại cuối phiên với thẻ đã lập lịch lại (ARCHITECTURE §7)
          this.queue.push({ ...item, card: result.card, previews: result.previews });
        }
        this.flipped = false;
        if (this.queue.length === 0) {
          this.status = 'done';
        }
      } catch {
        this.error = 'Không lưu được đánh giá. Kiểm tra kết nối rồi thử lại.';
      } finally {
        this.grading = false;
      }
    },

    onKey(event) {
      if (this.status !== 'reviewing' || event.target.closest?.('input, textarea, [contenteditable]')) {
        return;
      }
      if (event.code === 'Space' || event.key === ' ') {
        event.preventDefault();
        this.flip();
        return;
      }
      const index = ['1', '2', '3', '4'].indexOf(event.key);
      if (index >= 0 && this.flipped) {
        event.preventDefault();
        this.grade(GRADES[index]);
      }
    },
  }));
});

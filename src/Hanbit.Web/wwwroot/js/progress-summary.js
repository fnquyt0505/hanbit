// Store Alpine "progress" (T5.4): số liệu dashboard/StreakBadge do SERVER tính từ tiến độ localStorage
// (POST /api/progress/summary — JS không tự tính streak/thẻ đến hạn, RULES). Làm mới khi tiến độ thay đổi.
import { apiFetch } from './api.js';
import { CHANGED_EVENT, load, loadLastSummary, saveLastSummary } from './progress-store.js';

/** Thân yêu cầu tóm tắt từ tiến độ đã lưu. */
export function summaryRequest(progress) {
  return {
    lessons: progress.lessons,
    cards: Object.values(progress.cards),
    activity: progress.activity,
    totalXp: progress.totalXp,
  };
}

document.addEventListener('alpine:init', () => {
  window.Alpine.store('progress', {
    loaded: false,
    error: false,
    summary: null, // ProgressSummaryResponse
    stale: null, // mất mạng → thời điểm (ISO) của số liệu lần trước đang hiện
    async refresh() {
      try {
        const response = await apiFetch('/api/progress/summary', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(summaryRequest(load())),
        });
        if (!response.ok) {
          throw new Error(String(response.status));
        }
        this.summary = await response.json();
        this.stale = null;
        saveLastSummary(this.summary);
        this.error = false;
      } catch {
        // Mất mạng (giai đoạn 10): hiện số liệu lần trước thay vì báo lỗi
        const last = loadLastSummary();
        if (last && !this.summary) {
          this.summary = last.summary;
          this.stale = last.savedAt;
        }
        this.error = !this.summary;
      } finally {
        this.loaded = true;
      }
    },
  });
});

document.addEventListener('alpine:initialized', () => window.Alpine.store('progress').refresh());
window.addEventListener(CHANGED_EVENT, () => window.Alpine?.store('progress')?.refresh());

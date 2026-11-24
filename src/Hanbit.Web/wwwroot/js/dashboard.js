// Dashboard (T2.4 → T5.4): server render skeleton, Alpine điền từ store "progress" (progress-summary.js —
// số liệu do server tính từ tiến độ localStorage). Lỗi mạng → trạng thái người học mới.

/** Tóm tắt tiến độ của người học mới (chưa có dữ liệu). */
export function emptySummary() {
  return {
    streakDays: 0,
    xpToday: 0,
    dueCards: 0,
    learnedWords: 0,
    nextLesson: null, // { title, href }
  };
}

/** Đổi phản hồi /api/progress/summary sang dạng dashboard dùng. */
export function toDashboardSummary(response) {
  if (!response) {
    return emptySummary();
  }
  return {
    streakDays: response.streak?.current ?? 0,
    xpToday: response.xpToday ?? 0,
    dueCards: response.dueCards ?? 0,
    learnedWords: response.learnedWords ?? 0,
    nextLesson: response.nextLesson
      ? { title: `Bài ${response.nextLesson.order} · ${response.nextLesson.title}`, href: response.nextLesson.href }
      : null,
  };
}

document.addEventListener('alpine:init', () => {
  window.Alpine.data('dashboard', () => ({
    get loaded() {
      return window.Alpine.store('progress').loaded;
    },
    get summary() {
      return toDashboardSummary(window.Alpine.store('progress').summary);
    },
    get isNewLearner() {
      return window.Alpine.store('progress').summary?.isNewLearner ?? true;
    },
  }));
});

// Dashboard (T2.4): server render skeleton, Alpine điền số liệu tiến độ.
// Giai đoạn 5 (T5.3/T5.4) thay emptySummary() bằng dữ liệu từ progress-store.js.
// KHÔNG đọc localStorage trực tiếp ở đây (RULES: chỉ qua progress-store.js).

/** Tóm tắt tiến độ của người học mới (chưa có dữ liệu). */
export function emptySummary() {
  return {
    streakDays: 0,
    xpToday: 0,
    dueCards: 0,
    learnedWords: 0,
    nextLesson: null, // { title, href } — null → dùng bài đầu tiên do server cung cấp
  };
}

document.addEventListener('alpine:init', () => {
  window.Alpine.data('dashboard', () => ({
    loaded: false,
    summary: emptySummary(),
    init() {
      this.summary = emptySummary();
      this.loaded = true;
    },
    get isNewLearner() {
      return this.summary.xpToday === 0 && this.summary.learnedWords === 0 && this.summary.nextLesson === null;
    },
  }));
});

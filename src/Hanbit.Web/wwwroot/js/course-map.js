// CourseMap (T4.1): trạng thái từng bài — completed | open | locked.
// Quy tắc: bài đã hoàn thành → completed; bài đầu tiên chưa hoàn thành → open; còn lại → locked.
// Giai đoạn 5 (T5.4): completedLessons() đọc từ progress-store.js. KHÔNG đọc localStorage trực tiếp.

/** Tính trạng thái cho danh sách khóa bài (theo thứ tự) từ tập bài đã hoàn thành. */
export function lessonStates(keys, completed) {
  const states = {};
  let opened = false;
  for (const key of keys) {
    if (completed.has(key)) {
      states[key] = 'completed';
    } else if (!opened) {
      states[key] = 'open';
      opened = true;
    } else {
      states[key] = 'locked';
    }
  }
  return states;
}

/** Bài đã hoàn thành — chưa có tiến độ cho tới giai đoạn 5. */
export function completedLessons() {
  return new Set();
}

document.addEventListener('alpine:init', () => {
  window.Alpine.data('courseMap', (keys) => ({
    states: lessonStates(keys, completedLessons()),
    state(key) {
      return this.states[key] ?? 'locked';
    },
    locked() {
      window.Alpine.store('toast')?.show('Hoàn thành bài trước để mở khóa bài này nhé.', 'info');
    },
  }));
});

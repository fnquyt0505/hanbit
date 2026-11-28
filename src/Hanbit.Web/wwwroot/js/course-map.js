// CourseMap (T4.1): trạng thái từng bài — completed | open | locked.
// Quy tắc: bài đã hoàn thành → completed; bài đầu tiên chưa hoàn thành → open; còn lại → locked.
// Bài đã hoàn thành đọc qua progress-store.js (T5.3). KHÔNG đọc localStorage trực tiếp.
import { CHANGED_EVENT, completedLessons as storedCompletedLessons } from './progress-store.js';

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

/** Bài đã hoàn thành (từ localStorage qua progress-store). */
export function completedLessons() {
  return storedCompletedLessons();
}

document.addEventListener('alpine:init', () => {
  window.Alpine.data('courseMap', (keys) => ({
    states: lessonStates(keys, completedLessons()),
    init() {
      // T8.3: tiến độ tải về từ tài khoản sau khi trang đã vẽ → cập nhật khóa/mở bài
      window.addEventListener(CHANGED_EVENT, () => {
        this.states = lessonStates(keys, completedLessons());
      });
    },
    state(key) {
      return this.states[key] ?? 'locked';
    },
    locked() {
      window.Alpine.store('toast')?.show('Hoàn thành bài trước để mở khóa bài này nhé.', 'info');
    },
  }));
});

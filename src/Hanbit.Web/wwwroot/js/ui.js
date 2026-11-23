// Thành phần UI dùng chung (Alpine): toast, dialog.

const DEFAULT_TOAST_MS = 3000;

document.addEventListener('alpine:init', () => {
  // Toast: Alpine.store('toast').show('Đã lưu', 'success') — kind: info | success | danger.
  window.Alpine.store('toast', {
    items: [],
    nextId: 1,
    show(message, kind = 'info', timeoutMs = DEFAULT_TOAST_MS) {
      const id = this.nextId++;
      this.items.push({ id, message, kind });
      if (timeoutMs > 0) {
        setTimeout(() => this.dismiss(id), timeoutMs);
      }
      return id;
    },
    dismiss(id) {
      this.items = this.items.filter((item) => item.id !== id);
    },
  });

  // Dialog (partial _Dialog): mở bằng $dispatch('hb-dialog-open', '<id>'), đóng bằng Esc / nút / bấm nền.
  window.Alpine.data('hbDialog', (id) => ({
    init() {
      window.addEventListener('hb-dialog-open', (event) => {
        if (event.detail === id) {
          this.$el.showModal();
        }
      });
    },
    closeOnBackdrop(event) {
      if (event.target === this.$el) {
        this.$el.close();
      }
    },
  }));
});

/** Hiện toast từ module JS khác: import { toast } from './ui.js'. */
export function toast(message, kind = 'info', timeoutMs = DEFAULT_TOAST_MS) {
  return window.Alpine.store('toast').show(message, kind, timeoutMs);
}

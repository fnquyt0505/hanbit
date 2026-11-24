// Trang /profile (T5.5): xuất / nhập / xóa tiến độ. Nhập: server kiểm tra + migrate + làm sạch (POST /api/progress/validate)
// rồi người dùng xác nhận mới ghi đè. Ghi/đọc localStorage chỉ qua progress-store.js.
import { backupFileName, clearProgress, exportJson, load, replaceProgress } from './progress-store.js';

/** Thống kê ngắn của một bản tiến độ (hiển thị khi xác nhận ghi đè). */
export function describe(progress) {
  return {
    lessons: Object.values(progress.lessons ?? {}).filter((l) => l?.status === 'completed').length,
    cards: Object.keys(progress.cards ?? {}).length,
    xp: progress.totalXp ?? 0,
  };
}

document.addEventListener('alpine:init', () => {
  window.Alpine.data('progressBackup', () => ({
    pending: null, // { progress, warnings, incoming, current, fileName }
    error: null,
    busy: false,
    confirmClear: false,

    exportFile() {
      const blob = new Blob([exportJson()], { type: 'application/json' });
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = backupFileName();
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
      window.Alpine.store('toast').show('Đã tải tệp sao lưu tiến độ.', 'success');
    },

    async importFile(event) {
      const file = event.target.files?.[0];
      event.target.value = ''; // chọn lại cùng tệp vẫn kích hoạt sự kiện
      if (!file) {
        return;
      }

      this.error = null;
      this.pending = null;
      this.busy = true;
      try {
        const text = await file.text();
        const response = await fetch('/api/progress/validate', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: text,
        });
        const body = await response.json().catch(() => null);
        if (!response.ok) {
          this.error = Object.values(body?.errors ?? {}).flat()[0] ?? 'Tệp không phải bản sao lưu tiến độ Hanbit hợp lệ.';
          return;
        }
        this.pending = {
          progress: body.progress,
          warnings: body.warnings ?? [],
          incoming: describe(body.progress),
          current: describe(load()),
          fileName: file.name,
        };
      } catch {
        this.error = 'Không đọc được tệp hoặc không kết nối được máy chủ.';
      } finally {
        this.busy = false;
      }
    },

    confirmImport() {
      try {
        replaceProgress(this.pending.progress);
        this.pending = null;
        window.Alpine.store('toast').show('Đã khôi phục tiến độ từ tệp sao lưu.', 'success');
      } catch {
        this.error = 'Không ghi được tiến độ.';
      }
    },

    cancelImport() {
      this.pending = null;
    },

    clearAll() {
      clearProgress();
      this.confirmClear = false;
      window.Alpine.store('toast').show('Đã xóa tiến độ trên thiết bị này.', 'info');
    },
  }));
});

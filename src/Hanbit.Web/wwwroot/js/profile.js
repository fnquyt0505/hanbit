// Trang /profile (T5.5): xuất / nhập / xóa tiến độ. Nhập: server kiểm tra + migrate + làm sạch (POST /api/progress/validate)
// rồi người dùng xác nhận mới ghi đè. Ghi/đọc localStorage chỉ qua progress-store.js.
import { apiFetch } from './api.js';
import { backupFileName, clearProgress, CHANGED_EVENT, exportJson, load, replaceProgress } from './progress-store.js';

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
        const response = await apiFetch('/api/progress/validate', {
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
      const signedIn = document.querySelector('meta[name="hanbit-user"]') !== null;
      window.Alpine.store('toast').show(signedIn ? 'Đã xóa tiến độ trên tài khoản và thiết bị này.' : 'Đã xóa tiến độ trên thiết bị này.', 'info');
    },
  }));
});

// ── Thống kê (T7.1): server tính (POST /api/progress/stats), JS hiển thị ──

/** Nhãn ngày dd/mm cho ngày ôn tiếp theo. */
export function shortDate(iso) {
  const d = new Date(iso);
  return `${String(d.getDate()).padStart(2, '0')}/${String(d.getMonth() + 1).padStart(2, '0')}`;
}

document.addEventListener('alpine:init', () => {
  window.Alpine.data('profileStats', () => ({
    loaded: false,
    error: false,
    stats: null,
    filter: 'all', // all | mastered | learning

    async init() {
      await this.refresh();
      window.addEventListener(CHANGED_EVENT, () => this.refresh());
    },

    async refresh() {
      try {
        const progress = load();
        const response = await apiFetch('/api/progress/stats', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ cards: Object.values(progress.cards), activity: progress.activity }),
        });
        if (!response.ok) {
          throw new Error(String(response.status));
        }
        this.stats = await response.json();
        this.error = false;
      } catch {
        this.error = true;
      } finally {
        this.loaded = true;
      }
    },

    get words() {
      const all = this.stats?.words ?? [];
      return this.filter === 'all' ? all : all.filter((w) => w.status === this.filter);
    },

    dayTitle(day) {
      const [y, m, d] = day.date.split('-');
      return `${d}/${m}/${y}: ${day.xp} XP, ${day.reviews} lượt ôn`;
    },

    shortDate,
  }));
});

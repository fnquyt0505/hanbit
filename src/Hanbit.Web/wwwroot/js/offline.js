// PWA offline (giai đoạn 10 — T10.1): đăng ký service worker, trạng thái mạng, nút tải khóa học, trang /offline.
import { isSaved, listSavedPages, offlineSupported, savePages } from './offline-cache.js';

export const SW_URL = '/sw.js';

/** Đăng ký service worker (mọi trang). Lỗi → bỏ qua: site vẫn chạy bình thường khi có mạng. */
export async function registerServiceWorker() {
  if (!offlineSupported()) {
    return null;
  }
  try {
    return await navigator.serviceWorker.register(SW_URL);
  } catch {
    return null;
  }
}

registerServiceWorker();

document.addEventListener('alpine:init', () => {
  // Trạng thái mạng — thanh báo offline trong _Layout
  window.Alpine.store('network', {
    online: navigator.onLine,
    init() {
      window.addEventListener('online', () => { this.online = true; });
      window.addEventListener('offline', () => { this.online = false; });
    },
  });

  // Nút "Tải để xem offline" trên trang khóa học — urls: trang khóa + từng bài
  window.Alpine.data('offlineCourse', (urls) => ({
    supported: offlineSupported(),
    state: 'idle', // idle | saving | saved | failed
    done: 0,
    total: urls.length,
    async init() {
      if (this.supported) {
        const saved = await Promise.all(urls.map(isSaved));
        if (saved.every(Boolean)) {
          this.state = 'saved';
        }
      }
    },
    async download() {
      if (this.state === 'saving') {
        return;
      }
      this.state = 'saving';
      this.done = 0;
      const saved = await savePages(urls, (done) => { this.done = done; });
      this.state = saved === urls.length ? 'saved' : 'failed';
    },
  }));

  // Trang /offline — liệt kê trang đã lưu
  window.Alpine.data('savedPages', () => ({
    pages: [],
    loaded: false,
    async init() {
      try {
        this.pages = await listSavedPages();
      } finally {
        this.loaded = true;
      }
    },
  }));
});

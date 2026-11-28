// Thao tác cache trang offline (giai đoạn 10) — KHÔNG có tác dụng phụ khi import (offline.js, progress-sync.js dùng chung).
// Cache do /sw.js quản lý — tên cache phải trùng.

export const PAGES_CACHE = 'hanbit-pages-v1';

/** Trình duyệt hỗ trợ xem offline? */
export function offlineSupported() {
  return 'serviceWorker' in navigator && 'caches' in window;
}

/** Đã lưu trang này chưa? */
export async function isSaved(url) {
  if (!('caches' in window)) {
    return false;
  }
  const cache = await caches.open(PAGES_CACHE);
  return Boolean(await cache.match(url));
}

/**
 * Lưu các trang (HTML) để xem offline. File JS/CSS đã được service worker lưu sẵn khi cài.
 * @param {string[]} urls
 * @param {(done: number, total: number) => void} [onProgress]
 * @returns {Promise<number>} số trang lưu được
 */
export async function savePages(urls, onProgress) {
  const cache = await caches.open(PAGES_CACHE);
  let done = 0;
  let saved = 0;
  for (const url of urls) {
    try {
      const response = await fetch(url, { credentials: 'same-origin', cache: 'no-cache' });
      if (response.ok && (response.headers.get('content-type') || '').includes('text/html')) {
        await cache.put(url, response);
        saved++;
      }
    } catch {
      // mất mạng giữa chừng → dừng ở số đã lưu
    }
    onProgress?.(++done, urls.length);
  }
  return saved;
}

/** Trang đã lưu (trừ /offline) kèm tiêu đề lấy từ thẻ <title>. */
export async function listSavedPages() {
  if (!('caches' in window)) {
    return [];
  }
  const cache = await caches.open(PAGES_CACHE);
  const pages = [];
  for (const request of await cache.keys()) {
    const url = new URL(request.url);
    if (url.pathname === '/offline') {
      continue;
    }
    const html = await (await cache.match(request))?.text() ?? '';
    const title = /<title>([^<]*)<\/title>/i.exec(html)?.[1]?.replace(/\s*[—-]\s*Hanbit.*$/, '').trim() || url.pathname;
    pages.push({ url: url.pathname + url.search, title: decodeEntities(title) });
  }
  return pages.sort((a, b) => a.url.localeCompare(b.url));
}

/** Xóa mọi trang đã lưu (đăng xuất: trang có tên/email người dùng). Giữ vỏ app JS/CSS. */
export async function clearSavedPages() {
  if ('caches' in window) {
    await caches.delete(PAGES_CACHE);
  }
}

function decodeEntities(text) {
  const textarea = document.createElement('textarea');
  textarea.innerHTML = text;
  return textarea.value;
}


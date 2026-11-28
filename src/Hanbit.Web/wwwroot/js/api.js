// Gọi API của site (T8.2): request ghi (POST/PUT/PATCH/DELETE) tự gắn mã chống CSRF (header X-CSRF-TOKEN) khi đã đăng nhập.
// Mã nằm trong <meta name="csrf-token"> — _Layout chỉ render khi có cookie đăng nhập; khách → không có, API ẩn danh không cần.

export const CSRF_HEADER = 'X-CSRF-TOKEN';

/** Mã CSRF hiện tại (null nếu chưa đăng nhập). */
export function csrfToken() {
  return document.querySelector('meta[name="csrf-token"]')?.getAttribute('content') || null;
}

/** fetch() cho /api/*: giống fetch, thêm header CSRF cho request ghi nếu có mã. */
export function apiFetch(input, init = {}) {
  const method = (init.method ?? 'GET').toUpperCase();
  const token = csrfToken();
  if (token && !['GET', 'HEAD', 'OPTIONS'].includes(method)) {
    const headers = new Headers(init.headers ?? {});
    headers.set(CSRF_HEADER, token);
    return fetch(input, { ...init, headers, credentials: 'same-origin' });
  }
  return fetch(input, init);
}

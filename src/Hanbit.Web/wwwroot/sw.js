// Service worker (giai đoạn 9 + 10): nhận thông báo nhắc học + XEM OFFLINE.
// Phạm vi "/" nên phải nằm ở gốc site (/sw.js). Đổi cách cache → tăng VERSION (cache cũ bị xóa khi kích hoạt).
//
// Chiến lược (ARCHITECTURE "PWA offline"):
//   - Trang HTML: mạng trước (chờ tối đa NETWORK_TIMEOUT_MS) → lưu bản mới; mất mạng → bản đã lưu → trang /offline.
//   - File tĩnh (/css, /js, /lib, /img…): mạng trước (cache HTTP của trình duyệt vẫn dùng) → lưu theo đường dẫn KHÔNG kèm
//     ?v= để module import không phiên bản cũng tìm được; mất mạng → bản đã lưu.
//   - /api, /internal, /tai-khoan, request không phải GET, khác nguồn: KHÔNG can thiệp (bài tập/ôn tập/chấm điểm cần mạng).

// Tên cache trùng với wwwroot/js/offline.js.
// Tên cache phải trùng với wwwroot/js/offline.js.
const VERSION = 'v1';
const STATIC_CACHE = `hanbit-static-${VERSION}`;
const PAGES_CACHE = `hanbit-pages-${VERSION}`;
const OFFLINE_URL = '/offline';
const NETWORK_TIMEOUT_MS = 4000;
const MAX_PAGES = 150;

const BYPASS = [/^\/api\//, /^\/internal\//, /^\/tai-khoan\//, /^\/signin-google/, /^\/health$/, /^\/sw\.js$/, /^\/offline\/assets\.json$/];
const STATIC = /^\/(css|js|lib|img)\/|^\/(favicon\.(svg|ico)|manifest\.webmanifest)$/;

self.addEventListener('install', (event) => {
  event.waitUntil((async () => {
    const cache = await caches.open(STATIC_CACHE);
    try {
      const { assets } = await (await fetch('/offline/assets.json', { cache: 'no-store' })).json();
      await cache.addAll(assets.map((url) => new Request(url, { cache: 'reload' })));
    } catch {
      // không lưu được vỏ app (mất mạng lúc cài) → vẫn cài, file sẽ được lưu dần khi dùng
    }
    try {
      const pages = await caches.open(PAGES_CACHE);
      await pages.add(new Request(OFFLINE_URL, { cache: 'reload' }));
    } catch {
      // bỏ qua
    }
    await self.skipWaiting();
  })());
});

self.addEventListener('activate', (event) => {
  event.waitUntil((async () => {
    const keep = new Set([STATIC_CACHE, PAGES_CACHE]);
    for (const key of await caches.keys()) {
      if (key.startsWith('hanbit-') && !keep.has(key)) {
        await caches.delete(key);
      }
    }
    await self.clients.claim();
  })());
});

self.addEventListener('fetch', (event) => {
  const request = event.request;
  if (request.method !== 'GET') {
    return;
  }
  const url = new URL(request.url);
  if (url.origin !== self.location.origin || BYPASS.some((re) => re.test(url.pathname))) {
    return;
  }

  if (request.mode === 'navigate') {
    event.respondWith(handlePage(request, url));
  } else if (STATIC.test(url.pathname)) {
    event.respondWith(handleStatic(request, url));
  }
});

/** Hết thời gian chờ mạng → coi như mất mạng (mạng 2G/chập chờn vẫn mở được bản đã lưu). */
function fetchWithTimeout(request) {
  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error('timeout')), NETWORK_TIMEOUT_MS);
    fetch(request).then(
      (response) => { clearTimeout(timer); resolve(response); },
      (error) => { clearTimeout(timer); reject(error); },
    );
  });
}

async function handlePage(request, url) {
  try {
    const response = await fetchWithTimeout(request);
    const type = response.headers.get('content-type') || '';
    if (response.ok && !response.redirected && type.includes('text/html')) {
      const cache = await caches.open(PAGES_CACHE);
      await cache.put(url.pathname + url.search, response.clone());
      trim(cache, MAX_PAGES);
    }
    return response;
  } catch {
    const cache = await caches.open(PAGES_CACHE);
    return (await cache.match(url.pathname + url.search))
      || (await cache.match(url.pathname))
      || (await cache.match(OFFLINE_URL))
      || new Response('<!doctype html><meta charset="utf-8"><title>Offline</title><p>Bạn đang offline.</p>', {
        status: 503,
        headers: { 'Content-Type': 'text/html; charset=utf-8' },
      });
  }
}

async function handleStatic(request, url) {
  const cache = await caches.open(STATIC_CACHE);
  try {
    const response = await fetch(request);
    if (response.ok) {
      await cache.put(url.pathname, response.clone());
    }
    return response;
  } catch {
    const cached = await cache.match(url.pathname);
    if (cached) {
      return cached;
    }
    throw new Error('offline');
  }
}

/** Giữ tối đa <max> trang (bỏ trang lưu lâu nhất), luôn giữ /offline. */
async function trim(cache, max) {
  const keys = (await cache.keys()).filter((r) => new URL(r.url).pathname !== OFFLINE_URL);
  for (const request of keys.slice(0, Math.max(0, keys.length - max))) {
    await cache.delete(request);
  }
}

// ── Thông báo nhắc học (giai đoạn 9) ──

self.addEventListener('push', (event) => {
  let data = {};
  try {
    data = event.data ? event.data.json() : {};
  } catch {
    data = { body: event.data?.text() };
  }

  const title = data.title || 'Hanbit 한빛';
  event.waitUntil(self.registration.showNotification(title, {
    body: data.body || 'Đến giờ học tiếng Hàn rồi!',
    icon: '/img/icon-192.png',
    badge: '/img/badge-96.png',
    tag: data.tag || 'hanbit-reminder',
    renotify: false,
    lang: 'vi',
    data: { url: data.url || '/dashboard' },
  }));
});

self.addEventListener('notificationclick', (event) => {
  event.notification.close();
  // Chỉ mở đường dẫn trong site
  const target = new URL(event.notification.data?.url || '/dashboard', self.location.origin);
  const url = target.origin === self.location.origin ? target.href : self.location.origin + '/dashboard';

  event.waitUntil((async () => {
    const windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
    for (const client of windows) {
      if (new URL(client.url).origin === self.location.origin && 'focus' in client) {
        await client.focus();
        return client.navigate ? client.navigate(url) : undefined;
      }
    }
    return self.clients.openWindow(url);
  })());
});

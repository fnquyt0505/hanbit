// Service worker (giai đoạn 9): CHỈ nhận thông báo nhắc học — không cache, không offline (PWA offline là việc khác trong Backlog).
// Phạm vi "/" nên phải nằm ở gốc site (/sw.js).

self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', (event) => event.waitUntil(self.clients.claim()));

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

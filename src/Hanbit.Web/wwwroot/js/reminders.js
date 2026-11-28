// Nhắc học (giai đoạn 9 — T9.1): bật/tắt thông báo đẩy trên thiết bị này, chọn giờ nhắc, gửi thử.
// Server quyết định khi nào nhắc (T9.2); trình duyệt chỉ đăng ký nhận (PushSubscription) và lưu cài đặt qua API.
import { apiFetch } from './api.js';

export const API = '/api/reminders';
export const SW_URL = '/sw.js';

/** Khóa VAPID base64url → Uint8Array cho pushManager.subscribe. */
export function base64UrlToBytes(value) {
  const padded = (value + '='.repeat((4 - (value.length % 4)) % 4)).replace(/-/g, '+').replace(/_/g, '/');
  return Uint8Array.from(atob(padded), (c) => c.charCodeAt(0));
}

/** Trình duyệt hỗ trợ thông báo đẩy? */
export function pushSupported() {
  return 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window;
}

/** iPhone/iPad mở trong Safari (chưa "Thêm vào Màn hình chính") → chưa nhận được thông báo. */
export function needsHomeScreen() {
  const ios = /iPad|iPhone|iPod/.test(navigator.userAgent) || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
  const standalone = window.matchMedia?.('(display-mode: standalone)').matches || navigator.standalone === true;
  return ios && !standalone;
}

async function send(url, method, body) {
  const response = await apiFetch(url, {
    method,
    headers: { 'Content-Type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  if (!response.ok) {
    let message = 'Không lưu được cài đặt nhắc học. Bạn thử lại nhé.';
    try {
      const problem = await response.json();
      message = Object.values(problem.errors ?? {}).flat()[0] ?? message;
    } catch {
      // giữ thông báo mặc định
    }
    throw new Error(message);
  }
  return response.status === 204 ? null : response.json();
}

document.addEventListener('alpine:init', () => {
  window.Alpine.data('reminderSettings', () => ({
    loaded: false,
    supported: pushSupported(),
    homeScreen: needsHomeScreen(),
    permission: 'Notification' in window ? Notification.permission : 'denied',
    settings: null, // { enabled, time, devices, allowedTimes, publicKey }
    subscribed: false, // thiết bị này đang nhận thông báo
    busy: false,
    error: null,
    notice: null,

    get active() {
      return Boolean(this.settings?.enabled && this.subscribed);
    },

    async init() {
      try {
        const response = await apiFetch(API);
        if (!response.ok) {
          throw new Error(String(response.status));
        }
        this.settings = await response.json();
        if (this.supported && !this.homeScreen) {
          const registration = await navigator.serviceWorker.register(SW_URL);
          const subscription = await registration.pushManager.getSubscription();
          this.subscribed = Boolean(subscription);
          if (subscription && this.settings.enabled) {
            // Gửi lại đăng ký (tài khoản khác từng đăng nhập trên máy này / server mất bản ghi)
            await send(`${API}/subscriptions`, 'POST', subscription.toJSON()).catch(() => {});
          }
        }
      } catch {
        this.error = 'Không tải được cài đặt nhắc học.';
      } finally {
        this.loaded = true;
      }
    },

    async subscribeDevice() {
      const registration = await navigator.serviceWorker.register(SW_URL);
      await navigator.serviceWorker.ready;
      const key = base64UrlToBytes(this.settings.publicKey);
      let subscription = await registration.pushManager.getSubscription();
      if (subscription) {
        // Khóa server đổi → đăng ký cũ không dùng được
        const current = subscription.options?.applicationServerKey;
        const same = current && new Uint8Array(current).every((b, i) => b === key[i]);
        if (!same) {
          await subscription.unsubscribe();
          subscription = null;
        }
      }
      subscription ??= await registration.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: key });
      await send(`${API}/subscriptions`, 'POST', subscription.toJSON());
      this.subscribed = true;
    },

    async enable() {
      await this.run(async () => {
        this.permission = await Notification.requestPermission();
        if (this.permission !== 'granted') {
          throw new Error('Bạn chưa cho phép thông báo. Hãy bật quyền thông báo cho trang này trong cài đặt trình duyệt.');
        }
        await this.subscribeDevice();
        await send(API, 'PUT', { enabled: true, time: this.settings.time });
        this.settings.enabled = true;
        this.notice = `Đã bật nhắc học lúc ${this.settings.time} mỗi ngày (nếu hôm đó bạn chưa học).`;
      });
    },

    async disable() {
      await this.run(async () => {
        await send(API, 'PUT', { enabled: false, time: this.settings.time });
        this.settings.enabled = false;
        const registration = await navigator.serviceWorker.getRegistration(SW_URL);
        const subscription = await registration?.pushManager.getSubscription();
        if (subscription) {
          await send(`${API}/subscriptions/remove`, 'POST', { endpoint: subscription.endpoint });
          await subscription.unsubscribe();
        }
        this.subscribed = false;
        this.notice = 'Đã tắt nhắc học.';
      });
    },

    async changeTime(time) {
      const previous = this.settings.time;
      this.settings.time = time;
      await this.run(async () => {
        try {
          await send(API, 'PUT', { enabled: this.settings.enabled, time });
          this.notice = `Đã đổi giờ nhắc sang ${time}.`;
        } catch (error) {
          this.settings.time = previous;
          throw error;
        }
      });
    },

    async test() {
      await this.run(async () => {
        const result = await send(`${API}/test`, 'POST');
        this.notice = result.sent > 0
          ? `Đã gửi thông báo thử tới ${result.sent} thiết bị.`
          : 'Chưa gửi được — hãy tắt rồi bật lại nhắc học trên thiết bị này.';
      });
    },

    async run(action) {
      this.busy = true;
      this.error = null;
      this.notice = null;
      try {
        await action();
      } catch (error) {
        this.error = error instanceof Error && error.message && !/^\d+$/.test(error.message)
          ? error.message
          : 'Có lỗi xảy ra. Bạn thử lại nhé.';
      } finally {
        this.busy = false;
      }
    },
  }));
});

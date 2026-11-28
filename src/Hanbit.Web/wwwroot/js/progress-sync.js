// Đồng bộ tiến độ với tài khoản (T8.3) — chỉ nạp khi đã đăng nhập (_Layout).
// localStorage vẫn là bản làm việc (progress-store.js); server giữ bản chính của tài khoản và GỘP (ProgressMerger):
//   - Lần đầu đăng nhập trên máy có tiến độ khách → gộp toàn bộ vào tài khoản (baseXp = 0).
//   - Sau mỗi thay đổi (bài, ôn thẻ…) → gửi lại; server chỉ cộng phần XP kiếm thêm từ lần trước (baseXp).
//   - Tiến độ trên máy của tài khoản KHÁC → sao lưu sang khóa backup, lấy tiến độ của tài khoản đang đăng nhập.
//   - Đăng xuất → đồng bộ lần cuối rồi xóa tiến độ trên máy (vẫn còn trên tài khoản).
import { apiFetch } from './api.js';
import { clearSavedPages } from './offline-cache.js';
import {
  backupCurrent, CHANGED_EVENT, emptyProgress, forgetAccountProgress, load, loadSyncState, reset, save, saveSyncState,
} from './progress-store.js';

export const SYNC_URL = '/api/progress/sync';
export const SYNCED_EVENT = 'hanbit:progress-synced';
const DEBOUNCE_MS = 1000;

/** Tài khoản đang đăng nhập (meta do _Layout render), null nếu khách. */
export function currentUserId() {
  return document.querySelector('meta[name="hanbit-user"]')?.getAttribute('content') || null;
}

/** Tiến độ trống (chưa học gì) — không cần gửi lên để gộp. */
export function isEmptyProgress(p) {
  return Object.keys(p.lessons).length === 0 && Object.keys(p.cards).length === 0 && p.activity.length === 0 && p.totalXp === 0;
}

/**
 * Quyết định lần đồng bộ (hàm thuần).
 * @returns {{ kind: 'adopt' | 'sync' | 'switch', body: object }}
 *   adopt = gộp tiến độ khách vào tài khoản; sync = đồng bộ thường; switch = máy đang giữ tiến độ của tài khoản khác.
 */
export function planSync(local, state, userId) {
  if (state.userId && state.userId !== userId) {
    return { kind: 'switch', body: { progress: null } };
  }
  if (!state.userId) {
    return { kind: 'adopt', body: isEmptyProgress(local) ? { progress: null } : { progress: local, baseXp: 0, mode: 'merge' } };
  }
  return {
    kind: 'sync',
    body: { progress: local, baseXp: Math.max(0, state.baseXp ?? 0), mode: state.replace ? 'replace' : 'merge' },
  };
}

let applying = false; // đang ghi bản từ server → bỏ qua sự kiện thay đổi do chính mình
let dirty = false; // có thay đổi trên máy trong lúc đang gửi
let inFlight = null;
let timer = null;

function applyServer(progress) {
  applying = true;
  try {
    reset();
    save(progress ?? emptyProgress()); // bản server đã được làm sạch
  } finally {
    applying = false;
  }
}

/** Đồng bộ ngay; trả về true nếu thành công. Gọi chồng nhau → chờ lần đang chạy rồi chạy thêm một lần. */
export async function syncNow() {
  const userId = currentUserId();
  if (!userId) {
    return false;
  }
  if (inFlight) {
    dirty = true;
    return inFlight;
  }

  inFlight = (async () => {
    dirty = false;
    const local = load();
    const plan = planSync(local, loadSyncState(), userId);
    try {
      const response = await apiFetch(SYNC_URL, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(plan.body),
      });
      if (!response.ok) {
        throw new Error(String(response.status));
      }
      const { progress } = await response.json();
      const syncedAt = new Date().toISOString();

      if (dirty && plan.kind === 'sync') {
        // Có thay đổi mới trong lúc gửi: server đã nhận phần trước → lấy XP đã gửi làm mốc, gửi tiếp (không ghi đè máy).
        saveSyncState({ userId, baseXp: local.totalXp, syncedAt });
      } else {
        if (plan.kind === 'switch') {
          backupCurrent();
        }
        if (progress || plan.kind !== 'adopt') {
          applyServer(progress);
        }
        saveSyncState({ userId, baseXp: load().totalXp, syncedAt });
      }

      window.dispatchEvent(new CustomEvent(SYNCED_EVENT, { detail: { kind: plan.kind, syncedAt } }));
      if (plan.kind === 'adopt' && plan.body.progress) {
        window.Alpine?.store('toast')?.show('Đã lưu tiến độ trên máy này vào tài khoản của bạn.', 'success');
      }
      return true;
    } catch {
      return false; // mất mạng / server lỗi → giữ nguyên, lần thay đổi sau hoặc lần mở trang sau gửi lại
    } finally {
      inFlight = null;
      if (dirty) {
        schedule();
      }
    }
  })();
  return inFlight;
}

function schedule() {
  clearTimeout(timer);
  timer = setTimeout(() => syncNow(), DEBOUNCE_MS);
}

/** Đăng xuất: đồng bộ lần cuối; thành công → xóa tiến độ trên máy. Lỗi → giữ (gắn tài khoản, lần đăng nhập sau gửi tiếp). */
export async function syncBeforeLogout() {
  clearTimeout(timer);
  const ok = await Promise.race([syncNow(), new Promise((resolve) => setTimeout(() => resolve(false), 4000))]);
  if (ok) {
    applying = true;
    try {
      forgetAccountProgress();
    } finally {
      applying = false;
    }
  }
  return ok;
}

if (currentUserId()) {
  window.addEventListener(CHANGED_EVENT, () => {
    if (!applying) {
      if (inFlight) {
        dirty = true;
      } else {
        schedule();
      }
    }
  });

  document.addEventListener('submit', async (event) => {
    const form = event.target;
    if (!(form instanceof HTMLFormElement) || !form.matches('[data-logout]') || form.dataset.synced === '1') {
      return;
    }
    event.preventDefault();
    await syncBeforeLogout();
    await clearSavedPages().catch(() => {}); // trang đã lưu có tên/email của tài khoản (giai đoạn 10)
    form.dataset.synced = '1';
    form.submit();
  });

  syncNow();
}

// progress-store.js (T5.3) — NƠI DUY NHẤT đọc/ghi localStorage (RULES §5).
// Tiến độ chế độ khách: server tính toán (XP, streak, SRS), trình duyệt chỉ lưu và gộp kết quả API (ARCHITECTURE §6).

export const STORAGE_KEY = 'hanbit.progress.v1';
export const SETTINGS_KEY = 'hanbit.settings.v1';
export const BACKUP_PREFIX = 'hanbit.progress.backup.';
export const SCHEMA_VERSION = 1;
export const CHANGED_EVENT = 'hanbit:progress-changed';

let memoryFallback = null; // localStorage bị chặn/đầy → giữ trong bộ nhớ trang
let cached = null;
let lastIssue = null; // 'corrupt' | 'version' | 'unavailable' | null

/** Tiến độ trống (giống LearnerProgress.Empty ở C#). */
export function emptyProgress(now = new Date()) {
  return { schemaVersion: SCHEMA_VERSION, createdAt: now.toISOString(), lessons: {}, cards: {}, activity: [], totalXp: 0 };
}

const isObject = (v) => v !== null && typeof v === 'object' && !Array.isArray(v);

/** Kiểm tra cấu trúc tối thiểu — giá trị chi tiết do server validate khi nhập file (T5.5). */
export function isValidProgress(value) {
  return isObject(value)
    && value.schemaVersion === SCHEMA_VERSION
    && typeof value.createdAt === 'string'
    && isObject(value.lessons)
    && isObject(value.cards)
    && Array.isArray(value.activity)
    && Number.isFinite(value.totalXp);
}

function storage() {
  try {
    const s = window.localStorage;
    const probe = '__hanbit_probe__';
    s.setItem(probe, '1');
    s.removeItem(probe);
    return s;
  } catch {
    lastIssue = 'unavailable';
    return null;
  }
}

function backup(raw) {
  try {
    window.localStorage.setItem(`${BACKUP_PREFIX}${Date.now()}`, raw);
  } catch {
    // không sao lưu được → bỏ qua, vẫn không làm hỏng trang
  }
}

/**
 * Đọc tiến độ. JSON hỏng hoặc khác SchemaVersion → cất bản gốc vào khóa sao lưu, dùng tiến độ trống,
 * <see cref="issue"/> cho biết lý do (trang hiện thông báo). Không bao giờ ném lỗi.
 */
export function load() {
  if (cached) {
    return cached;
  }

  const s = storage();
  if (!s) {
    cached = memoryFallback ?? emptyProgress();
    return cached;
  }

  const raw = s.getItem(STORAGE_KEY);
  if (raw === null) {
    cached = emptyProgress();
    return cached;
  }

  try {
    const parsed = JSON.parse(raw);
    if (isValidProgress(parsed)) {
      cached = parsed;
      return cached;
    }
    lastIssue = isObject(parsed) && parsed.schemaVersion !== SCHEMA_VERSION ? 'version' : 'corrupt';
  } catch {
    lastIssue = 'corrupt';
  }

  backup(raw);
  cached = emptyProgress();
  save(cached);
  return cached;
}

/** Ghi tiến độ + phát sự kiện thay đổi. Trả về false nếu chỉ lưu được trong bộ nhớ. */
export function save(progress) {
  cached = progress;
  let persisted = false;
  const s = storage();
  if (s) {
    try {
      s.setItem(STORAGE_KEY, JSON.stringify(progress));
      persisted = true;
    } catch {
      lastIssue = 'unavailable';
    }
  }
  if (!persisted) {
    memoryFallback = progress;
  }
  window.dispatchEvent(new CustomEvent(CHANGED_EVENT, { detail: progress }));
  return persisted;
}

/** Lý do lần đọc gần nhất phải bỏ dữ liệu cũ / không lưu được: 'corrupt' | 'version' | 'unavailable' | null. */
export function issue() {
  return lastIssue;
}

/** Xóa bộ nhớ đệm (test / sau khi nhập file). */
export function reset() {
  cached = null;
  lastIssue = null;
}

/**
 * Gộp kết quả POST /api/lessons/{c}/{l}/complete: bản ghi bài, thẻ mới (ghi đè theo itemId),
 * hoạt động (thay toàn bộ), cộng XP. Hàm thuần — không ghi storage.
 */
export function mergeLessonResult(progress, result) {
  const cards = { ...progress.cards };
  for (const card of result.newCards ?? []) {
    cards[card.itemId] = card;
  }
  return {
    ...progress,
    lessons: { ...progress.lessons, [result.lessonKey]: result.lessonRecord },
    cards,
    activity: result.activity ?? progress.activity,
    totalXp: progress.totalXp + Math.max(0, result.xp ?? 0),
  };
}

/** Gộp + lưu kết quả hoàn thành bài; trả về tiến độ mới. */
export function applyLessonResult(result) {
  const next = mergeLessonResult(load(), result);
  save(next);
  return next;
}

/** Tập key "course/lesson" đã hoàn thành. */
export function completedLessons(progress = load()) {
  return new Set(Object.entries(progress.lessons).filter(([, r]) => r?.status === 'completed').map(([k]) => k));
}

/** Phần tiến độ gửi kèm khi hoàn thành bài (server không lưu gì). */
export function lessonContext(lessonKey, progress = load()) {
  return {
    previous: progress.lessons[lessonKey] ?? null,
    cards: Object.values(progress.cards),
    activity: progress.activity,
  };
}

// ── Cài đặt (chế độ sáng/tối) — chốt 2026-11-23: lưu qua progress-store.js ──

export function loadSettings() {
  const s = storage();
  try {
    const parsed = JSON.parse(s?.getItem(SETTINGS_KEY) ?? '{}');
    return isObject(parsed) ? parsed : {};
  } catch {
    return {};
  }
}

export function saveSettings(patch) {
  const next = { ...loadSettings(), ...patch };
  try {
    storage()?.setItem(SETTINGS_KEY, JSON.stringify(next));
  } catch {
    // bỏ qua — chỉ mất lựa chọn giao diện
  }
  return next;
}

// Chế độ giao diện: "system" (theo hệ điều hành), "light", "dark" → thuộc tính data-theme trên <html>.
// Lựa chọn chưa được lưu lại giữa các lần tải trang (xem câu hỏi trong docs/PROGRESS.md).

export const THEME_MODES = ['system', 'light', 'dark'];

export function currentTheme() {
  const theme = document.documentElement.dataset.theme;
  return theme === 'light' || theme === 'dark' ? theme : 'system';
}

export function applyTheme(mode) {
  const root = document.documentElement;
  if (mode === 'light' || mode === 'dark') {
    root.dataset.theme = mode;
  } else {
    delete root.dataset.theme;
  }
  window.dispatchEvent(new CustomEvent('theme-changed', { detail: { mode } }));
}

document.addEventListener('alpine:init', () => {
  window.Alpine.data('themeSwitcher', () => ({
    mode: currentTheme(),
    select(mode) {
      this.mode = mode;
      applyTheme(mode);
    },
  }));
});

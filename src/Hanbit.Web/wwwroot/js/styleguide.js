// Trang /styleguide: hiển thị giá trị thực tế của token màu (đổi theo sáng/tối).

document.addEventListener('alpine:init', () => {
  window.Alpine.data('tokenValue', (name) => ({
    value: '',
    init() {
      this.read();
      window.addEventListener('theme-changed', () => this.read());
      window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', () => this.read());
    },
    read() {
      this.value = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
    },
  }));
});

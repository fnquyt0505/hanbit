// Bảng ghép âm (T3.3): chọn phụ âm đầu + nguyên âm (+ phụ âm cuối) → server ghép qua GET /api/hangul/compose.
import { speak } from './speech.js';

document.addEventListener('alpine:init', () => {
  window.Alpine.data('hangulComposer', (initial) => ({
    c: initial.c,
    v: initial.v,
    f: initial.f ?? '',
    syllable: initial.syllable,
    romanization: initial.romanization,
    error: null,
    loading: false,
    controller: null,

    get formula() {
      return [this.c, this.v, this.f].filter(Boolean).join(' + ');
    },

    select(part, jamo) {
      this[part] = part === 'f' && this.f === jamo ? '' : jamo; // bấm lại phụ âm cuối đang chọn → bỏ chọn
      this.compose();
    },

    async compose() {
      this.controller?.abort();
      const controller = new AbortController();
      this.controller = controller;
      this.loading = true;
      const params = new URLSearchParams({ c: this.c, v: this.v });
      if (this.f) {
        params.set('f', this.f);
      }

      try {
        const response = await fetch(`/api/hangul/compose?${params}`, { signal: controller.signal });
        const body = await response.json();
        if (!response.ok) {
          this.error = Object.values(body.errors ?? {}).flat()[0] ?? 'Không ghép được.';
          return;
        }
        this.error = null;
        this.syllable = body.syllable;
        this.romanization = body.romanization;
      } catch (e) {
        if (e.name !== 'AbortError') {
          this.error = 'Không kết nối được máy chủ. Bạn thử lại nhé.';
        }
      } finally {
        if (this.controller === controller) {
          this.loading = false;
        }
      }
    },

    async listen() {
      try {
        await speak(this.syllable);
      } catch {
        window.Alpine.store('toast')?.show('Thiết bị chưa đọc được tiếng Hàn.', 'danger');
      }
    },
  }));
});

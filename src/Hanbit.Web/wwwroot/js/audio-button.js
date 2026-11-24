// Alpine component cho Tag Helper <audio-button>: bấm để nghe, trạng thái đang phát, báo lỗi bằng toast.
import { speak } from './speech.js';

const ERROR_MESSAGES = {
  'no-voice': 'Thiết bị chưa có giọng đọc tiếng Hàn. Hãy cài giọng "Tiếng Hàn" trong cài đặt chuyển văn bản thành giọng nói.',
  unsupported: 'Trình duyệt này không hỗ trợ phát âm. Hãy thử Chrome, Edge hoặc Safari bản mới.',
  failed: 'Không phát âm được. Bạn thử lại nhé.',
};

document.addEventListener('alpine:init', () => {
  window.Alpine.data('audioButton', ({ text, audio = null }) => ({
    playing: null, // null | 'normal' | 'slow'
    run: 0, // lần phát mới nhất — lần cũ bị thay thế không được xóa trạng thái của lần mới
    async play(slow = false) {
      const run = ++this.run;
      this.playing = slow ? 'slow' : 'normal';
      try {
        await speak(text, { slow, audioUrl: audio });
      } catch (error) {
        window.Alpine.store('toast')?.show(ERROR_MESSAGES[error?.code] ?? ERROR_MESSAGES.failed, 'danger', 6000);
      } finally {
        if (run === this.run) {
          this.playing = null;
        }
      }
    },
  }));
});

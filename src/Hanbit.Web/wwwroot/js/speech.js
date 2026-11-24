// Phát âm tiếng Hàn bằng Web Speech API (giọng ko-KR), dự phòng file MP3 khi có (ARCHITECTURE: Audio).
// Module thuần (không phụ thuộc Alpine) — dùng bởi audio-button.js và các màn học sau này.

export const LANG = 'ko-KR';

/** Tốc độ đọc: thường và chậm (DESIGN.md §5 — AudioButton hỗ trợ 0.7x). */
export const RATES = Object.freeze({ normal: 1, slow: 0.7 });

const VOICES_TIMEOUT_MS = 1500;

let voicesPromise = null;

/** Trình duyệt có Web Speech API (đọc) không. */
export function isSupported() {
  return typeof window !== 'undefined'
    && 'speechSynthesis' in window
    && typeof window.SpeechSynthesisUtterance === 'function';
}

/**
 * Danh sách giọng — nhiều trình duyệt (Chrome, Android) nạp bất đồng bộ: chờ sự kiện voiceschanged,
 * tối đa VOICES_TIMEOUT_MS rồi trả về những gì đang có.
 */
export function loadVoices() {
  if (!isSupported()) {
    return Promise.resolve([]);
  }

  const current = window.speechSynthesis.getVoices();
  if (current.length > 0) {
    return Promise.resolve(current);
  }

  voicesPromise ??= new Promise((resolve) => {
    const done = () => {
      window.speechSynthesis.removeEventListener?.('voiceschanged', done);
      resolve(window.speechSynthesis.getVoices());
    };
    window.speechSynthesis.addEventListener?.('voiceschanged', done);
    setTimeout(done, VOICES_TIMEOUT_MS);
  }).finally(() => {
    voicesPromise = null;
  });
  return voicesPromise;
}

const isKorean = (v) => (v.lang ?? '').replace('_', '-').toLowerCase().startsWith('ko');

/**
 * Điểm chất lượng giọng: giọng neural/online (Edge "Microsoft SunHi Online (Natural)", Chrome "Google 한국의")
 * tự nhiên hơn hẳn giọng offline cũ của Windows (VD "Microsoft Heami") — người dùng báo giọng máy tính khó nghe (câu hỏi 12).
 */
export function voiceScore(voice) {
  const name = voice.name ?? '';
  let score = 0;
  if ((voice.lang ?? '').replace('_', '-') === LANG) score += 10;
  if (/natural|neural/i.test(name)) score += 8;
  if (/online/i.test(name)) score += 4;
  if (/google/i.test(name)) score += 3;
  return score;
}

/** Giọng tiếng Hàn tốt nhất theo <see cref="voiceScore"/> (cùng điểm → giữ thứ tự trình duyệt trả về). */
export function pickKoreanVoice(voices) {
  return voices.filter(isKorean).reduce((best, v) => (!best || voiceScore(v) > voiceScore(best) ? v : best), null);
}

/** Giọng Hàn cục bộ (dùng được khi mất mạng) — dự phòng khi giọng online lỗi. */
function pickLocalKoreanVoice(voices) {
  return pickKoreanVoice(voices.filter((v) => v.localService));
}

/**
 * Tình trạng phát âm trên thiết bị: 'ok' | 'no-voice' (có API nhưng không có giọng Hàn) | 'unsupported'.
 * Dùng để hiện hướng dẫn cài giọng đọc.
 */
export async function speechStatus() {
  if (!isSupported()) {
    return 'unsupported';
  }
  return pickKoreanVoice(await loadVoices()) ? 'ok' : 'no-voice';
}

/** Lỗi phát âm có mã để giao diện hiện thông báo phù hợp. */
export class SpeechError extends Error {
  constructor(code, message) {
    super(message);
    this.name = 'SpeechError';
    this.code = code; // 'unsupported' | 'no-voice' | 'failed'
  }
}

function playAudio(url, rate) {
  return new Promise((resolve, reject) => {
    const audio = new Audio(url);
    audio.playbackRate = rate;
    audio.addEventListener('ended', () => resolve(), { once: true });
    audio.addEventListener('error', () => reject(new SpeechError('failed', 'Không phát được file âm thanh.')), { once: true });
    audio.play().catch(() => reject(new SpeechError('failed', 'Không phát được file âm thanh.')));
  });
}

/**
 * Đọc <paramref name="text"/> bằng tiếng Hàn. Trả về Promise hoàn tất khi đọc xong.
 * @param {string} text
 * @param {{ slow?: boolean, audioUrl?: string | null }} [options] audioUrl: MP3 dự phòng khi không có giọng ko-KR.
 */
export async function speak(text, { slow = false, audioUrl = null } = {}) {
  const rate = slow ? RATES.slow : RATES.normal;
  const voices = isSupported() ? await loadVoices() : [];
  const voice = pickKoreanVoice(voices);

  if (!voice) {
    if (audioUrl) {
      return playAudio(audioUrl, rate);
    }
    throw isSupported()
      ? new SpeechError('no-voice', 'Thiết bị chưa có giọng đọc tiếng Hàn.')
      : new SpeechError('unsupported', 'Trình duyệt không hỗ trợ phát âm.');
  }

  window.speechSynthesis.cancel(); // dừng câu đang đọc (bấm liên tiếp)

  try {
    await utter(text, voice, rate);
  } catch (error) {
    // Giọng online (Natural/Google) lỗi, VD mất mạng → đọc lại bằng giọng cục bộ nếu có.
    const local = voice.localService ? null : pickLocalKoreanVoice(voices);
    if (!local) {
      throw error;
    }
    await utter(text, local, rate);
  }
}

function utter(text, voice, rate) {
  return new Promise((resolve, reject) => {
    const utterance = new window.SpeechSynthesisUtterance(text);
    utterance.lang = LANG;
    utterance.voice = voice;
    utterance.rate = rate;
    utterance.onend = () => resolve();
    utterance.onerror = (event) => {
      // 'interrupted'/'canceled': bị câu mới thay thế → không coi là lỗi.
      if (event.error === 'interrupted' || event.error === 'canceled') {
        resolve();
      } else {
        reject(new SpeechError('failed', 'Không phát âm được.'));
      }
    };
    window.speechSynthesis.speak(utterance);
  });
}

/** Dừng mọi câu đang đọc. */
export function stop() {
  if (isSupported()) {
    window.speechSynthesis.cancel();
  }
}

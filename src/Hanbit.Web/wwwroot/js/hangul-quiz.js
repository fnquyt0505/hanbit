// Trang /hangul/{group} (T3.4): học từng chữ (stepper) → quiz 10 câu, đề + chấm qua API (đáp án không nằm trong HTML/JSON đề).
import { apiFetch } from './api.js';
import { speak } from './speech.js';
import { applyLessonResult, load } from './progress-store.js';

document.addEventListener('alpine:init', () => {
  window.Alpine.data('hangulLesson', ({ group, letterCount }) => ({
    phase: 'learn', // learn | quiz | done
    index: 0,

    // quiz
    questions: [],
    current: 0,
    selected: null,
    result: null, // { correct, correctAnswer, letter, romanization }
    score: 0,
    answers: [], // { kind, prompt, answer } — gửi lại server chấm khi hoàn thành
    completion: null,
    saved: null, // null | 'saving' | 'saved' | 'failed'
    loading: false,
    error: null,

    get question() {
      return this.questions[this.current] ?? null;
    },
    get isLastLetter() {
      return this.index === letterCount - 1;
    },
    get isListen() {
      return this.question?.kind === 'listen-to-char';
    },
    get promptLabel() {
      return {
        'char-to-romanization': 'Chữ này đọc là gì?',
        'romanization-to-char': 'Chữ nào có phiên âm này?',
        'listen-to-char': 'Nghe và chọn chữ đúng',
      }[this.question?.kind] ?? '';
    },

    prev() {
      this.index = Math.max(0, this.index - 1);
    },
    next() {
      this.index = Math.min(letterCount - 1, this.index + 1);
    },

    async startQuiz() {
      this.loading = true;
      this.error = null;
      try {
        const response = await apiFetch(`/api/hangul/quiz/${encodeURIComponent(group)}`);
        if (!response.ok) {
          throw new Error(String(response.status));
        }
        const body = await response.json();
        this.questions = body.questions;
        this.current = 0;
        this.score = 0;
        this.answers = [];
        this.completion = null;
        this.saved = null;
        this.selected = null;
        this.result = null;
        this.phase = 'quiz';
        this.$nextTick(() => this.$refs.quiz?.focus());
        if (this.isListen) {
          this.listen();
        }
      } catch {
        this.error = 'Không tải được bài kiểm tra. Bạn thử lại nhé.';
      } finally {
        this.loading = false;
      }
    },

    async choose(choice) {
      if (this.result || this.loading) {
        return;
      }
      this.selected = choice;
      this.loading = true;
      this.error = null;
      try {
        const q = this.question;
        const response = await apiFetch('/api/hangul/quiz/check', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ group, kind: q.kind, prompt: q.prompt, answer: choice }),
        });
        if (!response.ok) {
          throw new Error(String(response.status));
        }
        this.result = await response.json();
        this.answers.push({ kind: q.kind, prompt: q.prompt, answer: choice });
        if (this.result.correct) {
          this.score++;
        }
      } catch {
        this.selected = null;
        this.error = 'Không chấm được câu này. Bạn thử lại nhé.';
      } finally {
        this.loading = false;
      }
    },

    continueQuiz() {
      if (this.current + 1 >= this.questions.length) {
        this.phase = 'done';
        this.saveProgress();
        return;
      }
      this.current++;
      this.selected = null;
      this.result = null;
      if (this.isListen) {
        this.listen();
      }
    },

    async listen(slow = false) {
      if (!this.isListen) {
        return;
      }
      try {
        await speak(this.question.prompt, { slow });
      } catch {
        window.Alpine.store('toast')?.show('Thiết bị chưa đọc được tiếng Hàn — hãy chọn theo trí nhớ nhé.', 'danger', 6000);
      }
    },

    choiceState(choice) {
      if (!this.result) {
        return this.selected === choice ? 'selected' : 'idle';
      }
      if (choice === this.result.correctAnswer) {
        return 'correct';
      }
      return choice === this.selected ? 'wrong' : 'idle';
    },

    // Lưu tiến độ (T5.4): server chấm lại + tính XP/streak, progress-store lưu.
    async saveProgress() {
      this.saved = 'saving';
      const progress = load();
      try {
        const response = await apiFetch('/api/hangul/quiz/complete', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            group,
            answers: this.answers,
            previous: progress.lessons[`hangul/${group}`] ?? null,
            activity: progress.activity,
          }),
        });
        if (!response.ok) {
          throw new Error(String(response.status));
        }
        this.completion = await response.json();
        applyLessonResult(this.completion);
        this.saved = 'saved';
      } catch {
        this.saved = 'failed';
      }
    },

    restart() {
      this.startQuiz();
    },
  }));
});

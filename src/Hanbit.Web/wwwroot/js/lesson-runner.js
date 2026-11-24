// Màn học (T4.4): từ vựng → ngữ pháp → bài tập (chấm qua POST /api/exercises/check) → kết thúc.
// Dữ liệu: <script type="application/json" id="lesson-data"> (bài tập KHÔNG đáp án).
import { speak } from './speech.js';

/** Loại bài tập màn học hỗ trợ hiển thị. */
export const SUPPORTED_TYPES = ['multiple_choice', 'listen_choose', 'match_pairs', 'reorder', 'type_answer'];

document.addEventListener('alpine:init', () => {
  window.Alpine.data('lessonRunner', () => {
    const data = JSON.parse(document.getElementById('lesson-data').textContent);
    const exercises = data.exercises.filter((e) => SUPPORTED_TYPES.includes(e.type));

    return {
      course: data.course,
      lesson: data.lesson,
      vocabCount: data.vocabCount,
      grammarCount: data.grammarCount,
      exercises,
      phase: data.vocabCount > 0 ? 'vocab' : data.grammarCount > 0 ? 'grammar' : 'exercises',
      vocabIndex: 0,
      grammarIndex: 0,
      exIndex: 0,
      selected: null,
      result: null,
      answers: [], // { exerciseId, correct }
      loading: false,
      error: null,

      get exercise() {
        return this.exercises[this.exIndex] ?? null;
      },
      get totalSteps() {
        return this.vocabCount + this.grammarCount + this.exercises.length;
      },
      get doneSteps() {
        switch (this.phase) {
          case 'vocab': return this.vocabIndex;
          case 'grammar': return this.vocabCount + this.grammarIndex;
          case 'exercises': return this.vocabCount + this.grammarCount + this.exIndex + (this.result ? 1 : 0);
          default: return this.totalSteps;
        }
      },
      get percent() {
        return this.totalSteps ? Math.round((this.doneSteps / this.totalSteps) * 100) : 100;
      },
      get correctCount() {
        return this.answers.filter((a) => a.correct).length;
      },

      // ── Từ vựng & ngữ pháp ──
      nextVocab() {
        if (this.vocabIndex < this.vocabCount - 1) {
          this.vocabIndex++;
        } else {
          this.goTo(this.grammarCount > 0 ? 'grammar' : 'exercises');
        }
      },
      prevVocab() {
        this.vocabIndex = Math.max(0, this.vocabIndex - 1);
      },
      nextGrammar() {
        if (this.grammarIndex < this.grammarCount - 1) {
          this.grammarIndex++;
        } else {
          this.goTo('exercises');
        }
      },
      prevGrammar() {
        if (this.grammarIndex > 0) {
          this.grammarIndex--;
        } else if (this.vocabCount > 0) {
          this.phase = 'vocab';
        }
      },
      goTo(phase) {
        this.phase = phase === 'exercises' && this.exercises.length === 0 ? 'done' : phase;
        if (this.phase === 'exercises') {
          this.onExerciseShown();
        }
        window.scrollTo({ top: 0 });
      },

      // ── Bài tập ──
      onExerciseShown() {
        if (this.exercise?.type === 'listen_choose') {
          this.listen();
        }
      },
      async listen(slow = false) {
        const text = this.exercise?.audioKo;
        if (!text) {
          return;
        }
        try {
          await speak(text, { slow });
        } catch {
          window.Alpine.store('toast')?.show('Thiết bị chưa đọc được tiếng Hàn — hãy chọn theo trí nhớ nhé.', 'danger', 6000);
        }
      },
      choose(index) {
        if (this.result || this.loading) {
          return;
        }
        this.selected = index;
        this.submit({ index });
      },
      async submit(answer) {
        this.loading = true;
        this.error = null;
        try {
          const response = await fetch('/api/exercises/check', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ course: this.course, lesson: this.lesson, exerciseId: this.exercise.id, answer }),
          });
          if (!response.ok) {
            throw new Error(String(response.status));
          }
          this.result = await response.json();
          this.answers.push({ exerciseId: this.exercise.id, correct: this.result.correct });
        } catch {
          this.selected = null;
          this.error = 'Không chấm được câu này. Bạn thử lại nhé.';
        } finally {
          this.loading = false;
        }
      },
      choiceState(index) {
        if (!this.result) {
          return this.selected === index ? 'selected' : 'idle';
        }
        if (this.exercise.choices[index] === this.result.correctAnswer) {
          return 'correct';
        }
        return this.selected === index ? 'wrong' : 'idle';
      },
      // ── match_pairs ──
      matchLeft: null,
      pairs: [], // [{ left, right }]
      pairNumber(side, value) {
        return this.pairs.findIndex((p) => p[side] === value) + 1;
      },
      tapLeft(value) {
        if (this.result || this.loading) return;
        const paired = this.pairNumber('left', value);
        if (paired) {
          this.pairs.splice(paired - 1, 1);
          return;
        }
        this.matchLeft = this.matchLeft === value ? null : value;
      },
      tapRight(value) {
        if (this.result || this.loading) return;
        const paired = this.pairNumber('right', value);
        if (paired) {
          this.pairs.splice(paired - 1, 1);
          return;
        }
        if (this.matchLeft !== null) {
          this.pairs.push({ left: this.matchLeft, right: value });
          this.matchLeft = null;
        }
      },
      get matchComplete() {
        return this.exercise?.left && this.pairs.length === this.exercise.left.length;
      },
      submitMatch() {
        if (this.matchComplete) this.submit({ pairs: this.pairs });
      },

      // ── reorder ──
      order: [], // chỉ số trong exercise.tokens theo thứ tự đã chọn
      pickToken(i) {
        if (!this.result && !this.loading && !this.order.includes(i)) this.order.push(i);
      },
      unpickToken(position) {
        if (!this.result && !this.loading) this.order.splice(position, 1);
      },
      get orderComplete() {
        return this.exercise?.tokens && this.order.length === this.exercise.tokens.length;
      },
      submitOrder() {
        if (this.orderComplete) this.submit({ tokens: this.order.map((i) => this.exercise.tokens[i]) });
      },

      // ── type_answer ──
      text: '',
      submitText() {
        if (this.text.trim() && !this.result && !this.loading) this.submit({ text: this.text });
      },

      next() {
        if (this.exIndex >= this.exercises.length - 1) {
          this.phase = 'done';
          return;
        }
        this.exIndex++;
        this.selected = null;
        this.result = null;
        this.matchLeft = null;
        this.pairs = [];
        this.order = [];
        this.text = '';
        this.onExerciseShown();
      },
    };
  });
});

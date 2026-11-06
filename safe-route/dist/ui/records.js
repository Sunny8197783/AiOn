// Challenges and records: every challenge with the day it was first done (or how to do it), the top-5 runs of
// this browser and the best time per chapter.
import { el, modal, icon, btn } from './dom.js';
import { CHAPTERS } from './menu.js';
import { CHALLENGES, TIMED, fmtMs } from '../game/challenges.js';
import { ENDINGS } from '../game/endings.js';

// records(rec: readRecords(...)) -> Promise
export function records(rec = { done: {}, best: {}, runs: [] }) {
  const n = CHALLENGES.filter((c) => rec.done[c.id]).length;
  const content = el('div', { class: 'about-panel gallery-panel' },
    el('header', { class: 'nb-head' }, el('p', { class: 'eyebrow' }, icon('check'), '도전 과제와 기록'), el('h2', {}, `도전 과제 ${n}/${CHALLENGES.length}`)),
    el('div', { class: 'about-scroll', tabindex: '0', 'aria-label': '도전 과제와 기록' },
      el('ol', { class: 'gallery-list', 'aria-label': '도전 과제' }, CHALLENGES.map((c, i) => el('li', { class: rec.done[c.id] ? 'g-seen' : 'g-unseen' },
        el('span', { class: 'g-no' }, rec.done[c.id] ? '✓' : String(i + 1)),
        el('div', {}, el('b', {}, c.title), el('small', {}, rec.done[c.id] ? `처음 달성 ${rec.done[c.id]} · ${c.hint}` : c.hint))))),
      el('h3', {}, icon('spark'), '내 기록 순위'),
      rec.runs.length
        ? el('ol', { class: 'rank-list' }, rec.runs.map((r, i) => el('li', {},
          el('b', {}, `${i + 1}위`),
          el('span', {}, `엔딩 ${ENDINGS.find((e) => e.id === r.ending)?.no} · ${ENDINGS.find((e) => e.id === r.ending)?.title}`),
          el('small', {}, `과제 ${r.n}/${CHALLENGES.length} · 다시 하기 ${r.retries}번 · ${fmtMs(r.ms)} · ${r.at}`))))
        : el('p', { class: 'muted' }, '처음부터 엔딩까지 한 번 마치면 순위가 남습니다.'),
      el('h3', {}, icon('hourglass'), '장별 최고 기록'),
      el('ul', { class: 'rank-list' }, TIMED.map((id, i) => el('li', {},
        el('b', {}, CHAPTERS[i][0]), el('span', {}, CHAPTERS[i][1]), el('small', {}, rec.best[id] ? fmtMs(rec.best[id]) : '—')))),
      el('p', { class: 'set-note' }, '순위는 도전 과제가 많은 순, 다시 하기가 적은 순, 시간이 짧은 순입니다. 빠르기보다 안전이 먼저입니다. 기록은 이 브라우저에만 저장됩니다.')),
    el('div', { class: 'set-actions' }, btn('닫기', { primary: true, 'data-autofocus': true, onclick: () => m.close() })));
  const m = modal({ className: 'about', label: '도전 과제와 기록', content, onKey(e) { if (e.key === 'Escape') { e.preventDefault(); m.close(); } } });
  return m.done;
}

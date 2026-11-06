// Ending gallery: the 11 endings in order; seen ones show where you ended up and three deeds that led there,
// unseen ones only a hint about the choice that opens them.
import { el, modal, icon, btn } from './dom.js';

// gallery(view: galleryView(...)) -> Promise
export function gallery(view = []) {
  const seen = view.filter((e) => e.seen).length;
  const content = el('div', { class: 'about-panel gallery-panel' },
    el('header', { class: 'nb-head' }, el('p', { class: 'eyebrow' }, icon('book'), '엔딩 기록'), el('h2', {}, `연무동의 열한 가지 끝 · ${seen}/11`)),
    el('ol', { class: 'gallery-list about-scroll', tabindex: '0', 'aria-label': '엔딩 목록' }, view.map((e) => el('li', { class: e.seen ? 'g-seen' : 'g-unseen' },
      el('span', { class: 'g-no' }, String(e.no)),
      e.seen
        ? el('div', {}, el('b', {}, e.title), el('small', {}, `${e.place}${e.at ? ` · 처음 본 날 ${e.at}` : ''}`),
          e.shorts.length ? el('ul', { class: 'g-trace' }, e.shorts.map((s) => el('li', {}, s))) : null)
        : el('div', {}, el('b', { 'aria-label': '아직 보지 못한 엔딩' }, '???'), el('small', {}, e.hint))))),
    el('div', { class: 'set-actions' }, btn('닫기', { primary: true, 'data-autofocus': true, onclick: () => m.close() })));
  const m = modal({ className: 'about', label: '엔딩 기록', content, onKey(e) { if (e.key === 'Escape') { e.preventDefault(); m.close(); } } });
  return m.done;
}

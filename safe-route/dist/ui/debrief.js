// Soft-fail card, chapter summary card and the printable "생존 기록" ending document.
import { el, modal, icon, btn, uiSound } from './dom.js';
import { PREP } from '../game/ripples.js';

// Payoffs arrive with Korean labels already (game/debrief.js); a bare prep id still gets its name.
const prepName = (id) => PREP[id] || id || '준비';
// 나비효과 (game/ripples.js rows): "D-1 오전 · cause → D-day 저녁 · effect".
const MAX_RIPPLES = 5;
const sameMoment = (r, p) => r.prep === (p.id ?? p.prep) && r.effect.text === p.moment;
const rippleText = (r) => [el('span', { class: 'ripple-when' }, r.cause.when), ` ${r.cause.text}`, el('span', { class: 'ripple-arrow', 'aria-label': ', 그래서' }, ' → '),
  el('span', { class: 'ripple-when' }, r.effect.when), ` ${r.effect.text}`];
const lines = (v) => [].concat(v ?? []).filter((x) => x != null && x !== '');
const sourceList = (sources) => {
  const list = lines(sources);
  return list.length ? el('ul', { class: 'src-list' }, list.map((s) => el('li', {},
    s.url ? el('a', { href: s.url, target: '_blank', rel: 'noopener noreferrer' }, `${s.publisher ? `${s.publisher} · ` : ''}${s.title}`) : el('span', {}, `${s.publisher ? `${s.publisher} · ` : ''}${s.title ?? ''}`)))) : null;
};

// failCard({ title, seen, missed, next, sources }) -> Promise<'retry'|'menu'>
export function failCard({ title = '', seen = '', missed = '', next = '', sources = [] } = {}) {
  const row = (ic, label, v) => el('div', { class: 'fail-row' }, el('dt', {}, icon(ic), label), el('dd', {}, lines(v).map((t) => el('p', {}, t))));
  const t0 = performance.now();
  const go = (id) => () => { if (performance.now() - t0 > 400) { uiSound('confirm'); m.close(id); } };
  const content = el('article', { class: 'fail-card' },
    el('p', { class: 'eyebrow' }, icon('retry'), '다시 해 볼 수 있습니다'),
    el('h2', {}, title || '이번에는 여기까지였습니다'),
    el('p', { class: 'fail-lead' }, '실패는 벌이 아니라 정보입니다. 무엇을 알고 있었고 무엇이 비어 있었는지 확인해 보세요.'),
    el('dl', { class: 'fail-rows' },
      row('site', '당시 확인한 정보', seen),
      row('eye', '판단에서 놓친 부분', missed),
      row('spark', '다음에 시도할 준비', next)),
    lines(sources).length ? el('section', { class: 'fail-sources' }, el('h3', {}, icon('official'), '참고한 공식 자료'), sourceList(sources)) : null,
    el('footer', { class: 'card-actions' },
      btn('메인 메뉴', { ghost: true, iconName: 'exit', onclick: go('menu') }),
      btn('체크포인트에서 다시', { primary: true, iconName: 'retry', 'data-autofocus': true, onclick: go('retry') })));
  const m = modal({ className: 'fail', label: title || '다시 시도', content });
  return m.done;
}

// chapterCard({ chapter, title, lines, payoffs: [{ prep, moment, id }], ripples? }) -> Promise
// ripples (game/ripples.js, optional): a payoff that has one shows the earlier moment that set it up.
export function chapterCard({ chapter = '', title = '', lines: ls = [], payoffs = [], ripples = [] } = {}) {
  const t0 = performance.now();
  const pays = lines(payoffs), rows = lines(ripples);
  const cause = (p) => { const r = rows.find((x) => sameMoment(x, p)); return r ? el('small', { class: 'payoff-cause' }, `${r.cause.when} · ${r.cause.text}`) : null; };
  const content = el('article', { class: 'chapter-card' },
    el('p', { class: 'eyebrow' }, icon('check'), `${chapter} 끝`),
    el('h2', {}, title),
    pays.length ? el('section', { class: 'payoffs' },
      el('h3', {}, icon('spark'), '준비가 도움이 된 순간'),
      el('ul', {}, pays.map((p) => el('li', {}, el('b', {}, prepName(p.prep)), el('span', {}, p.moment || ''), cause(p))))) : null,
    el('section', { class: 'chapter-lines' },
      el('h3', {}, icon('book'), '이번 장의 기록'),
      lines(ls).length ? el('ul', {}, lines(ls).map((t) => el('li', {}, t))) : el('p', { class: 'muted' }, '기록할 것이 많지 않았습니다.')),
    el('footer', { class: 'card-actions' }, btn('다음으로', { primary: true, iconName: 'arrow', 'data-autofocus': true, onclick: () => { if (performance.now() - t0 > 400) m.close(); } })));
  const m = modal({ className: 'chapter', label: `${chapter} 정리 · ${title}`, content });
  uiSound('payoff');
  return m.done;
}

// ending(record: { fate, helped, unused, unchecked, people, ripples, practice }, sources) -> Promise<'restart'|'menu'>
export function ending(record = {}, sources = []) {
  const { fate = null, helped = [], unused = [], unchecked = [], people = [], ripples = [], practice = null } = record || {};
  const chain = lines(ripples).slice(0, MAX_RIPPLES), rest = lines(helped).filter((h) => !chain.some((r) => sameMoment(r, h)));
  const src = practice?.sourceId ? lines(sources).find((s) => s.id === practice.sourceId) : null;
  const section = (ic, title, items, render, empty) => el('section', { class: 'doc-section' },
    el('h3', {}, icon(ic), title),
    lines(items).length ? el('ul', {}, lines(items).map((x) => el('li', {}, render(x)))) : el('p', { class: 'muted' }, empty));
  const t0 = performance.now();
  const go = (id) => () => { if (performance.now() - t0 > 400) { uiSound('confirm'); m.close(id); } };
  const doc = el('article', { class: 'ending-doc' },
    el('header', { class: 'doc-head' },
      el('p', { class: 'doc-kicker' }, '연무동 · 한빛아파트 103동 302호 · 정민'),
      el('h2', {}, fate ? `엔딩 · ${fate.title}` : '생존 기록'),
      el('p', { class: 'doc-sub' }, '전쟁 직전의 준비가 전쟁 이후의 선택지를 어떻게 바꾸었는지 돌아봅니다.')),
    fate ? el('section', { class: 'doc-section doc-fate' }, lines(fate.lines).map((t) => el('p', {}, t))) : null,
    chain.length ? section('clock', '나비효과 · 작은 선택이 남긴 것', chain, (r) => [el('b', {}, r.label), el('span', {}, rippleText(r))], '') : null,
    chain.length && !rest.length ? null : section('spark', chain.length ? '그 밖에 도움이 된 준비' : '도움이 된 준비', rest, (h) => [el('b', {}, prepName(h.prep)), el('span', {}, h.moment || '')], '이번에는 미리 해 둔 준비가 큰 역할을 하지 못했습니다. 다음에는 하나만 먼저 해 보세요.'),
    section('box', '쓰지 못한 물건과 그 이유', unused, (u) => [el('b', {}, u.item), el('span', {}, u.why || '')], '가진 물건을 모두 쓸 수 있었습니다.'),
    section('rumor', '확인하지 못한 정보', unchecked, (t) => el('span', {}, t), '필요한 정보를 대부분 확인했습니다.'),
    section('people', '함께 버틴 사람들', people, (p) => [el('b', {}, p.who), el('span', {}, p.what || '')], '이번에는 혼자 버텼습니다. 이웃과의 연락 한 가지가 선택지를 넓힙니다.'),
    practice?.text ? el('section', { class: 'doc-section doc-practice' },
      el('h3', {}, icon('official'), '실제 생활에서 확인해 볼 한 가지'),
      el('p', { class: 'practice-text' }, practice.text),
      src ? el('p', { class: 'practice-src' }, '출처: ', src.url ? el('a', { href: src.url, target: '_blank', rel: 'noopener noreferrer' }, `${src.publisher} · ${src.title}`) : `${src.publisher} · ${src.title}`,
        src.checkedAt ? ` (확인 ${src.checkedAt})` : '') : null) : null,
    el('p', { class: 'doc-fiction' }, '이 기록은 가상의 이야기에서 나온 것입니다. 실제 상황에서는 재난방송·재난문자·지자체 안내를 따르세요.'));
  const content = el('div', { class: 'ending-wrap' }, doc,
    el('footer', { class: 'card-actions ending-actions' },
      btn('인쇄하기', { ghost: true, iconName: 'print', onclick: () => print() }),
      btn('메인 메뉴', { ghost: true, iconName: 'exit', onclick: go('menu') }),
      btn('처음부터 다시', { primary: true, iconName: 'retry', 'data-autofocus': true, onclick: go('restart') })));
  const m = modal({ className: 'ending', label: '생존 기록', content });
  uiSound('payoff');
  return m.done;
}

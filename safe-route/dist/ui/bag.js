// Packing one bag: two panes (집 비축 / 가방), click or keys move units, weight and volume meters, the goals
// checklist. guided shows item tips inline, hint behind a button, free shows nothing extra.
import { el, modal, header, ladderHelp, meterState, METER_WORD, fmtKg, announce } from './mini-dom.js';

const TITLE = { alert: '비상가방 꾸리기', water: '물 받으러 갈 준비', evac: '대피 가방 하나' };
const TAG = { water: '물', food: '식품', info: '정보', power: '전원', light: '빛', health: '건강', hygiene: '위생', warmth: '보온', tool: '도구', docs: '서류', fire: '화기 주의', spoil: '상하기 쉬움' };
const WARN = new Set(['fire', 'spoil']);

// Which items tick a goal line, matched on the director's wording. A line ticks when every group it
// mentions has something in the bag. ponytail: keyword match; pass goal objects if wording drifts.
const GOAL_WORDS = [
  [/물(?!통)/, ['water_500', 'water_2l']], [/음식|식량/, ['bars', 'canned', 'rice_instant']], [/빛|손전등/, ['flashlight']],
  [/정보|라디오/, ['radio', 'powerbank']], [/약|구급/, ['meds', 'first_aid']], [/연락/, ['contact_card']], [/서류|사본/, ['doc_copies']],
  [/현금/, ['cash']], [/보온|담요|여벌/, ['blanket', 'clothes', 'raincoat']], [/위생/, ['hygiene_kit', 'wipes', 'tissue', 'masks']], [/물통/, ['water_jug']],
];

export async function bag({ mode = 'guided', context = 'alert', limit = { weightKg: 8, volumeL: 25 }, items = [], goals = [], tips = [] } = {}) {
  const st = Object.fromEntries(items.map(i => [i.id, { home: Math.max(0, i.home | 0), bag: Math.max(0, i.bag | 0) }]));
  const load = () => items.reduce((a, i) => ({ w: a.w + (i.weightKg || 0) * st[i.id].bag, v: a.v + (i.volumeL || 0) * st[i.id].bag }), { w: 0, v: 0 });
  const result = () => ({ bag: Object.fromEntries(items.map(i => [i.id, st[i.id].bag])) });
  const title = TITLE[context] || '가방 꾸리기';
  const m = modal({ className: `mg-bag is-${mode}`, label: title, onKey: (e) => { if (e.key === 'Escape') { m.close(result()); return true; } } });

  const lists = { home: el('ul', { class: 'mg-list', 'aria-label': '집 비축 (누르면 가방에 넣기)' }), bag: el('ul', { class: 'mg-list', 'aria-label': '가방 (누르면 집에 꺼내 두기)' }) };
  const counts = { home: el('small'), bag: el('small') };
  const meters = ['w', 'v'].map(k => ({ k, bar: el('span', { class: 'mg-bar-fill' }), text: el('b') }));
  const goalList = el('ul', { class: 'mg-goals', 'aria-label': '챙길 것' });
  const goalGroups = goals.map(g => GOAL_WORDS.filter(([re]) => re.test(g)).map(([, ids]) => ids));

  const move = (id, from, n) => {
    const to = from === 'home' ? 'bag' : 'home', k = Math.min(n, st[id][from]);
    if (!k) return;
    st[id][from] -= k; st[id][to] += k;
    const it = items.find(i => i.id === id);
    render(from, id);
    const { w } = load();
    announce(`${it.name} ${k}개를 ${to === 'bag' ? '가방에 넣었다' : '집에 두었다'}. 가방 ${fmtKg(w)}`);
  };

  function row(it, where) {
    const n = st[it.id][where];
    const tags = (it.tags || []).filter((t, i, a) => TAG[t] && a.indexOf(t) === i);
    const btn = el('button', { type: 'button', class: 'mg-row', 'data-id': it.id, 'aria-label': `${it.name} ${n}개 · ${fmtKg(it.weightKg || 0)}씩 · ${where === 'home' ? '가방에 하나 넣기' : '하나 꺼내 두기'}` },
      where === 'bag' ? el('span', { class: 'mg-arrow', 'aria-hidden': 'true' }, '←') : null,
      el('span', { class: 'mg-row-main' },
        el('b', {}, it.name, el('span', { class: 'mg-count' }, ` ×${n}`)),
        el('span', { class: 'mg-badges' }, el('small', { class: 'mg-spec' }, `${fmtKg(it.weightKg || 0)} · ${it.volumeL ?? 0}L`),
          tags.map(t => el('span', { class: `mg-badge${WARN.has(t) ? ' is-low' : ''}` }, WARN.has(t) ? el('span', { 'aria-hidden': 'true' }, '! ') : null, TAG[t]))),
        it.tip && mode !== 'free' ? el('small', { class: 'mg-tip' }, it.tip) : null),
      where === 'home' ? el('span', { class: 'mg-arrow', 'aria-hidden': 'true' }, '→') : null);
    btn.addEventListener('click', (e) => move(it.id, where, e.shiftKey ? Infinity : 1));
    return el('li', {}, btn);
  }

  function render(focusWhere, focusId) {
    const idx = focusWhere ? [...lists[focusWhere].querySelectorAll('.mg-row')].findIndex(b => b.dataset.id === focusId) : -1;
    for (const where of ['home', 'bag']) {
      const shown = items.filter(i => st[i.id][where] > 0);
      lists[where].replaceChildren(...shown.map(i => row(i, where)));
      if (!shown.length) lists[where].append(el('li', { class: 'mg-empty' }, where === 'bag' ? '아직 비어 있다.' : '집에 남은 물건이 없다.'));
      counts[where].textContent = `${shown.reduce((s, i) => s + st[i.id][where], 0)}개`;
    }
    const { w, v } = load();
    for (const mt of meters) {
      const val = mt.k === 'w' ? w : v, max = mt.k === 'w' ? limit.weightKg : limit.volumeL, s = meterState(val, max);
      mt.bar.style.width = `${Math.min(100, (val / max) * 100)}%`;
      mt.bar.parentElement.dataset.state = s;
      mt.text.textContent = `${mt.k === 'w' ? fmtKg(val) : `${val.toFixed(1)}L`} / ${mt.k === 'w' ? fmtKg(max) : `${max}L`} · ${METER_WORD[s]}`;
    }
    const inBag = (id) => (st[id]?.bag ?? 0) > 0;
    goalList.replaceChildren(...goals.map((g, i) => {
      const groups = goalGroups[i];
      if (mode === 'free' || !groups.length) return el('li', { class: 'is-plain' }, el('span', { 'aria-hidden': 'true' }, '·'), g);
      const ok = groups.every(ids => ids.some(inBag));
      return el('li', { class: ok ? 'is-done' : '' }, el('span', { 'aria-hidden': 'true' }, ok ? '✓' : '○'), g, el('span', { class: 'sr-only' }, ok ? ' (담음)' : ' (아직)'));
    }));
    if (!focusWhere) return;
    const rowsNow = [...lists[focusWhere].querySelectorAll('.mg-row')];
    const target = rowsNow.find(b => b.dataset.id === focusId) || rowsNow[Math.min(Math.max(idx, 0), rowsNow.length - 1)]
      || lists[focusWhere === 'home' ? 'bag' : 'home'].querySelector('.mg-row') || doneBtn;
    target?.focus({ preventScroll: false });
  }

  for (const where of ['home', 'bag']) lists[where].addEventListener('keydown', (e) => {
    const btn = e.target.closest?.('.mg-row');
    if (!btn) return;
    const id = btn.dataset.id;
    if ((where === 'home' && e.key === 'ArrowRight') || (where === 'bag' && (e.key === 'ArrowLeft' || e.key === 'Backspace' || e.key === 'Delete'))) { e.preventDefault(); move(id, where, e.shiftKey ? Infinity : 1); }
    else if (e.key === 'Enter' && e.shiftKey) { e.preventDefault(); move(id, where, Infinity); }
    else if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
      e.preventDefault();
      const all = [...lists[where].querySelectorAll('.mg-row')], i = all.indexOf(btn);
      all[Math.max(0, Math.min(all.length - 1, i + (e.key === 'ArrowDown' ? 1 : -1)))]?.focus();
    } else if ((where === 'home' && e.key === 'ArrowLeft') || (where === 'bag' && e.key === 'ArrowRight')) {
      e.preventDefault(); (lists[where === 'home' ? 'bag' : 'home'].querySelector('.mg-row') || btn).focus();
    }
  });

  const doneBtn = el('button', { type: 'button', class: 'mg-btn mg-primary' }, '다 챙겼다');
  doneBtn.addEventListener('click', () => m.close(result()));
  const empty = el('button', { type: 'button', class: 'mg-btn mg-ghost' }, '가방 비우기');
  empty.addEventListener('click', () => { for (const i of items) { st[i.id].home += st[i.id].bag; st[i.id].bag = 0; } render(); announce('가방을 비웠다.'); });

  const help = ladderHelp(mode, () => [
    el('p', {}, '목표 목록에서 ○ 표시가 남은 것을 먼저 채워 보세요. 무게와 부피 막대가 넘치지 않게.'),
    tips.length ? el('ul', { class: 'mg-tips' }, tips.map(t => el('li', {}, t))) : null,
    mode === 'hint' ? el('p', { class: 'mg-why' }, '각 물건 아래에 도움말이 함께 보입니다.') : null,
  ]);

  m.root.append(
    header('가방', title, mode, 'mg-bag-title'),
    el('div', { class: 'mg-bag-grid' },
      el('section', { class: 'mg-pane', 'aria-labelledby': 'mg-pane-home' }, el('h3', { id: 'mg-pane-home' }, '집 비축 ', counts.home), lists.home),
      el('section', { class: 'mg-pane is-bag', 'aria-labelledby': 'mg-pane-bag' }, el('h3', { id: 'mg-pane-bag' }, '가방 ', counts.bag), lists.bag),
      el('aside', { class: 'mg-side' },
        meters.map(mt => el('div', { class: 'mg-meter' }, el('small', {}, el('span', { 'aria-hidden': 'true' }, mt.k === 'w' ? '⚖ ' : '▣ '), mt.k === 'w' ? '무게' : '부피'), mt.text,
          el('span', { class: 'mg-bar', 'aria-hidden': 'true' }, mt.bar))),
        el('p', { class: 'mg-why' }, `무게가 ${fmtKg(limit.weightKg)}을 넘으면 걸음이 느려집니다.`),
        goals.length ? el('h3', {}, '챙길 것') : null, goalList,
        help)),
    el('footer', { class: 'mg-foot' }, el('p', { class: 'mg-keys' }, '누르기(Enter) 한 개 · Shift+Enter 전부 · → 넣기 · ← 꺼내기 · ↑↓ 이동 · Esc 닫기'), el('div', { class: 'mg-actions' }, empty, doneBtn)));
  m.root.setAttribute('aria-labelledby', 'mg-bag-title');
  render();
  return m.done;
}

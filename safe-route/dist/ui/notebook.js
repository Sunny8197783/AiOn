// The notebook (what 정민 learned, by day, with where it came from) and the inventory (home stock vs bag).
import { el, modal, icon, btn, labelTag, LABELS } from './dom.js';

const dayLabel = (d) => (d < 0 ? `D${d}` : d === 0 ? 'D-day' : `D+${d}`);
const hhmm = (m) => `${String(Math.floor((m ?? 0) / 60) % 24).padStart(2, '0')}:${String((m ?? 0) % 60).padStart(2, '0')}`;
const PLACE = { office: '사무실', apartment: '집', town: '동네', mart: '연무마트', store: '바로편의점', shelter: '대피소', culture: '생활문화센터' };
const LABEL_WHY = {
  official: '정부·지자체·재난방송·재난문자',
  site: '내 눈으로 직접 본 것',
  neighbor: '아는 사람에게 들은 것',
  rumor: '누가 처음 말했는지 모르는 것',
  self: '내가 정리하거나 한 일',
};

function closeButton(m) { return btn('닫기', { ghost: true, iconName: 'close', className: 'modal-close', 'aria-label': '닫기', onclick: () => m.close() }); }

// notebook(entries: [{ day, minute, text, label, place }]) -> Promise
export function notebook(entries = []) {
  const list = [...(entries || [])].filter((e) => e && e.text).sort((a, b) => (a.day - b.day) || (a.minute - b.minute));
  let filter = 'all';
  const counts = Object.fromEntries(Object.keys(LABELS).map((k) => [k, list.filter((e) => (LABELS[e.label] ? e.label : 'self') === k).length]));
  const filterBtns = [['all', null], ...Object.keys(LABELS).map((k) => [k, k])].map(([k]) => {
    const b = el('button', { type: 'button', class: `nb-filter${k === 'all' ? ' all' : ''}`, 'aria-pressed': String(k === 'all'), 'data-k': k },
      k === 'all' ? el('span', { class: 'tag tag-all' }, icon('book'), '전체') : labelTag(k),
      el('span', { class: 'nb-count' }, String(k === 'all' ? list.length : counts[k])));
    b.addEventListener('click', () => { filter = k; filterBtns.forEach((x) => x.setAttribute('aria-pressed', String(x.dataset.k === k))); render(); });
    return b;
  });
  const body = el('div', { class: 'nb-pages', tabindex: '0', 'aria-label': '수첩 기록' });
  function render() {
    const shown = list.filter((e) => filter === 'all' || (LABELS[e.label] ? e.label : 'self') === filter);
    if (!shown.length) { body.replaceChildren(el('p', { class: 'empty' }, list.length ? '이 분류의 기록이 없습니다.' : '아직 적은 것이 없습니다. 확인한 정보는 여기에 쌓입니다.')); return; }
    const days = new Map();
    for (const e of shown) { const k = e.day ?? 0; if (!days.has(k)) days.set(k, []); days.get(k).push(e); }
    body.replaceChildren(...[...days].map(([d, es]) => el('section', { class: 'nb-day' },
      el('h3', {}, dayLabel(d)),
      el('ol', {}, es.map((e) => el('li', { class: `nb-entry nb-${LABELS[e.label] ? e.label : 'self'}` },
        el('time', {}, hhmm(e.minute)),
        labelTag(e.label),
        el('p', {}, e.text),
        PLACE[e.place] ? el('small', { class: 'nb-place' }, PLACE[e.place]) : null))))));
    body.scrollTop = body.scrollHeight;
  }
  const content = el('div', { class: 'nb' },
    el('header', { class: 'nb-head' }, el('p', { class: 'eyebrow' }, icon('book'), '정민의 수첩'), el('h2', {}, '확인한 것과 들은 것'), null),
    el('div', { class: 'nb-filters', role: 'group', 'aria-label': '정보 출처로 거르기' }, filterBtns),
    body,
    el('details', { class: 'nb-legend' }, el('summary', {}, '출처 표시는 무엇을 뜻하나요?'),
      el('ul', {}, Object.keys(LABELS).map((k) => el('li', {}, labelTag(k), el('span', {}, LABEL_WHY[k]))))));
  const m = modal({ className: 'notebook', label: '수첩', content, onKey(e) { if (e.key === 'Escape') { e.preventDefault(); m.close(); } } });
  content.querySelector('.nb-head').append(closeButton(m));
  render();
  filterBtns[0].focus();
  return m.done;
}

const CATEGORY = { water: '물', food: '먹을 것', info: '정보', power: '전원', light: '불빛', health: '건강', hygiene: '위생', warmth: '보온', tool: '도구', fire: '화기', docs: '서류·현금' };
const ORDER = Object.keys(CATEGORY);
const CHECK = { batteries: '건전지를 넣어 봐야 쓸 수 있음', charge: '충전해 둬야 쓸 수 있음', tune: '주파수를 맞춰 봐야 함' };
const count = (v, key) => (typeof v === 'number' ? v : Number(v?.[key]) || 0);

// inventory({ home, bag, catalog, load: { weightKg, volumeL, over }, limit: { weightKg, volumeL } }) -> Promise
export function inventory({ home = {}, bag = {}, catalog = {}, load = null, limit = null } = {}) {
  const ids = [...new Set([...Object.keys(home || {}), ...Object.keys(bag || {})])];
  const rowsFor = (where, src) => {
    const items = ids.map((id) => ({ id, n: count(src?.[id], where), c: catalog[id] || { name: id, unit: '개', category: 'tool' }, entry: home?.[id] ?? bag?.[id] }))
      .filter((x) => x.n > 0)
      .sort((a, b) => ORDER.indexOf(a.c.category) - ORDER.indexOf(b.c.category) || a.c.name.localeCompare(b.c.name, 'ko'));
    if (!items.length) return el('p', { class: 'empty' }, where === 'bag' ? '가방이 비어 있습니다.' : '집에 남은 것이 없습니다.');
    return el('ul', { class: 'inv-list' }, items.map(({ n, c, entry }) => {
      const tags = [];
      if (c.needsCheck) tags.push(entry?.checked ? el('span', { class: 'inv-tag ok' }, icon('check'), '점검 완료') : el('span', { class: 'inv-tag warn' }, icon('alert'), `점검 전 · ${CHECK[c.needsCheck] || '확인 필요'}`));
      if (c.risk === 'fire') tags.push(el('span', { class: 'inv-tag risk' }, icon('flame'), '화기 주의'));
      if (c.risk === 'spoil') tags.push(el('span', { class: 'inv-tag risk' }, icon('hourglass'), '정전 땐 오래 못 둠'));
      return el('li', { class: 'inv-row' },
        el('span', { class: 'inv-cat' }, CATEGORY[c.category] || '기타'),
        el('b', { class: 'inv-name' }, c.name),
        el('span', { class: 'inv-qty' }, `${n}${c.unit || '개'}`),
        tags.length ? el('span', { class: 'inv-tags' }, tags) : null);
    }));
  };
  const meter = (label, v, max, unit, over) => {
    const pct = max ? Math.min(100, (v / max) * 100) : 0;
    const state = over ? 'over' : pct > 80 ? 'near' : 'ok';
    return el('div', { class: `inv-meter ${state}` },
      el('span', { class: 'inv-meter-label' }, label),
      el('span', { class: 'meter', 'aria-hidden': 'true' }, el('span', { class: 'meter-fill', style: { width: `${pct}%` } })),
      el('span', { class: 'inv-meter-value' }, `${v.toFixed(1)} / ${max}${unit}`, over ? ' · 초과' : pct > 80 ? ' · 거의 참' : ''));
  };
  const loadBox = load && limit ? el('div', { class: 'inv-load', role: 'group', 'aria-label': '가방 무게와 부피' },
    meter('무게', Number(load.weightKg) || 0, limit.weightKg, 'kg', load.over?.weight),
    meter('부피', Number(load.volumeL) || 0, limit.volumeL, 'L', load.over?.volume)) : null;
  const content = el('div', { class: 'inv' },
    el('header', { class: 'nb-head' }, el('p', { class: 'eyebrow' }, icon('box'), '소지품'), el('h2', {}, '집 비축과 가방')),
    el('div', { class: 'inv-panes' },
      el('section', { class: 'inv-pane', 'aria-label': '집 비축' }, el('h3', {}, icon('home'), '집 비축'), rowsFor('home', home)),
      el('section', { class: 'inv-pane', 'aria-label': '가방' }, el('h3', {}, icon('bag'), '가방'), loadBox, rowsFor('bag', bag))),
    el('p', { class: 'inv-note' }, '산 것과 쓸 수 있는 것은 다릅니다. "점검 전" 물건은 미리 확인해 두어야 필요할 때 씁니다.'));
  const m = modal({ className: 'inventory', label: '소지품', content, onKey(e) { if (e.key === 'Escape' || (e.code === 'KeyI' && !e.repeat)) { e.preventDefault(); m.close(); } } });
  content.querySelector('.nb-head').append(closeButton(m));
  content.querySelector('.modal-close').focus();
  return m.done;
}

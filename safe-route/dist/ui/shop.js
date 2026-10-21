// Shelf picker for the mart and the convenience store: quantity steppers, running total against the money
// in hand, the weight that has to be carried home, stock badges and an info strip (labelled by source).
import { el, modal, header, pill, won, fmtKg, announce } from './mini-dom.js';

const MAX_QTY = 20;
const STOCK = { out: ['품절', 'is-out', '✕'], low: ['얼마 안 남음', 'is-low', '!'] };

// Small product drawings: [shape, body colour, scale]. Unknown ids fall back to a carton.
const ART = {
  water_2l: ['bottle', '#8cc8e6', 1], water_500: ['bottle', '#a9d8ef', 0.72], rice_instant: ['tray', '#f2ede3'], canned: ['can', '#c8a13c'],
  ramen: ['pouch', '#cf3b2c'], bars: ['pouch', '#e2ad38'], radio: ['radio', '#3b4046'], batteries_aa: ['cells', '#d6a21e'], flashlight: ['torch', '#2e3338'],
  powerbank: ['slab', '#e7ebee'], candles: ['candles', '#f3eee2'], first_aid: ['box', '#f2f4f3', 'cross'], masks: ['box', '#dbe7f1'], wipes: ['pouch', '#58b29e'],
  tissue: ['roll', '#f6f6f2'], trash_bags: ['roll', '#3f6c4e'], tape: ['roll', '#8a8e93'], raincoat: ['pouch', '#e5d04a'], blanket: ['pouch', '#c3c9d0'],
  water_jug: ['jug', '#4a8fd4'], gas_stove: ['box', '#c3352d'], butane: ['can', '#ebe5db', 1.15], whistle: ['torch', '#ef8a25'],
};
function art(id) {
  const [shape, c, s = 1] = ART[id] || ['box', '#b9a27f'];
  const sh = 'fill="rgba(0,0,0,.18)"';
  const body = {
    bottle: `<rect x="${32 - 9 * s}" y="${60 - 46 * s}" width="${18 * s}" height="${46 * s}" rx="${6 * s}" fill="${c}" opacity=".9"/><rect x="${32 - 4 * s}" y="${60 - 52 * s}" width="${8 * s}" height="${7 * s}" rx="1.5" fill="#2d6fb3"/><rect x="${32 - 9 * s}" y="${60 - 30 * s}" width="${18 * s}" height="${11 * s}" fill="#fff" opacity=".85"/><rect x="${32 - 9 * s}" y="${60 - 27 * s}" width="${18 * s}" height="${3 * s}" fill="#2d6fb3"/>`,
    can: `<rect x="${32 - 12 * s}" y="${60 - 26 * s}" width="${24 * s}" height="${26 * s}" rx="3" fill="${c}"/><ellipse cx="32" cy="${60 - 26 * s}" rx="${12 * s}" ry="3" fill="#d9dde0"/><rect x="${32 - 12 * s}" y="${60 - 18 * s}" width="${24 * s}" height="${9 * s}" fill="#fff" opacity=".7"/>`,
    tray: `<path d="M12 44h40l-4 14H16z" fill="${c}"/><rect x="10" y="40" width="44" height="5" rx="2" fill="#fff"/><rect x="20" y="48" width="24" height="5" rx="1" fill="#b23a2a" opacity=".8"/>`,
    pouch: `<path d="M14 22h36l-2 36H16z" fill="${c}"/><path d="M14 22h36v5H14z" fill="rgba(255,255,255,.35)"/><rect x="20" y="34" width="24" height="12" rx="2" fill="#fff" opacity=".8"/><rect x="23" y="38" width="18" height="3" fill="${c}"/>`,
    radio: `<rect x="10" y="26" width="44" height="30" rx="5" fill="${c}"/><circle cx="23" cy="41" r="9" fill="#23272b"/><circle cx="23" cy="41" r="6" fill="none" stroke="#5a6067" stroke-dasharray="2 2"/><rect x="36" y="32" width="14" height="6" rx="1" fill="#e9b55a"/><circle cx="43" cy="48" r="3.5" fill="#888"/><path d="M48 26l6-16" stroke="#aaa" stroke-width="2"/>`,
    cells: `<g fill="${c}"><rect x="14" y="20" width="8" height="38" rx="2"/><rect x="24" y="20" width="8" height="38" rx="2"/><rect x="34" y="20" width="8" height="38" rx="2"/><rect x="44" y="20" width="8" height="38" rx="2"/></g><g fill="#222"><rect x="14" y="40" width="38" height="18" rx="2" opacity=".85"/></g>`,
    torch: `<rect x="14" y="34" width="34" height="12" rx="4" fill="${c}"/><path d="M48 31h8v18h-8z" fill="#9aa3ab"/><rect x="24" y="36" width="5" height="8" rx="1" fill="#e2c14a"/>`,
    slab: `<rect x="16" y="18" width="30" height="40" rx="5" fill="${c}"/><rect x="20" y="22" width="22" height="3" rx="1" fill="#6bd18a"/><circle cx="31" cy="50" r="3" fill="#9aa"/>`,
    candles: `<g fill="${c}"><rect x="16" y="26" width="7" height="32" rx="1"/><rect x="26" y="22" width="7" height="36" rx="1"/><rect x="36" y="26" width="7" height="32" rx="1"/><rect x="46" y="24" width="7" height="34" rx="1"/></g><path d="M12 46h44v12H12z" fill="#d9c9a5"/>`,
    box: `<rect x="12" y="24" width="40" height="34" rx="3" fill="${c}"/><path d="M12 30h40" stroke="rgba(0,0,0,.15)"/>${s === 'cross' ? '<path d="M28 34h8v6h6v8h-6v6h-8v-6h-6v-8h6z" fill="#2f9a59"/>' : '<rect x="18" y="36" width="28" height="10" rx="2" fill="#fff" opacity=".75"/>'}`,
    roll: `<rect x="14" y="26" width="36" height="32" rx="4" fill="${c}"/><ellipse cx="32" cy="26" rx="18" ry="5" fill="${c}" stroke="rgba(0,0,0,.2)"/><ellipse cx="32" cy="26" rx="6" ry="2" fill="#6b6b66"/>`,
    jug: `<path d="M14 24h36v34H14z" fill="${c}" opacity=".88"/><rect x="38" y="16" width="9" height="9" rx="2" fill="#f2f2f2"/><path d="M18 24v-6h14v6" fill="none" stroke="#244f7d" stroke-width="3"/>`,
  }[shape] || '';
  return `<svg viewBox="0 0 64 64" aria-hidden="true" focusable="false"><ellipse cx="32" cy="60" rx="22" ry="3" ${sh}/>${body}</svg>`;
}

const carryWord = (kg) => kg <= 6 ? '가벼움' : kg <= 12 ? '들 만함' : '버거움';

export async function shop({ title = '진열대', money = 0, items = [], info = [], budgetNote = '' } = {}) {
  const qty = Object.fromEntries(items.map(i => [i.id, 0]));
  const total = () => items.reduce((s, i) => s + (i.price || 0) * qty[i.id], 0);
  const kg = () => items.reduce((s, i) => s + (i.weightKg || 0) * qty[i.id], 0);
  const count = () => Object.values(qty).reduce((s, n) => s + n, 0);

  const m = modal({ className: 'mg-shop', label: `${title} 진열대`, onKey: (e) => { if (e.key === 'Escape') { m.close(null); return true; } } });
  const sumMoney = el('b'), sumBar = el('span', { class: 'mg-bar-fill' }), sumNote = el('small', { class: 'mg-why' });
  const sumKg = el('b'), kgBar = el('span', { class: 'mg-bar-fill' });
  const buy = el('button', { type: 'button', class: 'mg-btn mg-primary' }, '계산대로 가져가기');
  const rows = new Map();

  const set = (it, n, say = true) => {
    const v = Math.max(0, Math.min(MAX_QTY, Math.round(Number(n) || 0)));
    qty[it.id] = it.stock === 'out' ? 0 : v;
    const r = rows.get(it.id);
    r.input.value = String(qty[it.id]);
    r.dec.disabled = qty[it.id] <= 0 || it.stock === 'out';
    r.inc.disabled = qty[it.id] >= MAX_QTY || it.stock === 'out';
    r.row.classList.toggle('is-picked', qty[it.id] > 0);
    refresh();
    if (say) announce(`${it.name} ${qty[it.id]}${it.unit || '개'} · 합계 ${won(total())}`);
  };

  const list = el('ul', { class: 'mg-shelf', 'aria-label': `${title} 상품` });
  for (const it of items) {
    const out = it.stock === 'out';
    const [stockText, stockCls, stockIcon] = STOCK[it.stock] || [];
    const input = el('input', { type: 'number', min: 0, max: MAX_QTY, step: 1, value: 0, inputmode: 'numeric', class: 'mg-qty', 'aria-label': `${it.name} 수량 (${it.unit || '개'})`, disabled: out });
    const dec = el('button', { type: 'button', class: 'mg-step', 'aria-label': `${it.name} 하나 빼기`, disabled: true }, '−');
    const inc = el('button', { type: 'button', class: 'mg-step', 'aria-label': `${it.name} 하나 담기`, disabled: out }, '+');
    const pic = el('div', { class: 'mg-art' }); pic.innerHTML = art(it.id); // constant markup, no user text
    const row = el('li', { class: `mg-item${out ? ' is-out' : ''}` },
      pic,
      el('div', { class: 'mg-item-main' },
        el('b', { class: 'mg-item-name' }, it.name),
        el('span', { class: 'mg-badges' },
          stockText ? el('span', { class: `mg-badge ${stockCls}` }, el('span', { 'aria-hidden': 'true' }, stockIcon), stockText) : null,
          it.label ? el('span', { class: 'mg-badge' }, it.label) : null),
        it.note ? el('small', { class: 'mg-note' }, it.note) : null,
        el('small', { class: 'mg-spec' }, `${fmtKg(it.weightKg || 0)} · ${it.volumeL ?? '-'}L`)),
      el('div', { class: 'mg-price', 'aria-label': `가격 ${won(it.price || 0)} / ${it.unit || '개'}` }, el('b', {}, won(it.price || 0)), el('small', {}, `/ ${it.unit || '개'}`)),
      el('div', { class: 'mg-stepper', role: 'group', 'aria-label': `${it.name} 수량 정하기` }, dec, input, inc));
    rows.set(it.id, { row, input, dec, inc });
    dec.addEventListener('click', () => set(it, qty[it.id] - 1));
    inc.addEventListener('click', () => set(it, qty[it.id] + 1));
    input.addEventListener('change', () => set(it, input.value));
    row.addEventListener('keydown', (e) => {
      if (out || e.target === input) return;
      if (e.key === '+' || e.key === '=') { e.preventDefault(); set(it, qty[it.id] + 1); }
      else if (e.key === '-') { e.preventDefault(); set(it, qty[it.id] - 1); }
    });
    list.append(row);
  }

  function refresh() {
    const t = total(), w = kg(), over = t > money;
    sumMoney.textContent = `${won(t)} / 가진 돈 ${won(money)}`;
    sumBar.style.width = `${Math.min(100, money ? (t / money) * 100 : t ? 100 : 0)}%`;
    sumBar.parentElement.dataset.state = over ? 'over' : t > money * 0.8 ? 'warn' : 'ok';
    sumNote.textContent = over ? '✕ 가진 돈보다 많습니다. 수량을 줄이세요.' : `담은 물건 ${count()}개`;
    sumKg.textContent = `약 ${fmtKg(w)} · ${carryWord(w)}`;
    kgBar.style.width = `${Math.min(100, (w / 16) * 100)}%`;
    kgBar.parentElement.dataset.state = w > 12 ? 'over' : w > 6 ? 'warn' : 'ok';
    buy.disabled = over;
  }

  buy.addEventListener('click', () => {
    if (total() > money) return;
    const cart = Object.fromEntries(Object.entries(qty).filter(([, n]) => n > 0));
    m.close({ cart, total: total() });
  });
  const cancel = el('button', { type: 'button', class: 'mg-btn mg-ghost' }, '그만두기');
  cancel.addEventListener('click', () => m.close(null));

  m.root.append(
    header('장보기', title, null, 'mg-shop-title'),
    info.length ? el('ul', { class: 'mg-info', 'aria-label': '들은 정보' }, info.map(i => el('li', {}, pill(i.label), el('span', {}, i.text)))) : null,
    list,
    el('footer', { class: 'mg-foot' },
      el('div', { class: 'mg-sum' },
        el('div', {}, el('small', {}, '합계'), sumMoney, el('span', { class: 'mg-bar', 'aria-hidden': 'true' }, sumBar), sumNote),
        el('div', {}, el('small', {}, '들고 갈 무게'), sumKg, el('span', { class: 'mg-bar', 'aria-hidden': 'true' }, kgBar),
          budgetNote ? el('small', { class: 'mg-why' }, budgetNote) : null)),
      el('div', { class: 'mg-actions' }, cancel, buy)),
    el('p', { class: 'mg-keys' }, 'Tab으로 상품 이동 · +/− 또는 ↑/↓로 수량 · Esc 그만두기'));
  m.root.setAttribute('aria-labelledby', 'mg-shop-title');
  refresh();
  return m.done;
}

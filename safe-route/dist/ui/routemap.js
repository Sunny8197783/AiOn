// 2D map of 연무동 drawn from game/townmap.js: roads, crossings, buildings, the park, Korean labels, shelter
// entrances with status icons, closures only when the player knows them, and up to 3 route options as lines
// told apart by colour AND dash. routemap() resolves { choice: index | null }; miniMap() draws the HUD map.
import { el, put, modal, header, ladderHelp } from './mini-dom.js';
import { BOUNDS, SIDEWALK, ROADS, CROSSWALKS, BUILDINGS, AREAS, NODES, PLACES, SHELTER_ENTRANCES, CLOSURES } from '../game/townmap.js';
import { explain } from '../game/routes.js';

const ROUTE_STYLE = [['#f2b33d', [], '실선'], ['#3cc6b5', [12, 7], '긴 점선'], ['#c49bff', [3, 6], '짧은 점선']];
const KIND = { shortest: '가장 짧은 길', detour: '돌아가는 길', stepFree: '계단 없는 길' };
const BCOL = { apartment: '#58656e', shop: '#6e5d4c', mart: '#715e45', office: '#4d5d75', civic: '#4f6d68', villa: '#5f5852', warehouse: '#4c5156' };
const AREA_COL = { park: '#24462f', parking: '#283034', plaza: '#2c3336', complex: '#1f2b26' };
const GATE = { open: ['✓', '열림', '#3cc6b5'], closed: ['✕', '닫힘', '#e5484d'], unknown: ['?', '확인 안 함', '#8b9599'] };
const FONT = "'Pretendard','Noto Sans KR','Malgun Gothic',sans-serif";
const gateState = (v) => (v === 'open' || v === true ? 'open' : v === 'closed' || v === false ? 'closed' : 'unknown');

// World (x east, z south) -> canvas pixels. rot turns the map (minimap: the way you face is up).
function projector(cx, cz, s, rot, W, H) {
  const c = Math.cos(rot), n = Math.sin(rot);
  return (x, z) => { const dx = (x - cx) * s, dz = (z - cz) * s; return [W / 2 + dx * c - dz * n, H / 2 + dx * n + dz * c]; };
}
const rectPts = (r, pad = 0) => [[r.x0 - pad, r.z0 - pad], [r.x1 + pad, r.z0 - pad], [r.x1 + pad, r.z1 + pad], [r.x0 - pad, r.z1 + pad]];
function poly(g, P, pts) { g.beginPath(); pts.forEach(([x, z], i) => { const [a, b] = P(x, z); i ? g.lineTo(a, b) : g.moveTo(a, b); }); g.closePath(); }
function label(g, text, x, y, px, color = '#e9eef0', weight = 600) {
  g.font = `${weight} ${px}px ${FONT}`; g.textAlign = 'center'; g.textBaseline = 'middle';
  g.lineWidth = px * 0.35; g.strokeStyle = 'rgba(10,14,16,.85)'; g.strokeText(text, x, y); g.fillStyle = color; g.fillText(text, x, y);
}

export function drawMap(g, P, s, { W, H, closures = [], gates = {}, options = [], sel = -1, player = null, playerYaw = null, dest = null, labels = true, dpr = 1 }) {
  const px = (n) => n * dpr;
  g.setLineDash([]); g.fillStyle = '#172023'; g.fillRect(0, 0, W, H);
  for (const a of AREAS) {
    g.fillStyle = AREA_COL[a.kind] || '#222b2e'; poly(g, P, rectPts(a)); g.fill();
    if (a.kind === 'parking') { g.strokeStyle = 'rgba(230,230,220,.18)'; g.lineWidth = px(1); for (let x = a.x0 + 2.5; x < a.x1; x += 2.5) { const [p, q] = [P(x, a.z0 + 1), P(x, a.z0 + 5)]; g.beginPath(); g.moveTo(...p); g.lineTo(...q); g.stroke(); } }
    if (a.kind === 'park') for (let i = 0; i < 46; i++) { // tree crowns, deterministic
      const tx = a.x0 + 3 + ((i * 37) % 97) / 97 * (a.x1 - a.x0 - 6), tz = a.z0 + 3 + ((i * 61) % 89) / 89 * (a.z1 - a.z0 - 6);
      const [cx, cz] = P(tx, tz); g.fillStyle = i % 5 ? 'rgba(70,120,72,.75)' : 'rgba(176,130,48,.7)'; g.beginPath(); g.arc(cx, cz, Math.max(px(1.5), 1.6 * s), 0, Math.PI * 2); g.fill();
    }
  }
  g.fillStyle = '#4b5251'; for (const r of ROADS) { poly(g, P, rectPts(r, SIDEWALK)); g.fill(); }
  g.fillStyle = '#30383c'; for (const r of ROADS) { poly(g, P, rectPts(r)); g.fill(); }
  for (const r of ROADS) {
    const ew = r.x1 - r.x0 > r.z1 - r.z0, mid = ew ? (r.z0 + r.z1) / 2 : (r.x0 + r.x1) / 2;
    const line = (off, color, dash) => {
      g.strokeStyle = color; g.setLineDash(dash.map(d => d * s)); g.lineWidth = Math.max(px(0.8), 0.22 * s); g.beginPath();
      g.moveTo(...(ew ? P(r.x0, mid + off) : P(mid + off, r.z0))); g.lineTo(...(ew ? P(r.x1, mid + off) : P(mid + off, r.z1))); g.stroke();
    };
    if (r.id === 'yeonmu') { line(-0.3, '#d6ae38', []); line(0.3, '#d6ae38', []); } else line(0, 'rgba(230,230,225,.5)', [3, 3]);
  }
  g.setLineDash([]); g.fillStyle = 'rgba(236,236,230,.78)';
  for (const c of CROSSWALKS) {
    const r = ROADS.find(x => x.id === c.road); if (!r) continue;
    const ew = r.x1 - r.x0 > r.z1 - r.z0, [k0, k1] = ew ? [r.z0, r.z1] : [r.x0, r.x1];
    for (let k = k0 + 0.4; k < k1 - 0.4; k += 1.2) { poly(g, P, rectPts(ew ? { x0: c.at - 2, x1: c.at + 2, z0: k, z1: k + 0.6 } : { x0: k, x1: k + 0.6, z0: c.at - 2, z1: c.at + 2 })); g.fill(); }
  }
  for (const b of BUILDINGS) {
    g.fillStyle = 'rgba(0,0,0,.35)'; poly(g, P, rectPts({ x0: b.x0 + 1.2, x1: b.x1 + 1.2, z0: b.z0 + 1.2, z1: b.z1 + 1.2 })); g.fill();
    g.fillStyle = BCOL[b.kind] || '#5a5a5a'; poly(g, P, rectPts(b)); g.fill();
    g.strokeStyle = b.id === 'culture' ? '#3cc6b5' : 'rgba(0,0,0,.45)'; g.lineWidth = px(b.id === 'culture' ? 2.5 : 1); g.stroke();
  }
  if (labels) {
    for (const r of ROADS) { const [x, y] = P(r.id === 'yeonmu' ? 30 : (r.x0 + r.x1) / 2, r.id === 'yeonmu' ? 0 : r.id === 'munhwa' || r.id === 'hanbit' ? -86 : (r.z0 + r.z1) / 2); label(g, r.name, x, y, px(10), '#aeb8bb', 500); }
    for (const b of BUILDINGS) if (b.name) { const [x, y] = P((b.x0 + b.x1) / 2, (b.z0 + b.z1) / 2); label(g, b.name.replace('한빛아파트 ', ''), x, y - (b.id === 'culture' ? px(8) : 0), px(11)); }
    { const [x, y] = P(-90, 31); label(g, '대피소(지하)', x, y + px(8), px(11), '#7fe0d3', 700); }
    for (const a of AREAS) if (a.kind === 'park') { const [x, y] = P((a.x0 + a.x1) / 2, (a.z0 + a.z1) / 2); label(g, a.name, x, y, px(11), '#bfe3c2'); }
  }
  for (const id of closures) for (const [i, r] of (CLOSURES[id]?.barricades || []).entries()) {
    poly(g, P, rectPts(r)); g.fillStyle = '#e5484d'; g.fill(); g.setLineDash([px(4), px(4)]); g.strokeStyle = '#fff'; g.lineWidth = px(2); g.stroke(); g.setLineDash([]);
    const [x, y] = P((r.x0 + r.x1) / 2, (r.z0 + r.z1) / 2);
    g.fillStyle = '#e5484d'; g.beginPath(); g.arc(x, y, px(9), 0, Math.PI * 2); g.fill(); g.fillStyle = '#fff'; g.fillRect(x - px(5), y - px(1.6), px(10), px(3.2));
    if (labels && i === 0) label(g, `통제 · ${CLOSURES[id].label}`, x, y - px(16), px(10), '#ffb3b5', 700);
  }
  const order = options.map((o, i) => i).sort((a, b) => (a === sel) - (b === sel));
  for (const i of order) {
    const o = options[i], [color, dash] = ROUTE_STYLE[i] || ROUTE_STYLE[0], pts = (o.nodes || []).map(n => NODES[n]).filter(Boolean).map(n => P(n.x, n.z));
    if (pts.length < 2) continue;
    const path = () => { g.beginPath(); g.moveTo(...pts[0]); pts.slice(1).forEach(p => g.lineTo(...p)); };
    g.lineJoin = g.lineCap = 'round';
    g.setLineDash([]); g.strokeStyle = 'rgba(8,12,14,.8)'; g.lineWidth = px(i === sel ? 11 : 8); path(); g.stroke();
    g.setLineDash(dash.map(px)); g.strokeStyle = color; g.globalAlpha = sel < 0 || i === sel ? 1 : 0.55; g.lineWidth = px(i === sel ? 6 : 4); path(); g.stroke(); g.globalAlpha = 1; g.setLineDash([]);
    if (labels && options.length > 1) { const [x, y] = pts[Math.floor(pts.length / 2)]; g.fillStyle = color; g.beginPath(); g.arc(x, y, px(10), 0, Math.PI * 2); g.fill(); label(g, String(i + 1), x, y, px(12), '#10161a', 800); }
  }
  for (const [k, e] of Object.entries(SHELTER_ENTRANCES)) {
    const p = PLACES[e.place]; if (!p) continue;
    const [x, y] = P(p.x, p.z), [icon, , col] = GATE[gateState(gates[k])], r = px(8);
    g.fillStyle = col; g.strokeStyle = '#0c1214'; g.lineWidth = px(2);
    if (gateState(gates[k]) === 'closed') { g.fillRect(x - r, y - r, r * 2, r * 2); g.strokeRect(x - r, y - r, r * 2, r * 2); } else { g.beginPath(); g.arc(x, y, r, 0, Math.PI * 2); g.fill(); g.stroke(); }
    label(g, icon, x, y + px(0.5), px(11), '#0c1214', 900);
    if (labels) label(g, e.short || '동쪽 출입구', x, y + px(17), px(10), '#d8f3ef');
  }
  if (dest) { const [x, y] = P(dest.x, dest.z); g.fillStyle = '#3cc6b5'; g.beginPath(); g.moveTo(x, y - px(9)); g.lineTo(x + px(9), y); g.lineTo(x, y + px(9)); g.lineTo(x - px(9), y); g.closePath(); g.fill(); }
  if (player) {
    const [x, y] = P(player.x, player.z);
    g.fillStyle = 'rgba(242,179,61,.25)'; g.beginPath(); g.arc(x, y, px(16), 0, Math.PI * 2); g.fill();
    g.fillStyle = '#f2b33d'; g.strokeStyle = '#10161a'; g.lineWidth = px(2);
    if (playerYaw != null) { g.beginPath(); g.moveTo(x, y - px(10)); g.lineTo(x + px(7), y + px(7)); g.lineTo(x, y + px(3)); g.lineTo(x - px(7), y + px(7)); g.closePath(); g.fill(); g.stroke(); }
    else { g.beginPath(); g.arc(x, y, px(7), 0, Math.PI * 2); g.fill(); g.stroke(); }
    if (labels) label(g, '나', x, y - px(20), px(12), '#ffd58a', 800);
  }
  if (labels) { // north arrow and a 50 m scale bar
    const nx = W - px(28), ny = px(30);
    g.fillStyle = '#e9eef0'; g.beginPath(); g.moveTo(nx, ny - px(14)); g.lineTo(nx + px(7), ny + px(6)); g.lineTo(nx - px(7), ny + px(6)); g.closePath(); g.fill();
    label(g, '북', nx, ny + px(18), px(11));
    const sx = px(18), sy = H - px(18);
    g.fillStyle = '#e9eef0'; g.fillRect(sx, sy, 50 * s, px(3)); label(g, '50m', sx + 25 * s, sy - px(10), px(10));
  }
}

export async function routemap({ mode = 'guided', from = 'home', to = 'shelter', options = [], closures = [], gates = {}, known = {}, playerAt = null } = {}) {
  const shown = [...new Set([...(closures || []), ...(known?.closures || [])])].filter(id => CLOSURES[id]);
  const view = mode === 'view' || !options.length;
  let sel = view ? -1 : 0;
  const done = () => { ro?.disconnect(); m.close({ choice: view ? null : sel }); };
  const m = modal({ className: 'mg-map', label: view ? '연무동 지도' : '대피 경로 고르기', onKey: (e) => { if (e.key === 'Escape') { done(); return true; } } });
  const cv = el('canvas', { class: 'mg-map-canvas', role: 'img', 'aria-label': '연무동 지도. 같은 내용이 옆 목록에 글로 있습니다.' });
  const wrap = el('div', { class: 'mg-map-wrap' }, cv);

  function draw() {
    const dpr = Math.min(2, globalThis.devicePixelRatio || 1), w = wrap.clientWidth || 640;
    const W = Math.max(240, Math.round(w * dpr)), H = Math.round(W * (BOUNDS.z1 - BOUNDS.z0) / (BOUNDS.x1 - BOUNDS.x0));
    if (cv.width !== W) { cv.width = W; cv.height = H; }
    const s = Math.min(W / (BOUNDS.x1 - BOUNDS.x0), H / (BOUNDS.z1 - BOUNDS.z0));
    const P = projector((BOUNDS.x0 + BOUNDS.x1) / 2, (BOUNDS.z0 + BOUNDS.z1) / 2, s, 0, W, H);
    drawMap(cv.getContext('2d'), P, s, { W, H, closures: shown, gates, options, sel, player: playerAt || NODES[from] || null, dest: NODES[to] || null, dpr: W / w });
  }
  const ro = globalThis.ResizeObserver ? new ResizeObserver(() => draw()) : null;

  const routes = view ? null : el('fieldset', { class: 'mg-routes' }, el('legend', {}, '갈 길 고르기'), options.slice(0, 3).map((o, i) => {
    const id = `mg-route-${i}`, [color, dash, dashName] = ROUTE_STYLE[i];
    const r = el('input', { type: 'radio', name: 'mg-route', id, value: i, checked: i === 0, 'data-autofocus': i === 0 });
    r.addEventListener('change', () => { if (r.checked) { sel = i; draw(); } });
    const sw = el('span', { class: 'mg-swatch', 'aria-hidden': 'true' }); sw.innerHTML = `<svg viewBox="0 0 40 8"><line x1="2" y1="4" x2="38" y2="4" stroke="${color}" stroke-width="4" stroke-linecap="round" stroke-dasharray="${dash.join(' ')}"/></svg>`;
    const ent = SHELTER_ENTRANCES[o.entrance];
    return el('label', { class: 'mg-route', for: id }, r,
      el('span', { class: 'mg-route-body' },
        el('b', {}, sw, `경로 ${i + 1} · ${KIND[o.kind] || '다른 길'}`, el('small', { class: 'mg-spec' }, ` (${dashName})`)),
        el('small', {}, explain(o)),
        ent ? el('span', { class: 'mg-badges' }, el('span', { class: 'mg-badge' }, `${ent.short || ent.label}로 들어감`)) : null));
  }));

  const gateList = el('ul', { class: 'mg-gates', 'aria-label': '대피소 출입구' }, Object.entries(SHELTER_ENTRANCES).map(([k, e]) => {
    const st = gateState(gates[k]), [icon, text] = GATE[st];
    return el('li', { class: `is-${st}` }, el('span', { class: 'mg-gate-ico', 'aria-hidden': 'true' }, icon), `${e.label} · ${text}`);
  }));
  const closureList = el('ul', { class: 'mg-closures' }, shown.length ? shown.map(id => el('li', {}, el('span', { 'aria-hidden': 'true' }, '⊘ '), CLOSURES[id].label)) : el('li', { class: 'mg-why' }, '알고 있는 도로 통제가 없다. (현장은 다를 수 있다)'));
  const help = view ? null : ladderHelp(mode, () => [el('p', {}, '선을 비교해 보세요. 걸리는 시간, 계단이나 가파른 길, 그리고 지나는 출입구.'), el('p', { class: 'mg-why' }, '첫 번째 길이 막히면 어느 길로 돌아갈지도 함께 봐 두세요.')]);
  const go = el('button', { type: 'button', class: 'mg-btn mg-primary' }, view ? '지도 닫기' : '이 길로 정한다');
  go.addEventListener('click', done);

  put(m.root,
    header(view ? '지도' : '경로 계획', view ? '연무동 지도' : '집에서 대피소까지', mode, 'mg-map-title'),
    el('div', { class: 'mg-map-grid' },
      wrap,
      el('div', { class: 'mg-map-side' }, help, routes,
        el('h3', { class: 'mg-sub' }, '대피소 출입구'), gateList,
        el('h3', { class: 'mg-sub' }, '알고 있는 통제'), closureList,
        el('p', { class: 'mg-legend' }, el('span', {}, '● 나'), el('span', {}, '◆ 대피소'), el('span', {}, '✓ 열림'), el('span', {}, '✕ 닫힘'), el('span', {}, '? 확인 안 함'), el('span', {}, '⊘ 통제')))),
    el('footer', { class: 'mg-foot' }, el('p', { class: 'mg-keys' }, view ? 'Esc 또는 M으로 닫기' : '↑/↓ 경로 바꾸기 · Enter 정하기 · Esc 닫기'), el('div', { class: 'mg-actions' }, go)));
  m.root.setAttribute('aria-labelledby', 'mg-map-title');
  m.root.addEventListener('keydown', (e) => {
    if (view && (e.code === 'KeyM')) { e.preventDefault(); done(); }
    else if (!view && e.key === 'Enter' && e.target.name === 'mg-route') { e.preventDefault(); done(); }
  });
  ro ? ro.observe(wrap) : requestAnimationFrame(draw);
  return m.done;
}

// HUD minimap: centred on `at`, turned so the facing direction is up (yaw 0 = north, as in townmap).
// ponytail: assumes yaw grows counter-clockwise seen from above (three.js rotation.y); negate if the HUD spins backwards.
export function miniMap(canvas, { at, yaw = 0, known = {}, route = null } = {}) {
  if (!canvas || !at) return;
  const g = canvas.getContext('2d'), W = canvas.width, H = canvas.height, dpr = W / Math.max(1, canvas.clientWidth || W), s = 2.2 * dpr;
  const opts = route ? [Array.isArray(route) ? { nodes: route } : route] : [];
  drawMap(g, projector(at.x, at.z, s, yaw, W, H), s, { W, H, closures: known.closures || [], gates: known.gates || {}, options: opts, sel: 0, player: at, playerYaw: yaw, dest: NODES.shelter, labels: false, dpr });
}

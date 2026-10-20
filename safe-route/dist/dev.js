// Level viewer for reviewing every level: dev.html?level=town&spawn=home&env=night&q=medium&view=third&rain=1
// A level module that fails to import or build shows an error tile; the other levels stay usable.
import { createEngine } from './engine/core.js';
import { PRESETS } from './engine/presets.js';

// Same registry the director uses.
export const REGISTRY = {
  town: (m) => m.buildTown, apartment: (m) => m.buildApartment, mart: (m) => m.buildMart,
  store: (m) => m.buildStore, office: (m) => m.buildOffice, shelter: (m) => m.buildShelter,
};
const LEVEL_NAMES = { town: '연무동 거리', apartment: '한빛아파트 103동', mart: '연무마트', store: '바로편의점', office: '한결물류 사무실', shelter: '생활문화센터 대피소' };
const ENV_NAMES = { day: '낮 13:00', afternoon: '오후 17:30', dusk: '해질녘 18:40', night: '밤 (전력 정상)', blackout: '정전 밤', interior: '실내', interiorDark: '실내 · 소등', underground: '지하' };
const MULTI = new Set(['closures', 'soldOut']); // array-valued states: toggle chips
const QS = new URLSearchParams(location.search);
const opt = {
  level: REGISTRY[QS.get('level')] ? QS.get('level') : 'town', spawn: QS.get('spawn') || null,
  env: PRESETS[QS.get('env')] ? QS.get('env') : null, q: ['high', 'medium', 'low'].includes(QS.get('q')) ? QS.get('q') : 'high',
  view: QS.get('view') === 'first' ? 'first' : 'third', rain: QS.get('rain') === '1',
};

const levels = Object.fromEntries(Object.entries(REGISTRY).map(([id, pick]) => [id, async (kit, people, fx) => {
  let mod;
  try { mod = await import(`./levels/${id}.js`); }
  catch (err) { throw new Error(`levels/${id}.js 를 불러오지 못했습니다.\n${err.message}`, { cause: err }); }
  const build = pick(mod);
  if (typeof build !== 'function') throw new Error(`levels/${id}.js 에 build 함수가 없습니다 (내보낸 이름: ${Object.keys(mod).join(', ') || '없음'})`);
  return build(kit, people, fx);
}]));

// ---- tiny DOM helper ----
function h(tag, attrs = {}, ...kids) {
  const el = document.createElement(tag);
  for (const [k, v] of Object.entries(attrs)) {
    if (k.startsWith('on')) el.addEventListener(k.slice(2), v);
    else if (v !== false && v != null) el.setAttribute(k, v === true ? '' : v);
  }
  for (const c of kids.flat()) if (c != null) el.append(c);
  return el;
}
const fmt = (v) => (v === undefined ? '—' : typeof v === 'string' ? v : JSON.stringify(v));
let toastTimer = 0;
function toast(text) {
  const t = document.getElementById('toast');
  t.textContent = text; t.classList.add('show');
  clearTimeout(toastTimer); toastTimer = setTimeout(() => t.classList.remove('show'), 2600);
}

// ---- engine ----
const canvas = document.getElementById('world');
const panel = document.getElementById('panel');
let engine;
try {
  engine = createEngine({ canvas, quality: opt.q, levels, onQualityChange: (q) => { opt.q = q; qualitySel.value = q; toast(`프레임이 낮아 품질을 ${q}(으)로 낮췄습니다`); syncUrl(); } });
} catch (err) {
  console.error('[dev] engine failed to start', err);
  panel.append(h('div', { class: 'tile error', role: 'alert' }, `엔진을 시작하지 못했습니다 (WebGL 확인).\n${err.message}`));
  throw err;
}
engine.setView(opt.view);
engine.onInteract((p) => toast(`상호작용: ${p.id} · ${p.label ?? ''}`));
window.engine = engine; // console access for review

// ---- panel ----
const status = h('div', { class: 'tile busy', role: 'status' }, '준비 중…');
const levelSel = h('select', { id: 'lv', onchange: () => load(levelSel.value, null) },
  Object.keys(REGISTRY).map((id) => h('option', { value: id }, `${id} · ${LEVEL_NAMES[id]}`)));
const spawnSel = h('select', { id: 'sp', onchange: () => { if (spawnSel.value) { engine.player.teleport(spawnSel.value); opt.spawn = spawnSel.value; syncUrl(); } } });
const envSel = h('select', { id: 'env', onchange: () => { opt.env = envSel.value; applyEnv(1.5); } },
  Object.keys(PRESETS).map((id) => h('option', { value: id }, `${id} · ${ENV_NAMES[id] ?? id}`)));
const rainBtn = h('button', { type: 'button', 'aria-pressed': String(opt.rain), onclick: () => { opt.rain = !opt.rain; rainBtn.setAttribute('aria-pressed', String(opt.rain)); applyEnv(1.5); } }, '비');
const qualitySel = h('select', { id: 'q', onchange: () => { opt.q = qualitySel.value; engine.setQuality(opt.q); syncUrl(); } },
  ['high', 'medium', 'low'].map((q) => h('option', { value: q }, q)));
qualitySel.value = opt.q;
const viewBtn = h('button', { type: 'button', onclick: () => { opt.view = engine.view === 'first' ? 'third' : 'first'; engine.setView(opt.view); syncUrl(); } }, '시점 전환 (V)');
const flashBtn = h('button', { type: 'button', 'aria-pressed': 'false', onclick: () => { const on = !engine.player.flashlight; engine.player.setFlashlight(on); flashBtn.setAttribute('aria-pressed', String(on)); } }, '손전등');
const motionBtn = h('button', { type: 'button', 'aria-pressed': String(engine.reduceMotion), onclick: () => { engine.setReduceMotion(!engine.reduceMotion); motionBtn.setAttribute('aria-pressed', String(engine.reduceMotion)); } }, '움직임 줄이기');
const statsBox = h('div', { class: 'stats', 'aria-live': 'off' }, '');
const statesBox = h('div', {});
const pointsBox = h('div', { class: 'list' });
const anchorsBox = h('div', { class: 'list' });
const toggle = h('button', { type: 'button', id: 'toggle', 'aria-expanded': 'true', onclick: () => { const hidden = panel.classList.toggle('hidden'); toggle.setAttribute('aria-expanded', String(!hidden)); } }, '패널 접기/펴기');
panel.append(
  toggle, status,
  h('h2', {}, '레벨'), h('div', { class: 'row' }, h('label', { for: 'lv' }, '레벨'), levelSel),
  h('div', { class: 'row' }, h('label', { for: 'sp' }, '스폰'), spawnSel),
  h('h2', {}, '환경'), h('div', { class: 'row' }, h('label', { for: 'env' }, '프리셋'), envSel, rainBtn),
  h('div', { class: 'row' }, h('label', { for: 'q' }, '품질'), qualitySel),
  h('div', { class: 'row' }, viewBtn, flashBtn, motionBtn),
  h('h2', {}, '성능'), statsBox,
  h('h2', {}, '상태 (setState)'), statesBox,
  h('h2', {}, '상호작용 지점 (클릭하면 이동)'), pointsBox,
  h('h2', {}, '카메라 앵커'), anchorsBox,
  h('p', { class: 'help' }, 'WASD·방향키 이동, Shift 달리기, 마우스 끌기 또는 Alt+방향키로 둘러보기, E 상호작용, V 시점. 콘솔에서 window.engine 사용 가능.'),
);

function syncUrl() {
  const p = new URLSearchParams({ level: opt.level, env: opt.env || '', q: opt.q, view: opt.view });
  if (opt.spawn) p.set('spawn', opt.spawn);
  if (opt.rain) p.set('rain', '1');
  history.replaceState(null, '', `?${p}`);
}
function applyEnv(seconds) { if (opt.env) engine.setEnv(opt.env, { rain: opt.rain, seconds }); syncUrl(); }
function showError(text) { status.className = 'tile error'; status.setAttribute('role', 'alert'); status.textContent = text; }

// One control per documented state key: cycle button, toggle chips for array values, or one cycle button per sub-key.
function buildStates(L) {
  statesBox.replaceChildren();
  const set = (key, value, btn, label) => {
    try { L.setState(key, value); if (btn) btn.textContent = label; }
    catch (err) { console.error(`[dev] setState(${key}) failed`, err); toast(`setState(${key}) 오류: ${err.message}`); }
  };
  const cycle = (key, values, apply) => {
    let i = -1;
    const btn = h('button', { type: 'button', title: values.map(fmt).join(' / ') }, `${key}: —`);
    btn.addEventListener('click', () => { i = (i + 1) % values.length; apply(values[i], btn, `${key}: ${fmt(values[i])}`); });
    return btn;
  };
  for (const [key, allowed] of Object.entries(L.states || {})) {
    if (Array.isArray(allowed) && MULTI.has(key) && allowed.every((v) => typeof v === 'string')) {
      const sel = new Set();
      const chips = allowed.map((id) => {
        const b = h('button', { type: 'button', 'aria-pressed': 'false' }, id);
        b.addEventListener('click', () => { sel.has(id) ? sel.delete(id) : sel.add(id); b.setAttribute('aria-pressed', String(sel.has(id))); set(key, [...sel]); });
        return b;
      });
      statesBox.append(h('div', { class: 'row' }, h('label', {}, key), h('div', { class: 'chips' }, chips)));
    } else if (Array.isArray(allowed) && allowed.length) {
      statesBox.append(h('div', { class: 'row' }, cycle(key, allowed, (v, btn, label) => set(key, v, btn, label))));
    } else if (allowed && typeof allowed === 'object' && !Array.isArray(allowed)) {
      const value = {};
      const subs = Object.entries(allowed).filter(([, vals]) => Array.isArray(vals) && vals.length).map(([sub, vals]) =>
        cycle(sub, vals, (v, btn, label) => { value[sub] = v; set(key, { ...value }, btn, label); }));
      statesBox.append(h('div', { class: 'row' }, h('label', {}, key), h('div', { class: 'chips' }, subs)));
    } else statesBox.append(h('div', { class: 'help' }, `${key}: 값 목록이 없습니다`));
  }
  if (!statesBox.childElementCount) statesBox.append(h('div', { class: 'help' }, '문서화된 상태가 없습니다'));
}

let anchorOn = null;
function buildLists(L) {
  spawnSel.replaceChildren(h('option', { value: '' }, '— 선택 —'), ...Object.keys(L.spawns || {}).map((s) => h('option', { value: s }, s)));
  spawnSel.value = opt.spawn && L.spawns?.[opt.spawn] ? opt.spawn : '';
  pointsBox.replaceChildren(...(L.points || []).map((p) => h('button', {
    type: 'button',
    onclick: () => { engine.player.teleport({ x: p.x, y: p.y, z: p.z, yaw: engine.player.yaw }); canvas.focus(); },
  }, h('span', {}, `${p.id}${p.enabled === false ? ' (꺼짐)' : ''}`), h('small', {}, `${p.kind ?? ''} · ${p.label ?? ''}`))));
  if (!pointsBox.childElementCount) pointsBox.append(h('div', { class: 'help' }, '지점이 없습니다'));
  anchorOn = null; engine.setCameraOverride(null);
  const look = { x: 0, y: 0, z: 0 };
  anchorsBox.replaceChildren(...Object.entries(L.anchors || {}).map(([name, a]) => {
    const b = h('button', { type: 'button', 'aria-pressed': 'false' }, h('span', {}, name), h('small', {}, `${a.x?.toFixed?.(0)}, ${a.y?.toFixed?.(0)}, ${a.z?.toFixed?.(0)}`));
    b.addEventListener('click', () => {
      const on = anchorOn !== name;
      anchorsBox.querySelectorAll('button').forEach((x) => x.setAttribute('aria-pressed', 'false'));
      anchorOn = on ? name : null; b.setAttribute('aria-pressed', String(on));
      engine.setCameraOverride(on ? (cam) => {
        const target = a.target || a.look || engine.player.position;
        cam.position.set(a.x, a.y, a.z); look.x = target.x; look.y = target.y ?? 0; look.z = target.z; cam.lookAt(look.x, look.y, look.z);
      } : null);
    });
    return b;
  }));
  if (!anchorsBox.childElementCount) anchorsBox.append(h('div', { class: 'help' }, '앵커가 없습니다'));
}

// env: preset to use; null = the level's suggested env.
async function load(id, spawn, env = null) {
  opt.level = id; levelSel.value = id;
  status.className = 'tile busy'; status.setAttribute('role', 'status'); status.textContent = `${id} 불러오는 중…`;
  const t0 = performance.now();
  try {
    const L = await engine.enter(id, spawn ?? undefined);
    opt.env = env || (PRESETS[L.env] ? L.env : 'day');
    envSel.value = opt.env; applyEnv(0);
    buildStates(L); buildLists(L);
    status.className = 'tile ok'; status.textContent = `${id} · ${((performance.now() - t0) / 1000).toFixed(1)}초에 불러옴`;
    opt.spawn = spawn; syncUrl();
  } catch (err) {
    console.error(`[dev] level ${id} failed`, err);
    showError(`${id}: ${err.message}`);
    statesBox.replaceChildren(); pointsBox.replaceChildren(); anchorsBox.replaceChildren();
  }
}

setInterval(() => {
  const s = engine.stats(), p = engine.player, n = engine.nearest;
  statsBox.textContent = `${s.fps} fps · 품질 ${engine.quality}\n드로우콜 ${s.drawCalls} · 삼각형 ${(s.triangles / 1000).toFixed(0)}k\n위치 ${p.x.toFixed(1)}, ${p.y.toFixed(2)}, ${p.z.toFixed(1)} · 방향 ${(p.yaw * 180 / Math.PI).toFixed(0)}°\n가까운 지점 ${n ? n.id : '없음'} · 환경 ${engine.env}${opt.rain ? ' + 비' : ''}`;
}, 500);

envSel.value = opt.env || 'day';
await engine.ready;
await load(opt.level, opt.spawn, opt.env);
canvas.focus();

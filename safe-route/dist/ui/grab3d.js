// Taking things by hand: the camera steps up to the shelf (or the stock at home), the products stand there
// in 3D, and a click reaches out and the item flies into the basket (or the backpack); a right click puts
// one back. Same contracts as ui/shop.js (-> { cart, total } | null) and ui/bag.js (-> { bag }).
// Keys: ←/→ choose, Enter/Space take, Backspace put back, Esc leave.
import * as THREE from 'three';
import { el, put, won, fmtKg, meterState, METER_WORD, announce } from './mini-dom.js';
import { goalStatus, itemNote } from './bag.js';
import { createLayout, addUnit, removeUnit, togglePocket, tidyLayout, sameLayout, pocketLoad, usedCells } from './bag-grid.js';
import { CATALOG, POCKET_L } from '../game/items.js';
import { shelfLayout, slotX } from './grab-layout.js';

// [shape, colour, w, h, d] in metres; cylinders use w as the diameter.
const LOOK = {
  water_2l: ['cyl', 0x8cc8e6, 0.11, 0.32], water_500: ['cyl', 0xa9d8ef, 0.07, 0.21], rice_instant: ['box', 0xf2ede3, 0.15, 0.05, 0.12],
  canned: ['cyl', 0xc8a13c, 0.08, 0.1], ramen: ['box', 0xcf3b2c, 0.16, 0.05, 0.12], bars: ['box', 0xe2ad38, 0.14, 0.04, 0.07],
  radio: ['box', 0x3b4046, 0.2, 0.12, 0.07], batteries_aa: ['box', 0xd6a21e, 0.08, 0.1, 0.02], flashlight: ['cyl', 0x2e3338, 0.04, 0.18],
  powerbank: ['box', 0xe7ebee, 0.07, 0.13, 0.02], candles: ['box', 0xf3eee2, 0.1, 0.14, 0.05], first_aid: ['box', 0xf2f4f3, 0.2, 0.13, 0.08],
  masks: ['box', 0xdbe7f1, 0.18, 0.08, 0.1], wipes: ['box', 0x58b29e, 0.18, 0.06, 0.1], tissue: ['cyl', 0xf6f6f2, 0.11, 0.1],
  trash_bags: ['cyl', 0x3f6c4e, 0.07, 0.1], tape: ['cyl', 0x8a8e93, 0.1, 0.05], raincoat: ['box', 0xe5d04a, 0.16, 0.04, 0.2],
  blanket: ['box', 0xc3c9d0, 0.14, 0.03, 0.18], water_jug: ['box', 0x4a8fd4, 0.2, 0.25, 0.12], gas_stove: ['box', 0xc3352d, 0.34, 0.1, 0.27],
  butane: ['cyl', 0xebe5db, 0.07, 0.2], whistle: ['cyl', 0xef8a25, 0.03, 0.07], clothes: ['box', 0x7b8aa0, 0.25, 0.08, 0.2],
  hygiene_kit: ['box', 0x9fc7d8, 0.18, 0.08, 0.1], cash: ['box', 0xd9c79a, 0.15, 0.01, 0.07], contact_card: ['box', 0xf5f0e0, 0.09, 0.005, 0.06],
  doc_copies: ['box', 0xf0f0ea, 0.21, 0.01, 0.3], id_card: ['box', 0xb9d4ec, 0.085, 0.005, 0.055], meds: ['box', 0xffffff, 0.08, 0.05, 0.04],
  old_bars: ['box', 0xb08a48, 0.14, 0.04, 0.07], batteries_old: ['box', 0x8c7a45, 0.08, 0.1, 0.02], towel: ['box', 0x7fa7bd, 0.22, 0.07, 0.15],
  photo_album: ['box', 0x6e3a33, 0.24, 0.05, 0.2], blackout_curtain: ['box', 0x262a33, 0.28, 0.3, 0.07],
};
const hex = (id) => `#${((LOOK[id] || [, 0xb9a27f])[1]).toString(16).padStart(6, '0')}`;
const geoCache = new Map();
function geo(id) {
  const [shape, , w = 0.12, h = 0.15, d = 0.08] = LOOK[id] || ['box'];
  const k = `${shape}${w}${h}${d}`;
  if (!geoCache.has(k)) geoCache.set(k, shape === 'cyl' ? new THREE.CylinderGeometry(w / 2, w / 2, h, 14).translate(0, h / 2, 0) : new THREE.BoxGeometry(w, h, d).translate(0, h / 2, 0));
  return geoCache.get(k);
}
function label(title, sub) { // a small price tag / name card as a sprite
  const c = document.createElement('canvas'); c.width = 256; c.height = 96;
  const g = c.getContext('2d');
  g.fillStyle = 'rgba(20,22,26,.86)'; g.beginPath(); g.roundRect?.(2, 2, 252, 92, 14); g.fill?.();
  if (!g.roundRect) g.fillRect(2, 2, 252, 92);
  g.fillStyle = '#fff'; g.font = 'bold 30px sans-serif'; g.textAlign = 'center'; g.fillText(title.length > 9 ? `${title.slice(0, 9)}…` : title, 128, 42);
  g.fillStyle = '#ffc04d'; g.font = 'bold 26px sans-serif'; g.fillText(sub, 128, 80, 236); // squeezed, not cut, when a note rides along
  const s = new THREE.Sprite(new THREE.SpriteMaterial({ map: new THREE.CanvasTexture(c), depthTest: false, transparent: true }));
  s.scale.set(0.26, 0.0975, 1); s.renderOrder = 20;
  return s;
}
function container(kind) { // an open basket or backpack: four walls and a floor
  const g = new THREE.Group(), m = new THREE.MeshStandardMaterial({ color: kind === 'bag' ? 0x2c3d5a : 0xc8302a, roughness: 0.8 });
  const W = 0.42, D = 0.3, H = 0.22, T = 0.012;
  for (const [w, h, d, x, y, z] of [[W, T, D, 0, 0, 0], [W, H, T, 0, H / 2, -D / 2], [W, H, T, 0, H / 2, D / 2], [T, H, D, -W / 2, H / 2, 0], [T, H, D, W / 2, H / 2, 0]]) {
    const b = new THREE.Mesh(new THREE.BoxGeometry(w, h, d), m); b.position.set(x, y, z); g.add(b);
  }
  return g;
}

// cfg: { kind: 'shop'|'bag', title, items: [{ id, name, sub, out }], st: { id: { src, dst } }, canTake(id) -> true|reason,
//        foot(): nodes, buttons: [{ label, primary, act(close) }], escape: () => result, info: [text], onChange(),
//        bag only: side(refresh): nodes (the backpack grid), onPut(id), toggle(id) (a held chip pressed; true = handled),
//        chipNote(id): node, onSelect(id) }
function grabRoom(engine, cfg) {
  const { scene, camera } = engine, P = engine.player, L = engine.level, canvas = engine.renderer.domElement;
  // Face the shelf: the nearest tall solid within 2.5 m, squared to its side; else straight ahead.
  let f = new THREE.Vector3(-Math.sin(P.yaw), 0, -Math.cos(P.yaw)), reach = cfg.kind === 'shop' ? 1.3 : 0.95;
  if (cfg.kind === 'shop') {
    let best = 2.5;
    for (const s of L?.solids || []) {
      if (!s || s.off || s.y1 < P.y + 1.2 || s.y0 > P.y + 0.5) continue;
      const cx = Math.min(Math.max(P.x, s.x0), s.x1), cz = Math.min(Math.max(P.z, s.z0), s.z1), d = Math.hypot(cx - P.x, cz - P.z);
      if (d > 0.05 && d < best) { best = d; f = Math.abs(cx - P.x) > Math.abs(cz - P.z) ? new THREE.Vector3(Math.sign(cx - P.x), 0, 0) : new THREE.Vector3(0, 0, Math.sign(cz - P.z)); reach = d; }
    }
  }
  const n = f.clone().negate(), right = new THREE.Vector3(-f.z, 0, f.x);
  const base = new THREE.Vector3(P.x, P.y, P.z).addScaledVector(f, reach - 0.12);
  // Rows and spacing (ui/grab-layout.js): a big home stock gets three shelf heights, and a long row closes up
  // (items and tags scaled with it) so every tag stays on screen.
  const dist = cfg.kind === 'shop' ? 1.15 : 1.1, N = cfg.items.length;
  const lay = shelfLayout({ kind: cfg.kind, n: N, fov: camera.fov, aspect: camera.aspect || innerWidth / innerHeight, dist });
  const midY = P.y + lay.rows.reduce((a, b) => a + b, 0) / lay.rows.length; // middle of the rows in use
  const camPos = base.clone().addScaledVector(n, dist).setY(midY + 0.3);
  const lookAt = base.clone().setY(midY - 0.12);
  const root = new THREE.Group(); scene.add(root);
  const fwd = lookAt.clone().sub(camPos).normalize(), camUp = new THREE.Vector3().crossVectors(right, fwd).normalize();
  const box = container(cfg.kind); box.position.copy(camPos).addScaledVector(fwd, 0.85).addScaledVector(camUp, -0.3).addScaledVector(right, 0.48); box.scale.setScalar(0.6); // lower right, clear of the panel
  box.lookAt(box.position.clone().add(n)); root.add(box);

  const slots = cfg.items.map((it, i) => {
    const { row, x } = slotX(lay, i, N);
    const pos = base.clone().addScaledVector(right, x).setY(P.y + lay.rows[Math.min(row, lay.rows.length - 1)]);
    const mat = new THREE.MeshStandardMaterial({ color: (LOOK[it.id] || [, 0xb9a27f])[1], roughness: 0.55, emissive: 0xffc04d, emissiveIntensity: 0 });
    const g = new THREE.Group(); g.position.copy(pos); g.lookAt(pos.clone().add(n)); g.scale.setScalar(lay.scale); root.add(g);
    const tag = label(it.name, it.sub); tag.scale.multiplyScalar(lay.scale); tag.position.copy(pos).setY(pos.y - 0.06).addScaledVector(n, 0.12); root.add(tag);
    return { it, g, mat, tag, copies: [] };
  });
  const inBox = [];
  function stock(slot) { // show up to four copies of what is still there, one behind the other
    const want = slot.it.out ? 0 : Math.min(4, cfg.st[slot.it.id].src);
    while (slot.copies.length > want) slot.g.remove(slot.copies.pop());
    while (slot.copies.length < want) { const m = new THREE.Mesh(geo(slot.it.id), slot.mat); m.position.z = -slot.copies.length * 0.09; m.userData.slot = slot; slot.g.add(m); slot.copies.push(m); }
    slot.tag.material.opacity = want || slot.it.out ? 1 : 0.45;
  }
  slots.forEach(stock);

  let sel = 0;
  const flights = [];
  function fly(mesh, from, to, done) { flights.push({ mesh, from, to, mid: from.clone().lerp(to, 0.5).add(new THREE.Vector3(0, 0.25, 0)), t: 0, done }); root.add(mesh); }
  function take(slot) {
    const id = slot.it.id, ok = slot.it.out ? '품절입니다.' : cfg.st[id].src <= 0 ? '더 없습니다.' : cfg.canTake(id);
    if (ok !== true) { announce(ok); toast(ok); if (cfg.side) refresh(); return; }
    cfg.st[id].src--; cfg.st[id].dst++;
    const from = new THREE.Vector3(); slot.copies[0].getWorldPosition(from);
    stock(slot); engine.act('point', 0.45);
    const m = new THREE.Mesh(geo(id), slot.mat);
    const to = box.position.clone().add(new THREE.Vector3((Math.random() - 0.5) * 0.25, 0.04 + Math.min(inBox.length, 8) * 0.012, (Math.random() - 0.5) * 0.15));
    fly(m, from, to, () => { m.scale.setScalar(0.85); m.rotation.y = Math.random() * 3; m.userData.id = id; inBox.push(m); });
    dispatchEvent(new CustomEvent('gyeongbo:ui', { detail: 'select' }));
    refresh(); announce(`${slot.it.name} 하나 담음`);
  }
  function putBack(slot) {
    const id = slot.it.id; if (cfg.st[id].dst <= 0) return;
    cfg.st[id].dst--; cfg.st[id].src++; cfg.onPut?.(id);
    const i = inBox.findLastIndex((m) => m.userData.id === id), m = i >= 0 ? inBox.splice(i, 1)[0] : new THREE.Mesh(geo(id), slot.mat);
    const from = m.position.clone(); m.scale.setScalar(1);
    fly(m, from, slot.g.position.clone(), () => { root.remove(m); stock(slot); });
    dispatchEvent(new CustomEvent('gyeongbo:ui', { detail: 'close' }));
    refresh(); announce(`${slot.it.name} 하나 내려놓음`);
  }

  // ---- DOM: what you hold, the totals, the buttons ----
  const note = el('p', { class: 'grab3d-toast', 'aria-hidden': 'true' });
  let noteT = 0;
  function toast(t) { note.textContent = t; note.classList.add('on'); clearTimeout(noteT); noteT = setTimeout(() => note.classList.remove('on'), 1600); }
  const held = el('ul', { class: 'grab3d-held', 'aria-label': '담은 물건' }), sum = el('div', { class: 'grab3d-sum' }), side = cfg.side ? el('div', { class: 'grab3d-pack' }) : null;
  const ui = el('div', { class: `grab3d is-${cfg.kind}`, role: 'dialog', 'aria-label': cfg.title });
  let resolve; const done = new Promise((r) => { resolve = r; });
  const close = (v) => { if (closed) return; closed = true; cleanup(); resolve(v); };
  let closed = false;
  const btns = cfg.buttons.map((b) => { const e = el('button', { type: 'button', class: `mg-btn ${b.primary ? 'mg-primary' : 'mg-ghost'}` }, b.label); e.addEventListener('click', () => b.act(close)); return e; });
  put(ui,
    el('header', { class: 'grab3d-top' }, el('b', {}, cfg.title), cfg.info?.length ? el('ul', {}, cfg.info.map((t) => el('li', {}, t))) : null),
    note,
    el('footer', { class: `grab3d-foot${side ? ' has-pack' : ''}` }, side, el('div', { class: 'grab3d-main' }, held, sum, el('div', { class: 'mg-actions' }, btns),
      el('p', { class: 'mg-keys' }, '클릭: 집기 · 우클릭: 내려놓기 · ←/→ 고르기 · Enter 집기 · Backspace 내려놓기 · Esc 나가기'))));
  document.body.append(ui); document.documentElement.classList.add('grab3d-open');
  (document.activeElement instanceof HTMLElement) && document.activeElement.blur(); // Enter takes, it does not press a button
  function refresh() {
    held.replaceChildren(...cfg.items.filter((i) => cfg.st[i.id].dst > 0).map((i) => {
      const slot = slots.find((s) => s.it === i), pocketable = !!cfg.toggle && !!CATALOG[i.id]?.pocket;
      const b = el('button', { type: 'button', class: 'grab3d-chip', title: pocketable ? '앞주머니 ↔ 본칸' : '하나 내려놓기' }, `${i.name} ×${cfg.st[i.id].dst}`, cfg.chipNote?.(i.id));
      b.addEventListener('click', () => { if (pocketable && cfg.toggle(i.id)) refresh(); else putBack(slot); });
      if (!pocketable) return el('li', {}, b);
      const x = el('button', { type: 'button', class: 'grab3d-chip grab3d-chip-x', title: '하나 내려놓기', 'aria-label': `${i.name} 하나 내려놓기` }, '−');
      x.addEventListener('click', () => putBack(slot));
      return el('li', { class: 'grab3d-pair' }, b, x);
    }));
    if (!held.children.length) held.append(el('li', { class: 'grab3d-empty' }, cfg.kind === 'shop' ? '장바구니가 비어 있다.' : '가방이 비어 있다.'));
    sum.replaceChildren(...cfg.foot());
    side?.replaceChildren(...cfg.side(refresh));
    cfg.onChange?.(btns);
  }
  refresh();

  // ---- input ----
  const ray = new THREE.Raycaster(), ndc = new THREE.Vector2();
  const hitSlot = (e) => {
    const r = canvas.getBoundingClientRect(); ndc.set(((e.clientX - r.left) / r.width) * 2 - 1, -((e.clientY - r.top) / r.height) * 2 + 1);
    ray.setFromCamera(ndc, camera);
    const hit = ray.intersectObjects(slots.flatMap((s) => s.copies.length ? s.copies : []), false)[0];
    if (hit) return hit.object.userData.slot;
    const tags = ray.intersectObjects(slots.map((s) => s.tag), false)[0];
    return tags ? slots.find((s) => s.tag === tags.object) : null;
  };
  const pick = (i) => { if (i !== sel) { sel = i; cfg.onSelect?.(slots[sel].it.id); } };
  const onMove = (e) => { const s = hitSlot(e); if (s) pick(slots.indexOf(s)); canvas.style.cursor = s ? 'grab' : ''; };
  const onDown = (e) => { const s = hitSlot(e); if (!s) return; e.preventDefault(); pick(slots.indexOf(s)); if (e.button === 2) putBack(s); else if (e.button === 0) take(s); };
  const onCtx = (e) => e.preventDefault();
  const onKey = (e) => {
    if (e.target.closest?.('.grab3d-foot') && (e.key === 'Enter' || e.key === ' ')) return; // a focused button
    if (e.key === 'Escape') { e.preventDefault(); close(cfg.escape()); }
    else if (e.key === 'ArrowRight' || e.key === 'ArrowLeft') { e.preventDefault(); pick((sel + (e.key === 'ArrowRight' ? 1 : slots.length - 1)) % slots.length); announce(`${slots[sel].it.name} · ${slots[sel].it.sub}`); }
    else if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); take(slots[sel]); }
    else if (e.key === 'Backspace' || e.key === 'Delete') { e.preventDefault(); putBack(slots[sel]); }
  };
  canvas.addEventListener('pointermove', onMove); canvas.addEventListener('pointerdown', onDown); canvas.addEventListener('contextmenu', onCtx);
  addEventListener('keydown', onKey, true);

  // ---- camera + per frame ----
  // The lowest tags stay above the panel: drop the camera (and the basket with it) until they clear its top edge.
  {
    const footEl = ui.querySelector('.grab3d-foot'), top = footEl ? 1 - (2 * footEl.getBoundingClientRect().top) / innerHeight : -1;
    const c = new THREE.PerspectiveCamera(camera.fov, camera.aspect || innerWidth / innerHeight, 0.05, 50); c.position.copy(camPos); c.lookAt(lookAt); c.updateMatrixWorld();
    const low = Math.min(...slots.map((s) => s.tag.position.clone().setY(s.tag.position.y - s.tag.scale.y / 2).project(c).y));
    const lift = Math.min(0.6, Math.max(0, top + 0.05 - low)) * Math.tan((camera.fov * Math.PI) / 360) * dist; // NDC -> metres at the shelf
    if (lift > 0) { camPos.y -= lift; lookAt.y -= lift; box.position.y -= lift; }
  }
  const view = engine.view; engine.setView('first'); // hide the player's own body while the camera stands at the shelf
  const from = { p: camera.position.clone(), q: camera.quaternion.clone() }, aim = new THREE.PerspectiveCamera(); aim.position.copy(camPos); aim.lookAt(lookAt); // a camera looks down -z
  let k = 0;
  engine.setCameraOverride((cam, dt) => {
    k = Math.min(1, k + dt / 0.5); const e = k * k * (3 - 2 * k);
    cam.position.lerpVectors(from.p, camPos, e); cam.quaternion.slerpQuaternions(from.q, aim.quaternion, e);
  });
  const offF = engine.onFrame((dt) => {
    slots.forEach((s, i) => { s.mat.emissiveIntensity += ((i === sel ? 0.35 : 0) - s.mat.emissiveIntensity) * Math.min(1, dt * 10); });
    for (let i = flights.length - 1; i >= 0; i--) {
      const F = flights[i]; F.t = Math.min(1, F.t + dt / 0.42); const t = F.t, a = 1 - t;
      F.mesh.position.set(a * a * F.from.x + 2 * a * t * F.mid.x + t * t * F.to.x, a * a * F.from.y + 2 * a * t * F.mid.y + t * t * F.to.y, a * a * F.from.z + 2 * a * t * F.mid.z + t * t * F.to.z);
      F.mesh.rotation.x += dt * 6;
      if (t >= 1) { F.mesh.rotation.x = 0; flights.splice(i, 1); F.done(); }
    }
  });
  function cleanup() {
    offF(); engine.setCameraOverride(null); engine.setView(view);
    canvas.removeEventListener('pointermove', onMove); canvas.removeEventListener('pointerdown', onDown); canvas.removeEventListener('contextmenu', onCtx);
    removeEventListener('keydown', onKey, true); canvas.style.cursor = '';
    ui.remove(); root.removeFromParent(); document.documentElement.classList.remove('grab3d-open');
    for (const s of slots) { s.mat.dispose(); s.tag.material.map.dispose(); s.tag.material.dispose(); }
  }
  return done;
}

const MAX_QTY = 20;
// ui/shop.js contract, by hand.
export function shop3d(engine) {
  return ({ title = '진열대', money = 0, items = [], info = [] } = {}) => {
    const st = Object.fromEntries(items.map((i) => [i.id, { src: i.stock === 'out' ? 0 : MAX_QTY, dst: 0 }]));
    const total = () => items.reduce((s, i) => s + (i.price || 0) * st[i.id].dst, 0), kg = () => items.reduce((s, i) => s + (i.weightKg || 0) * st[i.id].dst, 0);
    return grabRoom(engine, {
      kind: 'shop', title: `장보기 · ${title}`, info: info.map((i) => i.text),
      items: items.map((i) => ({ id: i.id, name: i.name, sub: i.stock === 'out' ? '품절' : `${won(i.price || 0)}${i.stock === 'low' ? ' · 적음' : ''}`, out: i.stock === 'out' })),
      st, canTake: (id) => (total() + (items.find((i) => i.id === id).price || 0) > money ? '가진 돈이 모자란다.' : true),
      foot: () => [el('span', {}, `합계 ${won(total())} / 가진 돈 ${won(money)}`), el('span', {}, `들고 갈 무게 약 ${fmtKg(kg())}`)],
      buttons: [{ label: '그만두기', act: (c) => c(null) }, { label: '계산대로 가져가기', primary: true, act: (c) => c({ cart: Object.fromEntries(Object.entries(st).filter(([, v]) => v.dst > 0).map(([id, v]) => [id, v.dst])), total: total() }) }],
      escape: () => null,
    });
  };
}
// ui/bag.js contract, by hand: the home stock laid out in front, the backpack below. The backpack is the
// 10x5 grid of game/pack.js (0.5 L a cell) plus the 0.5 L front pocket (ui/bag-grid.js keeps where each piece
// sits). Items may carry pocket / charge / expiry (ui/bag.js bagInfo). tidy: { cost: '1분', run() } when
// tidying costs time (the alert); its run() is called each time the layout really changes.
// -> { bag: { id: n }, pocket: { id: n } }
const SHORT = (it) => { const n = itemNote(it); return !n ? '' : n.startsWith('건전지') ? n.slice(4) : `(${n})`; }; // the price tag's second line
export function bag3d(engine) {
  return ({ mode = 'guided', limit = { weightKg: 8, volumeL: 25 }, items = [], goals = [], tidy = null } = {}) => {
    const st = Object.fromEntries(items.map((i) => [i.id, { src: Math.max(0, i.home | 0), dst: Math.max(0, i.bag | 0) }]));
    const name = (id) => items.find((i) => i.id === id)?.name ?? id;
    // Lay out what is packed already; anything that no longer fits goes back home, and the panel says so.
    const packed = items.filter((i) => CATALOG[i.id] && st[i.id].dst > 0).map((i) => ({ id: i.id, n: st[i.id].dst, pocket: Math.min(Math.max(0, i.pocket | 0), st[i.id].dst) }));
    let { layout: L, overflow } = createLayout(packed);
    for (const { id, n } of overflow) { st[id].dst -= n; st[id].src += n; }
    let note = overflow.length ? `가방에 다 들어가지 않아 집에 꺼내 두었다: ${overflow.map(({ id, n }) => `${name(id)} ${n}${CATALOG[id]?.unit ?? '개'}`).join(', ')}` : '';
    let hintTidy = false, selId = null, grid = null;
    const kg = () => items.reduce((a, i) => a + (i.weightKg || 0) * st[i.id].dst, 0);
    const result = () => ({ bag: Object.fromEntries(items.map((i) => [i.id, st[i.id].dst])), pocket: { ...L.pocket } });
    const shown = items.filter((i) => st[i.id].src + st[i.id].dst > 0);
    const say = (t) => { note = t; announce(t); };
    const tidyUp = (refresh) => {
      const T = tidyLayout(L);
      if (!T || sameLayout(T, L)) say(T ? '이미 가지런히 들어 있다.' : '지금 놓인 대로가 가장 많이 들어간다.');
      else { L = T; hintTidy = false; tidy?.run?.(); say(`큰 것부터 다시 넣어 가지런히 정리했다.${tidy?.cost ? ` (${tidy.cost})` : ''}`); }
      refresh();
    };
    const mark = () => grid?.querySelectorAll('.bagpack-piece').forEach((p) => p.classList.toggle('is-sel', p.dataset.id === selId));
    return grabRoom(engine, {
      kind: 'bag', title: '가방 꾸리기', items: shown.map((i) => ({ id: i.id, name: i.name, sub: [fmtKg(i.weightKg || 0), SHORT(i)].filter(Boolean).join(' · ') })), st,
      canTake: (id) => {
        if (!CATALOG[id]) return true;
        const r = addUnit(L, id);
        if (r === 'grid' || r === 'pocket') { hintTidy = false; if (r === 'pocket') say('본칸이 차서 앞주머니에 넣었다.'); return true; }
        hintTidy = r === 'tidy';
        return r === 'tidy' ? '이대로는 자리가 없다. 정리하기를 누르면 들어갈 것 같다.' : '가방에 더 들어가지 않는다. 다른 것을 꺼내면 자리가 생긴다.';
      },
      onPut: (id) => { removeUnit(L, id); hintTidy = false; },
      toggle: (id) => {
        const r = togglePocket(L, id);
        if (r.to) { say(r.to === 'pocket' ? `${name(id)}: 앞주머니로. 바로 꺼낼 수 있다.` : `${name(id)}: 본칸으로 옮겼다.`); return true; }
        hintTidy = r.refused === 'tidy';
        say({ pocketFull: `앞주머니(${POCKET_L}L)가 꽉 찼다.`, tidy: '본칸에 자리가 없다. 정리하기를 먼저 해 보자.', full: '본칸에 자리가 없어 앞주머니에 그대로 두었다.' }[r.refused] ?? '');
        return true;
      },
      chipNote: (id) => (L.pocket[id] ? el('small', { class: 'grab3d-pocket-tag' }, ` · 앞주머니${L.pocket[id] < st[id].dst ? ` ${L.pocket[id]}` : ''}`) : null),
      onSelect: (id) => { selId = id; mark(); },
      side: (refresh) => {
        const used = usedCells(L), cells = L.cols * L.rows, inPocket = Object.entries(L.pocket);
        grid = el('div', { class: 'bagpack-grid', role: 'img', 'aria-label': `가방 칸 ${cells}칸 중 ${used}칸 사용`, style: `--cols:${L.cols};--rows:${L.rows}` },
          L.pieces.map((p) => el('span', { class: 'bagpack-piece', 'data-id': p.id, title: name(p.id), style: `grid-column:${p.x + 1} / span ${p.w};grid-row:${p.y + 1} / span ${p.h};background:${hex(p.id)}` })));
        mark();
        const btn = el('button', { type: 'button', class: `mg-btn mg-ghost bagpack-tidy${hintTidy ? ' is-hint' : ''}`, title: '큰 것부터 다시 넣기' }, tidy?.cost ? `정리하기 · ${tidy.cost}` : '정리하기');
        btn.addEventListener('click', () => tidyUp(refresh));
        return [el('div', { class: 'bagpack-head' }, el('b', {}, '배낭 안'), el('small', {}, `${used}/${cells}칸`), btn), grid,
          el('p', { class: 'bagpack-pocket' }, el('b', {}, '앞주머니'), ` ${pocketLoad(L).toFixed(2)}/${POCKET_L}L`,
            el('span', {}, inPocket.length ? ` · ${inPocket.map(([id, n]) => (n > 1 ? `${name(id)} ${n}` : name(id))).join(', ')}` : ' · 작은 카드·서류는 칩을 눌러 여기로'))];
      },
      foot: () => {
        const w = kg(), sw = meterState(w, limit.weightKg);
        return [el('div', { class: 'bagpack-weight', 'data-state': sw }, el('span', {}, `무게 ${fmtKg(w)} / ${fmtKg(limit.weightKg)} · ${METER_WORD[sw]}`),
          el('span', { class: 'mg-bar', 'data-state': sw, 'aria-hidden': 'true' }, el('span', { class: 'mg-bar-fill', style: `width:${Math.min(100, (w / limit.weightKg) * 100)}%` }))),
        note ? el('p', { class: 'bagpack-note' }, note) : null,
        el('ul', { class: 'grab3d-goals' }, goalStatus(goals, mode, (id) => st[id]?.dst ?? 0, items).map(({ label: l, ok }) => el('li', { class: ok ? 'is-done' : '' }, ok === null ? '· ' : ok ? '✓ ' : '○ ', l)))].filter(Boolean);
      },
      buttons: [{ label: '다 챙겼다', primary: true, act: (c) => c(result()) }],
      escape: result,
    });
  };
}

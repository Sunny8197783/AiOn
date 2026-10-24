// 전 회차의 나 (echoes): what you did in earlier runs stays in the world for the next one. The path you
// walked comes back as a pale ghost that walks it again, the spots where you fell are marked with a red X,
// and chalk notes you wrote on the ground (B) are still there. Kept in browser storage, last 3 runs.
// createEcho({ engine, ctx, store }) -> { begin(), death(why), chalk() }
import * as THREE from 'three';
import { groundAt } from '../engine/geom.js';

const KEY = 'gyeongbo.echo.v1', RUNS = 3, MAX_TRAIL = 1500, MAX_NOTES = 30;
const PRESETS = ['여기 위험해', '이 길 막혔음', '물 있음', '숨을 곳 있음', '여기서 맞았다. 엎드려'];
const finite = (...v) => v.every(Number.isFinite);
const worth = (r) => r.trail.length > 4 || r.notes.length || r.deaths.length; // a run that left a mark

// Saved runs come from browser storage: keep only well-formed entries.
export function readEcho(raw) {
  let o; try { o = JSON.parse(raw); } catch { return { runs: [] }; }
  const runs = Array.isArray(o?.runs) ? o.runs.slice(-RUNS) : [];
  return { runs: runs.map((r) => ({
    trail: (Array.isArray(r?.trail) ? r.trail : []).filter((p) => Array.isArray(p) && typeof p[0] === 'string' && finite(p[1], p[2])).slice(-MAX_TRAIL),
    deaths: (Array.isArray(r?.deaths) ? r.deaths : []).filter((d) => typeof d?.lv === 'string' && finite(d.x, d.z)).slice(-10).map(({ lv, x, z, why, yaw }) => ({ lv, x, z, why: String(why ?? ''), yaw: Number.isFinite(yaw) ? yaw : 0 })),
    notes: (Array.isArray(r?.notes) ? r.notes : []).filter((n) => typeof n?.lv === 'string' && finite(n.x, n.z) && typeof n.text === 'string').slice(-MAX_NOTES).map(({ lv, x, z, text, yaw }) => ({ lv, x, z, text: text.slice(0, 24), yaw: Number.isFinite(yaw) ? yaw : 0 })),
  })).filter(worth) };
}

const WHY = { fall: '추락', fight: '몸싸움', blast: '포격', debris: '붕괴', fire: '화재', hurt: '부상' };

function textPlane(text, color, w = 3) {
  const c = document.createElement('canvas'); c.width = 512; c.height = 128;
  const g = c.getContext('2d'); g.font = 'bold 64px sans-serif'; g.textAlign = 'center'; g.textBaseline = 'middle';
  g.fillStyle = color; g.globalAlpha = 0.9; g.fillText(text, 256, 64);
  for (let i = 0; i < 400; i++) { g.clearRect(Math.random() * 512, Math.random() * 128, 2, 2); } // chalky gaps
  const m = new THREE.Mesh(new THREE.PlaneGeometry(w, w / 4).rotateX(-Math.PI / 2), new THREE.MeshBasicMaterial({ map: new THREE.CanvasTexture(c), transparent: true, depthWrite: false, polygonOffset: true, polygonOffsetFactor: -3 }));
  m.renderOrder = 5;
  return m;
}

export function createEcho({ engine, ctx, store }) {
  let data = readEcho(store.get(KEY));
  let run = null; // the run being recorded
  const save = () => store.set(KEY, JSON.stringify({ runs: [...data.runs, ...(run && worth(run) ? [run] : [])].slice(-RUNS) }));
  addEventListener('pagehide', () => save());
  const past = () => data.runs; // earlier runs only
  let shown = null, ghost = null, ghostI = 0, ghostPts = [], lastSave = 0;

  function show(L) { // build the marks of earlier runs for this level
    shown?.group.removeFromParent(); shown = { L, group: new THREE.Group() };
    const group = shown.group, y = (x, z) => groundAt(L.walk, x, z, 0, true)?.y ?? 0;
    let any = false;
    past().forEach((r, k) => {
      const age = past().length - k; // 1 = the last run
      for (const d of r.deaths.filter((d) => d.lv === L.id)) {
        const m = textPlane('✕', '#e0302a', 1.6); m.position.set(d.x, y(d.x, d.z) + 0.08, d.z); group.add(m);
        // the label on the near side, read facing the way you were going
        const t = textPlane(`${age}회 전의 내가 쓰러진 곳 · ${WHY[d.why] ?? ''}`, '#ff8a7a', 3);
        t.position.set(d.x + Math.sin(d.yaw) * 0.9, y(d.x, d.z) + 0.08, d.z + Math.cos(d.yaw) * 0.9); t.rotation.y = d.yaw; group.add(t); any = true;
      }
      for (const n of r.notes.filter((n) => n.lv === L.id)) { const t = textPlane(n.text, '#f4f1e8'); t.position.set(n.x, y(n.x, n.z) + 0.08, n.z); t.rotation.y = n.yaw + Math.random() * 0.3 - 0.15; group.add(t); any = true; }
    });
    // the last run's walk through this level, for the ghost
    const last = past().at(-1);
    ghostPts = last ? last.trail.filter((p) => p[0] === L.id).map(([, x, z]) => ({ x, z })) : [];
    ghostI = 0;
    if (ghostPts.length > 4) { ghost ||= makeGhost(); if (ghost) { L.group.add(ghost.group); ghost.group.position.set(ghostPts[0].x, y(ghostPts[0].x, ghostPts[0].z), ghostPts[0].z); any = true; } }
    else ghost?.group.removeFromParent();
    L.group.add(group);
    if (any && !shown.told) { shown.told = true; ctx.toast('이전 회차의 내가 남긴 흔적이 보인다. 그때의 나는 이 길로 걸었다.', 'self', 5200); }
  }
  function makeGhost() {
    try {
      const p = engine.people.createPerson({ preset: 'jeongmin', seed: 1 });
      p.group.traverse((o) => { if (o.isMesh) { o.material = [].concat(o.material).map((m) => { const c = m.clone(); c.transparent = true; c.opacity = 0.3; c.depthWrite = false; c.emissive?.setHex(0x6fa8ff); c.emissiveIntensity = 0.6; return c; }); if (o.material.length === 1) o.material = o.material[0]; o.castShadow = false; } });
      p.setAnim('walk'); p.hostile = false; p.ghost = true;
      engine.persons.delete(p); // not something to punch
      return p;
    } catch (err) { console.error('[echo] ghost model failed', err); return null; }
  }

  engine.onFrame((dt, t) => {
    const L = engine.level, G = ctx.G;
    if (L && shown?.L !== L) show(L);
    if (ghost && ghostPts.length > 4 && L) { // walk the old path at an easy pace, then start over
      const g = ghost.group.position, to = ghostPts[ghostI], d = Math.hypot(to.x - g.x, to.z - g.z);
      if (d < 0.3) ghostI = (ghostI + 1) % ghostPts.length;
      else { const k = Math.min(1, (1.5 * dt) / d); g.x += (to.x - g.x) * k; g.z += (to.z - g.z) * k; ghost.group.rotation.y = Math.atan2(-(to.x - g.x), -(to.z - g.z)); }
      if (ghostI === 0 && d > 6) g.set(to.x, g.y, to.z);
      g.y = groundAt(L.walk, g.x, g.z, g.y + 0.3, false, 2)?.y ?? g.y;
      ghost.setSpeed?.(1.5); ghost.update?.(dt);
    }
    if (!run || !G || !ctx.roaming || !L) return;
    const P = engine.player, last = run.trail.at(-1);
    if (!last || last[0] !== L.id || Math.hypot(last[1] - P.x, last[2] - P.z) > 1.5) { run.trail.push([L.id, Math.round(P.x * 10) / 10, Math.round(P.z * 10) / 10]); if (run.trail.length > MAX_TRAIL) run.trail.shift(); }
    if (t - lastSave > 10) { lastSave = t; save(); }
  });

  return {
    // A new game: the run that was being recorded becomes an echo.
    begin() { if (run && worth(run)) data.runs = [...data.runs, run].slice(-RUNS); run = { trail: [], deaths: [], notes: [] }; save(); shown = null; },
    // Continuing a save keeps recording into the same run.
    resume() { run ||= { trail: [], deaths: [], notes: [] }; },
    death(why) { const L = engine.level; if (!run || !L) return; run.deaths.push({ lv: L.id, x: engine.player.x, z: engine.player.z, why, yaw: engine.player.yaw }); save(); },
    async chalk() {
      const L = engine.level; if (!run || !L) return;
      const pick = await ctx.say('분필', ['바닥에 뭐라고 적을까? 다음 회차의 내가 보게 된다.'], [...PRESETS.map((t, i) => ({ id: String(i), label: t })), { id: 'own', label: '직접 쓰기' }, { id: 'no', label: '그만' }]);
      let text = PRESETS[Number(pick)];
      if (pick === 'own') text = (prompt('바닥에 적을 말 (24자까지)') || '').trim().slice(0, 24);
      if (!text || pick === 'no') return;
      run.notes.push({ lv: L.id, x: engine.player.x + Math.sin(engine.player.yaw) * -1.2, z: engine.player.z + Math.cos(engine.player.yaw) * -1.2, text, yaw: engine.player.yaw }); // just ahead of you if (run.notes.length > MAX_NOTES) run.notes.shift(); save();
      const n = run.notes.at(-1), m = textPlane(text, '#f4f1e8'); m.position.set(n.x, engine.player.y + 0.08, n.z); m.rotation.y = n.yaw; L.group.add(m);
      ctx.toast('바닥에 분필로 적었다. 다음 회차의 내가 이걸 보게 된다.', 'self');
    },
  };
}

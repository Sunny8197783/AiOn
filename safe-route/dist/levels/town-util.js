// Small helpers shared by the town modules: rect carving, walk-zone heights, world-UV geometry,
// wind sway for foliage/cloth and Hangul canvas boards.
import * as THREE from 'three';
import { FONT } from '../engine/kit.js';

// Subtract holes from a rect (keeps extra fields such as y). Returns non-overlapping rects.
export function carve(r, holes) {
  let parts = [r];
  for (const h of holes) {
    const next = [];
    for (const p of parts) {
      if (h.x1 <= p.x0 || h.x0 >= p.x1 || h.z1 <= p.z0 || h.z0 >= p.z1) { next.push(p); continue; }
      if (h.z0 > p.z0) next.push({ ...p, z1: h.z0 });
      if (h.z1 < p.z1) next.push({ ...p, z0: h.z1 });
      const z0 = Math.max(p.z0, h.z0), z1 = Math.min(p.z1, h.z1);
      if (h.x0 > p.x0) next.push({ ...p, x1: h.x0, z0, z1 });
      if (h.x1 < p.x1) next.push({ ...p, x0: h.x1, z0, z1 });
    }
    parts = next;
  }
  return parts;
}

export function zoneY(w, x, z) {
  if (!w.ramp) return w.y;
  const { axis, y0, y1 } = w.ramp;
  const t = axis === 'x' ? (x - w.x0) / (w.x1 - w.x0) : (z - w.z0) / (w.z1 - w.z0);
  return y0 + (y1 - y0) * Math.min(1, Math.max(0, t));
}
// Highest walk surface at (x, z); 0 when outside every zone.
export function heightAt(walk, x, z) {
  let best = -Infinity;
  for (const w of walk) if (x >= w.x0 && x <= w.x1 && z >= w.z0 && z <= w.z1) best = Math.max(best, zoneY(w, x, z));
  return best === -Infinity ? 0 : best;
}

// Yaw (0 = facing -z) for a person or object facing direction (dx, dz).
export const yawTo = (dx, dz) => Math.atan2(-dx, -dz);

// Horizontal plane over a rect at height y, UVs in world metres (textures line up across pieces).
export function flatGeo(r, y = 0) {
  const g = new THREE.PlaneGeometry(r.x1 - r.x0, r.z1 - r.z0);
  g.rotateX(-Math.PI / 2); g.translate((r.x0 + r.x1) / 2, y, (r.z0 + r.z1) / 2);
  const p = g.attributes.position, uv = g.attributes.uv;
  for (let i = 0; i < p.count; i++) uv.setXY(i, p.getX(i), -p.getZ(i));
  return g;
}
export function flat(r, y, material, receive = true) {
  const m = new THREE.Mesh(flatGeo(r, y), material);
  m.receiveShadow = receive; m.castShadow = false;
  return m;
}
// Fence line -> blocking AABB (0.4 m thick, 2 m tall), ends padded so corners close.
export function lineSolid([x0, z0, x1, z1], t = 0.2, y0 = -0.5, y1 = 2) {
  return { x0: Math.min(x0, x1) - t, x1: Math.max(x0, x1) + t, z0: Math.min(z0, z1) - t, z1: Math.max(z0, z1) + t, y0, y1 };
}
export const rectSolid = (r, y0 = -0.5, y1 = 3, extra = {}) => ({ x0: r.x0, x1: r.x1, z0: r.z0, z1: r.z1, y0, y1, ...extra });

// Shared wind uniforms. sway() patches a material so vertices move with height above their origin.
export const WIND = { uTime: { value: 0 }, uWind: { value: 1 } };
export function sway(material, { amp = 0.06, freq = 1.7, ref = 1 } = {}) {
  material.onBeforeCompile = (sh) => {
    sh.uniforms.uTime = WIND.uTime; sh.uniforms.uWind = WIND.uWind;
    sh.vertexShader = 'uniform float uTime; uniform float uWind;\n' + sh.vertexShader.replace('#include <begin_vertex>', `#include <begin_vertex>
      vec3 swayO = vec3(0.0);
      #ifdef USE_INSTANCING
        swayO = vec3(instanceMatrix[3][0], instanceMatrix[3][1], instanceMatrix[3][2]);
      #endif
      vec4 swayW = modelMatrix * vec4(swayO, 1.0);
      float swayP = swayW.x * 0.37 + swayW.z * 0.23;
      float swayK = uWind * (0.65 + 0.35 * sin(uTime * 0.45 + swayP * 0.2)) * max(0.0, position.y + ${ref.toFixed(2)});
      transformed.x += sin(uTime * ${freq.toFixed(2)} + swayP + position.y * 1.7) * ${amp.toFixed(3)} * swayK;
      transformed.z += cos(uTime * ${(freq * 0.83).toFixed(2)} + swayP * 1.3 + position.x * 1.1) * ${(amp * 0.8).toFixed(3)} * swayK;`);
  };
  material.customProgramCacheKey = () => `sway${amp}:${freq}:${ref}`;
  return material;
}

// Notice/board canvas: header band + lines. Returns { texture, draw(title, lines, accent) }.
export function board(kit, { w = 512, h = 384, paper = '#f4f1e8' } = {}) {
  const c = kit.canvas(w, h), tex = kit.texture(c);
  function draw(title, lines = [], accent = '#1f4e8c', sub = '') {
    const g = c.getContext('2d');
    g.fillStyle = paper; g.fillRect(0, 0, w, h);
    g.fillStyle = accent; g.fillRect(0, 0, w, h * 0.17);
    g.fillStyle = '#ffffff'; g.font = `800 ${Math.round(h * 0.085)}px ${FONT}`; g.textBaseline = 'middle'; g.textAlign = 'left';
    g.fillText(title, w * 0.05, h * 0.085, w * 0.9);
    if (sub) { g.fillStyle = '#5a5a5a'; g.font = `600 ${Math.round(h * 0.045)}px ${FONT}`; g.fillText(sub, w * 0.05, h * 0.22, w * 0.9); }
    g.fillStyle = '#1d1d1d';
    let y = h * (sub ? 0.32 : 0.27), size = Math.round(h * 0.06);
    for (const line of lines.slice(0, 9)) {
      g.font = `600 ${size}px ${FONT}`;
      for (const part of wrap(g, String(line), w * 0.9)) { if (y > h * 0.95) break; g.fillText(part, w * 0.05, y); y += size * 1.35; }
    }
    tex.needsUpdate = true;
  }
  return { canvas: c, texture: tex, draw };
}
function wrap(g, text, max) {
  const out = []; let line = '';
  for (const ch of text) { if (g.measureText(line + ch).width > max && line) { out.push(line); line = ch.trimStart(); } else line += ch; }
  if (line) out.push(line);
  return out;
}

// Deterministic pick from a weighted list [[value, weight], ...].
export function pick(r, list) {
  let t = r() * list.reduce((s, [, w]) => s + w, 0);
  for (const [v, w] of list) { if ((t -= w) <= 0) return v; }
  return list[list.length - 1][0];
}

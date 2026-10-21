// Local copy of kit.facade for the town. kit.js (lead-owned) shadows its own lit() with the `lit` option
// inside facade(), so kit.facade() throws "lit is not a function"; this copy renames the option to
// `litShare` and is otherwise the same drawing. Remove once kit.js is fixed.
import * as THREE from 'three';
import { FONT } from '../engine/kit.js';

export function facade(kit, kind, { floors = 5, cols = 6, seed = 1, label = '', lit: litShare = 0.55, w = 1024, h = 1024 } = {}) {
  return kit.memo(`town:facade:${kind}:${floors}:${cols}:${seed}:${label}:${litShare}`, () => {
    const r = kit.rng(seed * 7919 + kind.length);
    const fh = h / floors, cw = w / cols;
    const glowCells = [];
    const color = kit.canvas(w, h, (g) => {
      const wall = { apartment: '#e8e3d8', office: '#8d99a3', shop: '#d9d2c4', villa: '#a8604a', civic: '#cfcac0' }[kind] || '#ddd';
      g.fillStyle = wall; g.fillRect(0, 0, w, h);
      for (let i = 0; i < 2000; i++) { g.fillStyle = `rgba(0,0,0,${r() * 0.04})`; g.fillRect(r() * w, r() * h, 2 + r() * 6, 2 + r() * 6); }
      for (let f = 0; f < floors; f++) {
        const y = f * fh;
        if (kind === 'apartment') {
          g.fillStyle = 'rgba(0,0,0,0.08)'; g.fillRect(0, y + fh - 4, w, 4);
          for (let c = 0; c < cols; c++) {
            const x = c * cw, big = c % 2 === 0;
            const wx = x + cw * 0.08, wy = y + fh * 0.18, ww = cw * (big ? 0.84 : 0.5), wh = fh * (big ? 0.68 : 0.42);
            g.fillStyle = '#c9c3b6'; g.fillRect(wx - 3, wy - 3, ww + 6, wh + 6);
            const on = r() < litShare; glowCells.push([wx, wy, ww, wh, on]);
            g.fillStyle = on ? '#3c4650' : '#26313b'; g.fillRect(wx, wy, ww, wh);
            g.fillStyle = 'rgba(180,205,220,0.18)'; g.fillRect(wx, wy, ww * 0.45, wh);
            g.fillStyle = '#d8d3c8'; g.fillRect(wx + ww / 2 - 1.5, wy, 3, wh); g.fillRect(wx, wy + wh * 0.62, ww, 3);
            if (big) { g.fillStyle = '#f2efe8'; g.fillRect(wx - 2, wy + wh * 0.72, ww + 4, wh * 0.28 + 3); }
            else if (r() < 0.6) { g.fillStyle = '#b8b5ae'; g.fillRect(wx + ww + cw * 0.06, wy + wh * 0.4, cw * 0.28, fh * 0.32); g.strokeStyle = '#8a877f'; for (let k = 0; k < 6; k++) { g.beginPath(); g.moveTo(wx + ww + cw * 0.07, wy + wh * 0.42 + k * fh * 0.05); g.lineTo(wx + ww + cw * 0.33, wy + wh * 0.42 + k * fh * 0.05); g.stroke(); } }
          }
        } else if (kind === 'office') {
          g.fillStyle = '#5f6b75'; g.fillRect(0, y + fh - 6, w, 6);
          for (let c = 0; c < cols; c++) { const wx = c * cw + 3, wy = y + 6, ww = cw - 6, wh = fh - 14; const on = r() < litShare; glowCells.push([wx, wy, ww, wh, on]); g.fillStyle = on ? '#2f4250' : '#1e2b35'; g.fillRect(wx, wy, ww, wh); g.fillStyle = 'rgba(190,215,230,0.15)'; g.fillRect(wx, wy, ww, wh * 0.4); }
        } else if (kind === 'villa' || kind === 'civic') {
          for (let c = 0; c < cols; c++) { const wx = c * cw + cw * 0.25, wy = y + fh * 0.25, ww = cw * 0.5, wh = fh * 0.45; g.fillStyle = '#e9e6df'; g.fillRect(wx - 4, wy - 4, ww + 8, wh + 8); const on = r() < litShare; glowCells.push([wx, wy, ww, wh, on]); g.fillStyle = on ? '#3a4652' : '#25303a'; g.fillRect(wx, wy, ww, wh); g.fillStyle = '#e9e6df'; g.fillRect(wx + ww / 2 - 2, wy, 4, wh); }
        } else { // shop: upper floors windows, ground floor a dark storefront
          if (f < floors - 1) for (let c = 0; c < cols; c++) { const wx = c * cw + cw * 0.15, wy = y + fh * 0.2, ww = cw * 0.7, wh = fh * 0.55; const on = r() < litShare; glowCells.push([wx, wy, ww, wh, on]); g.fillStyle = on ? '#3a4652' : '#25303a'; g.fillRect(wx, wy, ww, wh); }
          else { g.fillStyle = '#2a3238'; g.fillRect(cw * 0.1, y + fh * 0.15, w - cw * 0.2, fh * 0.8); glowCells.push([cw * 0.1, y + fh * 0.15, w - cw * 0.2, fh * 0.8, true]); }
        }
      }
      if (label) { g.fillStyle = '#5b6770'; g.font = `700 ${Math.round(h * 0.09)}px ${FONT}`; g.textAlign = 'center'; g.fillText(label, w * 0.5, h * 0.12); }
    });
    const emissive = kit.canvas(w, h, (g) => {
      g.fillStyle = '#000'; g.fillRect(0, 0, w, h);
      for (const [x, y, ww, hh, on] of glowCells) if (on) { const warm = r() < 0.75; g.fillStyle = warm ? '#ffcf8a' : '#d8ecff'; g.globalAlpha = 0.55 + r() * 0.45; g.fillRect(x, y, ww, hh); }
      g.globalAlpha = 1;
    });
    const m = new THREE.MeshStandardMaterial({ map: kit.texture(color), emissiveMap: kit.texture(emissive), emissive: 0xffffff, emissiveIntensity: 0, roughness: 0.75, metalness: 0.05 });
    kit.lit('windows', m, 1.1);
    return m;
  });
}

// Shared building kit: procedural PBR textures, materials, mesh helpers and switchable light groups.
// Every level builds from this so the town, interiors and people share one look.
import * as THREE from 'three';
import { RoundedBoxGeometry } from 'three/addons/geometries/RoundedBoxGeometry.js';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';

export const FONT = "'Pretendard','Noto Sans KR','Malgun Gothic','Apple SD Gothic Neo',sans-serif";

export function rng(seed = 1) {
  let a = seed >>> 0;
  return () => {
    a = (a + 0x6D2B79F5) >>> 0;
    let t = a;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

export function createKit(renderer, { quality = 'high' } = {}) {
  const aniso = Math.min(8, renderer?.capabilities?.getMaxAnisotropy?.() || 1);
  const S = quality === 'low' ? 256 : 512;
  const cache = new Map();
  const memo = (key, make) => { if (!cache.has(key)) cache.set(key, make()); return cache.get(key); };

  function canvas(w, h, draw) {
    const c = document.createElement('canvas'); c.width = w; c.height = h;
    if (draw) draw(c.getContext('2d'), w, h);
    return c;
  }
  function texture(c, { repeat = [1, 1], color = true } = {}) {
    const t = new THREE.CanvasTexture(c);
    t.wrapS = t.wrapT = THREE.RepeatWrapping;
    t.repeat.set(repeat[0], repeat[1]);
    t.anisotropy = aniso;
    if (color) t.colorSpace = THREE.SRGBColorSpace;
    return t;
  }
  // Grayscale height canvas -> tangent-space normal map canvas.
  function normalFrom(c, strength = 2) {
    const w = c.width, h = c.height;
    const src = c.getContext('2d').getImageData(0, 0, w, h).data;
    const out = canvas(w, h); const ctx = out.getContext('2d'); const img = ctx.createImageData(w, h);
    const H = (x, y) => src[((((y + h) % h) * w) + ((x + w) % w)) * 4] / 255;
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      const dx = (H(x - 1, y) - H(x + 1, y)) * strength, dy = (H(x, y + 1) - H(x, y - 1)) * strength;
      const len = Math.hypot(dx, dy, 1), i = (y * w + x) * 4;
      img.data[i] = (dx / len * 0.5 + 0.5) * 255; img.data[i + 1] = (dy / len * 0.5 + 0.5) * 255;
      img.data[i + 2] = (1 / len * 0.5 + 0.5) * 255; img.data[i + 3] = 255;
    }
    ctx.putImageData(img, 0, 0);
    return out;
  }
  function speckle(ctx, w, h, r, n, colors, size = [0.5, 1.6]) {
    for (let i = 0; i < n; i++) {
      ctx.fillStyle = colors[(r() * colors.length) | 0];
      const s = size[0] + r() * (size[1] - size[0]);
      ctx.fillRect(r() * w, r() * h, s, s);
    }
  }

  // ---- Surfaces: each returns { color, height } canvases at S x S. Tiling sizes are in metres. ----
  const SURFACES = {
    asphalt(r) {
      const color = canvas(S, S, (g, w, h) => {
        g.fillStyle = '#3a3c3f'; g.fillRect(0, 0, w, h);
        for (let i = 0; i < 6; i++) { g.fillStyle = `rgba(20,22,24,${0.12 + r() * 0.12})`; g.beginPath(); g.ellipse(r() * w, r() * h, 20 + r() * 70, 14 + r() * 40, r() * 3, 0, 7); g.fill(); }
        speckle(g, w, h, r, S * 30, ['#4d5054', '#2c2e31', '#55585c', '#26282a', '#5f6165']);
        g.strokeStyle = 'rgba(15,15,16,0.55)'; g.lineWidth = 1;
        for (let i = 0; i < 4; i++) { g.beginPath(); let x = r() * w, y = r() * h; g.moveTo(x, y); for (let k = 0; k < 8; k++) { x += (r() - 0.5) * 40; y += (r() - 0.5) * 40; g.lineTo(x, y); } g.stroke(); }
      });
      const height = canvas(S, S, (g, w, h) => { g.fillStyle = '#808080'; g.fillRect(0, 0, w, h); speckle(g, w, h, r, S * 40, ['#9a9a9a', '#5e5e5e', '#b0b0b0', '#4a4a4a']); });
      return { color, height, tile: 4, rough: 0.92 };
    },
    sidewalk(r) { // Korean interlocking pavers, 20 x 10 cm, running bond
      const cols = 10, rows = 20, pw = S / cols, ph = S / rows;
      const tones = ['#9c9590', '#a8a19a', '#8f8882', '#b0a79e', '#968c86', '#a39890'];
      const color = canvas(S, S, (g, w, h) => {
        g.fillStyle = '#6f6a66'; g.fillRect(0, 0, w, h);
        for (let y = 0; y < rows; y++) for (let x = -1; x < cols; x++) {
          const ox = (y % 2) * pw / 2;
          g.fillStyle = tones[(r() * tones.length) | 0]; g.fillRect(x * pw + ox + 1.5, y * ph + 1.5, pw - 3, ph - 3);
        }
        speckle(g, w, h, r, S * 10, ['rgba(0,0,0,0.12)', 'rgba(255,255,255,0.08)']);
      });
      const height = canvas(S, S, (g, w, h) => {
        g.fillStyle = '#202020'; g.fillRect(0, 0, w, h); g.fillStyle = '#c8c8c8';
        for (let y = 0; y < rows; y++) for (let x = -1; x < cols; x++) g.fillRect(x * pw + (y % 2) * pw / 2 + 2, y * ph + 2, pw - 4, ph - 4);
      });
      return { color, height, tile: 2, rough: 0.85 };
    },
    concrete(r) {
      const color = canvas(S, S, (g, w, h) => {
        g.fillStyle = '#a7a7a3'; g.fillRect(0, 0, w, h);
        for (let i = 0; i < 10; i++) { g.fillStyle = `rgba(${r() < 0.5 ? '70,70,66' : '200,200,195'},${0.05 + r() * 0.07})`; g.beginPath(); g.ellipse(r() * w, r() * h, 30 + r() * 90, 20 + r() * 60, r() * 3, 0, 7); g.fill(); }
        speckle(g, w, h, r, S * 12, ['#9a9a96', '#b4b4b0', '#8e8e8a']);
      });
      const height = canvas(S, S, (g, w, h) => { g.fillStyle = '#808080'; g.fillRect(0, 0, w, h); speckle(g, w, h, r, S * 12, ['#8c8c8c', '#747474']); });
      return { color, height, tile: 3, rough: 0.9 };
    },
    tile(r) { // 60 cm floor tiles
      const n = 4, s = S / n;
      const color = canvas(S, S, (g, w, h) => {
        g.fillStyle = '#9d9c97'; g.fillRect(0, 0, w, h);
        for (let y = 0; y < n; y++) for (let x = 0; x < n; x++) { const v = 222 + ((r() * 14) | 0); g.fillStyle = `rgb(${v},${v - 2},${v - 6})`; g.fillRect(x * s + 1, y * s + 1, s - 2, s - 2); }
        speckle(g, w, h, r, S * 6, ['rgba(0,0,0,0.05)']);
      });
      const height = canvas(S, S, (g, w, h) => { g.fillStyle = '#303030'; g.fillRect(0, 0, w, h); g.fillStyle = '#d0d0d0'; for (let y = 0; y < n; y++) for (let x = 0; x < n; x++) g.fillRect(x * s + 2, y * s + 2, s - 4, s - 4); });
      return { color, height, tile: 2.4, rough: 0.35 };
    },
    wood(r) { // floor planks 15 cm
      const n = 8, s = S / n;
      const color = canvas(S, S, (g, w, h) => {
        for (let i = 0; i < n; i++) {
          const base = [150 + r() * 30, 104 + r() * 22, 66 + r() * 16];
          g.fillStyle = `rgb(${base.map(v => v | 0)})`; g.fillRect(0, i * s, w, s);
          for (let k = 0; k < 40; k++) { g.strokeStyle = `rgba(70,40,20,${0.05 + r() * 0.08})`; g.beginPath(); const y = i * s + r() * s; g.moveTo(0, y); g.bezierCurveTo(w * 0.3, y + (r() - 0.5) * 6, w * 0.6, y + (r() - 0.5) * 6, w, y); g.stroke(); }
          g.fillStyle = 'rgba(40,24,12,0.6)'; g.fillRect(0, i * s, w, 1.5); g.fillRect(r() * w, i * s, 1.5, s);
        }
      });
      const height = canvas(S, S, (g, w, h) => { g.fillStyle = '#909090'; g.fillRect(0, 0, w, h); g.fillStyle = '#404040'; for (let i = 0; i < n; i++) g.fillRect(0, i * s, w, 2); });
      return { color, height, tile: 1.2, rough: 0.55 };
    },
    grass(r) {
      const color = canvas(S, S, (g, w, h) => {
        g.fillStyle = '#4d6b34'; g.fillRect(0, 0, w, h);
        speckle(g, w, h, r, S * 50, ['#5c7d3d', '#3f5a2a', '#6b8a45', '#45622f', '#7a8f4e'], [1, 3]);
      });
      const height = canvas(S, S, (g, w, h) => { g.fillStyle = '#808080'; g.fillRect(0, 0, w, h); speckle(g, w, h, r, S * 40, ['#a0a0a0', '#606060'], [1, 3]); });
      return { color, height, tile: 3, rough: 0.95 };
    },
    plaster(r) {
      const color = canvas(S, S, (g, w, h) => { g.fillStyle = '#e9e5dc'; g.fillRect(0, 0, w, h); speckle(g, w, h, r, S * 8, ['rgba(0,0,0,0.03)', 'rgba(255,255,255,0.05)'], [1, 4]); });
      const height = canvas(S, S, (g, w, h) => { g.fillStyle = '#808080'; g.fillRect(0, 0, w, h); speckle(g, w, h, r, S * 10, ['#888', '#777'], [1, 3]); });
      return { color, height, tile: 2, rough: 0.9 };
    },
    brick(r) {
      const rows = 16, cols = 4, bh = S / rows, bw = S / cols;
      const color = canvas(S, S, (g, w, h) => {
        g.fillStyle = '#b9b2a6'; g.fillRect(0, 0, w, h);
        for (let y = 0; y < rows; y++) for (let x = -1; x <= cols; x++) {
          const t = [[150, 72, 54], [136, 64, 48], [160, 84, 62], [122, 58, 44]][(r() * 4) | 0];
          g.fillStyle = `rgb(${t})`; g.fillRect(x * bw + (y % 2) * bw / 2 + 1.5, y * bh + 1.5, bw - 3, bh - 3);
        }
      });
      const height = canvas(S, S, (g, w, h) => { g.fillStyle = '#303030'; g.fillRect(0, 0, w, h); g.fillStyle = '#c0c0c0'; for (let y = 0; y < rows; y++) for (let x = -1; x <= cols; x++) g.fillRect(x * bw + (y % 2) * bw / 2 + 2, y * bh + 2, bw - 4, bh - 4); });
      return { color, height, tile: 1.6, rough: 0.88 };
    },
  };

  // Material for a named surface. tile = metres per texture repeat; pass { repeat } to override.
  function surface(name, { repeat, color, roughness } = {}) {
    const key = `surface:${name}:${repeat}:${color}:${roughness}`;
    return memo(key, () => {
      const base = memo(`surfaceTex:${name}`, () => {
        const s = SURFACES[name](rng(name.length * 977 + 13));
        return { map: texture(s.color), normalMap: texture(normalFrom(s.height, name === 'sidewalk' || name === 'tile' || name === 'brick' ? 4 : 2), { color: false }), tile: s.tile, rough: s.rough };
      });
      const map = base.map.clone(), normalMap = base.normalMap.clone();
      const rep = repeat || [1 / base.tile, 1 / base.tile];
      map.repeat.set(rep[0], rep[1]); normalMap.repeat.set(rep[0], rep[1]);
      map.needsUpdate = normalMap.needsUpdate = true;
      return new THREE.MeshStandardMaterial({ map, normalMap, roughness: roughness ?? base.rough, metalness: 0, color: color ?? 0xffffff });
    });
  }
  // World-space UVs: scale a mesh's UVs so a surface texture tiles by metres. Use on planes/boxes built at real size.
  function worldUV(geometry, w, d) {
    const uv = geometry.attributes.uv;
    for (let i = 0; i < uv.count; i++) uv.setXY(i, uv.getX(i) * w, uv.getY(i) * d);
    uv.needsUpdate = true;
    return geometry;
  }

  // Plain PBR materials by name, or 'paint:#rrggbb', 'emit:#rrggbb'.
  const PRESETS = {
    curb: { color: 0xb8b6b0, roughness: 0.8 }, metal: { color: 0x9aa0a6, roughness: 0.35, metalness: 0.85 },
    steel: { color: 0x5d6368, roughness: 0.45, metalness: 0.8 }, darkMetal: { color: 0x2a2d31, roughness: 0.5, metalness: 0.7 },
    glass: { color: 0x9fb8c8, roughness: 0.05, metalness: 0.9, transparent: true, opacity: 0.55 },
    darkGlass: { color: 0x1d2a33, roughness: 0.08, metalness: 0.9 }, rubber: { color: 0x1b1b1c, roughness: 0.9 },
    plastic: { color: 0xdcdcdc, roughness: 0.45 }, fabric: { color: 0x6b6f78, roughness: 1 },
    whiteWall: { color: 0xeeeae2, roughness: 0.92 }, yellow: { color: 0xf2c230, roughness: 0.6 },
    hazard: { color: 0xf0b323, roughness: 0.5 }, foliage: { color: 0x4a6b33, roughness: 0.95 }, bark: { color: 0x4b3a2c, roughness: 1 },
  };
  function mat(name, extra) {
    return memo(`mat:${name}:${extra ? JSON.stringify(extra) : ''}`, () => {
      if (name.startsWith('paint:')) return new THREE.MeshStandardMaterial({ color: new THREE.Color(name.slice(6)), roughness: 0.55, ...extra });
      if (name.startsWith('emit:')) { const c = new THREE.Color(name.slice(5)); return new THREE.MeshStandardMaterial({ color: c, emissive: c, emissiveIntensity: 2, roughness: 0.4, ...extra }); }
      if (!PRESETS[name]) throw new Error(`kit.mat: unknown material ${name}`);
      return new THREE.MeshStandardMaterial({ ...PRESETS[name], ...extra });
    });
  }

  // ---- Facades: one texture per building face with optional night window glow (emissiveMap). ----
  // kind: 'apartment' | 'office' | 'shop' | 'villa' | 'civic'. Returns a material registered in light group 'windows'.
  function facade(kind, { floors = 5, cols = 6, seed = 1, label = '', lit: litShare = 0.55, w = 1024, h = 1024 } = {}) {
    return memo(`facade:${kind}:${floors}:${cols}:${seed}:${label}:${litShare}`, () => {
      const r = rng(seed * 7919 + kind.length);
      const fh = h / floors, cw = w / cols;
      const glowCells = [];
      const color = canvas(w, h, (g) => {
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
          } else { // shop: upper floors windows, ground floor storefront drawn by level
            if (f < floors - 1) for (let c = 0; c < cols; c++) { const wx = c * cw + cw * 0.15, wy = y + fh * 0.2, ww = cw * 0.7, wh = fh * 0.55; const on = r() < litShare; glowCells.push([wx, wy, ww, wh, on]); g.fillStyle = on ? '#3a4652' : '#25303a'; g.fillRect(wx, wy, ww, wh); }
            else { g.fillStyle = '#2a3238'; g.fillRect(cw * 0.1, y + fh * 0.15, w - cw * 0.2, fh * 0.8); glowCells.push([cw * 0.1, y + fh * 0.15, w - cw * 0.2, fh * 0.8, true]); }
          }
        }
        if (label) { g.fillStyle = '#5b6770'; g.font = `700 ${Math.round(h * 0.09)}px ${FONT}`; g.textAlign = 'center'; g.fillText(label, w * 0.5, h * 0.12); }
      });
      const emissive = canvas(w, h, (g) => {
        g.fillStyle = '#000'; g.fillRect(0, 0, w, h);
        for (const [x, y, ww, hh, on] of glowCells) if (on) { const warm = r() < 0.75; g.fillStyle = warm ? '#ffcf8a' : '#d8ecff'; g.globalAlpha = 0.55 + r() * 0.45; g.fillRect(x, y, ww, hh); }
        g.globalAlpha = 1;
      });
      const m = new THREE.MeshStandardMaterial({ map: texture(color), emissiveMap: texture(emissive), emissive: 0xffffff, emissiveIntensity: 0, roughness: 0.75, metalness: 0.05 });
      lit('windows', m, 1.1);
      return m;
    });
  }

  // ---- Signs ----
  // lines: string[]; returns a CanvasTexture. Text auto-fits the width.
  function signTexture(lines, { w = 512, h = 256, bg = '#1f4e8c', fg = '#ffffff', border = null, weight = 700, align = 'center', pad = 0.08, sizes } = {}) {
    return memo(`sign:${lines.join('|')}:${w}:${h}:${bg}:${fg}:${border}:${align}`, () => texture(canvas(w, h, (g) => {
      g.fillStyle = bg; g.fillRect(0, 0, w, h);
      if (border) { g.strokeStyle = border; g.lineWidth = h * 0.04; g.strokeRect(g.lineWidth / 2, g.lineWidth / 2, w - g.lineWidth, h - g.lineWidth); }
      const lh = (h * (1 - pad * 2)) / lines.length;
      g.fillStyle = fg; g.textAlign = align; g.textBaseline = 'middle';
      lines.forEach((line, i) => {
        let size = sizes?.[i] ?? lh * 0.72;
        g.font = `${weight} ${size}px ${FONT}`;
        while (g.measureText(line).width > w * (1 - pad * 2) && size > 8) { size -= 2; g.font = `${weight} ${size}px ${FONT}`; }
        g.fillText(line, align === 'left' ? w * pad : align === 'right' ? w * (1 - pad) : w / 2, h * pad + lh * (i + 0.5));
      });
    }), { repeat: [1, 1] }));
  }
  // A flat sign panel (plane, both sides unless single). Mesh origin at panel centre.
  function sign(lines, { w = 1.2, h = 0.6, emissive = 0, single = false, ...style } = {}) {
    const map = signTexture(lines, { w: 512, h: Math.max(64, Math.round(512 * h / w)), ...style });
    const m = new THREE.MeshStandardMaterial({ map, roughness: 0.6, emissive: emissive ? 0xffffff : 0x000000, emissiveMap: emissive ? map : null, emissiveIntensity: emissive, side: single ? THREE.FrontSide : THREE.DoubleSide });
    return new THREE.Mesh(new THREE.PlaneGeometry(w, h), m);
  }
  // Official civil-defence shelter sign (2023 design): red field, white ring, green C·D ring,
  // orange disc with blue triangle, '대피소 / SHELTER' plus Chinese and Japanese. Ratio 45 x 60.
  function shelterSign({ w = 0.45 } = {}) {
    const map = memo('shelterSign', () => texture(canvas(450, 600, (g) => {
      g.fillStyle = '#d7262d'; g.fillRect(0, 0, 450, 600);
      const cx = 225, cy = 205;
      g.fillStyle = '#ffffff'; g.beginPath(); g.arc(cx, cy, 165, 0, 7); g.fill();
      g.strokeStyle = '#1f9a4a'; g.lineWidth = 26; g.beginPath(); g.arc(cx, cy, 138, 0.35, Math.PI - 0.35); g.stroke(); g.beginPath(); g.arc(cx, cy, 138, Math.PI + 0.35, Math.PI * 2 - 0.35); g.stroke();
      g.fillStyle = '#f39200'; g.beginPath(); g.arc(cx, cy, 108, 0, 7); g.fill();
      g.fillStyle = '#1d4fa0'; g.beginPath(); g.moveTo(cx, cy - 78); g.lineTo(cx + 68, cy + 42); g.lineTo(cx - 68, cy + 42); g.closePath(); g.fill();
      g.fillStyle = '#fffbe6'; g.textAlign = 'center'; g.textBaseline = 'middle';
      g.font = `800 68px ${FONT}`; g.fillText('대피소', cx, 425);
      g.font = `700 40px ${FONT}`; g.fillText('SHELTER', cx, 488);
      g.font = `600 26px ${FONT}`; g.fillText('应急避难场所 · 待避所', cx, 545);
    }), { repeat: [1, 1] }));
    const m = new THREE.MeshStandardMaterial({ map, roughness: 0.5, emissive: 0xffffff, emissiveMap: map, emissiveIntensity: 0.05 });
    lit('signs', m, 0.25);
    return new THREE.Mesh(new THREE.PlaneGeometry(w, w * 60 / 45), m);
  }
  // Shelter guide sign: white field, small shelter mark, blue running figure, blue distance and arrow.
  // arrow: 'left' | 'right' | 'up'. Ratio 50 x 25.
  function guideSign({ arrow = 'right', meters = 30, w = 0.5 } = {}) {
    const map = memo(`guideSign:${arrow}:${meters}`, () => texture(canvas(500, 250, (g) => {
      g.fillStyle = '#ffffff'; g.fillRect(0, 0, 500, 250);
      g.fillStyle = '#d7262d'; g.fillRect(24, 40, 96, 128);
      g.fillStyle = '#ffffff'; g.beginPath(); g.arc(72, 90, 34, 0, 7); g.fill();
      g.fillStyle = '#f39200'; g.beginPath(); g.arc(72, 90, 23, 0, 7); g.fill();
      g.fillStyle = '#1d4fa0'; g.beginPath(); g.moveTo(72, 74); g.lineTo(87, 100); g.lineTo(57, 100); g.closePath(); g.fill();
      g.strokeStyle = '#1d4fa0'; g.fillStyle = '#1d4fa0'; g.lineWidth = 13; g.lineCap = 'round';
      const fx = arrow === 'left' ? -1 : 1, px = 190;
      g.beginPath(); g.arc(px + 12 * fx, 52, 14, 0, 7); g.fill();
      g.beginPath(); g.moveTo(px + 6 * fx, 72); g.lineTo(px - 6 * fx, 128); g.stroke();
      g.beginPath(); g.moveTo(px - 6 * fx, 128); g.lineTo(px + 22 * fx, 160); g.lineTo(px + 18 * fx, 196); g.stroke();
      g.beginPath(); g.moveTo(px - 6 * fx, 128); g.lineTo(px - 32 * fx, 158); g.lineTo(px - 52 * fx, 160); g.stroke();
      g.beginPath(); g.moveTo(px - 30 * fx, 90); g.lineTo(px + 4 * fx, 80); g.lineTo(px + 34 * fx, 104); g.stroke();
      g.font = `800 62px ${FONT}`; g.textAlign = 'center'; g.textBaseline = 'middle'; g.fillText(`${meters}m`, 330, 205);
      g.save(); g.translate(395, 92); g.rotate(arrow === 'left' ? Math.PI : arrow === 'up' ? -Math.PI / 2 : 0);
      g.beginPath(); g.moveTo(-70, -16); g.lineTo(20, -16); g.lineTo(20, -40); g.lineTo(72, 0); g.lineTo(20, 40); g.lineTo(20, 16); g.lineTo(-70, 16); g.closePath(); g.fill(); g.restore();
    }), { repeat: [1, 1] }));
    const m = new THREE.MeshStandardMaterial({ map, roughness: 0.5, emissive: 0xffffff, emissiveMap: map, emissiveIntensity: 0.05, side: THREE.DoubleSide });
    lit('signs', m, 0.3);
    return new THREE.Mesh(new THREE.PlaneGeometry(w, w / 2), m);
  }

  // ---- Light groups: 'street' | 'windows' | 'shops' | 'indoor' | 'signs' | 'emergency' | any name ----
  // Lights are never hidden, only dimmed to 0: a changing light count recompiles every shader.
  const groups = new Map(), itemOf = new WeakMap();
  // userData.unlitColor: a backlit panel (vending machine) also darkens its colour when its power is off.
  const put = (G, it) => {
    const v = G.on && !it.off ? it.base * G.scale : 0;
    if (it.isLight) { it.obj.intensity = v; return; }
    it.obj.emissiveIntensity = v;
    const dim = it.obj.userData?.unlitColor;
    if (dim !== undefined) it.obj.color.setScalar(dim + (1 - dim) * Math.min(1, v / (it.base || 1)));
  };
  function lit(group, obj, on) {
    if (!groups.has(group)) groups.set(group, { items: [], on: true, scale: 1 });
    const G = groups.get(group);
    const isLight = obj.isLight;
    const it = { obj, base: on ?? (isLight ? obj.intensity : obj.emissiveIntensity), isLight, off: false };
    G.items.push(it); itemOf.set(obj, [G, it]); put(G, it);
    return obj;
  }
  function setLit(group, on, scale = 1) {
    const G = groups.get(group); if (!G) return;
    G.on = on; G.scale = scale;
    for (const it of G.items) put(G, it);
  }
  // Per-object switch on top of its group (a lamp behind a shutter, a unit on mains power).
  function gateLit(obj, on) { const r = itemOf.get(obj); if (r) { r[1].off = !on; put(...r); } }
  const isLit = (group) => groups.get(group)?.on ?? false;

  // ---- Mesh helpers. o: { x, y, z, ry, rx, rz, cast=true, receive=true }. Box and cylinder y is the BOTTOM. ----
  function place(mesh, o = {}, lift = 0) {
    mesh.position.set(o.x || 0, (o.y || 0) + lift, o.z || 0);
    mesh.rotation.set(o.rx || 0, o.ry || 0, o.rz || 0);
    mesh.castShadow = o.cast ?? true; mesh.receiveShadow = o.receive ?? true;
    return mesh;
  }
  const box = (w, h, d, material, o) => place(new THREE.Mesh(new THREE.BoxGeometry(w, h, d), material), o, h / 2);
  const rbox = (w, h, d, radius, material, o) => place(new THREE.Mesh(new RoundedBoxGeometry(w, h, d, 3, Math.min(radius, w / 2, h / 2, d / 2)), material), o, h / 2);
  const cyl = (rt, rb, h, material, o, seg = 16) => place(new THREE.Mesh(new THREE.CylinderGeometry(rt, rb, h, seg), material), o, h / 2);
  // Horizontal plane at height y, size w (x) by d (z), with world-scaled UVs for surface materials.
  function plane(w, d, material, o = {}) {
    const g = worldUV(new THREE.PlaneGeometry(w, d), w, d);
    const m = new THREE.Mesh(g, material);
    m.rotation.x = -Math.PI / 2; m.position.set(o.x || 0, o.y || 0, o.z || 0); if (o.ry) m.rotation.z = o.ry;
    m.receiveShadow = o.receive ?? true; m.castShadow = false;
    return m;
  }
  // Box with world-scaled UVs (for surface materials on walls/floors).
  function slab(w, h, d, material, o) {
    const g = new THREE.BoxGeometry(w, h, d);
    const uv = g.attributes.uv, n = g.attributes.normal;
    for (let i = 0; i < uv.count; i++) {
      const ax = Math.abs(n.getX(i)), ay = Math.abs(n.getY(i));
      const [su, sv] = ax > 0.5 ? [d, h] : ay > 0.5 ? [w, d] : [w, h];
      uv.setXY(i, uv.getX(i) * su, uv.getY(i) * sv);
    }
    return place(new THREE.Mesh(g, material), o, h / 2);
  }
  // Instanced copies. list: [{ x, y, z, ry, s, sx, sy, sz }]
  function instanced(geometry, material, list, { cast = true, receive = true } = {}) {
    const im = new THREE.InstancedMesh(geometry, material, list.length);
    const m = new THREE.Matrix4(), q = new THREE.Quaternion(), e = new THREE.Euler(), p = new THREE.Vector3(), s = new THREE.Vector3();
    list.forEach((t, i) => { e.set(t.rx || 0, t.ry || 0, t.rz || 0); q.setFromEuler(e); p.set(t.x || 0, t.y || 0, t.z || 0); s.set(t.sx ?? t.s ?? 1, t.sy ?? t.s ?? 1, t.sz ?? t.s ?? 1); m.compose(p, q, s); im.setMatrixAt(i, m); });
    im.castShadow = cast; im.receiveShadow = receive;
    im.instanceMatrix.needsUpdate = true;
    im.computeBoundingSphere();
    return im;
  }
  // Collapse static meshes under root into one mesh per material (fewer draw calls). Skips instanced,
  // skinned, transparent-sorted or userData.keep meshes. Returns root.
  function merge(root) {
    root.updateMatrixWorld(true);
    const inv = new THREE.Matrix4().copy(root.matrixWorld).invert();
    const byMat = new Map(), victims = [];
    root.traverse((o) => {
      if (!o.isMesh || o.isInstancedMesh || o.isSkinnedMesh || o.userData.keep || Array.isArray(o.material)) return;
      const g = o.geometry.index ? o.geometry.toNonIndexed() : o.geometry.clone();
      for (const k of Object.keys(g.attributes)) if (!['position', 'normal', 'uv'].includes(k)) g.deleteAttribute(k);
      if (!g.attributes.uv || !g.attributes.normal) return;
      g.applyMatrix4(new THREE.Matrix4().multiplyMatrices(inv, o.matrixWorld));
      const key = o.material.uuid + (o.castShadow ? 'c' : '') + (o.receiveShadow ? 'r' : '');
      if (!byMat.has(key)) byMat.set(key, { material: o.material, cast: o.castShadow, receive: o.receiveShadow, geoms: [] });
      byMat.get(key).geoms.push(g); victims.push(o);
    });
    for (const o of victims) o.parent.remove(o);
    for (const { material, cast, receive, geoms } of byMat.values()) {
      const merged = new THREE.Mesh(mergeGeometries(geoms, false), material);
      merged.castShadow = cast; merged.receiveShadow = receive; root.add(merged);
    }
    return root;
  }

  return {
    THREE, quality, rng, memo, canvas, texture, normalFrom, surface, worldUV, mat, facade,
    signTexture, sign, shelterSign, guideSign, lit, setLit, gateLit, isLit,
    place, box, rbox, cyl, plane, slab, instanced, merge,
  };
}

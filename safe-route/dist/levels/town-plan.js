// Derived layout for the 3D town: fences that seal block interiors, barricade extents, closure guide
// posts, pedestrian paths and traffic lanes. Pure data (no three.js); frame as in game/townmap.js.
// Rule the fences keep: every block interior is closed except the pockets listed below, so a closure
// barricade (which spans road + both sidewalks) cannot be walked around through a park, lot or yard.
import { CLOSURES } from '../game/townmap.js';

export const CURB = 0.12;

// Axis-aligned fence lines [x0, z0, x1, z1, kind]. Openings are gaps between lines.
// kinds: mesh (steel palisade), park (low park fence + hedge), hedge, wall (block wall).
export const FENCES = [
  // C-N block: sealed except the mart lot, which opens onto 연무로 (x -36..4).
  [-42, -10, -36, -10, 'hedge'], [4, -10, 42, -10, 'mesh'], [-42, -52, -42, -10, 'hedge'],
  [-42, -52, 42, -52, 'wall'], [42, -52, 42, -10, 'mesh'], [-36, -24, -36, -10, 'wall'], [4, -24, 4, -10, 'wall'],
  // C-S block: park gates north (x -3..3) and west (z 28..33) only; shop alleys and the back lot sealed.
  [-42, 12, -3, 12, 'park'], [3, 12, 12, 12, 'park'], [-42, 12, -42, 28, 'park'], [-42, 33, -42, 52, 'park'],
  [-42, 52, 12, 52, 'park'], [12, 12, 12, 52, 'park'], [12, 12, 16, 12, 'wall'], [29, 12, 30, 12, 'wall'],
  [42, 22, 42, 52, 'mesh'], [12, 52, 42, 52, 'mesh'],
  // Background blocks north of 시장길 and south of 단지길.
  [-42, -68, 42, -68, 'wall'], [-42, -110, -42, -68, 'wall'], [42, -110, 42, -68, 'wall'],
  [-42, 68, 42, 68, 'wall'], [-42, 68, -42, 110, 'wall'], [42, 68, 42, 110, 'wall'],
  // W-N block: centre yard + cafe front open to 연무로, assembly bay open to 문화로, the rest sealed.
  [-130, -16, -126, -16, 'hedge'], [-110, -16, -104, -16, 'hedge'], [-104, -28, -104, -16, 'hedge'],
  [-74, -27.5, -58.2, -27.5, 'hedge'], [-58.2, -27.5, -58.2, -10, 'hedge'],
  [-74, -56.5, -74, -50, 'wall'], [-74, -56.5, -58, -56.5, 'wall'], [-58, -110, -58, -56.5, 'wall'],
  // W-S block: north walkway (A, C) and east forecourt open; B driveway open to 문화로 south of z 46.
  [-130, 16.5, -112, 16.5, 'wall'], [-130, 46, -112, 46, 'wall'], [-70, 46, -58, 46, 'hedge'],
  [-130, 60, -126, 60, 'wall'], [-62, 60, -58, 60, 'wall'], [-58, 60, -58, 110, 'wall'],
  // E-N block: logistics yard, truck gate x 62..72, pedestrian gate x 84..90.
  [58, -10, 62, -10, 'mesh'], [72, -10, 84, -10, 'mesh'], [90, -10, 130, -10, 'mesh'],
  [58, -110, 58, -10, 'mesh'], [58, -60, 64, -60, 'mesh'], [124, -60, 130, -60, 'mesh'],
  // E-S block: apartment complex, main gate on 한빛로 (z 15..25).
  [58, 10, 130, 10, 'mesh'], [58, 10, 58, 15, 'mesh'], [58, 25, 58, 110, 'mesh'],
];

// Barricade solids. yeonmuCenter's rects are widened to meet the fence/building lines so the corner
// squares and the 2 m strips in front of the park and shops cannot be used to step around them.
export const BARRICADES = Object.fromEntries(Object.entries(CLOSURES).map(([id, c]) => [id, c.barricades.map(b => ({ ...b }))]));
BARRICADES.yeonmuCenter = [{ x0: 27, x1: 29, z0: -10, z1: 12 }, { x0: -46, x1: -42, z0: -10, z1: 12 }];

// Where the guide of each closure stands (the side a player from 103동 usually arrives on) and faces.
export const CLOSURE_GUIDES = {
  yeonmuCenter: { x: 30.4, z: 8.4, face: [1, 0] },
  danji: { x: 2.4, z: 54, face: [1, 0] },
  sijang: { x: 2.4, z: -54, face: [1, 0] },
  munhwaSouth: { x: -56, z: 47.4, face: [0, 1] },
  munhwaNorth: { x: -56, z: -21.4, face: [0, -1] },
};

// Walkable rects at curb height (everything between the roads). Roads are y 0.
export const BLOCKS = [
  { x0: -130, x1: -54, z0: -110, z1: -6 }, { x0: -130, x1: -54, z0: 6, z1: 110 },
  { x0: 54, x1: 130, z0: -110, z1: -6 }, { x0: 54, x1: 130, z0: 6, z1: 110 },
  { x0: -46, x1: 46, z0: -110, z1: -64 }, { x0: -46, x1: 46, z0: -56, z1: -6 },
  { x0: -46, x1: 46, z0: 6, z1: 56 }, { x0: -46, x1: 46, z0: 64, z1: 110 },
];

// Sidewalk centre lines (2 m from the curb) the crowd strolls on. Closures cut them (see clipPaths).
export const SIDEWALKS = [
  [[-128, -8], [-56, -8]], [[-44, -8], [44, -8]], [[56, -8], [128, -8]],
  [[-128, 8], [-56, 8]], [[-44, 8], [44, 8]], [[56, 8], [128, 8]],
  [[-56, -108], [-56, -8]], [[-56, 8], [-56, 108]], [[-44, -54], [-44, -8]], [[-44, 8], [-44, 54]],
  [[44, -54], [44, -8]], [[44, 8], [44, 54]], [[56, -108], [56, -8]], [[56, 8], [56, 108]],
  [[-44, -54], [44, -54]], [[-44, -66], [44, -66]], [[-44, 54], [44, 54]], [[-44, 66], [44, 66]],
  // crossings (zebra) and inner walks
  [[0, -8], [0, 8]], [[-104, -8], [-104, 8]], [[-56, -14], [-44, -14]], [[-56, 14], [-44, 14]],
  [[44, -14], [56, -14]], [[44, 14], [56, 14]], [[-60, -8], [-60, 8]], [[60, -8], [60, 8]],
  [[0, 8], [0, 11], [-15, 22], [-15, 40], [-30, 44], [-38, 30.5], [-44, 30.5]],
  [[56, 20], [62, 20], [70, 27], [100, 27]], [[-66, -11], [-80, -14], [-96, -18]],
];

// Remove the parts of axis-aligned polylines that run through active barricades (1 m margin).
export function clipPaths(paths, rects) {
  const out = [], flush = (c) => { if (c && c.length > 1) out.push(c); };
  for (const path of paths) {
    let cur = null;
    for (let i = 1; i < path.length; i++) {
      const a = path[i - 1], b = path[i];
      let pieces = [[0, 1]];
      for (const r of rects) {
        const t = segRect(a, b, { x0: r.x0 - 1, x1: r.x1 + 1, z0: r.z0 - 1, z1: r.z1 + 1 });
        if (t) pieces = pieces.flatMap(([u, v]) => [[u, Math.min(v, t[0])], [Math.max(u, t[1]), v]].filter(([p, q]) => q - p > 1e-3));
      }
      const at = (t) => [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t];
      if (!pieces.length) { flush(cur); cur = null; }
      for (const [u, v] of pieces) {
        if (u > 0 || !cur) { flush(cur); cur = [at(u)]; }
        cur.push(at(v));
        if (v < 1) { flush(cur); cur = null; }
      }
    }
    flush(cur);
  }
  return out.filter(p => Math.hypot(p[p.length - 1][0] - p[0][0], p[p.length - 1][1] - p[0][1]) > 3);
}
// Parametric [t0, t1] where segment a-b is inside rect r, or null (slab method).
function segRect(a, b, r) {
  let t0 = 0, t1 = 1;
  for (const [p, d, lo, hi] of [[a[0], b[0] - a[0], r.x0, r.x1], [a[1], b[1] - a[1], r.z0, r.z1]]) {
    if (Math.abs(d) < 1e-9) { if (p < lo || p > hi) return null; continue; }
    let u = (lo - p) / d, v = (hi - p) / d; if (u > v) [u, v] = [v, u];
    t0 = Math.max(t0, u); t1 = Math.min(t1, v); if (t0 >= t1) return null;
  }
  return [t0, t1];
}

// Traffic lanes (Korea drives on the right). s runs in the travel direction; stops are world
// coordinates along the axis where cars wait at red. group: which signal phase lets them go.
export const LANES = [
  { axis: 'x', dir: 1, c: 1.6, group: 'EW', vmax: 12, stops: [-107.5, -63.5, -3.5, 44.5] },
  { axis: 'x', dir: 1, c: 4.4, group: 'EW', vmax: 10.5, stops: [-107.5, -63.5, -3.5, 44.5] },
  { axis: 'x', dir: -1, c: -1.6, group: 'EW', vmax: 12, stops: [63.5, 3.5, -44.5, -100.5] },
  { axis: 'x', dir: -1, c: -4.4, group: 'EW', vmax: 10.5, stops: [63.5, 3.5, -44.5, -100.5] },
  { axis: 'z', dir: -1, c: -48, group: 'NS', vmax: 9, stops: [17.5] },
  { axis: 'z', dir: 1, c: -52, group: 'NS', vmax: 9, stops: [-17.5] },
  { axis: 'z', dir: -1, c: 52, group: 'NS', vmax: 9, stops: [17.5] },
  { axis: 'z', dir: 1, c: 48, group: 'NS', vmax: 9, stops: [-17.5] },
];
export const LANE_SPAN = 340; // lanes run -340..340 so cars wrap far out in the city ring

// Signal cycle (seconds): EW green, EW amber, all red, NS green, NS amber, all red.
export const CYCLE = [['EW', 'green', 20], ['EW', 'amber', 3], ['ALL', 'red', 1.5], ['NS', 'green', 13], ['NS', 'amber', 3], ['ALL', 'red', 1.5]];
export function signalAt(t) {
  const total = CYCLE.reduce((s, c) => s + c[2], 0);
  let u = ((t % total) + total) % total;
  for (const [g, c, d] of CYCLE) { if (u < d) return { EW: g === 'EW' ? c : 'red', NS: g === 'NS' ? c : 'red', left: d - u }; u -= d; }
  return { EW: 'red', NS: 'red', left: 0 };
}

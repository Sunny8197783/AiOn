// Building blocks for the apartment level (한빛아파트 103동): walls with openings, doors, windows, rods,
// signs, materials and the bookkeeping a Level needs (walk zones, solids, points, power-switched lights).
// Frame: x east, z south, metres. Floor N stands at y = FH * (N - 1).
import * as THREE from 'three';
import { FONT } from '../engine/kit.js';

export const FH = 2.8, F3 = 5.6, FL = 5.75; // floor-to-floor, 3rd floor, 302호 main floor (현관 step 0.15)

export function createBuilder(kit) {
  const B = {
    kit, walk: [], solids: [], points: [], spawns: {}, anchors: {},
    powered: [],  // { obj, base }: on while the building has power
    backup: [],   // battery emergency lamps: on only while the power is off
    sensors: [],  // motion-sensor ceiling lamps { x, y, z, b, hold }
    doors: [],    // auto doors (open when the player comes close)
    ticks: [],    // (dt, t, S) per-frame callbacks
  };
  const reg = (list, obj, base) => { list.push({ obj, base: base ?? (obj.isLight ? obj.intensity : obj.emissiveIntensity) }); return obj; };
  B.onPower = (obj, base) => reg(B.powered, obj, base);
  B.onBackup = (obj, base) => reg(B.backup, obj, base);
  B.solid = (x0, x1, z0, z1, y0, y1, id) => { const s = { x0, x1, z0, z1, y0, y1 }; if (id) s.id = id; B.solids.push(s); return s; };
  B.floor = (x0, x1, z0, z1, y) => B.walk.push({ x0, x1, z0, z1, y });
  B.ramp = (x0, x1, z0, z1, axis, y0, y1) => B.walk.push({ x0, x1, z0, z1, ramp: { axis, y0, y1 } });
  B.point = (id, label, x, y, z, kind, r) => { const p = { id, label, x, y, z, kind }; if (r) p.r = r; B.points.push(p); return p; };

  // ---- materials ----
  const s = (name, o) => kit.surface(name, o), p = (hex, o) => kit.mat('paint:' + hex, o);
  B.M = {
    wallpaper: s('plaster', { color: 0xf3efe5 }), concrete: s('concrete'),
    paintWhite: p('#ecebe6', { roughness: 0.92 }), ceiling: p('#f4f3ef', { roughness: 0.95 }),
    corridorWall: p('#e2dccf', { roughness: 0.85 }), skirting: p('#6f6a62', { roughness: 0.6 }),
    stairWall: p('#e9e5d8', { roughness: 0.9 }), stairBand: p('#6f8f78', { roughness: 0.55 }),
    exterior: p('#e6e1d6', { roughness: 0.9 }),
    lobbyFloor: s('tile', { color: 0xd6cdbd, roughness: 0.2 }), lobbyWall: s('tile', { color: 0xcfc6b6, roughness: 0.3 }),
    corridorFloor: s('tile', { color: 0xa9a7a0, roughness: 0.38 }), step: s('concrete', { color: 0xc4bfb3, roughness: 0.55 }),
    nosing: p('#2a2a29', { roughness: 0.95 }),
    wood: s('wood', { color: 0xe8d6b8 }), entryTile: s('tile', { color: 0x8e8c87, roughness: 0.3 }),
    balconyTile: s('tile', { color: 0xc9c0b0, roughness: 0.6 }), paving: s('sidewalk'),
    frame: p('#e9e9e6', { roughness: 0.4 }), alu: p('#9aa0a3', { roughness: 0.35, metalness: 0.7 }),
    darkFrame: p('#3a3c3f', { roughness: 0.45, metalness: 0.6 }),
    glass: new THREE.MeshStandardMaterial({ color: 0xc4d6de, roughness: 0.03, metalness: 0.1, transparent: true, opacity: 0.16, depthWrite: false }),
    frosted: new THREE.MeshStandardMaterial({ color: 0xdfe6e6, roughness: 0.4, transparent: true, opacity: 0.85 }),
    mirror: kit.mat('metal', { color: 0xdfe6ea, roughness: 0.04, metalness: 1 }),
    steel: kit.mat('steel'), metal: kit.mat('metal'), darkMetal: kit.mat('darkMetal'), rubber: kit.mat('rubber'), plastic: kit.mat('plastic'),
    unitDoor: p('#5b5047', { roughness: 0.42, metalness: 0.35 }), fireDoor: p('#b9b2a1', { roughness: 0.5, metalness: 0.3 }),
    red: p('#c4161c', { roughness: 0.4, metalness: 0.2 }), yellowPipe: p('#e7b400', { roughness: 0.4, metalness: 0.2 }),
    black: p('#151515', { roughness: 0.35 }), white: p('#f4f4f1', { roughness: 0.3 }), cardboard: p('#b08a5c', { roughness: 0.95 }),
    fabric: p('#8d867b', { roughness: 0.97 }), darkWood: p('#5e4632', { roughness: 0.6 }), lightWood: p('#c9a67c', { roughness: 0.55 }),
    rail: p('#4f5a52', { roughness: 0.5, metalness: 0.4 }), stone: p('#3b3a38', { roughness: 0.3 }),
  };
  // A fresh emissive material (power-switched ones must not be the kit's shared memo copies).
  B.glowMat = (hex, intensity = 2, extra = {}) => { const c = new THREE.Color(hex); return new THREE.MeshStandardMaterial({ color: c.clone().multiplyScalar(0.3), emissive: c, emissiveIntensity: intensity, roughness: 0.4, ...extra }); };

  // ---- walls: a slab of thickness t on one side of the plane (side -1 / +1), with holes [{a,b,y0,y1,door}] ----
  function wall(g, axis, c, a0, a1, y0, y1, mat, { t = 0.1, side = 1, holes = [], solid = true } = {}) {
    const mid = c + side * t / 2;
    const seg = (a, b, lo, hi) => { if (b - a < 1e-3 || hi - lo < 1e-3) return; g.add(axis === 'z' ? kit.slab(b - a, hi - lo, t, mat, { x: (a + b) / 2, y: lo, z: mid }) : kit.slab(t, hi - lo, b - a, mat, { x: mid, y: lo, z: (a + b) / 2 })); };
    const hs = holes.filter(h => h.b > a0 && h.a < a1).sort((m, n) => m.a - n.a);
    let u = a0;
    for (const h of hs) { seg(u, h.a, y0, y1); seg(h.a, h.b, y0, Math.max(y0, h.y0)); seg(h.a, h.b, Math.min(y1, h.y1), y1); u = h.b; }
    seg(u, a1, y0, y1);
    if (!solid) return;
    let v = a0;
    const push = (a, b) => { if (b - a > 1e-3) axis === 'z' ? B.solid(a, b, c - 0.12, c + 0.12, y0, y1) : B.solid(c - 0.12, c + 0.12, a, b, y0, y1); };
    for (const h of hs.filter(h => h.door)) { push(v, h.a); v = h.b; }
    push(v, a1);
  }
  B.wallZ = (g, z, x0, x1, y0, y1, mat, o) => wall(g, 'z', z, x0, x1, y0, y1, mat, o); // plane z = const, spans x
  B.wallX = (g, x, z0, z1, y0, y1, mat, o) => wall(g, 'x', x, z0, z1, y0, y1, mat, o); // plane x = const, spans z

  // ---- window frame + glass in an opening. axis 'z': opening spans x on plane z = c; 'x': spans z on plane x = c ----
  B.window = (g, axis, c, a, b, y0, y1, { mat = B.M.frame, glass = B.M.glass, split = 2, depth = 0.07, f = 0.05 } = {}) => {
    const w = b - a, h = y1 - y0;
    const put = (len, hh, d, m, u, y, o = {}) => g.add(axis === 'z' ? kit.box(len, hh, d, m, { x: u, y, z: c, ...o }) : kit.box(d, hh, len, m, { x: c, y, z: u, ...o }));
    put(w, f, depth, mat, (a + b) / 2, y0); put(w, f, depth, mat, (a + b) / 2, y1 - f);
    put(f, h, depth, mat, a + f / 2, y0); put(f, h, depth, mat, b - f / 2, y0);
    for (let i = 1; i < split; i++) put(f * 0.9, h, depth, mat, a + w * i / split, y0);
    put(w - 2 * f, h - 2 * f, 0.012, glass, (a + b) / 2, y0 + f, { cast: false });
  };

  // ---- hinged door in an opening [a,b] on plane c. hinge 'a'|'b'; dir: which side it swings to (-1|+1 along the normal) ----
  B.door = (g, axis, c, a, b, y, h, mat, { hinge = 'a', dir = 1, t = 0.05, open = 0, solid = true, lock = false, frameMat = B.M.darkFrame, maxAngle = 1.5 } = {}) => {
    const w = b - a, lw = w - 0.03, pivot = new THREE.Group(), sgn = hinge === 'a' ? 1 : -1;
    const hu = hinge === 'a' ? a + 0.015 : b - 0.015;
    if (axis === 'z') pivot.position.set(hu, y, c); else pivot.position.set(c, y, hu);
    const along = (len, hh, d, m, u, yy, n = 0) => axis === 'z' ? kit.box(len, hh, d, m, { x: u, y: yy, z: n }) : kit.box(d, hh, len, m, { x: n, y: yy, z: u });
    pivot.add(along(lw, h - 0.01, t, mat, sgn * lw / 2, 0));
    for (const n of [-1, 1]) { // lever handles both faces, near the free edge
      pivot.add(along(0.13, 0.022, 0.022, B.M.metal, sgn * (lw - 0.13), 1.0, n * (t / 2 + 0.04)));
      pivot.add(along(0.03, 0.03, 0.045, B.M.metal, sgn * (lw - 0.07), 0.99, n * (t / 2 + 0.02)));
    }
    if (lock) { // digital door lock on the outside face (dir is the outside)
      pivot.add(along(0.075, 0.2, 0.035, B.M.black, sgn * (lw - 0.09), 1.12, dir * (t / 2 + 0.018)));
      const led = B.onPower(B.glowMat('#4aa8ff', 1.6));
      pivot.add(along(0.04, 0.05, 0.004, led, sgn * (lw - 0.09), 1.2, dir * (t / 2 + 0.037)));
      pivot.add(along(0.02, 0.02, 0.02, B.M.metal, sgn * lw / 2, 1.55, dir * (t / 2 + 0.01))); // peephole
    }
    g.add(pivot);
    if (frameMat) { // frame posts + head
      const ft = 0.05, fd = 0.16;
      g.add(axis === 'z' ? kit.box(ft, h + ft, fd, frameMat, { x: a - ft / 2, y, z: c }) : kit.box(fd, h + ft, ft, frameMat, { x: c, y, z: a - ft / 2 }));
      g.add(axis === 'z' ? kit.box(ft, h + ft, fd, frameMat, { x: b + ft / 2, y, z: c }) : kit.box(fd, h + ft, ft, frameMat, { x: c, y, z: b + ft / 2 }));
      g.add(axis === 'z' ? kit.box(w + 2 * ft, ft, fd, frameMat, { x: (a + b) / 2, y: y + h, z: c }) : kit.box(fd, ft, w + 2 * ft, frameMat, { x: c, y: y + h, z: (a + b) / 2 }));
    }
    const ang = axis === 'z' ? (hinge === 'a' ? -dir : dir) : (hinge === 'a' ? dir : -dir);
    const D = { pivot, k: open, target: open, x: axis === 'z' ? (a + b) / 2 : c, z: axis === 'z' ? c : (a + b) / 2, y, solid: null,
      set(k) { D.k = k; pivot.rotation.y = ang * k * maxAngle; if (D.solid) D.solid.off = k > 0.12; } };
    if (solid) D.solid = axis === 'z' ? B.solid(a, b, c - 0.1, c + 0.1, y, y + h) : B.solid(c - 0.1, c + 0.1, a, b, y, y + h);
    D.set(open);
    return D;
  };
  B.autoDoor = (D, r = 1.6) => { D.auto = r; B.doors.push(D); return D; };

  // Cylinder between two points (handrails, pipes, rods).
  B.rod = (g, a, b, r, mat, seg = 8) => {
    const va = new THREE.Vector3(...a), vb = new THREE.Vector3(...b), d = vb.clone().sub(va), len = d.length();
    const m = new THREE.Mesh(new THREE.CylinderGeometry(r, r, len, seg), mat);
    m.position.copy(va).addScaledVector(d, 0.5);
    m.quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), d.normalize());
    m.castShadow = m.receiveShadow = true;
    g.add(m); return m;
  };

  // Flat sign plate placed on a wall. o: { x, y (centre), z, ry, w, h, bg, fg, emissive, border, lines }
  B.plate = (g, lines, { x, y, z, ry = 0, w = 0.3, h = 0.15, ...style }) => {
    const m = kit.sign([].concat(lines), { w, h, single: true, ...style });
    m.position.set(x, y, z); m.rotation.y = ry; m.receiveShadow = true;
    g.add(m); return m;
  };
  // Texture drawn on a canvas (memoised by key).
  B.tex = (key, w, h, draw) => kit.memo('apt:tex:' + key, () => kit.texture(kit.canvas(w, h, draw), { repeat: [1, 1] }));

  // Exit signs: 'exit' = 피난구유도등 (green field, white figure running into a doorway), 'pass' = 통로유도등 (white field,
  // green figure and arrow). Always lit (light group 'emergency'); ry turns the face, arrow 'left'|'right' for 'pass'.
  const exitMats = {};
  B.exitSign = (g, x, y, z, ry, kind = 'exit', arrow = 'right') => {
    const key = kind + arrow;
    const mat = exitMats[key] ||= (() => {
      const map = B.tex('exit:' + key, 256, 128, (c, w, h) => drawExitSign(c, w, h, kind, arrow));
      return kit.lit('emergency', new THREE.MeshStandardMaterial({ map, emissive: 0xffffff, emissiveMap: map, emissiveIntensity: 2.4, roughness: 0.4 }), 2.4);
    })();
    const grp = new THREE.Group(); grp.position.set(x, y, z); grp.rotation.y = ry;
    grp.add(kit.box(0.44, 0.24, 0.06, B.M.white, { y: -0.12, z: -0.03 }));
    const face = new THREE.Mesh(new THREE.PlaneGeometry(0.4, 0.2), mat); face.position.z = 0.002;
    grp.add(face); g.add(grp);
    return grp;
  };
  return B;
}

// Running figure (pictogram) at hip (x, y), scale s px per unit, facing +x unless flip.
export function runner(g, x, y, s, color, flip = false) {
  g.save(); g.translate(x, y); g.scale(flip ? -s : s, s);
  g.strokeStyle = g.fillStyle = color; g.lineCap = g.lineJoin = 'round'; g.lineWidth = 0.24;
  g.beginPath(); g.arc(0.42, -1.3, 0.25, 0, 7); g.fill();
  const line = (...pts) => { g.beginPath(); g.moveTo(pts[0], pts[1]); for (let i = 2; i < pts.length; i += 2) g.lineTo(pts[i], pts[i + 1]); g.stroke(); };
  g.lineWidth = 0.3; line(0.25, -0.95, -0.05, -0.12);
  g.lineWidth = 0.22; line(-0.6, -0.72, -0.05, -0.88, 0.25, -0.62, 0.68, -0.5); line(-0.05, -0.12, 0.45, 0.28, 0.4, 0.86); line(-0.05, -0.12, -0.42, 0.35, -0.92, 0.42);
  g.restore();
}

function drawExitSign(g, w, h, kind, arrow) {
  const green = '#0b8a3e';
  if (kind === 'exit') {
    g.fillStyle = green; g.fillRect(0, 0, w, h);
    g.fillStyle = '#ffffff'; g.fillRect(w * 0.6, h * 0.14, w * 0.3, h * 0.74); // doorway
    g.fillStyle = green; g.fillRect(w * 0.64, h * 0.22, w * 0.22, h * 0.66);
    runner(g, w * 0.4, h * 0.56, h * 0.27, '#ffffff');
    g.fillStyle = '#ffffff'; g.font = `800 ${h * 0.14}px ${FONT}`; g.textAlign = 'center'; g.fillText('비상구', w * 0.75, h * 0.97 - 2);
  } else {
    g.fillStyle = '#f8faf6'; g.fillRect(0, 0, w, h);
    const left = arrow === 'left';
    runner(g, left ? w * 0.68 : w * 0.32, h * 0.55, h * 0.26, green, left);
    g.fillStyle = green; g.save(); g.translate(left ? w * 0.26 : w * 0.74, h * 0.5); if (left) g.scale(-1, 1);
    g.beginPath(); g.moveTo(-34, -10); g.lineTo(6, -10); g.lineTo(6, -26); g.lineTo(36, 0); g.lineTo(6, 26); g.lineTo(6, 10); g.lineTo(-34, 10); g.closePath(); g.fill(); g.restore();
    g.strokeStyle = green; g.lineWidth = 6; g.strokeRect(3, 3, w - 6, h - 6);
  }
}

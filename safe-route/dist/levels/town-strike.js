// The D-day missile strike on a background villa south of 연무공원 (no story place): the intact villa from
// town-buildings is hidden and replaced by a gutted shell, a rubble heap, fires in the openings and a smoke
// column that leans with the wind and can be seen from most of the town. state 'hit' | 'none'.
import * as THREE from 'three';

const PUFFS = 22, TOP = 70;

export function buildStrike(kit, fx, strike) {
  const group = new THREE.Group(); group.name = 'strike'; group.visible = false;
  const b = strike?.box;
  if (!b) return { group, setHit() {}, update() {} };
  const cx = (b.x0 + b.x1) / 2, cz = (b.z0 + b.z1) / 2, W = b.x1 - b.x0, D = b.z1 - b.z0;
  const rand = kit.rng(4242);
  const scorched = kit.mat('paint:#2a2624', { roughness: 0.95 }), concrete = kit.surface('concrete', { color: 0x8d8780 });
  const soot = kit.mat('paint:#121110', { roughness: 1 });

  // Gutted shell: corner piers and broken wall stubs of uneven height, burnt slabs hanging between them.
  const ruin = new THREE.Group(); group.add(ruin);
  for (const [x, z] of [[b.x0, b.z0], [b.x1, b.z0], [b.x0, b.z1], [b.x1, b.z1]]) {
    const h = b.h * (0.35 + rand() * 0.55);
    ruin.add(kit.box(0.9, h, 0.9, concrete, { x: x + (x === b.x0 ? 0.45 : -0.45), y: 0, z: z + (z === b.z0 ? 0.45 : -0.45) }));
  }
  for (let i = 0; i < 9; i++) { // jagged wall stubs along the faces
    const along = rand(), side = i % 4, h = 1.5 + rand() * b.h * 0.45, w = 1.5 + rand() * 3;
    const x = side < 2 ? b.x0 + along * W : side === 2 ? b.x0 + 0.2 : b.x1 - 0.2, z = side < 2 ? (side ? b.z1 - 0.2 : b.z0 + 0.2) : b.z0 + along * D;
    ruin.add(kit.box(side < 2 ? w : 0.3, h, side < 2 ? 0.3 : w, i % 3 ? scorched : concrete, { x, y: 0, z, rz: (rand() - 0.5) * 0.12 }));
  }
  for (let f = 1; f <= 2; f++) { // two floor slabs snapped and tilted into the heap
    const s = kit.box(W * 0.55, 0.25, D * 0.7, concrete, { x: cx + (rand() - 0.5) * 3, y: f * 2.6, z: cz + (rand() - 0.5) * 3 });
    s.rotation.set((rand() - 0.5) * 0.5, rand() * 0.6, (f === 1 ? 1 : -1) * (0.25 + rand() * 0.2)); ruin.add(s);
  }
  // Rubble heap and debris thrown into the street on the park side.
  const chunks = [];
  for (let i = 0; i < 140; i++) {
    const r = Math.sqrt(rand()), a = rand() * Math.PI * 2, out = i > 110;
    const x = out ? b.x0 + rand() * W : cx + Math.cos(a) * r * W * 0.55, z = out ? b.z0 - 1 - rand() * 7 : cz + Math.sin(a) * r * D * 0.55;
    const s = out ? 0.2 + rand() * 0.5 : 0.5 + rand() * 1.6;
    chunks.push({ x, y: out ? 0 : (1 - r) * 3.2 * rand(), z, sx: s * (0.8 + rand()), sy: s * 0.6, sz: s, rx: rand() * 3, ry: rand() * 3, rz: rand() * 3 });
  }
  ruin.add(kit.instanced(new THREE.BoxGeometry(1, 1, 1), concrete, chunks, { cast: true }));
  const scorch = new THREE.Mesh(new THREE.CircleGeometry(Math.max(W, D) * 0.85, 28).rotateX(-Math.PI / 2), soot);
  scorch.position.set(cx, 0.03, cz - 2); scorch.receiveShadow = true; ruin.add(scorch);
  kit.merge(ruin);

  // Fires in the heap (no point light: the light count stays fixed, the glow comes from the flames).
  const fires = [[cx - 3, 1.6, cz - 2, 3.2], [cx + 4, 0.8, cz + 1, 2.4], [cx, 3.2, cz + 3, 2.8]].map(([x, y, z, scale]) =>
    fx.fire({ x, y, z, scale, radius: 0.5, light: false, level: 0 }));
  fires.forEach((f) => group.add(f.group));

  // Smoke column: big soft puffs that rise, widen and drift east with the wind, recycled at the base.
  const tex = kit.memo('town:strikeSmoke', () => kit.texture(kit.canvas(128, 128, (g, w) => {
    const gr = g.createRadialGradient(w / 2, w / 2, 4, w / 2, w / 2, w / 2);
    gr.addColorStop(0, 'rgba(255,255,255,0.9)'); gr.addColorStop(0.5, 'rgba(255,255,255,0.45)'); gr.addColorStop(1, 'rgba(255,255,255,0)');
    g.fillStyle = gr; g.fillRect(0, 0, w, w);
  }), { repeat: [1, 1] }));
  const puffs = [];
  for (let i = 0; i < PUFFS; i++) {
    const m = new THREE.SpriteMaterial({ map: tex, color: 0x34302d, transparent: true, depthWrite: false, opacity: 0.8 });
    const s = new THREE.Sprite(m); group.add(s);
    puffs.push({ s, k: i / PUFFS, spin: rand() });
  }

  let hit = false;
  function setHit(on) {
    hit = !!on; group.visible = hit; strike.group.visible = !hit;
    for (const f of fires) f.setLevel(hit ? 0.9 : 0);
  }
  function update(dt, t, camera, night) {
    if (!hit) return;
    for (const f of fires) f.update(dt, camera);
    for (const p of puffs) {
      p.k = (p.k + dt / 55) % 1;
      const y = 3 + p.k * TOP, size = 6 + p.k * 34;
      p.s.position.set(cx + p.k * p.k * 38 + Math.sin(t * 0.2 + p.spin * 6) * 2, y, cz - 2 + Math.cos(t * 0.17 + p.spin * 5) * 2);
      p.s.scale.set(size, size, 1);
      p.s.material.opacity = 0.85 * Math.min(1, p.k * 8) * (1 - p.k) ** 0.7;
      // Lit from below by the fire at night, plain grey-black by day.
      const glow = night ? Math.max(0, 1 - p.k * 4) : 0;
      p.s.material.color.setRGB(0.2 + glow * 0.55, 0.19 + glow * 0.22, 0.18 + glow * 0.05);
    }
  }
  setHit(false);
  return { group, setHit, update };
}

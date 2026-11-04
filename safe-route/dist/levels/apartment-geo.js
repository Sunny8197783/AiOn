// Pure geometry of the apartment level shared by its builders and the tests (no three.js): the stairwell plan,
// where each tread sits and the walk ramp it is climbed on, and the collision boxes of an open door leaf.
// Frame: x east, z south, metres; floor N stands at y = 2.8 * (N - 1).

export const STAIR = { x0: 3.4, x1: 6.6, laneA: [5.1, 6.6], laneB: [3.4, 4.9], zLand: 1.4, zMid: 3.6, zEnd: 5.0, top: 11.2 };
export const RISER = 0.175, TREAD = 0.275, STEPS = 8;

// Tread i (0..7) of the flight that leaves the floor at height y0. Lane A climbs from the floor landing to the mid
// landing (y0 -> y0 + 1.4) going south; lane B climbs from the mid landing to the next floor (y0 + 1.4 -> y0 + 2.8)
// going north. { top, z0, z1 }: the tread's top height and its z extent.
export function tread(y0, lane, i) {
  const base = lane === 'A' ? y0 : y0 + STEPS * RISER;
  const z0 = lane === 'A' ? STAIR.zLand + i * TREAD : STAIR.zMid - (i + 1) * TREAD;
  return { top: base + (i + 1) * RISER, z0, z1: z0 + TREAD };
}

// The walk ramps of that flight, as level walk zones; each runs through the tread centres.
export function flightRamps(y0) {
  const [ax0, ax1] = STAIR.laneA, [bx0, bx1] = STAIR.laneB, h = STEPS * RISER;
  return [
    { x0: ax0, x1: ax1, z0: STAIR.zLand - TREAD / 2, z1: STAIR.zMid - TREAD / 2, ramp: { axis: 'z', y0, y1: y0 + h } },
    { x0: bx0, x1: bx1, z0: STAIR.zLand + TREAD / 2, z1: STAIR.zMid + TREAD / 2, ramp: { axis: 'z', y0: y0 + 2 * h, y1: y0 + h } },
  ];
}

// Height of a ramp walk zone at z (axis 'z').
export const rampY = (w, z) => w.ramp.y0 + (w.ramp.y1 - w.ramp.y0) * (z - w.z0) / (w.z1 - w.z0);

// Collision boxes of a hinged leaf: the hinge at (px, pz), the closed leaf's far edge at (px + lx, pz + lz), turned
// by phi about y (three.js: x' = x cos + z sin, z' = -x sin + z cos). Three boxes cover the leaf from 17 % of its
// length (the doorway by the hinge is the wall's job) to its free edge, each padded by pad. Writes into out.
export function leafBoxes(px, pz, lx, lz, phi, pad, out = [{}, {}, {}]) {
  const ex = lx * Math.cos(phi) + lz * Math.sin(phi), ez = -lx * Math.sin(phi) + lz * Math.cos(phi), n = out.length;
  out.forEach((s, i) => {
    const u0 = 0.17 + i * 0.83 / n, u1 = u0 + 0.83 / n, x0 = px + ex * u0, x1 = px + ex * u1, z0 = pz + ez * u0, z1 = pz + ez * u1;
    s.x0 = Math.min(x0, x1) - pad; s.x1 = Math.max(x0, x1) + pad; s.z0 = Math.min(z0, z1) - pad; s.z1 = Math.max(z0, z1) + pad;
  });
  return out;
}

// The body under load, as plain numbers (no DOM, no three.js) so chapters stay short and tests can pin it.
//
// carryStep(dt, speed, carry) -> { fatigue, thirst } deltas for one frame of carrying water home.
//   Walking with a load tires; running tires more; standing still (the jug put down) lets you catch your breath.
// smokeStep(s, dt, { smoke, crouch, towel }) -> { exposure, hurt, level }
//   s = { exposure, hurtT } is updated in place. smoke 0..1 is how thick the air is where you are.
//   Crouching (C) and a wet towel over nose and mouth cut what you breathe in (fact fire_big).
//   Past SMOKE.safe, health goes 1 point at a time, faster the longer you stay. level 0..1 drives the blur.

export const CARRY = { heavy: 12, walk: [0.05, 0.12], run: [0.1, 0.2], rest: 0.4, thirst: 0.05, moving: 0.3, running: 2.4, warn: 85 };

export function carryStep(dt, speed, carry) {
  if (!(dt > 0)) return { fatigue: 0, thirst: 0 };
  const heavy = carry > CARRY.heavy ? 1 : 0;
  if (speed < CARRY.moving) return { fatigue: -CARRY.rest * dt, thirst: 0 };
  const rate = (speed > CARRY.running ? CARRY.run : CARRY.walk)[heavy];
  return { fatigue: rate * dt, thirst: CARRY.thirst * dt };
}

export const SMOKE = { base: 1, thick: 1.5, crouch: 0.3, towel: 0.4, safe: 6, ramp: 12, every: 2, blur: 8 };

export function smokeStep(s, dt, { smoke = 0, crouch = false, towel = false } = {}) {
  s.exposure ??= 0; s.hurtT ??= 0;
  let hurt = 0;
  if (dt > 0 && smoke > 0) {
    s.exposure += dt * smoke * (SMOKE.base + SMOKE.thick * smoke) * (crouch ? SMOKE.crouch : 1) * (towel ? SMOKE.towel : 1);
    if (s.exposure > SMOKE.safe) {
      s.hurtT += dt * (1 + (s.exposure - SMOKE.safe) / SMOKE.ramp);
      while (s.hurtT >= SMOKE.every) { s.hurtT -= SMOKE.every; hurt += 1; }
    }
  }
  return { exposure: s.exposure, hurt, level: Math.min(1, s.exposure / SMOKE.blur) };
}

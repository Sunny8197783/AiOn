// The story's cutscenes (story/cine.js shots): the opening over 연무동, time passing over the 한빛아파트
// complex, the D-day strike, the blackout and the air-raid siren. Each stands in for a card that only told it.
// Coordinates: town x = east, z = south, ground 0 (game/townmap.js); the 302호 floor is at y 5.75.
import { FL } from '../levels/apartment-parts.js';

const NIGHT = new Set(['night', 'blackout']);

// Where the player was, so a cutscene shot elsewhere can put them back.
function here(ctx) {
  const P = ctx.engine.player, L = ctx.engine.level;
  return L ? { level: L.id, at: { x: P.x, y: P.y, z: P.z, yaw: P.yaw } } : null;
}
async function back(ctx, h) { if (h && ctx.engine.level?.id !== h.level) await ctx.goto(h.level, h.at); }

// ---- opening: the town on an ordinary Tuesday, then 정민 on his way back into the office ----
export async function opening(ctx) {
  const { cine } = ctx;
  ctx.outdoorEnv = 'afternoon';
  ctx.setLevelState('town', 'crowd', 'normal');
  await ctx.goto('town', 'office', 'afternoon');
  let jm = null;
  const jmAt = () => jm?.group.position ?? { x: 70, y: 0, z: -10 };
  return cine.play([
    { dur: 9, fade: true, cam: { p: [[175, 95, 150], [150, 78, 110], [128, 66, 72]], t: [[40, 8, 10], [58, 4, -4], [78, 2, -18]], fov: [55, 50], hand: 0.3 },
      lines: [[1.4, '전쟁이 나기 사흘 전. 경기도 연무동의 평범한 화요일 오후였다.', null, 6500]] },
    { dur: 6.5, cam: { p: [[8, 1.7, 9], [24, 1.7, 9.5]], t: [[60, 2.2, 3], [64, 2, 1]], fov: [48, 48], hand: 0.05 },
      lines: [[0.6, '퇴근길 사람들, 버스를 기다리는 학생들. 아무도 서두르지 않았다.', null, 5200]] },
    { dur: 8.5, setup: () => { jm = cine.actor('jeongmin', { path: [[64, -8.5], [76, -13], [86, -18.8], [87, -20.4]], speed: 1.45 }); },
      cam: (k, s) => { const a = jmAt(); return { p: [a.x - 4.2 + k * 2.5, 1.75 + k * 0.2, a.z + 3.2 - k * 1.2], t: [a.x + 0.4, a.y + 1.45, a.z - 0.6], fov: 42 }; },
      lines: [[0.4, '정민, 31세. 한결물류 연무센터에서 배송 운영을 맡고 있다.', null, 4000], [4.4, '오늘 오후부터, 배송 취소가 이상하게 많다.', null, 3800]] },
  ], { title: ['연무동 · 경기도', 'D-3 화요일 17:30'] });
}

// ---- time passing (stands in for the time-skip card): the sky turns over the apartment complex ----
const envAt = (label, power) => {
  const m = /(\d{1,2}):(\d{2})/.exec(label), h = m ? +m[1] + m[2] / 60 : /밤/.test(label) ? 22 : /아침/.test(label) ? 8 : 13;
  const e = h < 5.5 || h >= 20 ? 'night' : h < 7 ? 'dusk' : h < 16 ? 'day' : h < 18.5 ? 'afternoon' : h < 20 ? 'dusk' : 'night';
  return e === 'night' && !power ? 'blackout' : e;
};
// text: one caption or several, shown one after another while the sky turns.
export async function timelapse(ctx, { from, to, text }) {
  const { cine } = ctx, h = here(ctx), a = envAt(from, ctx.power), b = envAt(to, ctx.power);
  await ctx.goto('town', 'home', a);
  const mid = NIGHT.has(a) || NIGHT.has(b) ? (a === b ? a : 'dusk') : ctx.power ? 'night' : 'blackout';
  const texts = [].concat(text), dur = 4 + texts.length * 4.2, step = (dur - 1.2) / texts.length;
  const res = await cine.play([
    { dur, fade: true, cam: { p: [[26, 30, -2], [34, 33, 2]], t: [[88, 18, 44], [92, 22, 40]], fov: [50, 46], hand: 0.15 },
      at: [[dur * 0.15, () => ctx.engine.setEnv(mid, { seconds: dur * 0.3 })], [dur * 0.52, () => ctx.engine.setEnv(b, { seconds: dur * 0.32 })], [dur * 0.52, () => cine.title([to])]],
      lines: texts.map((t, i) => [0.8 + i * step, t, null, step * 1000 - 300]) },
  ], { title: [from] });
  await back(ctx, h);
  return res;
}

// ---- D-day 20:1x: a missile comes down on the villa south of the park ----
const VILLA = { x: 12, y: 9, z: 78 };
export async function strikeScene(ctx, onImpact) {
  const { cine, engine } = ctx, P = engine.player;
  return cine.play([
    { dur: 2.6, cam: { p: [[54, 1.75, 21], [53.6, 1.7, 21.4]], t: [[VILLA.x + 30, 70, VILLA.z - 40], [VILLA.x + 4, 14, VILLA.z - 4]], fov: [60, 50], hand: 0.12 }, exposure: 0.5,
      setup: () => { cine.missile([VILLA.x + 120, 240, VILLA.z - 220], [VILLA.x, VILLA.y, VILLA.z], 2.5); ctx.audio.hit('whistle', 2.4); } },
    { dur: 4.4, slow: 0.4, exposure: 0.6, cam: { p: [[2, 3.4, 54], [3, 3.1, 55.5]], t: [[VILLA.x, 8, VILLA.z], [VILLA.x, 12, VILLA.z]], fov: [46, 40], hand: 0.08 },
      at: [[0.05, () => { onImpact(); engine.shake(0.6, 1.4); ctx.boom?.(VILLA.x, VILLA.z - 9, 2.6); }],
        [0.45, () => ctx.boom?.(VILLA.x - 6, VILLA.z - 9, 1.4)], [0.8, () => ctx.boom?.(VILLA.x + 6, VILLA.z - 8.5, 1.2)]],
      lines: [[0.4, '쾅— 땅이 울렸다. 공원 너머 남쪽 빌라 한 동이 무너져 내린다.', null, 4200]] },
    { dur: 3.4, exposure: 0.4, cam: { p: [[P.x + 2.5, P.y + 1.4, P.z - 2.2], [P.x + 2.2, P.y + 1.5, P.z - 1.9]], t: [[P.x, P.y + 1.0, P.z], [P.x - 3, P.y + 3, P.z + 3]], fov: [50, 54], hand: 0.06 },
      setup: () => { const jm = cine.actor('jeongmin', { at: [P.x, P.z], yaw: Math.atan2(-(VILLA.x - P.x), -(VILLA.z - P.z)) }); jm.setAnim('crouch'); },
      lines: [[0.3, '불길과 검은 연기가 치솟는다. 멈추지 말고 대피소로.', null, 3200]] },
  ], { title: ['연무동', 'D-day 금요일 20:12'] });
}

// ---- D-day 19:40: the lights go out at home, then across the complex ----
export async function blackoutScene(ctx, cut) {
  const { cine } = ctx, y = FL;
  const flick = (on) => { ctx.setLevelState('apartment', 'power', on ? 'on' : 'off'); ctx.engine.setPower(on); }; // the lights stutter first
  return cine.play([
    { dur: 3.2, cam: { p: [[-1.0, y + 1.75, 3.4], [-1.15, y + 1.72, 3.8]], t: [[-2.3, y + 1.1, 8.6], [-2.4, y + 1.05, 8.6]], fov: [60, 57], hand: 0.03 },
      at: [[1.4, () => flick(false)], [1.55, () => flick(true)], [1.8, () => flick(false)], [1.9, () => flick(true)], [2.3, cut]] },
    { dur: 3.6, cam: { p: [[-1.6, y + 1.6, 5.2], [-2.0, y + 1.55, 7.0]], t: [[-2.4, y + 1.4, 12], [-2.4, y + 1.6, 14]], fov: [52, 48], hand: 0.04 },
      lines: [[0.2, '정전 — 집 안의 불이 한꺼번에 꺼졌다. 창밖 건너편 동도 캄캄하다.', null, 3400]] },
  ], { title: ['한빛아파트 103동 302호', 'D-day 금요일 19:40'] });
}

// ---- D-day 20:05: the siren over the dark complex ----
export async function sirenScene(ctx) {
  const { cine } = ctx, h = here(ctx);
  let lead = null;
  await ctx.goto('town', 'home');
  const res = await cine.play([
    { dur: 6.5, fade: true, exposure: 1.2, cam: { p: [[50, 1.5, 12.5], [49, 1.55, 11.5]], t: [[64, 1.5, 15], [38, 1.4, 9]], fov: [50, 50], hand: 0.12 },
      setup: () => { // residents of the complex hurry out of the gate toward the shelter, phone torches on
        const who = [['crowd', 0, 1.9, ['flashlight']], ['crowd', 0.5, 2.4, []], ['elder', 1.1, 1.2, []], ['crowd', 1.6, 2.6, ['backpack', 'flashlight']], ['crowd', 2.4, 2.1, ['backpack']]];
        lead = null;
        who.forEach(([preset, delay, speed, props], i) => { const a = cine.actor(preset, { path: [[66 + i * 0.6, 16 + (i % 2) * 1.2], [56, 11 + (i % 3) * 0.7], [30, 9 + (i % 2)]], speed, delay, props }); lead ||= a; });
      },
      fill: { at: () => lead && [lead.group.position.x - 1.5, 2.6, lead.group.position.z + 1.5], i: 9, color: '#e8ecff' }, // phone torches
      lines: [[0.5, '20:05 — 공습경보. 사이렌이 오르내리며 1분 동안 울린다.', null, 5200]] },
    { dur: 5, exposure: 1.1, cam: { p: [[40, 2, -8], [46, 2.2, -6]], t: [[-20, 6, 30], [-30, 10, 34]], fov: [46, 44], hand: 0.08 },
      lines: [[0.4, '어두운 거리로 사람들이 쏟아져 나온다. 다들 지하로 향한다.', null, 4200]] },
  ], { title: ['연무동', 'D-day 금요일 20:05'] });
  await back(ctx, h);
  return res;
}

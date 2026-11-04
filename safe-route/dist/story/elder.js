// 경보 속 딜레마: 304호 어르신 (design M2). On the way to the stairs during the air-raid alert, 어르신 stands at
// her door holding the rail. Three answers, none of them wrong:
//   help    – go down together (3 minutes to get ready, then the player's pace drops to hers until 1F)
//   manager – go first and tell 박 소장 at the 1F window, who goes up for her
//   go      – go first; 박 소장 brings her down later
// G.know.elderPick keeps the answer (endure's corridor fire and the shelter read it).
// createElderDilemma(ctx) -> { tick(dt), managerLine() }: call tick from the alert roam, managerLine at the window.
import { recordEvent } from '../game/state.js';
import { groundAt } from '../engine/geom.js';
import { fact } from '../game/facts.js';

const CALL_R = 11, BEHIND = 1.1, PACE = 0.5;
const SPOT = new WeakMap(); // her model -> where the level put her (the follower moves her; a retry puts her back)

// Pure: where a follower walking the player's breadcrumb trail should head (the crumb about `behind` m back).
export function followTarget(crumbs, behind = BEHIND) {
  let d = 0;
  for (let i = crumbs.length - 1; i > 0; i--) {
    d += Math.hypot(crumbs[i].x - crumbs[i - 1].x, crumbs[i].z - crumbs[i - 1].z);
    if (d >= behind) return crumbs[i - 1];
  }
  return crumbs[0] ?? null;
}
// Pure: the elder dilemma counts as "brought down" for the later payoffs when helped or 박 소장 was told.
export const elderSafeEarly = (know) => know?.elderPick === 'help' || !!know?.elderTold;

export function createElderDilemma(ctx) {
  const G = ctx.G, engine = ctx.engine;
  let asking = false, follow = false, crumbs = [], anim = '';
  const point = () => engine.level?.points?.find(p => p.id === 'elder304');
  const npc = () => (engine.level?.id === 'apartment' ? engine.level.npcs?.elder : null);
  { const g = npc()?.group; if (g) { const o = SPOT.get(g); if (o) { g.position.set(o.x, o.y, o.z); g.rotation.y = o.ry; npc().setAnim?.('idle'); } else SPOT.set(g, { x: g.position.x, y: g.position.y, z: g.position.z, ry: g.rotation.y }); } }

  async function ask() {
    asking = true;
    try {
      await ctx.caption('계단 옆 304호 앞에서 어르신이 벽 손잡이를 붙들고 서 있다.', null, 2600);
      const pick = await ctx.say('304호 어르신', ['"302호 정민 씨예요? 사이렌이 울리는데 계단이 캄캄해서… 혼자 내려가기가 겁나네요."'], [
        { id: 'help', label: '부축해서 같이 내려간다 (시간이 더 걸린다)' },
        { id: 'manager', label: '1층 관리사무소에 알리고 먼저 간다' },
        { id: 'go', label: '먼저 내려간다' },
      ], '경보 중 · 3층 복도');
      G.know.elderPick = pick;
      recordEvent(G, 'elder_choice', { pick, minute: G.minute });
      if (pick === 'help') {
        await ctx.say('304호 어르신', ['"고마워요. 신발만 신을게요. 천천히 가요. 손잡이 쪽에 서 줘요."'], null, '304호 앞');
        ctx.pass(3);
        ctx.toast(`어르신 걸음에 맞춰 계단으로. ${fact('people_ask').text}`, 'official', 6000);
        follow = true; crumbs = [];
        const p = point(); if (p) p.enabled = false;
        ctx.speedOverride = PACE; ctx.refresh();
      } else if (pick === 'manager') {
        await ctx.say('304호 어르신', ['"그래요, 소장님한테 말 좀 해 줘요. 나는 여기 손잡이 잡고 있을게요."'], null, '304호 앞');
        ctx.toast('1층 관리사무소 창구에서 박 소장에게 알리자.', 'self', 4200);
      } else {
        await ctx.say('304호 어르신', ['"그래요, 어서 가요. 나는 소장님 올라오면 같이 갈게요."'], null, '304호 앞');
      }
    } finally { asking = false; }
  }

  // The follower: 어르신 walks the player's trail a step behind, at her own pace, down to 1F.
  function walk(dt) {
    const n = npc(), P = engine.player, L = engine.level;
    if (!n) return;
    let last = crumbs.at(-1);
    if (last && Math.hypot(last.x - P.x, last.y - P.y, last.z - P.z) > 3) { crumbs = []; last = null; } // a door or teleport: start over
    if (!last || Math.hypot(last.x - P.x, last.z - P.z) > 0.3 || Math.abs(last.y - P.y) > 0.3) crumbs.push({ x: P.x, y: P.y, z: P.z });
    if (crumbs.length > 400) crumbs.splice(0, 100);
    const g = n.group, t = followTarget(crumbs);
    if (!t) return;
    const dx = t.x - g.position.x, dz = t.z - g.position.z, d = Math.hypot(dx, dz);
    if (d > 8 || Math.abs(g.position.y - P.y) > 3) { g.position.set(t.x, t.y, t.z); return; } // a door or teleport in between
    let moving = false;
    if (d > 0.2) { const v = Math.min(d * 2, 2.2) * dt; g.position.x += (dx / d) * v; g.position.z += (dz / d) * v; g.rotation.y = Math.atan2(-dx, -dz); moving = true; }
    const ground = groundAt(L.walk, g.position.x, g.position.z, g.position.y + 0.4, false, 1.2);
    g.position.y += ((ground ? ground.y : t.y) - g.position.y) * Math.min(1, dt * 10);
    const want = moving ? 'walk' : 'idle';
    if (want !== anim) { anim = want; n.setAnim?.(want); }
    n.setSpeed?.(moving ? Math.min(d * 2, 1.2) : 0);
  }

  async function arrive(talk = true) {
    follow = false;
    ctx.speedOverride = null; ctx.refresh();
    anim = 'idle'; npc()?.setAnim?.('idle');
    (ctx.levelStates.apartment ||= {}).elder304 = 'shelter'; // gone down; she stays in view until the next enter
    ctx.setLevelState('shelter', 'elder', 'on');
    recordEvent(G, 'help', { who: 'elder', what: '경보 속 캄캄한 계단에서 304호 어르신을 부축해 1층까지 함께 내려갔다' });
    if (!talk) return void ctx.toast('어르신은 1층에서 박 소장과 함께 천천히 오기로 했다.', 'neighbor', 4200);
    await ctx.say('304호 어르신', ['"다 왔네. 여기서부터는 소장님이랑 천천히 갈게요. 고마워요. 어서 가요."'], null, '1층 계단 앞');
  }

  return {
    get following() { return follow; },
    tick(dt) {
      if (follow && engine.level?.id !== 'apartment') return void arrive(false); // out of the building first
      if (asking || engine.level?.id !== 'apartment') return;
      const P = engine.player;
      if (!G.know.elderPick) {
        const p = point();
        if (p?.enabled !== false && P.y > 4 && Math.abs(P.y - p.y) < 1.5 && Math.hypot(P.x - p.x, P.z - p.z) < CALL_R) ctx.modal(ask).catch(err => console.error('[elder] dilemma failed', err));
        return;
      }
      if (!follow) return;
      walk(dt);
      const g = npc()?.group;
      if (P.y < 1 && g && g.position.y < 1 && Math.hypot(g.position.x - P.x, g.position.z - P.z) < 3) { asking = true; ctx.modal(arrive).finally(() => { asking = false; }); }
    },
    // At the 1F window: tell 박 소장 (once). Returns true when the line was said.
    async managerLine() {
      if (G.know.elderPick !== 'manager' || G.know.elderTold) return false;
      G.know.elderTold = true;
      await ctx.ui.talk({ speaker: '정민', role: '1층 관리사무소', lines: ['소장님, 304호 어르신이 3층 계단 앞에 계세요. 혼자 내려오시기 어려워 보여요.'] });
      await ctx.ui.talk({ speaker: '박영수 소장', role: '1층 관리사무소', lines: ['알려 줘서 고마워요. 제가 바로 올라가서 모시고 갈게요. 정민 씨는 먼저 가요!'] });
      recordEvent(G, 'help', { who: 'manager', what: '정민의 말을 듣고 304호 어르신을 모시고 내려왔다' });
      ctx.setLevelState('apartment', 'elder304', 'shelter');
      ctx.setLevelState('shelter', 'elder', 'on');
      return true;
    },
  };
}

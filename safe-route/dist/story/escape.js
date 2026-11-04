// 연기 속 대피 (design M7): after choosing to leave the corridor fire, 정민 gets down to 1F through the smoke.
// A wet towel over nose and mouth and a low body (C) cut what you breathe in (fact fire_big); the screen blurs
// with the smoke you have breathed (#world.sim-smoke, --smoke 0..1). Health runs out -> dead { why: 'fire' }.
// smokeEscape(ctx, { size, grow }) resolves at the 1F lobby with { exposure, towel, crouch }.
import { recordEvent, consume } from '../game/state.js';
import { fact } from '../game/facts.js';
import { smokeStep } from './strain.js';

const count = (G, id) => (G.items[id]?.home ?? 0) + (G.items[id]?.bag ?? 0);
const AIR = { small: 0.25, large: 0.7 }; // apartment 'smoke' state per fire size

export async function smokeEscape(ctx, { size = 'large', grow = () => {} } = {}) {
  const G = ctx.G, engine = ctx.engine, world = document.getElementById('world');
  const s = { exposure: 0, hurtT: 0 };
  let towel = false, low = 0, total = 0, warned = 0, t = 0, hurtShown = 0;
  const show = () => ctx.objective('연기 속에서 1층까지', '몸을 낮추고(C) 계단으로 내려갑니다. 엘리베이터는 쓰지 않습니다.', [
    { text: '젖은 수건으로 코와 입 막기 (302호 싱크대·베란다, 선택)', done: towel }, { text: '계단으로 1층까지', done: false }]);
  show();
  const wet = async () => {
    if (towel) return void ctx.toast('젖은 수건으로 코와 입을 막고 있다.');
    if (!count(G, 'towel')) return void ctx.toast('수건이 보이지 않는다. 소매로라도 코와 입을 막자.');
    const bottle = ['water_500', 'water_2l'].find(id => count(G, id) > 0);
    if (!G.know.tubFilled && !bottle) return void ctx.toast('수건을 적실 물이 없다. 몸을 낮춰 바로 내려가자.');
    if (G.know.tubFilled) ctx.payoff('tub', '받아 둔 물로 수건을 적셨다');
    else { consume(G, bottle, 1, G.items[bottle].home > 0 ? 'home' : 'bag'); ctx.toast('생수 한 병으로 수건을 적셨다.'); }
    towel = true; recordEvent(G, 'towel_wet', { with: G.know.tubFilled ? 'tub' : bottle }); show();
    ctx.toast(fact('fire_big').text, 'official', 5200);
  };
  world?.classList.add('sim-smoke');
  try {
    return await ctx.roam({ rate: 0, tick: (dt) => {
      const P = engine.player, inApt = engine.level?.id === 'apartment';
      if ((t += dt) > 20 && size === 'small') { size = 'large'; grow(); }
      // 302호 itself (floor 5.75, the corridor is 5.6) has its door shut: less smoke; the stairwell less.
      const air = !inApt ? 0 : P.y > 5.68 ? AIR[size] * 0.3 : P.y > 4 ? AIR[size] : P.y > 1 ? AIR[size] * 0.4 : 0;
      const crouch = !!engine.crouching;
      total += dt; if (crouch) low += dt;
      const r = smokeStep(s, dt, { smoke: air, crouch, towel });
      world?.style.setProperty('--smoke', r.level.toFixed(3));
      hurtShown -= dt;
      if (r.hurt) { ctx.deathCause = 'fire'; ctx.stat('health', -r.hurt); if (hurtShown <= 0) { hurtShown = 1.5; ctx.hud.hurt?.(0.25); } }
      if (warned === 0 && s.exposure > 4) { warned = 1; ctx.toast(crouch ? '연기가 목을 찌른다. 이대로 낮게 계단까지.' : '연기가 목을 찌른다. 몸을 낮추자 (C).', 'self', 3600); }
      else if (warned === 1 && s.exposure > 14) { warned = 2; ctx.toast('눈앞이 뿌옇다. 계단은 복도 가운데, 비상구 표시등 쪽이다.', 'self', 4200); }
      if (inApt && P.y < 1) return { exposure: Math.round(s.exposure * 10) / 10, towel, crouch: total > 0 && low / total > 0.5 };
    }, on: {
      homeExitDoor: async () => { await ctx.goto('apartment', 'corridor3F'); },
      homeDoor: async () => { await ctx.goto('apartment', 'homeEntry'); }, // back in for a towel
      homeSink: wet, homeStock: wet,
      elevator: async () => { ctx.toast(fact('fire_lift').text, 'official', 5000); recordEvent(G, 'elevator_tried', { fire: true }); },
      hallFire: async () => { ctx.toast('불길에서 떨어져 계단으로.', 'self'); },
      extinguisher: async () => { ctx.toast('불이 이미 번지고 있다. 끄기보다 대피가 먼저다.', 'self'); },
    } }).then((res) => { recordEvent(G, 'smoke_escape', res); return res; });
  } finally {
    world?.classList.remove('sim-smoke'); world?.style.removeProperty('--smoke');
  }
}

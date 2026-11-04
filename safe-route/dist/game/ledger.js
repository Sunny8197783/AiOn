// Day ledger (하루 장부): the three picks before a time skip (water L/day, meals/day, the daily power hour)
// played out day by day. Pure and deterministic; game numbers, not advice (water_sphere: 2.5~3 L to drink).
//
// simulateDays(G, plan, days, { place = 'home' } = {}) -> result. Does NOT change G.
//   plan: { water: 1|2|3 (L/day), meals: 1|2|3, power: 'phone'|'powerbank'|'radio' } (see PICKS).
//   days: whole days from G.day on (rows are G.day .. G.day + days - 1). place: 'home' | 'shelter'.
//   result: { plan, place, rows, lines, use, stats, phone, radioDrain, powerbankCharged, radioDaily, summary }
//     rows: one { day, water, meals, power, place, drank, ate, cooked, skipped, trips, loose, gasLeft } per day.
//     lines: 1..5 calm Korean sentences for the timelapse (ctx.skip text array).
//     use: { id: { home, bag } } units eaten/drunk; stats: { stat: delta } already capped (see below).
// applyLedger(G, result) -> rows: consumes the items, applies the stats, phone and radio charge, charges the
//   powerbank, sets G.know.radioDaily, appends rows to G.ledger and records 'ledger' { plan, place, days, ... }.
// planLine(plan) -> '하루 배분: 물 2L · 두 끼 · 보조배터리' (chapter summary). ledgerSummary(G) -> totals.
//
// Rules: stock never goes negative. At home water comes from bottles (home, then bag) and, when they run out,
// a trip to the water point (+10 L with water_jug, +4 L without; fatigue +6 each, as in story/endure.js).
// Ramen needs gas_stove + butane AND place 'home' (home_stove, shelter_rules); one butane bundle cooks 12
// meals. Other food: canned, rice_instant, bars, then last year's old_bars (health -4, tension +4 each).
// At the shelter, water and two meals a day are provided; a third meal comes from the bag. Daily radio
// listening drains the radio 8 %/day. The ledger alone never pushes thirst past 80 or fatigue past 85,
// so it cannot cause the 'exhausted' fail (that stays tied to story/endure.js).
import { CATALOG } from './items.js';
import { adjust, consume, check, drainCharge, recordEvent } from './state.js';

export const PICKS = {
  water: [{ id: 1, label: '하루 1L' }, { id: 2, label: '하루 2L' }, { id: 3, label: '하루 3L' }],
  meals: [{ id: 1, label: '한 끼' }, { id: 2, label: '두 끼' }, { id: 3, label: '세 끼' }],
  power: [{ id: 'phone', label: '휴대폰 충전' }, { id: 'powerbank', label: '보조배터리' }, { id: 'radio', label: '라디오 매일 듣기' }],
};
const label = (k, id) => PICKS[k].find(p => p.id === id)?.label;
const LITRES = { water_2l: 2, water_500: 0.5 };
const FOOD = ['canned', 'rice_instant', 'bars'];
const RADIO_PER_DAY = 8, GAS_MEALS = 12, TRIP_FATIGUE = 6;
const THIRST = { 1: 8, 2: 2, 3: -4 }, TIRED = { 1: 4, 2: 1, 3: 0 };
const CAP = { thirst: 80, fatigue: 85 }, FLOOR = { health: 20 };
const r1 = (x) => Math.round(x * 10) / 10;
const times = (n, unit) => (n >= 1 && n <= 4 ? `${['한', '두', '세', '네'][n - 1]} ${unit}` : `${n}${unit}`); // 한 번, 두 끼, 12번

export const planLine = (plan) => `하루 배분: 물 ${plan.water}L · ${label('meals', plan.meals)} · ${label('power', plan.power)}`;

function checkArgs(plan, days, place) {
  if (!label('water', plan?.water) || !label('meals', plan?.meals) || !label('power', plan?.power)) throw new Error(`bad ledger plan: ${JSON.stringify(plan)}`);
  if (!Number.isInteger(days) || days < 1 || days > 30) throw new RangeError(`days must be 1..30, got ${days}`);
  if (place !== 'home' && place !== 'shelter') throw new Error(`place must be 'home' or 'shelter', got ${place}`);
}

export function simulateDays(G, plan, days, { place = 'home' } = {}) {
  checkArgs(plan, days, place);
  const from = place === 'home' ? ['home', 'bag'] : ['bag'];
  const left = {}, use = {};
  const count = (id) => from.reduce((s, k) => s + (G.items[id]?.[k] ?? 0), 0) - (left[id] ?? 0);
  const take = (id) => {
    const k = from.find(k => (G.items[id]?.[k] ?? 0) - (use[id]?.[k] ?? 0) > 0);
    if (!k) return false;
    (use[id] ??= { home: 0, bag: 0 })[k] += 1;
    left[id] = (left[id] ?? 0) + 1;
    return true;
  };
  const owns = (id) => (G.items[id]?.home ?? 0) + (G.items[id]?.bag ?? 0) > 0;
  const prev = G.ledger?.at(-1);
  let loose = prev?.loose ?? 0, gasLeft = prev?.gasLeft ?? 0;
  const jug = owns('water_jug'), stove = place === 'home' && owns('gas_stove');
  const radioOn = plan.power === 'radio' && !!G.items.radio?.checked;
  const bank = plan.power === 'powerbank' && owns('powerbank');
  let radioCharge = radioOn ? G.items.radio.charge ?? 100 : 0, radioDays = 0;
  const raw = { thirst: 0, fatigue: 0, tension: 0, health: 0 };
  let phone = G.phone.battery;
  const rows = [], tot = { trips: 0, cooked: 0, skipped: 0, oldBars: 0 };

  for (let i = 0; i < days; i++) {
    // Water: what is open first, then a bottle that fits the need, then the water point.
    let need = plan.water, trips = 0;
    if (place === 'shelter') need = 0;
    const sip = () => { const d = Math.min(loose, need); loose = r1(loose - d); need = r1(need - d); };
    sip();
    while (need > 0) {
      const id = (need >= 2 ? ['water_2l', 'water_500'] : ['water_500', 'water_2l']).find(x => count(x) > 0);
      if (!id) break;
      take(id); loose = r1(loose + LITRES[id]); sip();
    }
    if (need > 0) { trips++; loose = r1(loose + (jug ? 10 : 4)); raw.fatigue += TRIP_FATIGUE; sip(); }
    raw.thirst += THIRST[plan.water];
    // Meals: provided at the shelter (two a day), otherwise cooked ramen first, then no-cook food.
    let ate = 0, cooked = 0, skipped = 0;
    for (let m = place === 'shelter' ? Math.min(2, plan.meals) : 0; m < plan.meals; m++) {
      if (stove && count('ramen') > 0 && (gasLeft > 0 || count('butane') > 0)) {
        if (gasLeft === 0) { take('butane'); gasLeft = GAS_MEALS; }
        take('ramen'); gasLeft--; cooked++; ate++; continue;
      }
      const id = FOOD.find(x => count(x) > 0);
      if (id) { take(id); ate++; continue; }
      if (take('old_bars')) { ate++; tot.oldBars++; raw.health -= 4; raw.tension += 4; continue; }
      skipped++; raw.fatigue += 3; raw.tension += 2;
    }
    if (place === 'shelter') ate += Math.min(2, plan.meals);
    raw.fatigue += TIRED[plan.meals];
    // The power hour.
    if (plan.power === 'phone' || (!bank && !radioOn)) phone = 100;
    else phone = Math.max(0, phone - (bank ? 6 : 12));
    if (radioOn && radioCharge > 0) { radioCharge = Math.max(0, radioCharge - RADIO_PER_DAY); radioDays++; raw.tension -= 2; }
    rows.push({ day: G.day + i, water: plan.water, meals: plan.meals, power: plan.power, place, drank: plan.water, ate, cooked, skipped, trips, loose, gasLeft });
    tot.trips += trips; tot.cooked += cooked; tot.skipped += skipped;
  }

  const stats = {};
  for (const [s, d] of Object.entries(raw)) {
    const cur = G.stats[s], hi = CAP[s] ? Math.max(cur, CAP[s]) : 100, lo = FLOOR[s] ? Math.min(cur, FLOOR[s]) : 0;
    stats[s] = Math.min(hi, Math.max(lo, cur + d)) - cur;
  }
  const lines = [`${planLine(plan).replace('하루 배분: ', '하루 ')}로 ${days}일을 보냈다.`];
  if (place === 'shelter') lines.push('임시 거처에서 물을 받고, 하루 두 번 배식을 받았다.');
  if (tot.trips) lines.push(`급수소에 ${times(tot.trips, '번')} 다녀왔다. ${jug ? '접이식 물통 덕분에 한 번에 10L씩 받아 왔다.' : '물통이 없어 한 번에 4L만 들고 올 수 있었다.'}`);
  if (tot.cooked) lines.push(`휴대용 가스레인지로 라면을 ${times(tot.cooked, '번')} 끓였다.`);
  if (tot.oldBars) lines.push('작년 가방에서 나온 건빵을 먹었다. 기한이 지나 속이 편하지 않았다.');
  if (tot.skipped) lines.push(`먹을 것이 모자라 ${times(tot.skipped, '끼')}를 걸렀다.`);
  if (plan.water === 1) lines.push('물을 아끼느라 목이 자주 말랐다.');
  if (plan.power === 'radio' && !radioOn) lines.push('라디오를 켤 수 없어 그 시간에 휴대폰을 충전했다.');
  else if (radioOn) lines.push(radioCharge === 0 ? '매일 라디오를 듣다 보니 건전지가 다 닳았다.' : `매일 라디오로 안내를 들었다. 건전지가 ${radioCharge}% 남았다.`);
  if (plan.power === 'powerbank' && !bank) lines.push('보조배터리가 없어 그 시간에 휴대폰을 충전했다.');
  return {
    plan: { ...plan }, place, rows, lines: lines.slice(0, 5), use, stats, phone,
    radioDrain: radioOn ? (G.items.radio.charge ?? 100) - radioCharge : 0, radioDaily: radioDays > 0, powerbankCharged: bank,
    summary: { days, ...tot },
  };
}

export function applyLedger(G, result) {
  if (!result?.rows?.length) throw new Error('applyLedger needs a simulateDays result');
  for (const [id, n] of Object.entries(result.use)) {
    if (!CATALOG[id]) throw new Error(`unknown item: ${id}`);
    if (n.home) consume(G, id, n.home, 'home');
    if (n.bag) consume(G, id, n.bag, 'bag');
  }
  for (const [s, d] of Object.entries(result.stats)) if (d) adjust(G, s, d);
  G.phone.battery = result.phone;
  if (result.radioDrain) drainCharge(G, 'radio', result.radioDrain);
  if (result.radioDaily) G.know.radioDaily = true;
  if (result.powerbankCharged) check(G, 'powerbank');
  (G.ledger ??= []).push(...result.rows.map(r => ({ ...r })));
  recordEvent(G, 'ledger', { plan: result.plan, place: result.place, ...result.summary });
  return result.rows;
}

// Totals over every ledger row so far (endings, debrief).
export function ledgerSummary(G) {
  const rows = G.ledger ?? [];
  const sum = (k) => rows.reduce((s, r) => s + (r[k] ?? 0), 0);
  return { days: rows.length, trips: sum('trips'), cooked: sum('cooked'), skipped: sum('skipped'), minWater: rows.length ? Math.min(...rows.map(r => r.water)) : 0 };
}

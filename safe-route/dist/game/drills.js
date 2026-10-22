// Minigame rules: bag packing, radio tuning, extinguisher order, emergency contact card.
// Safety text is never written here: goals and steps point at FACTS ids (game/facts.js).
import { CATALOG } from './items.js';
import { fact } from './facts.js';
import { bagLoad, recordEvent } from './state.js';

// --- Encounter ladder: guided -> hint -> free -------------------------------------------------

export function ladderStage(G, tool) {
  const runs = G?.skill?.[tool];
  if (!Array.isArray(runs)) throw new Error(`unknown drill tool: ${tool}`);
  return runs.length === 0 ? 'guided' : runs.length === 1 ? 'hint' : 'free';
}

export function recordEncounter(G, tool, result) {
  const stage = ladderStage(G, tool);
  const r = { day: G.day, minute: G.minute, stage, result };
  G.skill[tool].push(r);
  recordEvent(G, 'encounter', { tool, result, stage });
  return r;
}

// --- Bag ------------------------------------------------------------------------------------

const WATER = ['water_500', 'water_2l'];
const LITRES = { water_500: 0.5, water_2l: 2 }; // drinking water is counted in litres, not bottles
const FOOD = ['bars', 'canned', 'rice_instant'];
const LIGHT = { id: 'light', label: '켜지는 손전등', items: ['flashlight'], facts: ['bag_days', 'power_prep'] };
const INFO = { id: 'info', label: '라디오 또는 충전된 보조배터리', items: ['radio', 'powerbank'], facts: ['bag_days', 'radio_batt', 'comms_battery'] };
const MEDS = { id: 'meds', label: '복용약·상비약', items: ['meds', 'first_aid'], facts: ['bag_days', 'contact_fields'] };
const WARMTH = { id: 'warmth', label: '보온(은박담요·여벌 옷·우의)', items: ['blanket', 'clothes', 'raincoat'], facts: ['bag_foil', 'home_clothes'] };

// Goals per context. min = units needed in the bag (default 1), or litres when per maps item -> litres.
// The bag UI shows the labels and ticks a goal with the same rule (goalMet).
export const BAG_GOALS = {
  alert: [
    { id: 'water', label: '마실 물 2L 이상', items: WATER, min: 2, per: LITRES, facts: ['bag_days', 'water_sphere'] },
    { id: 'food', label: '조리 없이 먹을 음식', items: FOOD, facts: ['bag_nocook'] },
    LIGHT, INFO, MEDS,
    { id: 'contacts', label: '연락처 카드 또는 서류 사본', items: ['contact_card', 'doc_copies'], facts: ['contact_card', 'bag_copies'] },
    WARMTH,
  ],
  water: [
    { id: 'container', label: '빈 물통', items: ['water_jug'], facts: ['water_point'] },
  ],
  evac: [
    { id: 'water', label: '마실 물 2L 이상', items: WATER, min: 2, per: LITRES, facts: ['bag_days', 'water_sphere'] },
    { id: 'food', label: '조리 없이 먹을 음식 3개 이상', items: FOOD, min: 3, facts: ['bag_days', 'bag_nocook'] },
    LIGHT, INFO, MEDS,
    { id: 'contacts', label: '비상연락처 카드', items: ['contact_card'], facts: ['contact_card', 'contact_fields'] },
    { id: 'docs', label: '신분증·중요 서류 사본', items: ['id_card', 'doc_copies'], facts: ['bag_copies', 'docs_take'] },
    { id: 'cash', label: '현금', items: ['cash'], facts: ['bag_cash'] },
    WARMTH,
    { id: 'hygiene', label: '위생용품', items: ['hygiene_kit', 'wipes', 'tissue', 'masks'], facts: ['bag_days'] },
  ],
};

// Why an item that fills no goal is worth a second look in a bag.
const EXTRA_TIPS = { gas_stove: 'shelter_rules', butane: 'shelter_rules', candles: 'candle_no', ramen: 'bag_nocook', fridge_food: 'food_4h', cooked_meat: 'myth_meat' };
const WATER_TRIP_KG = 2; // game number: the way back carries up to 10 L of water

export const goalMet = (g, count, ready) => g.items.filter(ready).reduce((s, id) => s + count(id) * (g.per?.[id] ?? 1), 0) >= (g.min ?? 1);

export function evalBag(G, context) {
  const goals = BAG_GOALS[context];
  if (!goals) throw new Error(`unknown bag context: ${context}`);
  const inBag = (id) => G.items[id]?.bag ?? 0;
  const ready = (id) => inBag(id) > 0 && (!CATALOG[id].needsCheck || G.items[id].checked);
  const { weightKg, volumeL, over } = bagLoad(G);
  const covered = [], missing = [], tips = new Set();
  for (const g of goals) {
    if (goalMet(g, inBag, ready)) covered.push(g.id);
    else { missing.push(g.id); g.facts.forEach(f => tips.add(f)); }
  }
  const wanted = new Set(goals.flatMap(g => g.items));
  const packed = Object.keys(G.items).filter(id => inBag(id) > 0);
  const extra = packed.filter(id => !wanted.has(id));
  extra.forEach(id => EXTRA_TIPS[id] && tips.add(EXTRA_TIPS[id]));
  // Packed but not usable yet (bought is not usable): no batteries in, not charged.
  const unready = packed.filter(id => CATALOG[id].needsCheck && !G.items[id].checked);
  const heavy = context === 'water' ? weightKg > WATER_TRIP_KG : over.weight || over.volume;
  if (heavy) tips.add(context === 'water' ? 'water_point' : 'bag_foil');
  return { ok: !missing.length && !heavy, weightKg, volumeL, covered, missing, extra, unready, tips: [...tips] };
}

// --- Radio ----------------------------------------------------------------------------------

export const RADIO_WIDTH = 0.6; // MHz either side of a station where anything is heard

export function radioSignal(freq, stations = []) {
  if (typeof freq !== 'number' || !Number.isFinite(freq)) throw new TypeError(`freq must be a number, got ${freq}`);
  let station = null, clarity = 0;
  for (const s of stations ?? []) {
    const c = Math.max(0, 1 - Math.abs(freq - s.freq) / RADIO_WIDTH);
    if (c > clarity) { clarity = c; station = s; }
  }
  return { station, clarity: Math.round(clarity * 1000) / 1000 };
}

// What stands between the player and the radio. An unchecked radio with spare batteries still
// works, but loading them costs time at the worst moment.
export function radioNeeds(G) {
  const has = (id) => { const e = G.items[id]; return !!e && e.home + e.bag > 0; };
  if (!has('radio')) return { ok: false, blocks: ['noRadio'], extraSeconds: 0, tips: ['power_prep'] };
  if (G.items.radio.checked) return { ok: true, blocks: [], extraSeconds: 0, tips: [] };
  if (!has('batteries_aa')) return { ok: false, blocks: ['noBatteries'], extraSeconds: 0, tips: ['radio_batt'] };
  return { ok: true, blocks: ['notChecked'], extraSeconds: 40, tips: ['radio_batt'] };
}

// --- Extinguisher ---------------------------------------------------------------------------

// Official order (fire_shout, ext_1..ext_3, ext_door).
export const EXT_STEPS = [
  { id: 'shout', label: '"불이야!" 외쳐 알리기', facts: ['fire_shout'] },
  { id: 'pin', label: '소화기를 옮겨 안전핀 뽑기', facts: ['ext_1'] },
  { id: 'aim', label: '바람·문을 등지고 노즐을 불 쪽으로', facts: ['ext_2', 'ext_door'] },
  { id: 'squeeze', label: '손잡이 힘껏 움켜쥐기', facts: ['ext_3'] },
  { id: 'sweep', label: '빗자루로 쓸듯이 뿌리기', facts: ['ext_3'] },
];
export const EXT_CHOICES = [
  { id: 'fight', label: '소화기로 끈다', facts: ['fire_small'] }, // candle-specific advice stays with the candle fire's own card
  { id: 'retreat', label: '대피하며 알린다 (계단 · 119)', facts: ['fire_big', 'fire_119'] },
];
const ORDER = EXT_STEPS.map(s => s.id);
const OPS = ORDER.slice(1); // what physically puts the fire out

// sequence: decision ('fight'|'retreat') and step ids in the order pressed. Retreating is always a
// success; on a large fire it is the only correct choice. result 'out' needs pin→aim→squeeze→sweep
// in order; `correct` also needs the alert first. firstError = the step/choice expected instead.
export function evalExtinguisher(sequence, { fire } = {}) {
  if (fire !== 'small' && fire !== 'large') throw new Error(`fire must be 'small' or 'large', got ${fire}`);
  if (!Array.isArray(sequence)) throw new TypeError('sequence must be an array');
  const bad = sequence.find(id => !ORDER.includes(id) && id !== 'fight' && id !== 'retreat');
  if (bad !== undefined) throw new Error(`unknown extinguisher step: ${bad}`);
  const decision = sequence.find(id => id === 'fight' || id === 'retreat');
  if (sequence.includes('retreat')) {
    const correct = fire === 'small' || decision === 'retreat';
    return { result: 'retreat-ok', correct, firstError: correct ? null : 'retreat' };
  }
  if (!sequence.length) return { result: 'incomplete', correct: false, firstError: fire === 'large' ? 'retreat' : ORDER[0] };
  if (fire === 'large') return { result: 'escalated', correct: false, firstError: 'retreat' };
  const steps = [...new Set(sequence.filter(id => ORDER.includes(id)))];
  const ops = steps.filter(id => OPS.includes(id));
  const out = ops.length === OPS.length && OPS.every((id, i) => ops[i] === id);
  const firstError = ORDER.find((id, i) => steps[i] !== id) ?? null;
  return { result: out ? 'out' : 'incomplete', correct: firstError === null, firstError };
}

// --- Emergency contact card -----------------------------------------------------------------

export const DOC_FIELDS = [
  { id: 'contacts', label: '가족·지인 연락처', facts: ['contact_card', 'contact_fields'] },
  { id: 'meds', label: '복용약·혈액형', facts: ['contact_fields'] },
  { id: 'shelter', label: '대피소 주소', facts: ['shelter_know', 'contact_meet'] },
  { id: 'meet', label: '만남의 장소(1·2·3차)', facts: ['contact_meet'] },
  { id: 'idCopy', label: '신분증 사본', facts: ['bag_copies', 'docs_take'] },
  { id: 'cash', label: '현금 챙기기 메모', facts: ['bag_cash'] },
].map(f => ({ ...f, why: fact(f.facts[0]).text, recommended: true }));

// card { fieldId: value } from the docs UI; where = 'bag' | 'home' (optional).
export function evalDocs(card, where) {
  const filled = (v) => v !== undefined && v !== null && v !== false && String(v).trim() !== '';
  const covered = DOC_FIELDS.filter(f => filled(card?.[f.id])).map(f => f.id);
  const missing = DOC_FIELDS.filter(f => f.recommended && !covered.includes(f.id)).map(f => f.id);
  const tips = new Set(DOC_FIELDS.filter(f => missing.includes(f.id)).flatMap(f => f.facts));
  const away = where !== undefined && where !== 'bag';
  if (away) tips.add('contact_card');
  return { ok: !missing.length && !away, covered, missing, tips: [...tips] };
}

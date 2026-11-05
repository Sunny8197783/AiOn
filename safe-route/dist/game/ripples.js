// 나비효과 기록: pairs each preparation that paid off (a 'payoff' event, story ctx.payoff) with the earlier
// moment that set it up, so the player sees "D-1 cause -> D+n effect". Pure, built from G.events only.
//
// CAUSES[prep] = [{ type, when?(details), text }]: event types that count as the cause of that payoff, for
//   every PREP id but 'judgement' (no single cause).
// ripples(G) -> [{ prep, label, cause: { day, minute, when, text }, effect: { day, minute, when, text }, gap }]
//   one row per prep id, for its FIRST payoff, using the latest cause event before it; sorted by gap
//   (minutes between cause and effect), largest first. Payoffs without a recorded cause are left out.
// causeOf(G, prep, beforeSeq = Infinity) -> { day, minute, when, text } | null (toast stamp: `(${when} · ${text})`).
// whenText(day, minute) -> 'D-1 오전' style label.

// ctx.payoff(prep, moment) passes a preparation id; the player sees these Korean names (debrief re-exports).
export const PREP = {
  bag: '비상가방', cash: '현금', docs: '비상연락처 카드·서류 사본', entrances: '대피소 출입구 확인', extinguisher: '소화기 위치·사용 순서',
  flashlight: '손전등 건전지', judgement: '화재 상황 판단', powerbank: '보조배터리 충전', radio: '라디오 주파수·건전지',
  route: '경로 확인', tub: '받아 둔 물', water_jug: '접이식 물통', curtain: '암막 커튼', towel: '젖은 수건', elder: '304호 어르신과 함께',
};

const has = (key) => (d) => (d?.cart?.[key] ?? 0) > 0;
export const CAUSES = {
  bag: [{ type: 'bag_packed', text: '비상가방을 꾸려 현관에 두었다' }],
  docs: [{ type: 'docs_done', text: '비상연락처 카드와 서류 사본을 정리했다' }],
  entrances: [{ type: 'shelter_checked', text: '대피소 출입구를 직접 둘러봤다' }],
  route: [{ type: 'shelter_checked', text: '대피소까지 가는 길을 미리 걸어 봤다' },
    { type: 'encounter', when: (d) => d.tool === 'route', text: '돌아가는 경로를 지도에 정해 두었다' }],
  radio: [{ type: 'check', when: (d) => d.id === 'radio', text: '라디오에 건전지를 넣어 두었다' }],
  flashlight: [{ type: 'check', when: (d) => d.id === 'flashlight', text: '손전등에 건전지를 넣어 두었다' }],
  powerbank: [{ type: 'check', when: (d) => d.id === 'powerbank', text: '보조배터리를 충전해 두었다' },
    { type: 'help', when: (d) => d.who === 'seoyeon' && /보조배터리/.test(d.what ?? ''), text: '서연과 보조배터리를 나눴다' }],
  tub: [{ type: 'tub_filled', text: '큰 통과 냄비에 물을 받아 두었다' }],
  water_jug: [{ type: 'buy', when: has('water_jug'), text: '접이식 물통을 사 두었다' }],
  cash: [{ type: 'cash', text: '현금을 찾아 두었다' }, { type: 'buy', when: has('cash'), text: '현금을 찾아 두었다' }],
  extinguisher: [{ type: 'encounter', when: (d) => d.tool === 'extinguisher' && d.result === 'practice', text: '복도 소화기 사용 순서를 익혀 두었다' }],
  curtain: [{ type: 'home_ready', text: '창문에 암막 커튼을 달아 두었다' }],
  elder: [{ type: 'help', when: (d) => d.who === 'elder', text: '304호 어르신과 함께 계단을 내려갔다' },
    { type: 'help', when: (d) => d.who === 'manager' && /304호/.test(d.what ?? ''), text: '박 소장에게 304호 어르신을 알렸다' }],
  towel: [{ type: 'tub_filled', text: '큰 통에 물을 받아 두었다' }],
};

const DAY = (d) => (d === 0 ? 'D-day' : d < 0 ? `D${d}` : `D+${d}`);
const PART = (m) => (m < 360 ? '새벽' : m < 720 ? '오전' : m < 1080 ? '오후' : m < 1260 ? '저녁' : '밤');
export const whenText = (day, minute) => `${DAY(day)} ${PART(minute)}`;
const abs = (e) => e.day * 1440 + e.minute;

function findCause(G, prep, beforeSeq) {
  const rules = CAUSES[prep];
  if (!rules) return null;
  for (let i = G.events.length - 1; i >= 0; i--) {
    const e = G.events[i];
    if (e.seq >= beforeSeq) continue;
    const rule = rules.find(r => r.type === e.type && (!r.when || r.when(e.details ?? {})));
    if (rule) return { e, text: rule.text };
  }
  return null;
}

export function causeOf(G, prep, beforeSeq = Infinity) {
  const c = findCause(G, prep, beforeSeq);
  return c ? { day: c.e.day, minute: c.e.minute, when: whenText(c.e.day, c.e.minute), text: c.text } : null;
}

export function ripples(G) {
  const first = new Map();
  for (const e of G.events) if (e.type === 'payoff' && e.details?.prep && !first.has(e.details.prep)) first.set(e.details.prep, e);
  const rows = [];
  for (const [prep, eff] of first) {
    const c = findCause(G, prep, eff.seq);
    if (!c) continue;
    rows.push({
      prep, label: PREP[prep] ?? '준비',
      cause: { day: c.e.day, minute: c.e.minute, when: whenText(c.e.day, c.e.minute), text: c.text },
      effect: { day: eff.day, minute: eff.minute, when: whenText(eff.day, eff.minute), text: eff.details.moment ?? '' },
      gap: abs(eff) - abs(c.e),
    });
  }
  return rows.sort((a, b) => b.gap - a.gap || (a.prep < b.prep ? -1 : 1));
}

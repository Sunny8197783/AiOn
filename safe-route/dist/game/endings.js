// Which of the 11 endings a run reaches, and why. The last-day path (G.know.path, set in story/evac.js) says how
// you left; on in-town paths the deed ledger can open a world ending. Deterministic, no G.karma. Pure: no DOM.
// The day ledger (game/ledger.js) adds how the rationed days went; stock is judged in litres, never in bottles.
import { DEEDS, CH, axes, deeds } from './deeds.js';
import { PATHS, LEAVE, TOWN, pathOf, peopleLines, townLines, letterOf } from './fates.js';
import { fact } from './facts.js';
import { fmtClock } from './state.js';
import { ledgerSummary } from './ledger.js';

export { PATHS, LEAVE, TOWN };
export const ORDER = ['commons', 'reopen', 'relief', 'board'];

// How you left: lines[0] of every ending on that path.
const OPENING = {
  bus: '버스는 남쪽의 임시 주거단지로 향했다. 좁은 컨테이너 방이었지만, 밤새 포성이 들리지 않았다.',
  missed: '버스는 이미 떠난 뒤였다. 다음 차량은 사흘 뒤라고 했다.', // missed can happen anywhere, not only at the assembly point
  stay: '박 소장과 함께 남았다. 빈 집들의 문을 단속하고, 남은 주민들과 물탱크를 나눠 썼다.',
  seoyeon: '서연의 지원 차량을 함께 기다렸다. 차량의 경사판(리프트)이 고장 나 둘이서 한참을 씨름한 끝에, 해 질 무렵 같은 차로 떠났다.',
  volunteer: '행정복지센터 자원봉사 명단에 이름을 적었다. 급수 줄을 정리하고, 거동이 불편한 집에 물을 날랐다.',
  walk: '버스를 기다리지 않고 걸어서 도시를 빠져나왔다. 통제선 밖 국도에는 같은 선택을 한 사람들이 길게 늘어서 있었다.',
};
const PATH_TRACE = {
  bus: '집결지에서 남쪽으로 가는 버스를 탔다.', walk: '버스를 기다리지 않고 걸어서 도시를 떠났다.', seoyeon: '서연과 함께 지원 차량을 기다렸다.',
  missed: '집결지에 늦어 버스를 놓쳤다.', stay: '박 소장과 함께 단지에 남았다.', volunteer: '행정복지센터 자원봉사에 남았다.',
};

const stockOf = (G, ids) => ids.reduce((s, id) => s + (G.items[id]?.home ?? 0) + (G.items[id]?.bag ?? 0), 0);
// Drinking water in litres (home + bag), as the day ledger counts it (story/common.js): never in bottles.
export const waterLitres = (G) => 2 * stockOf(G, ['water_2l']) + 0.5 * stockOf(G, ['water_500']);
export const STOCK_OK = { litres: 10, food: 4 }; // enough laid in to see out the days the taps were off
const stocked = (G) => waterLitres(G) >= STOCK_OK.litres && stockOf(G, ['rice_instant', 'canned', 'bars', 'ramen']) >= STOCK_OK.food;
const stockLine = (G) => (stocked(G) ? '미리 쌓아 둔 물과 식량 덕분에, 급수가 끊긴 날들을 버틸 수 있었다.' : '물과 먹을 것이 금세 바닥났다. 급수차가 오는 날만 손꼽아 기다렸다.');
// How the rationed days went; a body that gave out is never told it held out to the end.
function ledgerLine(G, path) {
  const led = ledgerSummary(G);
  if (!led.days) return null;
  const weak = (path === 'walk' || path === 'volunteer') && G.stats.health < 40;
  if (led.skipped) return weak ? '끼니를 거른 날도 있었다. 하루치를 나눠 쓴 것만으로는 몸이 버티지 못했다.' : '끼니를 거른 날도 있었지만, 하루치를 나눠 쓴 덕분에 마지막까지 버틸 수 있었다.';
  if (led.trips) return `급수소에 다녀온 날이 ${led.trips}일이었다. 하루치를 정해 두니 물이 언제 떨어질지 미리 알 수 있었다.`;
  return TOWN.has(path) && !stocked(G) ? '하루치를 정해 두니 언제 떨어질지 미리 알 수 있었다.' : '물과 먹을 것을 하루치씩 나눠 둔 덕분에, 바닥이 보이기 전에 다음 일을 정할 수 있었다.';
}

// need: [[axis, threshold]] for world endings; axes: what the trace scores on leave/base endings.
export const ENDINGS = [
  { no: 1, id: 'bus', kind: 'leave', title: '남쪽에서 온 편지', place: '남부 임시 주거단지 · D+14', need: [], axes: ['seoyeon', 'manager', 'clerk', 'elder'],
    hint: '집결지에서 버스를 타고 떠났다면.',
    lines: (G, a, has) => [
      "단지 입구 게시판에 '연무동 주민 우편함'이 생겼다는 소식을 박 소장의 문자로 들었다.",
      ['seoyeon', 'manager', 'clerk', 'elder'].some((k) => letterOf(k, a, has)) ? '우편함에 내 앞으로 온 짧은 편지들이 있었다.' : '우편함은 비어 있었다. 연무동에 연락처를 남겨 둔 사람이 없었다.',
      a.logistics >= 6 && '윤 팀장의 연락으로 단지 구호물자 창고에서 분류 일을 돕기 시작했다.',
      has('bus_helped') && '리프트 앞에서 짐을 들어 드린 어르신과 같은 동에 배정됐다. 아침마다 먼저 인사를 건네 오신다.',
    ] },
  { no: 2, id: 'walk', kind: 'leave', title: '국도의 끝', place: '국도 · 대구 방향 · D+14', need: [], axes: ['logistics', 'info'],
    hint: '버스를 기다리지 않고 걸어서 떠났다면.',
    lines: (G, a, has) => [
      has('driver_text') ? '국도에서 낯익은 화물차가 섰다. 문자를 받았던 김 기사님이었다. 남은 길은 조수석에서 갔다.'
        : G.injury?.includes('leg') ? '부러진 다리로 걷는 길은 끝이 없었다. 지나가던 구호 트럭이 태워 주지 않았다면 그 길 위에서 멈췄을 것이다.'
          : G.stats.health < 40 ? '몸이 버티지 못해 길가에 주저앉았다가 구호소에 실려 갔다. 멈춘 건 실패가 아니었다. 도움을 받을 수 있는 곳까지 간 것이었다.'
            : '사흘을 걸어 친척이 있는 남쪽 도시에 닿았다.',
      has('card_family') && '종이 카드에 적어 둔 번호로 대구 부모님께 전화를 걸었다. 휴대폰이 꺼져 있어도 번호는 남아 있었다.',
    ] },
  { no: 3, id: 'seoyeon', kind: 'leave', title: '같은 속도로', place: '연무 생활문화센터 · C 경사로 · D+14', need: [], axes: ['seoyeon'],
    hint: '서연의 지원 차량을 함께 기다렸다면.',
    lines: (G, a, has) => [
      'D+14, 서연과 함께 생활문화센터로 돌아왔다. 이번에는 대피하러 온 게 아니라 출입구를 점검하러 왔다.',
      has('stairs_asked') ? '서연이 만든 점검표 첫 줄에는 이렇게 적혀 있었다. "돕기 전에 먼저 물어볼 것."' : '경사로의 폭과 손잡이 높이를 하나씩 재어 적었다.',
    ] },
  { no: 4, id: 'missed', kind: 'base', title: '두 번째 버스', place: '103동 302호 · D+15', need: [], axes: ['manager', 'town'],
    hint: '집결지에 늦어 버스를 놓쳤다면.',
    lines: (G, a, has) => [
      a.manager >= 4 ? '사흘째 되던 날, 박 소장이 문을 두드렸다. "302호, 있어요? 다음 버스 명단에 올려 뒀어요."'
        : has('door_note') ? '남긴 쪽지를 보고 대피 담당 공무원이 찾아왔다. 다음 버스 명단에 이름이 올랐다.'
          : has('card_meet') ? '카드에 적어 둔 두 번째 만남의 장소, 행정복지센터에 가서 이름을 남겼다. 다음 버스 명단에 이름이 올랐다.'
          : '사흘 동안 아무도 문을 두드리지 않았다. 두 번째 버스는 하루 늦게 왔다.',
      stockLine(G),
    ] },
  { no: 5, id: 'stay', kind: 'base', title: '지킨 사람', place: '한빛아파트 관리사무소 · D+14', need: [], axes: ['manager', 'town'],
    hint: '박 소장과 함께 단지에 남았다면.', lines: (G) => [stockLine(G)] },
  { no: 6, id: 'volunteer', kind: 'base', title: '남은 손', place: '행정복지센터 비상급수시설 · D+14', need: [], axes: ['town', 'logistics'],
    hint: '행정복지센터 자원봉사에 남았다면.',
    lines: (G) => [G.stats.health < 40 && '다친 몸으로 무리한 뒤 한동안 앓아누웠지만, 그 일을 후회하지는 않았다.'] },
  { no: 7, id: 'commons', kind: 'world', title: '불 꺼진 단지의 공동 부엌', place: '관리사무소 앞 · D+14', need: [['manager', 6], ['elder', 4], ['town', 5]], axes: [],
    hint: '박 소장, 304호 어르신과 자주 얼굴을 마주하고 이웃 일을 함께 나눴다면.',
    lines: (G, a, has) => [
      '관리사무소 앞에 천막이 섰다. 남은 주민들이 쌀과 버너를 모아 공동 부엌을 꾸렸다.',
      '박 소장은 남은 집 명단 옆에 끼니 명부를 붙였다. 누가 먹었는지가 아니라, 누가 아직 못 왔는지를 보는 명부였다.',
      has('stock_to_manager') && "첫 상자에는 '302호'라고 적혀 있었다.",
    ] },
  { no: 8, id: 'reopen', kind: 'world', title: '다시 문을 연 가게', place: '바로편의점 · D+13', need: [['clerk', 5]], axes: [],
    hint: '수아와 한 약속을 지키고, 비어 있는 가게를 대신 돌봤다면.',
    lines: (G, a, has) => [
      'D+13, 바로편의점 셔터가 다시 올라갔다. 수아가 돌아왔다.',
      '계산대 옆에 나눔 선반이 생겼다. 필요한 만큼 가져가고, 남는 것은 두고 가는 선반이었다.',
      has('store_shuttered') ? '"셔터 내려 주신 거, 쪽지 보고 알았어요. 덕분에 가게가 그대로였어요." 수아가 말했다.'
        : has('store_iou') && '"쪽지 봤어요. 금액은 천천히 주셔도 돼요. 적어 두신 게 고마웠어요." 수아가 말했다.',
    ] },
  { no: 9, id: 'relief', kind: 'world', title: '물길을 잇는 사람', place: '한결물류 연무센터 · D+14', need: [['logistics', 6], ['town', 3]], axes: [],
    hint: '물류 일을 하던 손으로 윤 팀장과 이웃들의 일을 도왔다면.',
    lines: () => [
      'D+10, 윤 팀장에게서 다시 전화가 왔다. 분류 일손이 모자라니 나와 줄 수 있느냐고 했다.',
      '확인된 수량만 적고, 모르는 것은 모른다고 적는 일이었다.',
    ] },
  { no: 10, id: 'board', kind: 'world', title: '확인된 것만 적는 사람', place: '행정복지센터 게시판 · D+14', need: [['info', 8]], axes: [], facts: ['info_rumor', 'info_scope'],
    hint: '들은 말을 옮기기 전에 확인하고, 본 것만 정확히 전했다면.',
    lines: () => [
      '행정복지센터 앞 주민 게시판 정리를 자원봉사로 맡은 지 엿새째다.',
      '게시판을 세 칸으로 나눴다. 공식 안내, 현장에서 확인한 것, 확인 중.',
    ] },
  { no: 11, id: 'shut', kind: 'shadow', title: '닫힌 문', place: '103동 3층 복도 · D+14', need: [], axes: ['harm'],
    hint: '연무동에 남아, 이웃에게 먼저 문을 닫았다면.',
    lines: () => [
      'D+14. 복도의 문들은 대부분 닫혀 있다. 급수 소식은 쪽지로 돌았지만, 302호 문에는 붙지 않았다.',
      '누구도 탓하지 않았다. 다만 아무도 먼저 문을 두드리지 않았다.',
      '그래도 급수차는 매일 왔고, 줄에 서면 물을 받을 수 있었다.',
      '몇 주 뒤, 급수대 줄에서 먼저 인사를 건넨 건 정민이었다. 대답은 짧았지만, 대답은 있었다.',
    ] },
];
const BY_ID = Object.fromEntries(ENDINGS.map((e) => [e.id, e]));
const fitOf = (a) => (e) => Math.min(...e.need.map(([k, t]) => a[k] / t));

function choose(G, a, path) {
  if (TOWN.has(path) && a.harm >= 4) return 'shut';
  if (!TOWN.has(path)) return path;
  const fit = fitOf(a);
  const best = ENDINGS.filter((e) => e.kind === 'world').map((e) => [e, fit(e)]).filter(([, f]) => f >= 1)
    .sort((x, y) => y[1] - x[1] || ORDER.indexOf(x[0].id) - ORDER.indexOf(y[0].id))[0];
  return best ? best[0].id : path;
}

const whenOf = (e) => `${CH[e.chapter] ?? CH.evac} · ${fmtClock({ day: e.day ?? 12, minute: e.minute ?? 0 })}`;

// 3–5 deeds that led here (fewer only when the run holds fewer), shown in the order they happened.
export function trace(G, id) {
  const end = BY_ID[id] ?? BY_ID.bus, path = pathOf(G), pe = G.events.findLast((e) => e.type === 'path');
  // The path item stands for the last-day choice, so a deed read from that same event (stayed, …) is left out.
  const ds = deeds(G).filter((d) => id === 'shut' || d.seq !== pe?.seq);
  const score = (d) => {
    const w = d.id === 'violence' ? { harm: d.n + 2 * d.ko } : DEEDS[d.id].w;
    if (end.kind === 'world') return end.need.reduce((s, [k, t]) => s + Math.max(0, w[k] ?? 0) / t, 0);
    return end.axes.reduce((s, k) => s + Math.max(0, w[k] ?? 0), 0);
  };
  const items = [];
  if (id !== 'shut') items.push({ id: 'path', seq: pe?.seq ?? Infinity, text: PATH_TRACE[path], when: pe ? whenOf(pe) : CH.evac, sourceIds: [] });
  const picked = ds.map((d) => [d, score(d)]).filter(([, s]) => s > 0).sort((x, y) => y[1] - x[1] || x[0].seq - y[0].seq).map(([d]) => d);
  const weighted = ds.filter((d) => Object.keys(DEEDS[d.id].w).length && !picked.includes(d)).reverse(); // most recent first
  for (const d of [...picked, ...weighted]) {
    if (items.length >= 5 || (items.length >= 3 && !picked.includes(d))) break;
    items.push({ id: d.id, seq: d.seq, text: DEEDS[d.id].line, when: whenOf(d), sourceIds: [fact(DEEDS[d.id].facts[0])?.sourceId].filter(Boolean) }); // the source of the one fact shown
  }
  return items.sort((x, y) => x.seq - y.seq).map(({ seq, ...t }) => t);
}

// World endings this run did not reach, closest first, with the first undone deed on the weakest axis.
export function nearMisses(G, id) {
  const a = axes(G, { final: true }), fit = fitOf(a), done = new Set(deeds(G).map((d) => d.id));
  const pre = LEAVE.has(pathOf(G)) ? '연무동에 남았다면 — ' : '';
  return ENDINGS.filter((e) => e.kind === 'world' && e.id !== id).map((e) => [e, fit(e)])
    .sort((x, y) => y[1] - x[1] || ORDER.indexOf(x[0].id) - ORDER.indexOf(y[0].id)).slice(0, 2)
    .map(([e]) => {
      const [weak] = [...e.need].sort((x, y) => a[x[0]] / x[1] - a[y[0]] / y[1])[0];
      const tip = Object.entries(DEEDS).find(([did, d]) => (d.w[weak] ?? 0) > 0 && !done.has(did));
      return { id: e.id, title: e.title, hint: pre + e.hint, tip: tip ? `${CH[tip[1].ch]} — ${tip[1].short}` : '' };
    });
}

export function pickEnding(G) {
  const path = pathOf(G), a = axes(G, { final: true });
  const done = new Set(deeds(G).map((d) => d.id)), has = (id) => done.has(id);
  const id = choose(G, a, path), e = BY_ID[id];
  const lines = [OPENING[path], ...e.lines(G, a, has), ledgerLine(G, path)].filter(Boolean);
  // The leg is told once: the walk's own line may already have said it.
  if (G.injury?.includes('leg') && !lines.some((l) => l.includes('부러진 다리'))) lines.push('부러진 다리는 제대로 붙지 않았다. 지금도 비가 오면 욱신거린다.');
  if (a.harm >= 2 && id !== 'shut') lines.push('몇몇 이웃은 끝내 눈을 마주치지 않았다.'); // on town paths harm 4+ is already 'shut'
  return {
    id, no: e.no, title: e.title, place: e.place, kind: e.kind, path, lines,
    people: peopleLines(G, id), town: townLines(G, id),
    trace: trace(G, id), missed: nearMisses(G, id), sourceIds: [...new Set((e.facts ?? []).map((f) => fact(f).sourceId))],
  };
}

const DAY = /^\d{4}-\d{2}-\d{2}$/;

// Sanitises the stored gallery { seen: { [endingId]: { at, runs, trace } } }; anything unexpected is dropped.
export function readGallery(raw) {
  let g = raw;
  if (typeof raw === 'string') try { g = JSON.parse(raw); } catch { g = null; }
  const seen = {};
  const src = g && typeof g === 'object' && g.seen && typeof g.seen === 'object' && !Array.isArray(g.seen) ? g.seen : {};
  for (const [id, v] of Object.entries(src)) {
    if (!BY_ID[id] || !v || typeof v !== 'object' || typeof v.at !== 'string' || !DAY.test(v.at)) continue;
    seen[id] = { at: v.at, runs: Number.isInteger(v.runs) && v.runs >= 1 ? v.runs : 1,
      trace: Array.isArray(v.trace) ? v.trace.filter((t) => typeof t === 'string' && DEEDS[t]).slice(0, 5) : [] };
  }
  return { seen };
}

export function addToGallery(gal, id, traceIds, at) {
  const out = readGallery(gal);
  if (!BY_ID[id] || !DAY.test(at)) return out;
  const old = out.seen[id];
  out.seen[id] = { at: old?.at ?? at, runs: (old?.runs ?? 0) + 1, trace: (traceIds ?? []).filter((t) => DEEDS[t]).slice(0, 5) };
  return out;
}

export function galleryView(gal) {
  const { seen } = readGallery(gal);
  return ENDINGS.map((e) => {
    const s = seen[e.id];
    return { no: e.no, id: e.id, seen: !!s, title: s ? e.title : null, place: s ? e.place : null, at: s?.at ?? null,
      shorts: s ? s.trace.slice(0, 3).map((t) => DEEDS[t].short) : [], hint: e.hint };
  });
}

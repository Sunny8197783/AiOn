// Which ending a run reaches. The path is the last choice (G.know.path, set in story/evac.js); the body,
// the stock at home, who you helped and whom you hurt colour it. Pure: no DOM, no three.js.
// pickEnding(G) -> { id, title, lines }: the opening line plus at most MAX_EXTRA lines, most telling first.
import { ledgerSummary } from './ledger.js';

export const MAX_EXTRA = 5;

const PATHS = {
  bus: ['떠난 사람', '버스는 남쪽의 임시 주거단지로 향했다. 좁은 컨테이너 방이었지만, 밤새 포성이 들리지 않았다.'],
  missed: ['남겨진 사람', '버스는 이미 떠난 뒤였다. 다음 차량은 사흘 뒤라고 했다.'],
  stay: ['지킨 사람', '박 소장과 함께 남았다. 빈 집들의 문을 단속하고, 남은 주민들과 물탱크를 나눠 썼다.'],
  seoyeon: ['함께 떠난 사람', '서연의 지원 차량을 함께 기다렸다. 차량의 경사판(리프트)이 고장 나 둘이서 한참을 씨름한 끝에, 해 질 무렵 같은 차로 떠났다.'],
  volunteer: ['남은 손', '행정복지센터 자원봉사 명단에 이름을 적었다. 급수 줄을 정리하고, 거동이 불편한 집에 물을 날랐다.'],
  walk: ['스스로 떠난 사람', '버스를 기다리지 않고 걸어서 도시를 빠져나왔다. 통제선 밖 국도에는 같은 선택을 한 사람들이 길게 늘어서 있었다.'],
};

const stockOf = (G, ids) => ids.reduce((s, id) => s + (G.items[id]?.home ?? 0) + (G.items[id]?.bag ?? 0), 0);
// Drinking water in litres (home + bag), as the day ledger counts it (story/common.js): never in bottles.
export const waterLitres = (G) => 2 * stockOf(G, ['water_2l']) + 0.5 * stockOf(G, ['water_500']);
export const STOCK_OK = { litres: 10, food: 4 }; // enough laid in to see out the days the taps were off

export function pickEnding(G) {
  const path = PATHS[G.know?.path] ? G.know.path : 'bus';
  const [title, opening] = PATHS[path];
  const lines = [];
  const water = waterLitres(G), food = stockOf(G, ['rice_instant', 'canned', 'bars', 'ramen']);
  const has = (type, pred = () => true) => G.events.some((e) => e.type === type && pred(e.details || {}));
  const helped = (who) => has('help', (d) => d.who === who);
  // Different people helped, not presses: the same help recorded twice counts once. Kindness 정민 received
  // (by: 'other', e.g. the clerk's note, 서연's powerbank) is not 정민's help.
  const helps = new Set(G.events.filter((e) => e.type === 'help' && e.details?.by !== 'other').map((e) => e.details?.who)).size;
  const led = ledgerSummary(G);
  const stocked = water >= STOCK_OK.litres && food >= STOCK_OK.food;
  const ranOut = (path === 'stay' || path === 'missed') && !stocked;
  const weak = (path === 'walk' || path === 'volunteer') && G.stats.health < 40; // the body gave out: no "버틸 수 있었다" after it
  if (path === 'stay' || path === 'missed') {
    lines.push(stocked ? '미리 쌓아 둔 물과 식량 덕분에, 급수가 끊긴 날들을 버틸 수 있었다.'
      : '물과 먹을 것이 금세 바닥났다. 급수차가 오는 날만 손꼽아 기다렸다.');
    if (helps >= 2) lines.push('서로 얼굴을 아는 이웃들이 공동 부엌을 꾸렸다. 혼자였다면 버티지 못했을 것이다.');
  }
  if (path === 'walk') lines.push(G.injury?.includes('leg') ? '부러진 다리로 걷는 길은 끝이 없었다. 지나가던 구호 트럭이 태워 주지 않았다면 그 길 위에서 멈췄을 것이다.'
    : G.stats.health < 40 ? '몸이 버티지 못해 길가에 주저앉았다가 구호소에 실려 갔다.' : '사흘을 걸어 친척이 있는 남쪽 도시에 닿았다.');
  if (path === 'volunteer' && G.stats.health < 40) lines.push('다친 몸으로 무리한 뒤 한동안 앓아누웠지만, 그 일을 후회하지는 않았다.');
  if (has('looter', (d) => d.result === 'gave')) lines.push('먹을 것을 나눠 준 그 사람을 몇 주 뒤 배급소에서 다시 만났다. 그는 고개를 숙여 인사했다.');
  if (has('looter', (d) => d.result === 'fought')) lines.push('골목에서 주먹을 휘둘렀던 날의 기억은 오래 꿈에 나왔다.');
  if ((G.karma ?? 0) <= -3) lines.push('이유 없이 사람을 때렸다는 소문이 단지에 돌았다. 몇몇 이웃은 끝내 눈을 마주치지 않았다.');
  if (G.injury?.includes('leg') && path !== 'walk') lines.push('부러진 다리는 제대로 붙지 않았다. 지금도 비가 오면 욱신거린다.');
  if (helped('seoyeon') && path !== 'seoyeon') lines.push('서연에게서 짧은 문자가 왔다. "덕분에 무사히 도착했어요."');
  if (helped('elder') || has('help', (d) => d.who === 'manager' && /304호/.test(d.what ?? ''))) {
    lines.push(['bus', 'walk', 'seoyeon'].includes(path) ? '304호 어르신이 박 소장 편에 안부를 전해 왔다. "그날 정말 고마웠어요."' // 정민 has left 연무동
      : '304호 어르신은 그 뒤로 정민을 볼 때마다 손을 꼭 잡았다.');
  }
  if (helped('stranger')) lines.push('그날 밤 문 너머로 물을 청하던 사람을, 며칠 뒤 조끼를 입은 자원봉사자로 다시 만났다.');
  if (led.days) lines.push(led.skipped ? (weak ? '끼니를 거른 날도 있었다. 하루치를 나눠 쓴 것만으로는 몸이 버티지 못했다.' : '끼니를 거른 날도 있었지만, 하루치를 나눠 쓴 덕분에 마지막까지 버틸 수 있었다.')
    : led.trips ? `급수소에 다녀온 날이 ${led.trips}일이었다. 하루치를 정해 두니 물이 언제 떨어질지 미리 알 수 있었다.`
      : ranOut ? '하루치를 정해 두니 언제 떨어질지 미리 알 수 있었다.'
        : '물과 먹을 것을 하루치씩 나눠 둔 덕분에, 바닥이 보이기 전에 다음 일을 정할 수 있었다.');
  return { id: path, title, lines: [opening, ...lines.slice(0, MAX_EXTRA)] };
}

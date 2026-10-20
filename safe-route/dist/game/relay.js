// The message 정민 sends 서연 from the shelter, and why it cannot be applied to her as-is.
// Pure logic (node-tested). Times come from G.relay, set by the D-day chapter from the real clock.
import { recordEvent } from './state.js';

export const ENTRANCES = {
  stairs: { label: '출입구 A · 계단', short: 'A 계단', seoyeonOk: false, card: '계단은 혼자 내려가지 않음' },
  ramp: { label: '출입구 B · 지하주차장 진출입로', short: 'B 진출입로', seoyeonOk: false, card: '가파른 내리막은 혼자 내려가지 않음' },
  access: { label: '출입구 C · 경사로', short: 'C 경사로', seoyeonOk: true },
};
export const MAX_NOTE = 120;
export const hhmm = (minute) => `${String(Math.floor(minute / 60) % 24).padStart(2, '0')}:${String(minute % 60).padStart(2, '0')}`;

// C opens after the message is sent and before 서연 reads it, so the message can never mention it from sight.
export function relayTimes(sentMinute, gateMinute = sentMinute - 3) {
  const sent = Math.max(sentMinute, gateMinute);
  const open = Math.max(20 * 60 + 16, sent + 3);
  return { gateSeenAt: hhmm(gateMinute), sentAt: hhmm(sent), accessOpenAt: hhmm(open), receivedAt: hhmm(open + 1) };
}

// What 정민 actually did, which the message may or may not say.
export function noteContext(G) {
  const events = G?.events || [];
  const entered = events.filter(e => e.type === 'route_attempt' && e.details.who === 'jeongmin' && (e.details.route === 'stairs' || e.details.route === 'ramp')).at(-1);
  const gate = events.find(e => e.type === 'gate_observed' && e.details.who === 'jeongmin');
  return { route: entered?.details.route || 'stairs', observedVia: gate?.details.via === 'notice_after' ? 'notice_after' : 'sign' };
}

const ENTERED = { stairs: '나는 계단(A)으로 들어왔어.', ramp: '나는 지하주차장 진출입로(B)로 들어왔어.' };

export function composeNote(chips = {}, { route = 'stairs', observedVia = 'sign', gateSeenAt = '20:09' } = {}) {
  const when = chips.time ? `${gateSeenAt}에 ` : '';
  const parts = [];
  if (chips.what === 'gate_closed') parts.push(observedVia === 'notice_after' ? `${when}도착해서 안내문을 보니 동쪽 출입구는 폐쇄래.` : `${when}동쪽 출입구가 닫혀 있었어.`);
  if (chips.what === 'all_closed') parts.push(`${when}대피소가 닫혀 있었어.`);
  if (chips.route) parts.push(ENTERED[route] || ENTERED.stairs);
  if (chips.unknowns) parts.push('다른 입구 상태는 확인 못 했어.');
  if (chips.advice === 'follow_me') parts.push('나처럼 오면 돼.');
  if (chips.advice === 'direction') parts.push(route === 'ramp' ? '연무로 따라 오다가 문화로에서 왼쪽으로 꺾어.' : '연무로 따라 서쪽으로 쭉 오면 왼쪽이야.');
  if (chips.advice === 'check_notice') parts.push('현장 안내를 꼭 다시 확인해.');
  return parts.join(' ');
}

// '막' alone is often the adverb "just", so only its closed forms count.
const SHUT = '폐쇄|닫|막[혀힌힘혔아았]';
const UNKNOWN = /모르|몰라|몰랐|미확인|확인\s*(못|안)|확인하지/;
const RULES = {
  what: [['all_closed', new RegExp(`(전체|모두|전부|다)\\s*(${SHUT})|대피(소|시설)[^.,]{0,8}(${SHUT})`)], ['gate_closed', new RegExp(`(동쪽|동문)[^.,]{0,8}(${SHUT}|통제)`)]],
  route: [['stairs', /계단|\bA\b/i], ['ramp', /진출입로|\bB\b/i], ['access', /\bC\b|경사로/i]],
  advice: [['follow_me', /나처럼|따라\s*와|똑같이|같은\s*길|여기로/], ['direction', /오른쪽|왼쪽|쭉|건너|꺾/], ['check_notice', /안내[^.]{0,8}확인|확인[^.]{0,8}안내/]],
};

// Rule-based reading of the message. Each quote is a whole clause, so it is always an exact substring.
export function parseNote(text) {
  const clauses = String(text ?? '').split(/[.!?\n]/).map(c => c.trim()).filter(Boolean);
  const claims = { what: null, time: null, route: null, unknowns: false, advice: null, quotes: {} };
  for (const field of Object.keys(RULES)) {
    for (const [value, pattern] of RULES[field]) {
      const clause = clauses.find(c => pattern.test(c)
        && !(value === 'all_closed' && (/동쪽|동문/.test(c) || UNKNOWN.test(c)))
        && !(field === 'route' && /말고|빼고|피해|안\s*돼|쓰지\s*마|가지\s*마/.test(c)));
      if (clause) { claims[field] = value; claims.quotes[field] = clause; break; }
    }
  }
  for (const clause of clauses) {
    const m = clause.match(/\b([01]?\d|2[0-3]):[0-5]\d\b/) || clause.match(/\d{1,2}\s*시(?!간)(\s*\d{1,2}\s*분)?/);
    if (m) { claims.time = m[0]; claims.quotes.time = clause; break; }
  }
  const unknown = clauses.find(c => UNKNOWN.test(c));
  if (unknown) { claims.unknowns = true; claims.quotes.unknowns = unknown; }
  return claims;
}

export function noteKind(claims) {
  if (claims.what === 'all_closed') return 'generalized';
  if (claims.what === 'gate_closed' && claims.time && claims.unknowns) return 'precise';
  return claims.what || claims.time || claims.route || claims.unknowns || claims.advice ? 'partial' : 'unparsed';
}

export function makeHandover(G, { chips = {}, text, edited = false } = {}) {
  if (!G || !Array.isArray(G.events)) throw new TypeError('올바른 게임 기록이 필요합니다.');
  const times = G.relay || relayTimes(20 * 60 + 13);
  const c = { ...noteContext(G), gateSeenAt: times.gateSeenAt };
  const composed = composeNote(chips, c);
  // Player text is untrusted: plain string, no control characters, bounded length.
  const cleaned = edited ? String(text ?? '').replace(/[\u0000-\u001f\u007f]/g, ' ').trim().slice(0, MAX_NOTE) : '';
  const finalText = cleaned || composed;
  const claims = { ...parseNote(finalText), parsedBy: cleaned ? 'rules' : 'chips' };
  const handover = { text: finalText, chips: structuredClone(chips), edited: Boolean(cleaned), claims, kind: noteKind(claims), actualRoute: c.route, observedVia: c.observedVia, ...times };
  recordEvent(G, 'handover_written', { kind: handover.kind, edited: handover.edited, length: finalText.length });
  G.handover = handover;
  return handover;
}

const gateKnown = h => (h?.observedVia === 'notice_after' ? '도착 후 안내문으로 안 동쪽 출입구 한 곳' : `직접 본 동쪽 출입구 한 곳 (${h?.gateSeenAt ?? ''})`);

// Three reasons the message cannot be applied as-is: body, time, and one more (scope/advice/place).
export function buildPins(G) {
  const h = G?.handover;
  const claims = h?.claims || parseNote('');
  const actual = h?.actualRoute || noteContext(G).route;
  const t = h || G?.relay || relayTimes(20 * 60 + 13);
  const named = claims.route === 'stairs' || claims.route === 'ramp' ? claims.route : null;
  const bodyAt = named || actual;
  const other = bodyAt === 'stairs' ? 'ramp' : 'stairs';
  const body = { factor: 'body', anchor: bodyAt,
    was: named ? `“${claims.quotes.route}”` : `메시지에 입구 없음 · 정민이 들어간 입구: ${ENTRANCES[actual].short}`,
    now: `서연의 카드: ${ENTRANCES[bodyAt].card}`,
    also: { anchor: other, now: `✕ ${ENTRANCES[other].short} · ${ENTRANCES[other].card}` } };
  const time = claims.route === 'access'
    ? { factor: 'time', anchor: 'access', was: `“${claims.quotes.route}”`, now: `${t.sentAt}의 C · 지금 상태는 안내판에서` }
    : { factor: 'time', anchor: 'notice', was: `메시지: ${claims.time ?? '시각 없음'} · 보냄 ${t.sentAt}`, now: `지금 ${t.receivedAt} · 안내판이 ${t.accessOpenAt}에 바뀜 — 메시지 뒤의 일` };
  let third;
  if (claims.what === 'all_closed') third = { factor: 'scope', anchor: 'gate', was: `“${claims.quotes.what}”`, now: `정민이 확인한 것: ${gateKnown(h)}` };
  else if (claims.advice === 'follow_me') third = { factor: 'advice', anchor: 'start1', was: `“${claims.quotes.advice}”`, now: '정민의 다리, 정민의 출발점 기준의 길' };
  else if (claims.advice === 'direction') third = { factor: 'direction', anchor: 'start1', was: `“${claims.quotes.advice}”`, now: '정민의 출발점(동쪽 아파트) 기준 방향 · 서연은 서쪽 카페에서 출발', line: 'start0' };
  else third = { factor: 'place', anchor: 'start1', was: '정민: 동쪽 한빛아파트에서 출발', now: '서연: 서쪽 카페 쉼표에서 출발', line: 'start0' };
  return [body, time, third];
}

export function closingLines(kind) {
  if (kind === 'generalized') return ['정민이 본 것보다 넓은 말이었습니다. 그리고 정민의 시각, 정민의 몸, 정민의 자리에서 쓴 말이었습니다.', '서연은 지금의 안내로 다시 판단합니다.'];
  if (kind === 'unparsed') return ['서연에게 닿은 것은 이 문장뿐이었습니다.', '서연은 지금의 안내에서 다시 시작합니다.'];
  return ['정민의 시각, 정민의 몸, 정민의 자리에서 쓴 말이었습니다.', '서연은 지금의 안내로 다시 판단합니다.'];
}

// How each part of the message read from 서연's side. status: helped | recheck | mismatch | missing.
export function readClaims(G) {
  const h = G?.handover;
  const c = h?.claims;
  if (!c) return [];
  const rows = [];
  const add = (label, quote, status, why, sourceId) => rows.push({ label, quote: quote ?? null, status, why, sourceId });
  if (c.what === 'gate_closed') add('본 것', c.quotes.what, c.time ? 'helped' : 'recheck', c.time ? '본 범위와 시각이 함께 있어 지금 안내와 비교할 수 있었습니다.' : '언제 본 것인지 없어 지금도 그런지 판단하기 어려웠습니다.', c.time ? 'press_code' : 'sms_list');
  if (c.what === 'all_closed') add('본 것', c.quotes.what, 'mismatch', `정민이 확인한 것은 ${gateKnown(h)}뿐이었습니다.`, 'press_code');
  if (c.route === 'stairs' || c.route === 'ramp') add('들어간 길', c.quotes.route, 'mismatch', '서연의 카드 기준과 다른 입구였습니다.', 'koddi_card');
  if (c.unknowns) add('모르는 것', c.quotes.unknowns, 'helped', '모르는 부분을 밝혀 서연이 직접 확인할 곳을 알 수 있었습니다.', 'press_code');
  if (c.advice === 'follow_me' || c.advice === 'direction') add('조언', c.quotes.advice, 'mismatch', '쓴 사람의 다리와 출발점 기준의 말이었습니다.', 'sms_area');
  if (c.advice === 'check_notice') add('조언', c.quotes.advice, 'helped', '지금의 현장 안내를 다시 보게 했습니다.', 'ps_airraid');
  if (!c.time) add('시각', null, 'missing', '언제의 정보인지가 없었습니다.', 'sms_list');
  if (!c.unknowns) add('모르는 것', null, 'missing', '확인하지 못한 부분이 적혀 있지 않았습니다.', 'press_code');
  add('C 경사로', c.route === 'access' ? c.quotes.route : null, c.route === 'access' ? 'recheck' : 'missing', `${h.accessOpenAt}에 열렸습니다 — 메시지를 보낸 뒤의 일이라 지금의 안내에서만 알 수 있었습니다.`, 'ps_airraid');
  return rows;
}

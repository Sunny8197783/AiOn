// Every source below was re-checked on 2026-10-19; the game only states what these pages say.
export const SOURCES = [
  { id: 'korea', title: '국민안전24 · 비상대비 행동요령', url: 'https://safekorea.go.kr/safekorea-kor/acts/nacts/action-guide.do?category=stateOfEmergency&actsHeaderTitle=%EB%B9%84%EC%83%81%EB%8C%80%EB%B9%84&menuSn=4' },
  { id: 'korea_alert', title: '국민안전24 · 민방공 경보 행동요령', url: 'https://safekorea.go.kr/safekorea-kor/acts/nacts/action-guide.do?category=civlAirDefenseWarning&actsHeaderTitle=%EB%AF%BC%EB%B0%A9%EA%B3%B5%EA%B2%BD%EB%B3%B4&menuSn=4' },
  { id: 'sms_area', title: '재난문자방송 기준 및 운영규정 (행정안전부)', url: 'https://www.law.go.kr/LSW/admRulInfoP.do?admRulSeq=2100000274384&chrClsCd=010201' },
  { id: 'press_code', title: '재난보도준칙 (한국기자협회)', url: 'https://www.journalist.or.kr/news/section4.html?p_num=10' },
  { id: 'seoul_2023', title: '서울신문 · 2023년 5월 31일 경계경보 문자', url: 'https://m.seoul.co.kr/news/2023/06/01/20230601002003' },
  { id: 'information', title: 'ICRC · 전시 유해 정보 Q&A', url: 'https://www.icrc.org/en/article/harmful-information-questions-and-answers' },
  { id: 'shelters', title: '대한민국 정책브리핑 · 민방위 대피소 찾기', url: 'https://www.korea.kr/news/policyNewsView.do?newsId=148970287' },
  { id: 'koddi_card', title: '한국장애인개발원 · 재난 대비 카드', url: 'https://www.koddi.or.kr/webzine/didimdol/vol304/page01-03.html' },
  { id: 'etiquette', title: '금천구 · 장애인 에티켓', url: 'https://www.geumcheon.go.kr/portal/contents.do?key=787' },
  { id: 'unhcr_disability', title: 'UNHCR · 장애인 보호 지침', url: 'https://emergency.unhcr.org/protection/persons-risk/persons-disabilities' },
];

// Scenario clock. C opens after 준 sends his note, so 서연's entrance can never be in it.
export const STORY = { alertAt: '14:08', gateSeenAt: '14:10', sentAt: '14:12', accessOpenAt: '14:15', receivedAt: '14:16', arrivedAt: '14:19' };

export const ENTRANCES = {
  stairs: { label: '출입구 A · 계단', short: 'A 계단', chapters: [0, 1], seoyeonOk: false, card: '계단은 혼자 내려가지 않음' },
  ramp: { label: '출입구 B · 지하주차장 진출입로', short: 'B 진출입로', chapters: [0, 1], seoyeonOk: false, card: '가파른 내리막은 혼자 내려가지 않음' },
  access: { label: '출입구 C · 경사로', short: 'C 경사로', chapters: [1], seoyeonOk: true, openedAt: STORY.accessOpenAt },
};

export const SEOYEON_CARD = ['보행 보조기를 씁니다', '평지와 완만한 경사로는 혼자 갑니다', '계단과 가파른 내리막은 혼자 내려가지 않습니다', '보조기와 떨어지지 않습니다', '도움이 필요하면 제가 말합니다'];

export const MAX_NOTE = 120;

export function createRun() {
  return { version: 2, startedAt: new Date().toISOString(), chapter: 0, events: [], handover: null };
}

export function recordEvent(run, type, details = {}) {
  if (!run || !Array.isArray(run.events) || typeof type !== 'string' || !type.trim()) {
    throw new TypeError('올바른 실행 기록과 이벤트 이름이 필요합니다.');
  }
  if (!details || typeof details !== 'object' || Array.isArray(details)) {
    throw new TypeError('이벤트 세부 정보는 객체여야 합니다.');
  }
  const event = { seq: run.events.length + 1, at: new Date().toISOString(), type, details: structuredClone(details) };
  run.events.push(event);
  return event;
}

// What 준 actually did, which the note may or may not say.
export function noteContext(run) {
  const events = run?.events || [];
  const entered = events.filter(e => e.type === 'route_attempt' && e.details.chapter === 0 && ENTRANCES[e.details.route]?.chapters.includes(0)).at(-1);
  const gate = events.find(e => e.type === 'gate_observed' && e.details.chapter === 0);
  return { route: entered?.details.route || 'stairs', observedVia: gate?.details.via === 'notice_after' ? 'notice_after' : 'sign' };
}

const ENTERED = { stairs: '나는 계단(A)으로 들어왔어.', ramp: '나는 지하주차장 진출입로(B)로 들어왔어.' };

export function composeNote(chips = {}, { route = 'stairs', observedVia = 'sign' } = {}) {
  const when = chips.time ? `${STORY.gateSeenAt}에 ` : '';
  const parts = [];
  if (chips.what === 'gate_closed') parts.push(observedVia === 'notice_after' ? `${when}도착해서 안내문을 보니 동쪽 출입구는 폐쇄래.` : `${when}동쪽 출입구가 닫혀 있었어.`);
  if (chips.what === 'all_closed') parts.push(`${when}대피소가 닫혀 있었어.`);
  if (chips.route) parts.push(ENTERED[route] || ENTERED.stairs);
  if (chips.unknowns) parts.push('다른 입구 상태는 확인 못 했어.');
  if (chips.advice === 'follow_me') parts.push('나처럼 오면 돼.');
  if (chips.advice === 'direction') parts.push(`큰길 따라 쭉 오다가 ${route === 'ramp' ? '오른쪽' : '왼쪽'}이야.`);
  if (chips.advice === 'check_notice') parts.push('현장 안내를 꼭 다시 확인해.');
  return parts.join(' ');
}

const RULES = {
  what: [['all_closed', /(전체|모두|전부|다)\s*(폐쇄|닫|막)|대피소[^.]{0,8}(폐쇄|닫|막)/], ['gate_closed', /(동쪽|동문)[^.]{0,8}(폐쇄|닫|막|통제)/]],
  route: [['stairs', /계단|\bA\b/i], ['ramp', /진출입로|주차장|\bB\b/i], ['access', /\bC\b|경사로/i]],
  advice: [['follow_me', /나처럼|따라\s*와|똑같이|같은\s*길|여기로/], ['direction', /오른쪽|왼쪽|쭉|건너/], ['check_notice', /안내[^.]{0,8}확인|확인[^.]{0,8}안내/]],
};

// Rule-based reading of the note. Each quote is the whole clause, so it is always an exact substring.
export function parseNote(text) {
  const clauses = String(text ?? '').split(/[.!?\n]/).map(c => c.trim()).filter(Boolean);
  const claims = { what: null, time: null, route: null, unknowns: false, advice: null, quotes: {} };
  for (const field of Object.keys(RULES)) {
    for (const [value, pattern] of RULES[field]) {
      const clause = clauses.find(c => pattern.test(c));
      if (clause) { claims[field] = value; claims.quotes[field] = clause; break; }
    }
  }
  for (const clause of clauses) {
    const m = clause.match(/\b([01]?\d|2[0-3]):([0-5]\d)\b/) || clause.match(/(\d{1,2})\s*시\s*(\d{1,2})\s*분/);
    if (m) { claims.time = `${m[1].padStart(2, '0')}:${m[2].padStart(2, '0')}`; claims.quotes.time = clause; break; }
  }
  const unknown = clauses.find(c => /모르|몰라|몰랐|미확인|확인\s*(못|안)|확인하지/.test(c));
  if (unknown) { claims.unknowns = true; claims.quotes.unknowns = unknown; }
  return claims;
}

export function noteKind(claims) {
  if (claims.what === 'all_closed') return 'generalized';
  if (claims.what === 'gate_closed' && claims.time && claims.unknowns) return 'precise';
  return claims.what || claims.time || claims.route || claims.unknowns || claims.advice ? 'partial' : 'unparsed';
}

export function makeHandover(run, { chips = {}, text, edited = false } = {}) {
  if (!run || !Array.isArray(run.events)) throw new TypeError('올바른 실행 기록이 필요합니다.');
  const ctx = noteContext(run);
  const composed = composeNote(chips, ctx);
  // Player text is untrusted: plain string, no control characters, bounded length.
  const cleaned = edited ? String(text ?? '').replace(/[\u0000-\u001f\u007f]/g, ' ').trim().slice(0, MAX_NOTE) : '';
  const finalText = cleaned || composed;
  const claims = { ...parseNote(finalText), parsedBy: cleaned ? 'rules' : 'chips' };
  const handover = {
    version: 2, text: finalText, chips: structuredClone(chips), edited: Boolean(cleaned), claims,
    kind: noteKind(claims), actualRoute: ctx.route, observedVia: ctx.observedVia, sentAt: STORY.sentAt, receivedAt: STORY.receivedAt,
  };
  recordEvent(run, 'handover_written', { kind: handover.kind, edited: handover.edited, length: finalText.length });
  run.handover = handover;
  return handover;
}

// What 준 actually knew about the gate, so no pin claims he saw more than he did.
const gateKnown = h => (h?.observedVia === 'notice_after' ? '도착 후 안내문으로 안 동쪽 출입구 한 곳' : `직접 본 동쪽 출입구 한 곳 (${STORY.gateSeenAt})`);

// The three reasons the note cannot be applied as-is. Text comes from templates plus the player's own quotes.
export function buildPins(run) {
  const h = run?.handover;
  const claims = h?.claims || parseNote('');
  const actual = h?.actualRoute || noteContext(run).route;
  const named = claims.route === 'stairs' || claims.route === 'ramp' ? claims.route : null;
  const bodyAt = named || actual;
  const other = bodyAt === 'stairs' ? 'ramp' : 'stairs';
  const body = {
    factor: 'body', anchor: bodyAt,
    was: named ? `“${claims.quotes.route}”` : `기록에 들어간 입구 없음 · 준은 ${ENTRANCES[actual].short}로 들어감`,
    now: `서연의 카드: ${ENTRANCES[bodyAt].card}`,
    also: { anchor: other, now: `✕ ${ENTRANCES[other].short} · ${ENTRANCES[other].card}` },
  };
  const time = claims.route === 'access'
    ? { factor: 'time', anchor: 'access', was: `“${claims.quotes.route}”`, now: `${STORY.sentAt}의 C · 지금 상태는 안내판에서` }
    : { factor: 'time', anchor: 'notice', was: `기록: ${claims.time ?? '시각 없음'} · 보냄 ${STORY.sentAt}`, now: `지금 ${STORY.receivedAt} · 안내판이 ${STORY.accessOpenAt}에 바뀜 — 기록 뒤의 일` };
  let third;
  if (claims.what === 'all_closed') third = { factor: 'scope', anchor: 'gate', was: `“${claims.quotes.what}”`, now: `준이 확인한 것: ${gateKnown(h)}` };
  else if (claims.advice === 'follow_me') third = { factor: 'advice', anchor: 'start1', was: `“${claims.quotes.advice}”`, now: '준의 다리, 준의 출발점 기준의 길' };
  else if (claims.advice === 'direction') third = { factor: 'direction', anchor: 'start1', was: `“${claims.quotes.advice}”`, now: '준의 출발점(남쪽 큰길) 기준 방향 · 서연은 서쪽 골목에서 출발', line: 'start0' };
  else third = { factor: 'place', anchor: 'start1', was: '준: 남쪽 큰길에서 출발', now: '서연: 서쪽 골목에서 출발', line: 'start0' };
  return [body, time, third];
}

export function closingLines(kind) {
  if (kind === 'generalized') return ['준이 본 것보다 넓은 말이었습니다. 그리고 준의 시각, 준의 몸, 준의 자리에서 쓴 말이었습니다.', '서연은 지금의 안내로 다시 판단합니다.'];
  if (kind === 'unparsed') return ['서연에게 닿은 것은 이 문장뿐이었습니다.', '서연은 지금의 안내에서 다시 시작합니다.'];
  return ['틀린 말은 아니었습니다. 준의 시각, 준의 몸, 준의 자리에서는.', '서연은 지금의 안내로 다시 판단합니다.'];
}

// How each part of the note read from 서연's side. status: helped | recheck | mismatch | missing.
export function readClaims(run) {
  const h = run?.handover;
  const c = h?.claims;
  if (!c) return [];
  const rows = [];
  const add = (label, quote, status, why, sourceId) => rows.push({ label, quote: quote ?? null, status, why, sourceId });
  if (c.what === 'gate_closed') add('본 것', c.quotes.what, c.time ? 'helped' : 'recheck', c.time ? '본 범위와 시각이 함께 있어 지금 안내와 비교할 수 있었습니다.' : '언제 본 것인지 없어 지금도 그런지 판단하기 어려웠습니다.', c.time ? 'press_code' : 'sms_area');
  if (c.what === 'all_closed') add('본 것', c.quotes.what, 'mismatch', `준이 확인한 것은 ${gateKnown(h)}뿐이었습니다.`, 'press_code');
  if (c.route === 'stairs' || c.route === 'ramp') add('들어간 길', c.quotes.route, 'mismatch', '서연의 카드 기준과 다른 입구였습니다.', 'koddi_card');
  if (c.unknowns) add('모르는 것', c.quotes.unknowns, 'helped', '모르는 부분을 밝혀 서연이 직접 확인할 곳을 알 수 있었습니다.', 'press_code');
  if (c.advice === 'follow_me' || c.advice === 'direction') add('조언', c.quotes.advice, 'mismatch', '쓴 사람의 다리와 출발점 기준의 말이었습니다.', 'sms_area');
  if (c.advice === 'check_notice') add('조언', c.quotes.advice, 'helped', '지금의 현장 안내를 다시 보게 했습니다.', 'korea_alert');
  if (!c.time) add('시각', null, 'missing', '언제의 정보인지가 없었습니다.', 'sms_area');
  if (!c.unknowns) add('모르는 것', null, 'missing', '확인하지 못한 부분이 적혀 있지 않았습니다.', 'press_code');
  add('C 경사로', null, 'missing', `${STORY.accessOpenAt}에 열렸습니다 — 기록을 보낸 뒤의 일이라 지금의 안내에서만 알 수 있었습니다.`, 'korea_alert');
  return rows;
}

const PRACTICES = {
  scope: {
    id: 'scope', title: '다른 상황에서 다시 판단하기 · 확인 범위', sourceId: 'press_code',
    prompt: '당신은 보호공간에 도착했습니다. 앞서 중앙문에 폐쇄 안내가 붙은 것만 보았습니다. 아직 확인하지 못한 옆문이 있습니다. 다음 주민에게 어떻게 전할까요?',
    options: [
      { id: 'all', label: '건물 전체가 폐쇄됐다고 전한다.', correct: false, feedback: '중앙문 하나의 상태만으로 건물 전체의 운영 여부는 알 수 없습니다.' },
      { id: 'scope', label: '확인 시각과 중앙문의 상태를 전하고, 옆문은 미확인이라고 밝힌다.', correct: true, feedback: '재난보도준칙도 단편적인 정보를 전할 때는 부족하거나 더 확인할 부분을 함께 밝히도록 합니다. 받는 사람이 정보의 한계를 알 수 있습니다.' },
      { id: 'inspect', label: '정보를 완성하려고 밖으로 나가 모든 문을 조사한다.', correct: false, feedback: '정보를 더 모으려고 보호공간을 벗어나지 않습니다. 모르는 부분은 미확인으로 남겨도 됩니다.' },
    ],
  },
  access: {
    id: 'access', title: '다른 상황에서 다시 판단하기 · 나의 기준', sourceId: 'unhcr_disability',
    prompt: '앞사람은 계단으로 대피했다고 적었습니다. 이번 주민은 계단을 혼자 내려가지 않는다는 자신의 기준이 있습니다. 현재 현장 안내는 다른 출입구도 운영 중이라고 알립니다. 어떻게 판단할까요?',
    options: [
      { id: 'same', label: '앞사람이 통과했으니 같은 계단으로 간다.', correct: false, feedback: '다른 사람에게 가능했던 이동이 지금 이 사람에게도 맞는 것은 아닙니다.' },
      { id: 'short', label: '지도에서 가장 짧아 보이는 길을 고른다.', correct: false, feedback: '거리만으로는 그 사람에게 맞는 길인지 알 수 없습니다.' },
      { id: 'own', label: '자신의 기준과 지금의 안내를 함께 보고 출입구를 고른다.', correct: true, feedback: '본인이 필요로 하는 것과 현재 안내를 함께 봅니다. 도울 때도 먼저 당사자에게 원하는 것을 묻습니다.' },
    ],
  },
  freshness: {
    id: 'freshness', title: '다른 상황에서 다시 판단하기 · 지금의 안내', sourceId: 'korea',
    prompt: '보호공간에서 안내를 듣고 있습니다. 이전 주민의 메모와 지금 시설 관리자의 안내가 다릅니다. 어떻게 판단할까요?',
    options: [
      { id: 'old', label: '아는 사람이 남긴 메모만 따른다.', correct: false, feedback: '메모를 쓴 뒤 상황이 달라졌을 수 있습니다.' },
      { id: 'current', label: '메모의 시각과 장소를 확인하고, 지금의 공식·현장 안내를 따른다.', correct: true, feedback: '경보가 발령되면 TV·라디오의 정부 안내와 현장 안내를 따르고, 확인되지 않은 정보에 휩쓸리지 않습니다.' },
      { id: 'vote', label: '단체 채팅에서 많이 공유된 설명을 따른다.', correct: false, feedback: '많이 공유됐다고 정확한 것은 아닙니다. 공식 안내와 비교합니다.' },
    ],
  },
};

const CARDS = [
  { title: '공식 문자에도 빠진 것이 있었습니다', body: '2023년 5월 31일 서울시는 06:41에 "6시 32분 서울지역에 경계경보 발령, 대피할 준비를 하라"는 문자를 보냈지만 이유와 대피 장소는 없었습니다. 07:03 행정안전부는 "오발령"이라고 알렸고, 서울시는 "과잉 대응일 수는 있어도 오발령은 아니다"라고 밝혔습니다.', sourceId: 'seoul_2023' },
  { title: '문자는 "언제, 어디"의 정보입니다', body: '재난문자는 영향 범위에 따라 읍·면·동 단위까지 지역을 좁혀 보냅니다. 내가 받은 문자는 내가 있던 곳 기준이라, 다른 곳에 있는 사람에게는 그대로 맞지 않을 수 있습니다.', sourceId: 'sms_area' },
  { title: '확인한 것과 모르는 것을 함께', body: '재난보도준칙은 전체 상황을 모를 때 단편적인 정보를 전한다면, 부족하거나 더 확인할 부분을 함께 밝혀 받는 사람이 정보의 한계를 알게 하도록 정합니다.', sourceId: 'press_code' },
  { title: '서연의 카드', body: '재난이 일어나기 전에 장애 유형, 의료 정보, 의사소통 방법을 담은 "재난 대비 카드"를 써 두면 도움이 됩니다. 서연의 카드 문장은 게임 속 인물 설정입니다.', sourceId: 'koddi_card' },
  { title: '대피가 먼저', body: '공습경보가 울리면 가까운 지하시설이나 유리창이 없는 건물 안으로 대피하고 방송 안내를 따릅니다. 장마·태풍 때는 침수 위험이 있는 저지대 지하시설로 대피하지 않습니다(이 시나리오는 호우 없음).', sourceId: 'korea' },
  { title: '평소에 대피소 찾아두기', body: '민방위 대피소는 전국 약 1만 7천 곳이 지정되어 있고, 지도앱과 "안전디딤돌" 앱에서 찾을 수 있습니다.', sourceId: 'shelters' },
];

export function buildDebrief(run) {
  const events = Array.isArray(run?.events) ? run.events.filter(event => event && typeof event.type === 'string' && event.details) : [];
  const has = (type, chapter, route) => events.some(event => event.type === type
    && (chapter === undefined || event.details.chapter === chapter)
    && (route === undefined || event.details.route === route));
  const kind = events.filter(event => event.type === 'handover_written').at(-1)?.details.kind;
  const readB = has('notice_read', 1);
  const triedB = ['stairs', 'ramp'].some(route => has('route_attempt', 1, route));
  const doneB = has('chapter_complete', 1, 'access');
  const completed = [0, 1].filter(chapter => has('chapter_complete', chapter)).length;
  const strengths = [];
  const improvements = [];

  if (kind === 'precise') strengths.push('본 것, 시각, 모르는 부분을 함께 전했습니다.');
  if (kind === 'generalized') improvements.push('출입구 한 곳의 폐쇄를 시설 전체로 넓혀 전했습니다. 확인한 범위와 시각을 남겨 보세요.');
  if (readB) strengths.push('서연으로 지금의 현장 안내를 확인했습니다.');
  if (has('help_choice', 1) || doneB) strengths.push('서연이 자신의 기준으로 입구를 다시 골랐습니다.');
  if (triedB && !has('help_choice', 1)) improvements.push('서연의 기준과 맞지 않는 입구에 다가갔습니다. 앞사람의 길을 내 기준에 대어 보세요.');

  const practiceId = kind === 'generalized' ? 'scope' : triedB ? 'access' : 'freshness';
  return {
    summary: events.length
      ? `대피 완료 ${completed}/2 · 규칙 기반 복기입니다. 기록에 나타난 선택만 설명하며 실제 생존 가능성이나 개인의 능력을 평가하지 않습니다.`
      : '아직 분석할 행동 기록이 없습니다. 아래 연습은 규칙 기반 예시입니다.',
    strengths,
    improvements,
    rows: readClaims(run),
    cards: structuredClone(CARDS),
    practice: structuredClone(PRACTICES[practiceId]),
  };
}

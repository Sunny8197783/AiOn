export const SOURCES = [
  { id: 'korea', title: '국민안전24 · 비상시 행동요령', url: 'https://www.safekorea.go.kr/safekorea-kor/acts/nacts/action-guide.do?actsHeaderTitle=%EB%B9%84%EC%83%81%EB%8C%80%EB%B9%84&category=stateOfEmergency&menuSn=4' },
  { id: 'information', title: 'ICRC · 전시 유해 정보의 영향', url: 'https://www.icrc.org/en/article/harmful-information-questions-and-answers' },
  { id: 'communication', title: 'Dovidka · 통신과 정보 확인', url: 'https://dovidka.info/en/communication-and-information-retrieval/' },
  { id: 'explosives', title: 'UNICEF · 폭발물 위험 교육', url: 'https://www.unicef.org/ukraine/en/stories/fostering-culture-safety-among-children-and-youth-making-mine-safety-rules-known-children' },
  { id: 'access', title: 'UNHCR · 안전한 거주 공간과 접근성', url: 'https://emergency.unhcr.org/emergency-assistance/settlement-and-shelter/guidance-settlements/safe-and-secure-settlements' },
];

export function createRun() {
  return { version: 1, startedAt: new Date().toISOString(), chapter: 0, events: [], handover: null };
}

export function recordEvent(run, type, details = {}) {
  if (!run || !Array.isArray(run.events) || typeof type !== 'string' || !type.trim()) {
    throw new TypeError('올바른 실행 기록과 이벤트 이름이 필요합니다.');
  }
  if (!details || typeof details !== 'object' || Array.isArray(details)) {
    throw new TypeError('이벤트 세부 정보는 객체여야 합니다.');
  }
  const event = {
    seq: run.events.length + 1,
    at: new Date().toISOString(),
    type,
    details: structuredClone(details),
  };
  run.events.push(event);
  return event;
}

export function makeHandover(run, kind) {
  if (!['precise', 'generalized'].includes(kind)) throw new RangeError('지원하지 않는 전달 방식입니다.');
  const precise = kind === 'precise';
  const observation = run.events.filter(event => event.type === 'gate_observed' && event.details.chapter !== 1).at(-1);
  const arrival = run.events.filter(event => ['route_attempt', 'chapter_complete'].includes(event.type)
    && event.details.chapter === 0 && ['stairs', 'ramp'].includes(event.details.route)).at(-1);
  const route = arrival ? (arrival.details.route === 'stairs' ? '계단' : '경사로') : null;
  const time = observation?.details.time || '14:10';
  const handover = {
    kind,
    text: precise
      ? `${time} 동쪽 출입구의 폐쇄 안내를 확인했습니다. ${route ? `저는 ${route} 입구로 대피시설에 도착했습니다. 이후에도 통행할 수 있다는 보장은 없으며, 나머지 입구는 미확인입니다.` : '다른 출입구는 확인하지 못했습니다.'}`
      : '이 대피소는 모두 폐쇄됐습니다. 이용할 수 없습니다.',
    source: observation?.details.source || '주민 A의 전달',
    time,
    scope: precise ? `동쪽 폐쇄 안내${route ? ` · 나의 ${route} 입구 도착` : ''} · 그 외 미확인` : '시설 전체로 확대 · 확인 근거 부족',
  };
  recordEvent(run, 'handover_written', { kind });
  run.handover = handover;
  return handover;
}

const PRACTICES = {
  scope: {
    id: 'scope', title: '다른 상황에서 다시 판단하기 · 확인 범위', sourceId: 'information',
    prompt: '당신은 보호공간에 도착했습니다. 앞서 중앙문에 폐쇄 안내가 붙은 것만 보았습니다. 아직 확인하지 못한 옆문이 있습니다. 다음 주민에게 어떻게 전할까요?',
    options: [
      { id: 'all', label: '건물 전체가 폐쇄됐다고 전한다.', correct: false, feedback: '중앙문 하나의 상태만으로 건물 전체의 운영 여부는 알 수 없습니다.' },
      { id: 'scope', label: '확인 시각과 중앙문의 상태를 전하고, 옆문은 미확인이라고 밝힌다.', correct: true, feedback: '직접 확인한 범위와 모르는 부분을 구분하면 다음 사람이 현재 안내와 비교할 수 있습니다.' },
      { id: 'inspect', label: '정보를 완성하려고 밖으로 나가 모든 문을 조사한다.', correct: false, feedback: '정보를 더 모으려고 보호공간을 벗어나지 않습니다. 모르는 부분을 미확인으로 남겨도 됩니다.' },
    ],
  },
  access: {
    id: 'access', title: '다른 상황에서 다시 판단하기 · 이동 조건', sourceId: 'access',
    prompt: '앞사람은 계단으로 대피했습니다. 이번 주민은 계단 이용이 어렵습니다. 현재 현장 안내는 이용 가능한 무단차 출입구와 연결 경로를 표시합니다. 어떤 경로를 선택할까요?',
    options: [
      { id: 'same', label: '앞사람이 통과했으니 같은 계단으로 간다.', correct: false, feedback: '다른 사람에게 가능했던 이동이 현재 주민에게도 가능한 것은 아닙니다.' },
      { id: 'any', label: '지도에서 가장 짧아 보이는 길을 고른다.', correct: false, feedback: '거리만으로 통행 가능 여부를 판단할 수 없습니다. 단차와 폐쇄 여부도 확인해야 합니다.' },
      { id: 'accessible', label: '현재 안내와 자신의 이동 조건에 맞는 무단차 경로로 바꾼다.', correct: true, feedback: '이 장면에서는 운영이 확인된 무단차 경로를 이용할 수 있습니다. 현실에서는 경사·폭·장애물과 필요한 도움도 고려합니다.' },
    ],
  },
  freshness: {
    id: 'freshness', title: '다른 상황에서 다시 판단하기 · 최신 안내', sourceId: 'information',
    prompt: '보호공간에서 다음 안내를 듣고 있습니다. 이전 주민의 메모와 현재 시설 관리자의 안내가 다릅니다. 현재 안내는 이 시설의 사용 가능한 출입구를 구체적으로 설명합니다. 어떻게 판단할까요?',
    options: [
      { id: 'old', label: '직접 아는 사람이 남긴 오래된 메모만 따른다.', correct: false, feedback: '메모가 작성된 뒤 시설 상태가 달라졌을 수 있습니다.' },
      { id: 'current', label: '출처·시각·적용 장소를 비교하고 현재 현장 안내에 맞춰 계획을 고친다.', correct: true, feedback: '최신이라는 이유만으로 믿기보다, 확인 가능한 출처와 내 위치에 적용되는 안내인지 함께 살핍니다.' },
      { id: 'vote', label: '단체 채팅에서 많이 공유된 설명을 따른다.', correct: false, feedback: '공유 횟수는 정확성을 보장하지 않습니다. 현장 상태가 확인된 안내와 비교합니다.' },
    ],
  },
  communication: {
    id: 'communication', title: '다른 상황에서 다시 판단하기 · 통신 대비', sourceId: 'communication',
    prompt: '아직 비상상황이 아닌 평상시입니다. 통신 두절에 대비해 휴대전화로 동네의 대피 장소를 살펴보려 합니다. 어떤 준비가 도움이 될까요?',
    options: [
      { id: 'later', label: '비상상황이 생긴 뒤 온라인 지도를 처음 연다.', correct: false, feedback: '그때는 통신망이 작동하지 않을 수 있습니다. 필요한 지도는 미리 저장하고 확인합니다.' },
      { id: 'offline', label: '필요한 지도를 미리 내려받고 오프라인에서도 열리는지 확인한다.', correct: true, feedback: '오프라인 지도는 사전 다운로드가 필요합니다. 저장된 지도만으로 현재 출입구 운영 상태까지 보장되지는 않습니다.' },
      { id: 'guarantee', label: '미리 저장한 지도는 항상 최신이므로 현장 안내는 생략한다.', correct: false, feedback: '저장한 정보와 현재 상황은 다를 수 있습니다. 대피 시에는 현재 당국과 현장 안내를 따릅니다.' },
    ],
  },
};

export function buildDebrief(run) {
  const events = Array.isArray(run?.events) ? run.events.filter(event => event && typeof event.type === 'string' && event.details) : [];
  const has = (type, chapter, route) => events.some(event => event.type === type
    && (chapter === undefined || event.details.chapter === chapter)
    && (route === undefined || event.details.route === route));
  const handover = events.filter(event => event.type === 'handover_written').at(-1)?.details.kind;
  const seenB = ['notice_read', 'route_attempt', 'chapter_complete'].some(type => has(type, 1));
  const readB = has('notice_read', 1);
  const stairsB = has('route_attempt', 1, 'stairs');
  const rampB = has('chapter_complete', 1, 'ramp');
  const completed = [0, 1].filter(chapter => has('chapter_complete', chapter)).length;
  const strengths = [];
  const improvements = [];
  const cards = [];

  if (handover === 'precise' && has('gate_observed')) {
    strengths.push('확인한 출입구 정보와 미확인 상태를 나누어 전달했습니다.');
  }
  if (handover === 'generalized') {
    improvements.push('출입구 한 곳의 폐쇄를 시설 전체의 폐쇄로 확대해 전달했습니다. 확인한 범위와 시각을 함께 남겨 보세요.');
  }
  if (handover) {
    cards.push({ title: '내가 본 것과 아직 모르는 것', body: '한 출입구의 상태로 전체 시설을 단정하지 않습니다. 이 게임은 ICRC의 유해 정보 위험 설명을 바탕으로 확인 범위와 시각을 전달하는 연습을 구성했습니다.', sourceId: 'information' });
  }
  if (readB) strengths.push('두 번째 주민으로 현재 현장 안내를 확인했습니다.');
  else if (seenB) improvements.push('두 번째 주민의 현재 안내를 읽은 기록이 없습니다. 보호공간이나 대피 동선에서 확인할 수 있는 당국·현장 안내를 반영해 보세요.');

  if (stairsB) improvements.push('두 번째 주민이 이용하기 어려운 계단 경로를 시도했습니다. 앞사람의 경로를 자신의 이동 조건에 맞춰 조정해 보세요.');
  if (rampB) strengths.push(stairsB ? '계단을 시도한 뒤 무단차 출입구로 경로를 수정해 대피를 마쳤습니다.' : '두 번째 주민의 이동 조건에 맞는 무단차 출입구로 대피를 마쳤습니다.');
  if (seenB) cards.push({ title: '같은 길도 사람마다 다릅니다', body: '대피 공간은 이동 조건이 다른 사람도 접근할 수 있어야 합니다. 게임 속 경사로의 운영 여부는 시나리오 설정이며, 현실의 모든 경사로가 안전하다는 뜻은 아닙니다.', sourceId: 'access' });

  cards.push({ title: '대피가 먼저, 확인은 가능한 범위에서', body: '이 연습은 일반 공습 상황입니다. 가까운 적절한 보호공간으로 신속히 대피하고 이후에도 정부·현장 안내를 따릅니다. 정보를 수집하려고 대피를 미루거나 위험한 곳으로 돌아가지 않습니다.', sourceId: 'korea' });
  const practiceId = handover === 'generalized' ? 'scope' : stairsB ? 'access' : seenB && !readB ? 'freshness' : 'communication';
  return {
    title: completed === 2 ? '두 사람의 길이 이어졌습니다' : '선택 기록 돌아보기',
    summary: events.length
      ? `대피 완료 ${completed}/2 · 규칙 기반 복기입니다. 기록에 나타난 선택만 설명하며 실제 생존 가능성이나 개인의 능력을 평가하지 않습니다.`
      : '아직 분석할 행동 기록이 없습니다. 아래 연습은 규칙 기반 예시이며 AI 분석은 연결되지 않았습니다.',
    strengths,
    improvements,
    cards,
    practice: structuredClone(PRACTICES[practiceId]),
  };
}

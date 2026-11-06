// 대피소 라운지 (shelter lounge): players talk in fixed phrases only, through a public relay (ntfy.sh, no account),
// and can post a finished run as its ending number and challenge count. Every received message is checked against
// these tables, so nothing another client sends can put its own words on screen. Pure: no DOM, no network.
import { ENDINGS } from './endings.js';
import { CHALLENGES } from './challenges.js';

export const RELAY = 'https://ntfy.sh', TOPIC = 'gyeongbo-ijeon-lounge-v1';
export const PHRASES = {
  hello: '안녕하세요, 연무동 이웃입니다.',
  cheer: '끝까지 함께 버텨요!',
  radio: '정전되면 라디오부터 켜 보세요.',
  rumor: '소문은 공식 안내로 먼저 확인해요.',
  ask: '돕기 전에 먼저 물어봐요.',
  elder: '304호 어르신을 잊지 마세요.',
  card: '비상연락카드에 만남의 장소를 적어 둬요.',
  retreat: '불 앞에서 물러나는 것도 좋은 판단이에요.',
  ending: '새 엔딩을 봤어요!',
  thanks: '알려 줘서 고마워요.',
};
const WHO = /^10[1-6]동 이웃 \d{4}$/;

// A neighbour name for this browser, e.g. '103동 이웃 4821'. rnd: () => [0, 1).
export const isName = (s) => typeof s === 'string' && WHO.test(s);
export const newName = (rnd = Math.random) => `${101 + Math.floor(rnd() * 6)}동 이웃 ${1000 + Math.floor(rnd() * 9000)}`;

export const phraseBody = (who, p) => JSON.stringify({ v: 1, who, p });
export const runBody = (who, run) => JSON.stringify({ v: 1, who, run: { e: run.ending, c: run.n } });

// One relay message ({ id, time, event, message }) -> { id, time, who, text, run } or null when anything is off.
export function readMessage(m) {
  if (!m || m.event !== 'message' || typeof m.message !== 'string' || typeof m.id !== 'string' || !Number.isInteger(m.time)) return null;
  let b;
  try { b = JSON.parse(m.message); } catch { return null; }
  if (!b || b.v !== 1 || !isName(b.who)) return null;
  if (typeof b.p === 'string' && Object.hasOwn(PHRASES, b.p)) return { id: m.id, time: m.time, who: b.who, text: PHRASES[b.p], run: false };
  const e = ENDINGS.find((x) => x.id === b.run?.e), c = b.run?.c;
  if (e && Number.isInteger(c) && c >= 0 && c <= CHALLENGES.length) return { id: m.id, time: m.time, who: b.who, text: `엔딩 ${e.no}에 닿았어요 · 도전 과제 ${c}/${CHALLENGES.length}`, run: true };
  return null;
}

// The relay's poll answer is one JSON object per line.
export const readHistory = (text) => String(text ?? '').split('\n').map((l) => { try { return readMessage(JSON.parse(l)); } catch { return null; } }).filter(Boolean);

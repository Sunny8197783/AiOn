// 대피소 라운지 panel: the last 12 hours of fixed-phrase messages, live updates, phrase buttons and posting your
// best run. Offline it says so and the game goes on.
import { el, modal, icon, btn } from './dom.js';
import { RELAY, TOPIC, PHRASES, phraseBody, runBody, readMessage, readHistory, isName, newName } from '../game/lounge.js';

const NAME_KEY = 'gyeongbo.lounge.v1';
// The lounge name is made once per browser. store: { get(k), set(k, v) } (app.js, never throws).
export function loungeName(store) {
  let name = store.get(NAME_KEY);
  if (!isName(name)) { name = newName(); store.set(NAME_KEY, name); }
  return name;
}

// lounge({ name, best, topic }) -> Promise. best = the top run of the records ({ ending, n }) or null.
export function lounge({ name, best = null, topic = TOPIC } = {}) {
  const url = `${RELAY}/${topic}`, seen = new Set();
  const feed = el('ol', { class: 'lounge-feed', 'aria-live': 'polite', 'aria-label': '라운지 대화' });
  const status = el('p', { class: 'set-note lounge-status' }, '라운지에 연결하는 중…');
  const add = (msg) => {
    if (!msg || seen.has(msg.id)) return;
    seen.add(msg.id);
    feed.append(el('li', { class: [msg.run ? 'run' : '', msg.who === name ? 'mine' : ''].join(' ').trim() },
      el('b', {}, msg.who), el('span', {}, msg.text), el('small', {}, new Date(msg.time * 1000).toLocaleTimeString('ko-KR', { hour: '2-digit', minute: '2-digit' }))));
    while (feed.children.length > 60) feed.firstElementChild.remove();
    feed.scrollTop = feed.scrollHeight;
  };
  fetch(`${url}/json?poll=1&since=12h`).then((r) => r.text()).then((t) => readHistory(t).forEach(add), () => {});
  const es = typeof EventSource === 'function' ? new EventSource(`${url}/sse`) : null;
  if (es) {
    es.onopen = () => { status.textContent = '연결됨 · 정해진 문구로만 이야기합니다.'; };
    es.onerror = () => { status.textContent = '라운지에 연결할 수 없습니다. 인터넷 연결을 확인해 주세요. 게임은 그대로 할 수 있습니다.'; };
    es.onmessage = (ev) => { try { add(readMessage(JSON.parse(ev.data))); } catch { /* not ours */ } };
  }
  // One message per 5 s from this panel (the relay limits bursts too).
  const send = (body) => {
    for (const b of buttons) b.disabled = true;
    fetch(url, { method: 'POST', body }).then((r) => { if (!r.ok) throw new Error(String(r.status)); })
      .catch(() => { status.textContent = '보내지 못했습니다. 잠시 뒤 다시 해 주세요.'; })
      .finally(() => setTimeout(() => { for (const b of buttons) b.disabled = false; }, 5000));
  };
  const phrases = Object.entries(PHRASES).map(([k, t]) => btn(t, { ghost: true, onclick: () => send(phraseBody(name, k)) }));
  const share = best ? btn('내 최고 기록 알리기', { iconName: 'spark', onclick: () => send(runBody(name, best)) }) : null;
  const buttons = [...phrases, share].filter(Boolean);
  const content = el('div', { class: 'about-panel lounge-panel' },
    el('header', { class: 'nb-head' }, el('p', { class: 'eyebrow' }, icon('people'), '대피소 라운지'), el('h2', {}, '연무동 이웃들과 이야기하기'),
      el('p', { class: 'set-note' }, `내 이름: ${name} · 정해진 문구만 보낼 수 있습니다.`)),
    feed, status,
    el('div', { class: 'lounge-phrases', role: 'group', 'aria-label': '보낼 문구' }, phrases),
    el('p', { class: 'set-note' }, '대화는 공개 중계 서버(ntfy.sh)를 거쳐 누구나 볼 수 있고, 12시간 뒤 사라집니다. 이름과 문구 말고는 아무것도 보내지 않습니다.'),
    el('div', { class: 'set-actions' }, share, btn('닫기', { primary: true, 'data-autofocus': true, onclick: () => m.close() })));
  const m = modal({ className: 'about', label: '대피소 라운지', content, onKey(e) { if (e.key === 'Escape') { e.preventDefault(); m.close(); } } });
  return m.done.finally(() => es?.close());
}

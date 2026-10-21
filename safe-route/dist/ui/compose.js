// 정민's message to 서연 (chips or free text), the phone/marked note panel, and the re-read captions.
import { composeNote, MAX_NOTE } from '../game/relay.js';

const root = () => document.getElementById('overlay');
const h = (tag, attrs = {}, ...kids) => {
  const e = document.createElement(tag);
  for (const [k, v] of Object.entries(attrs)) if (k === 'class') e.className = v; else if (k.startsWith('on')) e.addEventListener(k.slice(2), v); else if (v !== false && v != null) e.setAttribute(k, v === true ? '' : v);
  for (const kid of kids.flat()) if (kid != null) e.append(kid.nodeType ? kid : document.createTextNode(String(kid)));
  return e;
};

// Every chip label is the exact fragment that goes into the message.
export function compose({ route, observedVia, gateSeenAt, sentAt }) {
  return new Promise((resolve) => {
    const c = { route, observedVia, gateSeenAt };
    const chips = { what: null, time: false, route: false, unknowns: false, advice: null };
    let edited = false;
    const prev = document.activeElement;
    const radio = (name, options) => options.map(([value, label]) => h('label', { class: 'pill' }, h('input', { type: 'radio', name, value }), h('span', {}, label)));
    const row = (legend, ...inner) => h('fieldset', { class: 'chips' }, h('legend', {}, legend), ...inner);
    const text = h('textarea', { id: 'note-text', maxlength: MAX_NOTE, rows: 3, 'aria-describedby': 'note-help' });
    const send = h('button', { type: 'button', class: 'primary', disabled: true }, `보내기 · ${sentAt}`);
    const reset = h('button', { type: 'button', class: 'secondary', hidden: true }, '칩으로 다시 쓰기');
    const box = h('div', { class: 'relay-modal', role: 'dialog', 'aria-modal': 'true', 'aria-labelledby': 'compose-title' },
      h('div', { class: 'relay-card' },
        h('p', { class: 'eyebrow' }, `서연(303호)에게 문자 · ${sentAt}`),
        h('h2', { id: 'compose-title' }, '무엇을 남길까요?'),
        h('p', {}, '정민은 대피소에 들어왔습니다. 서연에게 보낼 문자를 고르거나 직접 고쳐 쓰세요.'),
        observedVia === 'notice_after' ? h('p', { class: 'quote' }, '입구 안내문: “동쪽 출입구 폐쇄 · A 계단 / B 진출입로 이용”') : null,
        row('무엇을 봤나', ...radio('what', [['gate_closed', composeNote({ what: 'gate_closed' }, c)], ['all_closed', composeNote({ what: 'all_closed' }, c)]])),
        row('언제', ...radio('time', [['true', `${gateSeenAt}에`], ['false', '시각 없이']])),
        row('내가 쓴 길', ...radio('route', [['true', composeNote({ route: true }, c)], ['false', '쓰지 않기']])),
        row('모르는 것', h('label', { class: 'pill' }, h('input', { type: 'checkbox', name: 'unknowns' }), h('span', {}, composeNote({ unknowns: true }, c)))),
        row('한마디', ...radio('advice', [['follow_me', composeNote({ advice: 'follow_me' }, c)], ['direction', composeNote({ advice: 'direction' }, c)], ['check_notice', composeNote({ advice: 'check_notice' }, c)], ['', '없음']])),
        h('label', { class: 'note-label', for: 'note-text' }, '보낼 문장 ', h('small', { id: 'note-help' }, `최대 ${MAX_NOTE}자 · 직접 고칠 수 있어요`)),
        text,
        h('div', { class: 'compose-actions' }, reset, send)));
    const refresh = () => {
      if (!edited) text.value = composeNote(chips, c);
      send.disabled = !(edited ? text.value.trim() : chips.what);
      reset.hidden = !edited;
    };
    box.querySelectorAll('input').forEach(input => input.addEventListener('change', () => {
      if (input.name === 'unknowns') chips.unknowns = input.checked;
      else if (input.name === 'time' || input.name === 'route') chips[input.name] = input.value === 'true';
      else chips[input.name] = input.value || null;
      refresh();
    }));
    text.addEventListener('input', () => { edited = true; refresh(); });
    reset.addEventListener('click', () => { edited = false; refresh(); });
    // Radio groups make the Tab order differ from the element list, so also pull escaped focus back in.
    const guard = (e) => { if (box.isConnected && !box.contains(e.target)) (box.querySelector('input:checked') || box.querySelector('input')).focus(); };
    document.addEventListener('focusin', guard);
    send.addEventListener('click', () => { document.removeEventListener('focusin', guard); box.remove(); prev?.focus?.(); resolve({ chips, text: text.value, edited }); });
    box.addEventListener('keydown', (e) => {
      if (e.key !== 'Tab') return;
      const f = [...box.querySelectorAll('input,textarea,button:not([hidden])')].filter(x => !x.disabled);
      if (e.shiftKey && document.activeElement === f[0]) { e.preventDefault(); f.at(-1).focus(); }
      else if (!e.shiftKey && document.activeElement === f.at(-1)) { e.preventDefault(); f[0].focus(); }
    });
    root().append(box);
    refresh();
    box.querySelector('input').focus();
  });
}

// The message as 서연 sees it. mode: 'phone' | 'marked' (claims underlined).
let panel = null;
export function showNote(hand, mode) {
  hideNote();
  const body = h('p', { id: 'relay-note-text' });
  if (mode === 'marked') {
    const spans = [...new Set(Object.values(hand.claims.quotes))].map(q => [hand.text.indexOf(q), q]).filter(([i]) => i >= 0).sort((a, b) => a[0] - b[0]);
    let at = 0;
    for (const [i, q] of spans) { if (i < at) continue; body.append(hand.text.slice(at, i), h('mark', { class: 'claim' }, q)); at = i + q.length; }
    body.append(hand.text.slice(at));
  } else body.textContent = hand.text;
  panel = h('aside', { class: 'relay-note phone' }, h('small', {}, `정민 · 302호 이웃 · 보냄 ${hand.sentAt} · 받음 ${hand.receivedAt}`), body);
  root().append(panel);
}
export function hideNote() { panel?.remove(); panel = null; }

// One step of the re-read. Resolves 'next', or 'skip' on Esc. The focused button carries the caption.
export function rereadCaption(text, pin, last = false) {
  return new Promise((resolve) => {
    document.querySelector('.relay-caption')?.remove();
    const btn = h('button', { type: 'button', class: 'primary', 'aria-describedby': 'relay-cap relay-pin' }, last ? '서연으로 계속 ▸' : '다음 ▸');
    const cap = h('div', { class: 'relay-caption', role: 'group', 'aria-label': '다시 읽기' },
      h('p', { id: 'relay-cap' }, text),
      pin ? h('p', { id: 'relay-pin', class: 'pin-lines' }, h('span', { class: 'was' }, pin.was), h('span', { class: 'now' }, pin.now), pin.also ? h('span', { class: 'now' }, pin.also.now) : null) : null,
      btn, h('small', {}, 'E · Enter · Space로 넘기기 · Esc로 끝까지'));
    const done = (v) => { removeEventListener('keydown', key, true); cap.remove(); resolve(v); };
    const key = (e) => {
      if (e.repeat) return;
      if (e.key === 'Escape') { e.preventDefault(); done('skip'); }
      else if (e.code === 'KeyE' || ((e.key === 'Enter' || e.key === ' ') && document.activeElement !== btn)) { e.preventDefault(); done('next'); }
    };
    btn.addEventListener('click', () => done('next'));
    addEventListener('keydown', key, true);
    root().append(cap);
    btn.focus({ preventScroll: true });
  });
}

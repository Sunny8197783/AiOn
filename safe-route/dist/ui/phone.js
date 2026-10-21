// 정민's phone: status bar (time, signal, battery %), the map app's state, the message list,
// and the full-width emergency alert sheet.
import { el, modal, icon, btn, uiSound } from './dom.js';

const SIGNAL = { ok: ['신호 좋음', 4], weak: ['신호 약함', 1], none: ['서비스 없음', 0] };
const KIND = {
  emergency: ['위급재난문자', 'alert'],
  safety: ['안전안내문자', 'info'],
  sms: ['문자', 'sms'],
  me: ['나', 'sms'],
};

function statusBar(time, signal, battery) {
  const [sigText, bars] = SIGNAL[signal] || SIGNAL.ok;
  const b = Math.max(0, Math.min(100, Number(battery) || 0));
  return el('div', { class: 'phone-status' },
    el('span', { class: 'ps-time' }, time || ''),
    el('span', { class: 'ps-right' },
      el('span', { class: 'ps-signal', role: 'img', 'aria-label': sigText, 'data-bars': bars },
        [1, 2, 3, 4].map((n) => el('i', { class: n <= bars ? 'on' : '' }))),
      bars === 0 ? el('span', { class: 'ps-note' }, '서비스 없음') : bars === 1 ? el('span', { class: 'ps-note' }, '약함') : null,
      el('span', { class: `ps-batt${b <= 20 ? ' low' : ''}`, role: 'img', 'aria-label': `배터리 ${Math.round(b)}%` },
        el('span', { class: 'batt', 'aria-hidden': 'true' }, el('span', { class: 'batt-fill', style: { width: `${b}%` } })),
        `${Math.round(b)}%`)));
}

function mapApp(state) {
  const body = {
    ok: [icon('map'), el('b', {}, '지도'), el('span', {}, '현재 위치 · 연무동')],
    loading: [el('span', { class: 'spinner', 'aria-hidden': 'true' }), el('b', {}, '지도를 불러오는 중…'), el('span', {}, '데이터 연결이 느립니다')],
    fail: [icon('alert'), el('b', {}, '지도를 불러올 수 없습니다'), el('span', {}, '네트워크 연결을 확인해 주세요')],
  }[state] || [];
  return el('section', { class: `app-map state-${state}`, 'aria-label': '지도앱' },
    el('div', { class: 'map-tiles', 'aria-hidden': 'true' }),
    el('div', { class: 'map-state', role: state === 'ok' ? null : 'status' }, ...body));
}

function bubble(m) {
  const [kindLabel, ic] = KIND[m.kind] || KIND.sms;
  const official = m.kind === 'emergency' || m.kind === 'safety';
  return el('li', { class: `msg msg-${m.kind || 'sms'}` },
    el('div', { class: 'msg-head' },
      official ? el('span', { class: 'msg-kind' }, icon(ic), kindLabel) : el('b', { class: 'msg-from' }, m.kind === 'me' ? '나' : (m.from || '알 수 없음')),
      official && m.from && m.from !== kindLabel ? el('span', { class: 'msg-from' }, m.from) : null,
      el('time', { class: 'msg-time' }, m.time || '')),
    el('p', { class: 'msg-text' }, m.text || ''));
}

// phone({ battery, signal, messages, mapApp, actions }) -> Promise<actionId>
export function phone({ battery = 50, signal = 'ok', messages = [], mapApp: map = null, actions = null } = {}) {
  const acts = (actions?.length ? actions : [{ id: 'close', label: '닫기' }]).map((a) => (typeof a === 'string' ? { id: a, label: a } : a));
  const msgs = [].concat(messages || []);
  const time = msgs.at(-1)?.time || '';
  const low = Number(battery) <= 15;
  const t0 = performance.now();
  const frame = el('div', { class: 'phone-frame' },
    el('div', { class: 'phone-notch', 'aria-hidden': 'true' }),
    statusBar(time, signal, battery),
    el('div', { class: 'phone-screen' },
      low ? el('p', { class: 'phone-banner', role: 'status' }, icon('battery'), `배터리 ${Math.max(0, Math.round(battery))}% · 절전 모드`) : null,
      map ? mapApp(map) : null,
      el('section', { class: 'app-msgs', 'aria-label': '메시지' },
        el('h3', {}, icon('sms'), '메시지'),
        msgs.length ? el('ol', { class: 'msg-list' }, msgs.map(bubble)) : el('p', { class: 'empty' }, '새 메시지가 없습니다.'))),
    el('div', { class: 'phone-actions' }, acts.map((a, n) => btn(a.label ?? a.id, { primary: !!a.primary, ghost: !a.primary, 'data-autofocus': n === 0 || null,
      onclick: () => { if (performance.now() - t0 > 200) { uiSound('click'); m.close(a.id); } } }))),
    el('div', { class: 'phone-home', 'aria-hidden': 'true' }));
  const escId = acts.find((a) => a.id === 'close' || a.id === 'back')?.id ?? (acts.length === 1 ? acts[0].id : null);
  const m = modal({ className: 'phone', label: '휴대폰', content: frame,
    onKey(e) { if (e.key === 'Escape' && escId != null) { e.preventDefault(); m.close(escId); } } });
  // Newest message in view.
  frame.querySelector('.msg-list')?.lastElementChild?.scrollIntoView?.({ block: 'nearest' });
  return m.done;
}

// emergencyAlert({ kind: '위급재난문자'|'긴급재난문자'|'안전안내문자', text, time }) -> Promise
export function emergencyAlert({ kind = '위급재난문자', text = '', time = '' } = {}) {
  const level = kind.includes('위급') ? 'critical' : kind.includes('긴급') ? 'urgent' : 'safety';
  const titleId = 'alert-title';
  const t0 = performance.now();
  const sheet = el('div', { class: `alert-sheet alert-${level}` },
    el('header', { class: 'alert-head' },
      icon(level === 'safety' ? 'info' : 'alert'),
      el('h2', { id: titleId }, kind),
      time ? el('time', {}, time) : null),
    el('p', { class: 'alert-text' }, text),
    el('footer', {},
      el('small', {}, '게임 속 가상 문자입니다'),
      btn('확인', { primary: true, 'data-autofocus': true, onclick: () => { if (performance.now() - t0 > 300) m.close(); } })));
  const m = modal({ className: `ealert ealert-${level}`, label: kind, content: sheet,
    onKey(e) { if (e.key === 'Escape') { e.preventDefault(); m.close(); } } });
  m.root.setAttribute('aria-labelledby', titleId);
  m.root.setAttribute('role', 'alertdialog');
  return m.done;
}

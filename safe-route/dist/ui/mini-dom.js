// DOM helpers for the minigames. Same API as ui/dom.js (el, trapFocus, modal, escapeText), which was not
// in this worktree; plus small shared bits (label pills, the guided/hint/free help block, reduced motion).

export function el(tag, attrs = {}, ...kids) {
  const e = document.createElement(tag);
  for (const [k, v] of Object.entries(attrs || {})) {
    if (v == null || v === false) continue;
    if (k === 'class') e.className = v;
    else if (k.startsWith('on') && typeof v === 'function') e.addEventListener(k.slice(2), v);
    else e.setAttribute(k, v === true ? '' : v);
  }
  for (const kid of kids.flat(Infinity)) if (kid != null && kid !== false) e.append(kid.nodeType ? kid : document.createTextNode(String(kid)));
  return e;
}

export const escapeText = (s) => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

const FOCUSABLE = 'button:not([disabled]),[href],input:not([disabled]),select:not([disabled]),textarea:not([disabled]),[tabindex]:not([tabindex="-1"])';
const visible = (n) => !n.closest('[hidden]') && n.getClientRects().length > 0;
export const focusables = (root) => [...root.querySelectorAll(FOCUSABLE)].filter(visible);

// Keeps Tab inside root. Returns a release function.
export function trapFocus(root) {
  const onKey = (e) => {
    if (e.key !== 'Tab') return;
    const f = focusables(root);
    if (!f.length) { e.preventDefault(); root.focus(); return; }
    const i = f.indexOf(document.activeElement);
    if (e.shiftKey && i <= 0) { e.preventDefault(); f.at(-1).focus(); }
    else if (!e.shiftKey && (i === -1 || i === f.length - 1)) { e.preventDefault(); f[0].focus(); }
  };
  root.addEventListener('keydown', onKey);
  return () => root.removeEventListener('keydown', onKey);
}

// A dialog over the canvas. onKey(e) returns true when it handled the key. close(result) resolves done
// and gives focus back to whatever had it before.
export function modal({ className = '', label = '', content = [], onKey } = {}) {
  const prev = document.activeElement;
  const root = el('div', { class: `mg-panel ${className}`, role: 'dialog', 'aria-modal': 'true', 'aria-label': label, tabindex: '-1' });
  const back = el('div', { class: 'mg-backdrop' }, root);
  root.append(...[].concat(content));
  let resolve, closed = false;
  const done = new Promise(r => { resolve = r; });
  const release = trapFocus(root);
  back.addEventListener('keydown', (e) => {
    if (onKey?.(e) === true) { e.preventDefault(); e.stopPropagation(); }
    else e.stopPropagation(); // keys typed in a minigame never reach the world controls
  });
  (document.getElementById('overlay') || document.body).append(back);
  requestAnimationFrame(() => { if (!closed) (root.querySelector('[data-autofocus]') || focusables(root)[0] || root).focus({ preventScroll: true }); });
  function close(result) {
    if (closed) return;
    closed = true; release(); back.remove();
    prev?.focus?.({ preventScroll: true });
    resolve(result);
  }
  return { root, close, done };
}

export const reduced = () => document.documentElement.classList.contains('reduce-motion') || !!globalThis.matchMedia?.('(prefers-reduced-motion: reduce)').matches;

// Information label pills: icon + text, never colour alone.
const LABELS = { official: ['◆', '공식 안내'], site: ['◎', '현장 확인'], neighbor: ['●', '이웃에게 들음'], rumor: ['?', '소문·미확인'], self: ['✎', '내 기록'] };
export function pill(label) {
  const [icon, text] = LABELS[label] || LABELS.self;
  return el('span', { class: `mg-pill is-${LABELS[label] ? label : 'self'}` }, el('span', { 'aria-hidden': 'true' }, icon), text);
}

const MODE_TEXT = { guided: '안내 있음', hint: '힌트 가능', free: '혼자 판단', view: '보기' };
export function header(eyebrow, title, mode, id) {
  return el('header', { class: 'mg-head' },
    el('div', {}, el('p', { class: 'mg-eyebrow' }, eyebrow), el('h2', { id }, title)),
    MODE_TEXT[mode] ? el('span', { class: `mg-mode is-${mode}` }, MODE_TEXT[mode]) : null);
}

// guided: help shown; hint: a 힌트 button reveals it; free/view: nothing. make() builds the help nodes.
export function ladderHelp(mode, make) {
  if (mode === 'guided') return el('div', { class: 'mg-help', role: 'note' }, make());
  if (mode !== 'hint') return null;
  const body = el('div', { class: 'mg-help', role: 'note', hidden: true });
  const btn = el('button', { type: 'button', class: 'mg-btn mg-ghost', 'aria-expanded': 'false' }, '힌트 보기');
  btn.addEventListener('click', () => {
    const open = body.hidden;
    if (open && !body.childElementCount) body.append(...[].concat(make()));
    body.hidden = !open; btn.setAttribute('aria-expanded', String(open)); btn.textContent = open ? '힌트 닫기' : '힌트 보기';
  });
  return el('div', { class: 'mg-hintbox' }, btn, body);
}

// Meter with text: 'ok' | 'warn' | 'over'.
export function meterState(v, max) { return v > max ? 'over' : v > max * 0.8 ? 'warn' : 'ok'; }
export const METER_WORD = { ok: '여유', warn: '거의 가득', over: '초과' };
export const fmtKg = (n) => `${(Math.round(n * 10) / 10).toFixed(1)}kg`;
export const won = (n) => `${Math.round(n).toLocaleString('ko-KR')}원`;

// Live region for short announcements inside a minigame.
export function announce(text) {
  const live = document.getElementById('live');
  if (live) { live.textContent = ''; requestAnimationFrame(() => { live.textContent = text; }); }
}

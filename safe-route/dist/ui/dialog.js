// Conversations, story cards and time-skip interstitials.
import { el, modal, icon, btn, labelTag, LABELS, uiSound } from './dom.js';

const armedAfter = (ms = 220) => { const t0 = performance.now(); return () => performance.now() - t0 > ms; };
const initial = (name) => (String(name || '·').trim()[0] || '·');

// talk({ speaker, role, portrait?, lines, choices? }) -> Promise<choiceId|null>
// Lines advance with Enter / Space / E / click; choices appear after the last line (1–9 pick directly).
// A disabled choice stays focusable and shows its reason, so it is never colour-only.
export function talk({ speaker = '', role = '', portrait = null, lines = [], choices = null } = {}) {
  const list = [].concat(lines ?? []).filter((l) => l != null && l !== '').map(String);
  if (!list.length) list.push('…');
  const opts = Array.isArray(choices) ? choices.filter(Boolean) : [];
  let i = 0, choiceArmed = () => false;

  const lineEl = el('p', { class: 'talk-line', 'aria-live': 'polite' });
  const counter = el('span', { class: 'talk-count', 'aria-hidden': 'true' });
  const next = btn('다음', { iconName: 'arrow', className: 'talk-next', 'data-autofocus': true });
  const choiceBox = el('div', { class: 'talk-choices', role: 'group', 'aria-label': '선택지', hidden: true });
  const face = portrait && portrait.nodeType
    ? el('div', { class: 'talk-portrait' }, portrait)
    : el('div', { class: 'talk-portrait mono', 'aria-hidden': 'true' }, initial(speaker));
  const box = el('div', { class: 'talk-box' }, face,
    el('div', { class: 'talk-main' },
      el('header', { class: 'talk-head' }, el('b', { class: 'talk-speaker' }, speaker), role ? el('span', { class: 'talk-role' }, role) : null, counter),
      lineEl, choiceBox,
      el('div', { class: 'talk-foot' }, el('span', { class: 'talk-hint', 'aria-hidden': 'true' }, el('kbd', {}, 'Enter'), ' 또는 ', el('kbd', {}, 'E'), ' 계속'), next)));

  const buttons = opts.map((c, n) => {
    const reason = c.disabled ? (c.reason || '지금은 고를 수 없습니다.') : '';
    const b = el('button', { type: 'button', class: `choice${c.disabled ? ' is-disabled' : ''}`, 'aria-disabled': c.disabled ? 'true' : null, 'data-id': c.id },
      el('span', { class: 'choice-no', 'aria-hidden': 'true' }, n < 9 ? String(n + 1) : '·'),
      el('span', { class: 'choice-text' }, el('span', { class: 'choice-label' }, c.label ?? c.id),
        c.note ? el('small', { class: 'choice-note' }, c.note) : null,
        reason ? el('small', { class: 'choice-reason' }, icon('lock'), reason) : null));
    b.addEventListener('click', () => pick(c, b));
    return b;
  });
  choiceBox.append(...buttons);

  const m = modal({ className: 'talk', label: speaker ? `대화 · ${speaker}` : '대화', content: box, backdrop: false, onKey });

  function render() {
    lineEl.textContent = list[i];
    lineEl.classList.remove('enter'); void lineEl.offsetWidth; lineEl.classList.add('enter');
    counter.textContent = list.length > 1 ? `${i + 1} / ${list.length}` : '';
    const last = i === list.length - 1;
    if (last && opts.length) {
      choiceBox.hidden = false; next.hidden = true;
      box.querySelector('.talk-hint').hidden = true;
      choiceArmed = armedAfter(260);
      (buttons.find((b) => !b.hasAttribute('aria-disabled')) || buttons[0]).focus({ preventScroll: true });
    } else {
      next.querySelector('span').textContent = last ? '닫기' : '다음';
      next.focus({ preventScroll: true });
    }
  }
  function advance() {
    if (!choiceBox.hidden) return;
    if (i < list.length - 1) { i++; uiSound('tick'); render(); } else m.close(null);
  }
  function pick(c, b) {
    if (!choiceArmed()) return;
    if (c.disabled) { b.classList.remove('nope'); void b.offsetWidth; b.classList.add('nope'); uiSound('error'); return; }
    uiSound('confirm');
    m.close(c.id);
  }
  function onKey(e) {
    if (e.code === 'KeyE' && !e.repeat) { e.preventDefault(); advance(); return; }
    if (e.key === 'Escape') {
      e.preventDefault();
      if (!opts.length) m.close(null);
      else if (choiceBox.hidden) { i = list.length - 1; render(); }
      return;
    }
    if (choiceBox.hidden) return;
    const n = Number(e.key);
    if (n >= 1 && n <= Math.min(9, buttons.length)) { e.preventDefault(); buttons[n - 1].click(); return; }
    if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
      e.preventDefault();
      const at = buttons.indexOf(document.activeElement);
      buttons[(at + (e.key === 'ArrowDown' ? 1 : buttons.length - 1)) % buttons.length].focus();
    }
  }
  next.addEventListener('click', advance);
  lineEl.addEventListener('click', advance);
  render();
  return m.done;
}

// card({ kind, title, meta, body, actions }) -> Promise<actionId>
const KIND = {
  alert: ['alert', '경보'], news: ['news', '뉴스'], notice: ['notice', '안내문'], info: ['info', '알아 둘 것'], sms: ['sms', '문자'],
};
export function card({ kind = 'info', title = '', meta = '', body = [], actions = null } = {}) {
  const [ic, kindLabel] = KIND[kind] || KIND.info;
  const acts = (actions?.length ? actions : [{ id: 'ok', label: '확인', primary: true }]).map((a) => (typeof a === 'string' ? { id: a, label: a } : a));
  const armed = armedAfter();
  const rows = [].concat(body ?? []).filter((b) => b != null && b !== '').map((b) => {
    if (typeof b === 'object' && !b.nodeType) {
      return el('div', { class: 'card-row' }, LABELS[b.label] ? labelTag(b.label) : el('b', { class: 'card-row-label' }, b.label ?? ''), el('p', {}, b.text ?? ''));
    }
    return b.nodeType ? b : el('p', {}, String(b));
  });
  const hasPrimary = acts.some((a) => a.primary);
  const buttons = acts.map((a, n) => btn(a.label ?? a.id, { primary: a.primary, ghost: !a.primary && hasPrimary, 'data-autofocus': (a.primary || (!hasPrimary && n === 0)) || null,
    onclick: () => { if (armed()) { uiSound('confirm'); m.close(a.id); } } }));
  const titleId = `card-title-${Math.random().toString(36).slice(2, 8)}`;
  const content = el('article', { class: `story-card card-${kind}` },
    el('header', { class: 'card-head' },
      el('span', { class: 'card-kind' }, icon(ic), kindLabel),
      meta ? el('span', { class: 'card-meta' }, meta) : null),
    el('h2', { class: 'card-title', id: titleId }, title),
    el('div', { class: 'card-body' }, rows),
    el('footer', { class: 'card-actions' }, buttons));
  const m = modal({ className: `card card-${kind}`, label: title || kindLabel, content,
    onKey(e) { if (e.key === 'Escape' && acts.length === 1) { e.preventDefault(); m.close(acts[0].id); } } });
  m.root.setAttribute('aria-labelledby', titleId);
  if (kind === 'alert') uiSound('alert');
  return m.done;
}

// timeSkip({ from, to, text }) -> Promise. Full-screen interstitial; continues on any key/click or after a
// reading-time based delay (at least 2.5 s).
export function timeSkip({ from = '', to = '', text = '' } = {}) {
  const ms = Math.max(2500, Math.min(7500, 1400 + String(text).length * 55));
  const cont = btn('계속', { iconName: 'arrow', ghost: true, className: 'skip-continue', 'data-autofocus': true });
  const content = el('div', { class: 'skip-inner', style: { '--skip-ms': `${ms}ms` } },
    el('p', { class: 'skip-times' },
      el('span', { class: 'skip-from' }, from), el('span', { class: 'skip-arrow', 'aria-hidden': 'true' }, icon('hourglass')), el('span', { class: 'skip-to' }, to)),
    el('p', { class: 'skip-text' }, text),
    el('div', { class: 'skip-progress', 'aria-hidden': 'true' }, el('span', {})),
    cont);
  const armed = armedAfter(400);
  const m = modal({ className: 'timeskip', label: `${from}에서 ${to}로 시간이 흐릅니다`, content,
    onKey(e) { if (e.key !== 'Tab' && !e.repeat && armed() && !['Shift', 'Control', 'Alt', 'Meta'].includes(e.key)) { e.preventDefault(); m.close(); } } });
  m.layer.addEventListener('click', () => { if (armed()) m.close(); });
  const timer = setTimeout(() => m.close(), ms);
  return m.done.then(() => clearTimeout(timer));
}

// Tiny DOM helpers shared by every UI module: el, trapFocus, modal, escapeText.
// Extras (additive): icon(name), labelTag(label), LABELS, uiSound(name).

const SVG_NS = 'http://www.w3.org/2000/svg';

export const escapeText = (s) => String(s ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

// el('button', { class: 'btn', onclick, disabled: true, 'aria-label': '닫기' }, '닫기', otherNode, [more])
// Text children are always text nodes (never parsed as HTML).
export function el(tag, attrs = {}, ...children) {
  const node = document.createElement(tag);
  if (attrs == null || typeof attrs !== 'object' || attrs.nodeType || Array.isArray(attrs)) { children.unshift(attrs); attrs = {}; }
  for (const [k, v] of Object.entries(attrs)) {
    if (v == null || v === false) continue;
    if (k === 'class' || k === 'className') node.className = v;
    else if (k === 'style' && typeof v === 'object') Object.assign(node.style, v);
    else if (k === 'dataset') Object.assign(node.dataset, v);
    else if (k.startsWith('on') && typeof v === 'function') node.addEventListener(k.slice(2).toLowerCase(), v);
    else if (k === 'text') node.textContent = v;
    else if (k === 'value' && 'value' in node) node.value = v;
    else node.setAttribute(k, v === true ? '' : v);
  }
  append(node, children);
  return node;
}
function append(node, kids) {
  for (const kid of kids.flat(Infinity)) {
    if (kid == null || kid === false || kid === true) continue;
    node.append(kid.nodeType ? kid : document.createTextNode(String(kid)));
  }
}

const FOCUSABLE = 'a[href],button:not([disabled]),input:not([disabled]),select:not([disabled]),textarea:not([disabled]),[tabindex]:not([tabindex="-1"])';
const focusables = (root) => [...root.querySelectorAll(FOCUSABLE)].filter((n) => !n.closest('[hidden],[inert]') && n.getClientRects().length);

// Keeps Tab / Shift+Tab inside root. Returns a release function.
export function trapFocus(root) {
  const onKey = (e) => {
    if (e.key !== 'Tab') return;
    const list = focusables(root);
    if (!list.length) { e.preventDefault(); root.focus(); return; }
    const first = list[0], last = list.at(-1), at = document.activeElement;
    if (e.shiftKey && (at === first || !root.contains(at))) { e.preventDefault(); last.focus(); }
    else if (!e.shiftKey && (at === last || !root.contains(at))) { e.preventDefault(); first.focus(); }
  };
  const onFocusIn = (e) => { if (!root.contains(e.target) && root.isConnected && isTop(root)) (focusables(root)[0] || root).focus(); };
  root.addEventListener('keydown', onKey);
  document.addEventListener('focusin', onFocusIn);
  return () => { root.removeEventListener('keydown', onKey); document.removeEventListener('focusin', onFocusIn); };
}
const stack = [];
const isTop = (root) => !stack.length || stack.at(-1) === root;

// modal({ className, label, content, onKey }) -> { root, close(result), done }
// root is the dialog element (role=dialog). onKey(e, api) sees every keydown inside; keys never reach the
// game controls behind the modal. Focus goes to [data-autofocus] or the first focusable; it is restored on close.
export function modal({ className = '', label = '', content = null, onKey = null, backdrop = true } = {}) {
  const prev = document.activeElement;
  const root = el('div', { class: `modal ${className}`.trim(), role: 'dialog', 'aria-modal': 'true', 'aria-label': label || null, tabindex: '-1' });
  const layer = el('div', { class: ['modal-layer', backdrop ? '' : 'no-backdrop', ...className.split(/\s+/).filter(Boolean).map((c) => `${c}-layer`)].join(' ').trim() }, root);
  if (content != null) append(root, [typeof content === 'function' ? content(root) : content]);
  let resolve, closed = false;
  const done = new Promise((r) => { resolve = r; });
  const release = trapFocus(root);
  const api = {
    root, layer, done,
    close(result) {
      if (closed) return; closed = true;
      release(); root.removeEventListener('keydown', keyHandler);
      const i = stack.indexOf(root); if (i >= 0) stack.splice(i, 1);
      layer.remove();
      uiSound('close');
      const back = prev && prev.isConnected && prev !== document.body ? prev : (stack.at(-1) || document.getElementById('world'));
      back?.focus?.({ preventScroll: true });
      resolve(result);
    },
  };
  const keyHandler = (e) => {
    e.stopPropagation();                                            // the engine listens on window
    if (e.repeat && (e.key === 'Enter' || e.key === ' ')) { e.preventDefault(); return; }
    onKey?.(e, api);
  };
  root.addEventListener('keydown', keyHandler);
  (document.getElementById('overlay') || document.body).append(layer);
  stack.push(root);
  uiSound('open');
  requestAnimationFrame(() => { if (!closed && !root.contains(document.activeElement)) focusFirst(root); });
  focusFirst(root);
  return api;
}
export function focusFirst(root) {
  const target = root.querySelector('[data-autofocus]:not([disabled])') || focusables(root)[0] || root;
  target.focus({ preventScroll: true });
}

// Info labels (text + icon, never colour alone).
export const LABELS = {
  official: { text: '공식 안내', icon: 'official' },
  site: { text: '현장 확인', icon: 'site' },
  neighbor: { text: '이웃에게 들음', icon: 'neighbor' },
  rumor: { text: '소문·미확인', icon: 'rumor' },
  self: { text: '내 기록', icon: 'self' },
};
export function labelTag(label) {
  const L = LABELS[label] || LABELS.self;
  return el('span', { class: `tag tag-${LABELS[label] ? label : 'self'}` }, icon(L.icon), L.text);
}

// Stroke icons, 24x24. icon(name, { label }) -> SVG (aria-hidden unless labelled).
const P = {
  heart: 'M12 20s-7-4.4-7-10a4 4 0 0 1 7-2.6A4 4 0 0 1 19 10c0 5.6-7 10-7 10z',
  drop: 'M12 3s6 6.5 6 11a6 6 0 0 1-12 0c0-4.5 6-11 6-11z',
  moon: 'M20 14.5A8 8 0 1 1 9.5 4a6.5 6.5 0 0 0 10.5 10.5z',
  pulse: 'M3 12h4l2-5 4 10 2-5h6',
  battery: 'M3 8h14a1 1 0 0 1 1 1v6a1 1 0 0 1-1 1H3a1 1 0 0 1-1-1V9a1 1 0 0 1 1-1zM21 11v2',
  bag: 'M8 8V6a4 4 0 0 1 8 0v2M5 10a2 2 0 0 1 2-2h10a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2zM9 14h6',
  clock: 'M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0zM12 7v5l3 2',
  check: 'M5 12.5l4.5 4.5L19 7.5',
  circle: 'M17 12a5 5 0 1 1-10 0 5 5 0 0 1 10 0z',
  official: 'M12 3l8 3v6c0 4.5-3.4 8.3-8 9-4.6-.7-8-4.5-8-9V6zM8.5 12l2.5 2.5 4.5-5',
  site: 'M2 12s3.6-7 10-7 10 7 10 7-3.6 7-10 7S2 12 2 12zM15 12a3 3 0 1 1-6 0 3 3 0 0 1 6 0z',
  neighbor: 'M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8zM2 21v-1a6 6 0 0 1 12 0v1M16 3.5a4 4 0 0 1 0 7.5M22 21v-1a6 6 0 0 0-4-5.6',
  rumor: 'M21 12a8 8 0 0 1-11.6 7.1L4 20l1.1-4.6A8 8 0 1 1 21 12zM9.8 9.5a2.3 2.3 0 0 1 4.4.8c0 1.5-2.2 2-2.2 2M12 15.5h.01',
  self: 'M4 20h4L19 9l-4-4L4 16zM13.5 6.5l4 4',
  alert: 'M12 3l10 18H2zM12 10v5M12 18h.01',
  info: 'M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0zM12 11v6M12 7.5h.01',
  news: 'M4 5h13v14H6a2 2 0 0 1-2-2zM17 9h3v8a2 2 0 0 1-2 2M8 9h5M8 13h5M8 16h3',
  notice: 'M9 3h6v3H9zM7 4.5H5V21h14V4.5h-2M8 11h8M8 15h6',
  sms: 'M4 5h16v11H9l-5 4z',
  signal: 'M4 20v-3M9 20v-7M14 20V9M19 20V5',
  map: 'M9 4L3 6v14l6-2 6 2 6-2V4l-6 2zM9 4v14M15 6v14',
  calendar: 'M4 6h16v15H4zM4 10h16M8 3v4M16 3v4',
  book: 'M5 4h11a3 3 0 0 1 3 3v13H8a3 3 0 0 1-3-3zM5 17a3 3 0 0 1 3-3h11',
  home: 'M3 11l9-7 9 7M5 10v10h14V10',
  play: 'M7 4l13 8-13 8z',
  sliders: 'M4 6h9M17 6h3M15 4v4M4 12h3M11 12h9M9 10v4M4 18h11M19 18h1M17 16v4',
  save: 'M5 4h11l3 3v13H5zM8 4v5h7M8 20v-6h8v6',
  exit: 'M14 4h5v16h-5M10 8l-4 4 4 4M6 12h10',
  retry: 'M20 11a8 8 0 1 0-2.3 5.7M20 4v7h-7',
  sound: 'M4 9h4l5-4v14l-5-4H4zM16.5 8.5a5 5 0 0 1 0 7',
  print: 'M7 9V3h10v6M7 17H4v-7h16v7h-3M7 14h10v7H7z',
  close: 'M6 6l12 12M18 6L6 18',
  arrow: 'M5 12h14M13 6l6 6-6 6',
  lock: 'M6 11h12v10H6zM8 11V8a4 4 0 0 1 8 0v3',
  spark: 'M12 3l2.2 6.8L21 12l-6.8 2.2L12 21l-2.2-6.8L3 12l6.8-2.2z',
  siren: 'M7 18v-6a5 5 0 0 1 10 0v6M4 18h16v3H4zM12 3v2M4.5 6.5L6 8M19.5 6.5L18 8',
  hourglass: 'M6 3h12M6 21h12M7 3c0 5 10 5 10 9s-10 4-10 9M17 3c0 5-10 5-10 9',
  people: 'M8 11a3.5 3.5 0 1 0 0-7 3.5 3.5 0 0 0 0 7zM16.5 11a3 3 0 1 0 0-6M2 20a6 6 0 0 1 12 0M15 14.5a5 5 0 0 1 7 4.5',
  box: 'M3 7l9-4 9 4v10l-9 4-9-4zM3 7l9 4 9-4M12 11v10',
  eye: 'M2 12s3.6-7 10-7 10 7 10 7-3.6 7-10 7S2 12 2 12zM15 12a3 3 0 1 1-6 0 3 3 0 0 1 6 0z',
  flame: 'M12 21a6 6 0 0 0 6-6c0-4-3-6-4-10-2 2-3 4-3 6-1-1-1.5-2-1.5-3C7 10 6 12.5 6 15a6 6 0 0 0 6 6z',
};
export function icon(name, { label = '', className = '' } = {}) {
  const svg = document.createElementNS(SVG_NS, 'svg');
  svg.setAttribute('viewBox', '0 0 24 24');
  svg.setAttribute('class', `icon ${className}`.trim());
  if (label) { svg.setAttribute('role', 'img'); svg.setAttribute('aria-label', label); } else svg.setAttribute('aria-hidden', 'true');
  const path = document.createElementNS(SVG_NS, 'path');
  path.setAttribute('d', P[name] || P.circle);
  svg.append(path);
  return svg;
}

// UI sounds without a reference to the audio object: audio.js listens for this event.
export function uiSound(name) { try { dispatchEvent(new CustomEvent('gyeongbo:ui', { detail: name })); } catch { /* no window */ } }

// Shared button factory: btn('확인', { primary, icon, onclick, ... })
export function btn(label, { primary = false, ghost = false, iconName = null, className = '', ...attrs } = {}) {
  return el('button', { type: 'button', class: ['btn', primary ? 'primary' : '', ghost ? 'ghost' : '', className].filter(Boolean).join(' '), ...attrs }, iconName ? icon(iconName) : null, el('span', {}, label));
}

export const reduceMotion = () => document.documentElement.classList.contains('reduce-motion') || matchMedia('(prefers-reduced-motion: reduce)').matches;

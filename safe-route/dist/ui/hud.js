// In-world HUD: chapter + clock, objective card, stats, phone battery, bag load, interact prompt,
// toasts and captions (aria-live), tension vignette and cinematic letterbox.
import { el, icon, labelTag } from './dom.js';

const STATS = [
  // key, label, icon, higherIsWorse
  ['health', '체력', 'heart', false],
  ['thirst', '갈증', 'drop', true],
  ['fatigue', '피로', 'moon', true],
  ['tension', '긴장', 'pulse', true],
];
const condition = (good) => (good >= 67 ? ['좋음', 'good'] : good >= 34 ? ['보통', 'mid'] : ['나쁨', 'bad']);
const clamp = (v) => Math.max(0, Math.min(100, Number(v) || 0));

export function createHud(root) {
  const $ = (cls, tag = 'div', attrs = {}) => el(tag, { class: cls, ...attrs });

  // Top-left: chapter, clock, objective.
  const chapterNo = $('hud-chapter-no', 'span');
  const chapterTitle = $('hud-chapter-title', 'span');
  const clockText = $('hud-clock-text', 'span');
  const objTitle = $('obj-title', 'h2');
  const objBody = $('obj-body', 'p');
  const objSteps = $('obj-steps', 'ol');
  const objective = el('section', { class: 'hud-objective panel', 'aria-label': '현재 목표', hidden: true },
    el('p', { class: 'obj-eyebrow' }, icon('arrow'), '목표'), objTitle, objBody, objSteps);
  const topLeft = el('div', { class: 'hud-tl' },
    el('div', { class: 'hud-chapter' }, chapterNo, chapterTitle),
    el('div', { class: 'hud-clock', 'aria-label': '게임 속 시각' }, icon('clock'), clockText),
    objective);

  // Top-right: battery and bag.
  const battFill = $('batt-fill', 'span');
  const battText = $('batt-text', 'span');
  const battery = el('div', { class: 'hud-chip hud-battery', hidden: true, role: 'img' },
    el('span', { class: 'batt', 'aria-hidden': 'true' }, battFill), battText);
  const bagFill = $('meter-fill', 'span');
  const bagText = $('bag-text', 'span');
  const bagChip = el('div', { class: 'hud-chip hud-bag', hidden: true, role: 'img' },
    icon('bag'), el('span', { class: 'meter', 'aria-hidden': 'true' }, bagFill), bagText);
  const topRight = el('div', { class: 'hud-tr' }, battery, bagChip);

  // Bottom-left: stats.
  const statEls = {};
  const stats = el('section', { class: 'hud-stats panel', 'aria-label': '몸 상태' }, STATS.map(([key, label, ic]) => {
    const fill = $('stat-fill', 'span'), value = $('stat-word', 'span');
    const row = el('div', { class: 'stat', 'data-stat': key }, icon(ic), el('span', { class: 'stat-label' }, label),
      el('span', { class: 'stat-bar', 'aria-hidden': 'true' }, fill), value);
    statEls[key] = { row, fill, value };
    return row;
  }));

  const promptText = $('prompt-text', 'span');
  const prompt = el('div', { class: 'hud-prompt', hidden: true }, el('kbd', {}, 'E'), promptText);
  const keys = el('p', { class: 'hud-keys' }, ...[['WASD', '이동'], ['Shift', '달리기'], ['E', '살피기'], ['F', '불빛'], ['Tab', '수첩'], ['I', '소지품'], ['M', '지도'], ['V', '시점'], ['Esc', '메뉴']]
    .map(([k, t]) => el('span', {}, el('kbd', {}, k), t)));

  const vignette = $('hud-vignette', 'div', { 'aria-hidden': 'true' });
  const banner = $('hud-banner', 'div', { 'aria-hidden': 'true' });
  const bars = [$('letterbox top', 'div', { 'aria-hidden': 'true' }), $('letterbox bottom', 'div', { 'aria-hidden': 'true' })];

  const hud = el('div', { class: 'hud', hidden: true }, vignette, ...bars, banner, topLeft, topRight, stats, prompt, keys);
  root.append(hud);

  // Toasts and captions float above modals, so they live directly on <body>.
  const toasts = el('div', { class: 'toasts', role: 'status', 'aria-live': 'polite' });
  const captionBox = el('div', { class: 'captions', 'aria-live': 'polite' });
  document.body.append(el('div', { class: 'hud-float' }, toasts, captionBox));

  let lastChapter = '', lastObjective = '', captionDone = null, bannerTimer = 0;

  return {
    show() { hud.hidden = false; },
    hide() { hud.hidden = true; prompt.hidden = true; hud.classList.remove('cinematic'); },

    setChapter(label, title) {
      chapterNo.textContent = label || '';
      chapterTitle.textContent = title || '';
      const key = `${label}|${title}`;
      if (key === lastChapter) return;
      lastChapter = key;
      banner.replaceChildren(el('span', { class: 'banner-no' }, label || ''), el('span', { class: 'banner-title' }, title || ''));
      banner.classList.remove('show'); void banner.offsetWidth; banner.classList.add('show');
      clearTimeout(bannerTimer); bannerTimer = setTimeout(() => banner.classList.remove('show'), 3600);
    },

    setClock(text) { clockText.textContent = text || ''; },

    setObjective(title, body = '', steps = []) {
      if (!title) { objective.hidden = true; lastObjective = ''; return; }
      objective.hidden = false;
      objTitle.textContent = title;
      objBody.textContent = body || '';
      objBody.hidden = !body;
      objSteps.replaceChildren(...(steps || []).map((s) => el('li', { class: s.done ? 'done' : '' },
        icon(s.done ? 'check' : 'circle'), el('span', {}, s.text), el('span', { class: 'sr-only' }, s.done ? ' (완료)' : ' (남음)'))));
      objSteps.hidden = !steps?.length;
      if (title !== lastObjective) { objective.classList.remove('flash'); void objective.offsetWidth; objective.classList.add('flash'); }
      lastObjective = title;
    },

    setStats(s = {}) {
      for (const [key, label, , worse] of STATS) {
        if (s[key] == null) continue;
        const v = clamp(s[key]), good = worse ? 100 - v : v;
        const [word, state] = condition(good);
        const { row, fill, value } = statEls[key];
        row.dataset.state = state;
        fill.style.width = `${good}%`;
        value.textContent = word;
        row.title = `${label} ${Math.round(v)} · ${word}`;
      }
    },

    setBattery(pct) {
      if (pct == null) { battery.hidden = true; return; }
      const p = clamp(pct);
      battery.hidden = false;
      battery.classList.toggle('low', p <= 20);
      battery.classList.toggle('dead', p <= 0);
      battFill.style.width = `${p}%`;
      battText.textContent = p <= 0 ? '꺼짐' : `${Math.round(p)}%${p <= 20 ? ' 부족' : ''}`;
      battery.setAttribute('aria-label', `휴대폰 배터리 ${Math.round(p)}%`);
    },

    setBag(load) {
      if (!load) { bagChip.hidden = true; return; }
      const w = Number(load.weightKg) || 0, lim = Number(load.limitKg) || 8, over = w > lim;
      bagChip.hidden = false;
      bagChip.classList.toggle('over', over);
      bagFill.style.width = `${Math.min(100, (w / lim) * 100)}%`;
      bagText.textContent = `${w.toFixed(1)} / ${lim}kg${over ? ' 무거움' : ''}`;
      bagChip.setAttribute('aria-label', `배낭 무게 ${w.toFixed(1)}kg, 기준 ${lim}kg${over ? ', 무거움' : ''}`);
    },

    prompt(text) {
      prompt.hidden = !text;
      if (text) promptText.textContent = text;
    },

    toast(text, { label = 'self', ms = 3800 } = {}) {
      const t = el('div', { class: `toast toast-${label}` }, labelTag(label), el('span', { class: 'toast-text' }, text));
      toasts.append(t);
      while (toasts.children.length > 4) toasts.firstChild.remove();
      setTimeout(() => { t.classList.add('out'); setTimeout(() => t.remove(), 400); }, ms);
    },

    caption(text, { speaker = '', ms } = {}) {
      captionDone?.();
      const dur = ms ?? Math.max(2600, Math.min(9000, 900 + String(text).length * 85));
      const c = el('div', { class: 'caption' }, speaker ? el('b', { class: 'caption-speaker' }, speaker) : null, el('span', {}, text));
      captionBox.replaceChildren(c);
      return new Promise((resolve) => {
        const timer = setTimeout(finish, dur);
        function finish() { clearTimeout(timer); if (captionDone === finish) captionDone = null; c.classList.add('out'); setTimeout(() => c.remove(), 350); resolve(); }
        captionDone = finish;
      });
    },

    setTension(level) {
      const k = Math.max(0, Math.min(1, Number(level) || 0));
      vignette.style.setProperty('--tension', k.toFixed(3));
      vignette.classList.toggle('pulse', k > 0.3);
    },

    setCinematic(on) {
      hud.classList.toggle('cinematic', !!on);
      if (on) prompt.hidden = true;
    },
  };
}

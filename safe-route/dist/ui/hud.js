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
  const keys = el('p', { class: 'hud-keys' }, ...[['WASD', '이동'], ['Shift', '달리기'], ['Space', '점프'], ['E', '살피기'], ['F', '불빛'], ['P', '휴대폰'], ['Tab', '수첩'], ['I', '소지품'], ['M', '지도'], ['V', '시점'], ['H', '조작법'], ['Esc', '메뉴']]
    .map(([k, t]) => el('span', {}, el('kbd', {}, k), t)));

  // Compass strip (top centre) and minimap (top right). Yaw 0 faces north (-z); positive yaw turns west.
  const compassCv = el('canvas', { class: 'hud-compass', width: 360, height: 34, 'aria-hidden': 'true' });
  const mapCv = el('canvas', { class: 'hud-minimap', width: 184, height: 184, 'aria-hidden': 'true' });
  const heading = el('span', { class: 'sr-only' });
  const mapBox = el('div', { class: 'hud-map', hidden: true }, mapCv, heading);
  const staminaFill = $('stamina-fill', 'span');
  const stamina = el('div', { class: 'hud-stamina', hidden: true, 'aria-hidden': 'true' }, el('span', { class: 'stamina-label' }, '기력'), el('span', { class: 'stamina-bar' }, staminaFill));

  const vignette = $('hud-vignette', 'div', { 'aria-hidden': 'true' });
  const banner = $('hud-banner', 'div', { 'aria-hidden': 'true' });
  const bars = [$('letterbox top', 'div', { 'aria-hidden': 'true' }), $('letterbox bottom', 'div', { 'aria-hidden': 'true' })];

  const hud = el('div', { class: 'hud', hidden: true }, vignette, ...bars, banner, compassCv, mapBox, topLeft, topRight, stats, stamina, prompt, keys);
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

    // deg: heading clockwise from north (0 = N, 90 = E).
    setCompass(deg) {
      const g = compassCv.getContext('2d'), W = compassCv.width, H = compassCv.height, PX = 2.2; // px per degree
      g.clearRect(0, 0, W, H);
      const bg = g.createLinearGradient(0, 0, W, 0);
      bg.addColorStop(0, 'rgba(10,15,18,0)'); bg.addColorStop(0.15, 'rgba(10,15,18,.72)'); bg.addColorStop(0.85, 'rgba(10,15,18,.72)'); bg.addColorStop(1, 'rgba(10,15,18,0)');
      g.fillStyle = bg; g.fillRect(0, 0, W, H);
      g.textAlign = 'center'; g.textBaseline = 'middle';
      const NAMES = { 0: '북 N', 45: '북동', 90: '동 E', 135: '남동', 180: '남 S', 225: '남서', 270: '서 W', 315: '북서' };
      for (let d = -80; d <= 80; d++) {
        const a = Math.round(deg) + d, x = W / 2 + (a - deg) * PX, n = ((a % 360) + 360) % 360;
        if (NAMES[n]) { g.fillStyle = n === 0 ? '#ff5a4a' : n % 90 === 0 ? '#f2b33d' : '#c9d3d6'; g.font = `${n % 90 === 0 ? 800 : 600} ${n % 90 === 0 ? 14 : 11}px Pretendard, 'Noto Sans KR', sans-serif`; g.fillText(NAMES[n], x, H / 2 + 2); }
        else if (n % 15 === 0) { g.fillStyle = 'rgba(220,230,232,.55)'; g.fillRect(x - 0.5, H - 9, 1, 6); }
      }
      g.fillStyle = '#f2b33d'; g.beginPath(); g.moveTo(W / 2 - 5, 0); g.lineTo(W / 2 + 5, 0); g.lineTo(W / 2, 6); g.fill();
      const dir = ['북', '북동', '동', '남동', '남', '남서', '서', '북서'][Math.round((((deg % 360) + 360) % 360) / 45) % 8];
      heading.textContent = `${dir}쪽을 보고 있음`;
    },

    // m: { x, y, z, yaw, walk, solids, points, scale (px per metre) } or null — north up, the player at the centre.
    setMap(m) {
      if (!m) { mapBox.hidden = true; return; }
      mapBox.hidden = false;
      const g = mapCv.getContext('2d'), S = mapCv.width, C = S / 2, k = m.scale, R = C / k;
      const X = (x) => C + (x - m.x) * k, Z = (z) => C + (z - m.z) * k;
      const near = (o) => o.x1 > m.x - R && o.x0 < m.x + R && o.z1 > m.z - R && o.z0 < m.z + R;
      g.save(); g.clearRect(0, 0, S, S);
      g.beginPath(); g.arc(C, C, C - 1, 0, Math.PI * 2); g.clip();
      g.fillStyle = 'rgba(8,12,15,.82)'; g.fillRect(0, 0, S, S);
      g.fillStyle = 'rgba(150,165,170,.32)';
      for (const w of m.walk) {
        if (!near(w)) continue;
        const y0 = w.ramp ? Math.min(w.ramp.y0, w.ramp.y1) : w.y ?? 0, y1 = w.ramp ? Math.max(w.ramp.y0, w.ramp.y1) : y0;
        if (y1 < m.y - 1.6 || y0 > m.y + 1.6) continue; // this floor only
        g.fillRect(X(w.x0), Z(w.z0), (w.x1 - w.x0) * k, (w.z1 - w.z0) * k);
      }
      g.fillStyle = 'rgba(205,190,160,.62)';
      for (const s of m.solids) {
        if (!s || s.off || !near(s) || s.y1 < m.y + 0.4 || s.y0 > m.y + 2) continue;
        g.fillRect(X(s.x0), Z(s.z0), Math.max(1, (s.x1 - s.x0) * k), Math.max(1, (s.z1 - s.z0) * k));
      }
      for (const p of m.points) {
        if (!p || p.enabled === false || Math.abs((p.y ?? m.y) - m.y) > 2.2) continue;
        const px = X(p.x), pz = Z(p.z);
        if (px < -4 || px > S + 4 || pz < -4 || pz > S + 4) continue;
        const big = p.kind === 'shelter' || p.kind === 'door';
        g.fillStyle = p.kind === 'shelter' ? '#ff6b5a' : p.kind === 'door' ? '#3cc6b5' : '#f2b33d';
        g.beginPath(); g.arc(px, pz, big ? 3.6 : 2.6, 0, Math.PI * 2); g.fill();
      }
      g.translate(C, C); g.rotate(-m.yaw); // the arrow points up (north) at yaw 0
      g.fillStyle = '#ffffff'; g.strokeStyle = '#0a0f12'; g.lineWidth = 1.5;
      g.beginPath(); g.moveTo(0, -9); g.lineTo(6, 7); g.lineTo(0, 3); g.lineTo(-6, 7); g.closePath(); g.fill(); g.stroke();
      g.restore();
      g.strokeStyle = 'rgba(255,255,255,.22)'; g.lineWidth = 2; g.beginPath(); g.arc(C, C, C - 1, 0, Math.PI * 2); g.stroke();
      g.font = "800 13px Pretendard, 'Noto Sans KR', sans-serif"; g.textAlign = 'center'; g.textBaseline = 'middle';
      for (const [t, x, y, c] of [['N', C, 11, '#ff5a4a'], ['S', C, S - 11, '#e8eef0'], ['E', S - 11, C, '#e8eef0'], ['W', 11, C, '#e8eef0']]) {
        g.fillStyle = 'rgba(8,12,15,.85)'; g.beginPath(); g.arc(x, y, 9, 0, Math.PI * 2); g.fill(); g.fillStyle = c; g.fillText(t, x, y + 1);
      }
    },

    setStamina(v, winded) {
      const k = Math.max(0, Math.min(1, Number(v) || 0));
      stamina.hidden = k > 0.995 && !winded;
      staminaFill.style.width = `${k * 100}%`;
      stamina.classList.toggle('winded', !!winded);
    },

    setCinematic(on) {
      hud.classList.toggle('cinematic', !!on);
      if (on) prompt.hidden = true;
    },
  };
}

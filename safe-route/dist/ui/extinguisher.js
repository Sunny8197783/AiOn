// Extinguisher encounter: decide first (끈다 / 대피하며 알린다), then work the extinguisher by pressing the
// steps in order — press-and-hold for 손잡이, ←/→ strokes for 쓸듯이 — over a live canvas of the fire.
// Retreat is always available. Resolves { sequence, result } (result from game/drills.js evalExtinguisher).
import { el, modal, header, ladderHelp, reduced, announce } from './mini-dom.js';
import { EXT_STEPS, EXT_CHOICES, evalExtinguisher } from '../game/drills.js';
import { fact } from '../game/facts.js';

const HOLD_S = 0.7;
const OPS = ['pin', 'aim', 'squeeze', 'sweep'];
const GROW_S = { guided: 70, hint: 45, free: 32 }; // seconds until a small fire spreads
const factText = (ids = []) => ids.map(id => fact(id)?.text).filter(Boolean);
const MISTAKE = {
  squeeze: { pin: '안전핀이 꽂혀 있어 손잡이가 눌리지 않는다.', aim: '노즐이 불을 향하지 않았다. 분말이 엉뚱한 곳으로 간다.' },
  sweep: { pin: '안전핀이 그대로다. 아무것도 나오지 않는다.', aim: '노즐을 먼저 불 쪽으로 향해야 한다.', squeeze: '손잡이를 쥐지 않아 분말이 나오지 않는다.' },
  aim: { pin: '안전핀을 먼저 뽑아야 한다.' },
};

export async function extinguisher({ mode = 'guided', fire = 'small', steps = EXT_STEPS, choices = EXT_CHOICES, practice = false } = {}) {
  const large = fire === 'large';
  const S = { phase: practice ? 'act' : 'decide', fireLv: practice ? 0.3 : large ? 0.88 : 0.36, danger: 0, done: new Set(), pinOut: false, aimed: false, spraying: false, hold: 0, holding: false, sweepDir: 0, lastDir: 0, shake: 0, particles: [], ended: false };
  const seq = practice ? ['fight'] : [];
  const rm = reduced();
  const growPerS = practice ? 0 : (1 / (GROW_S[mode] || 40)) * (large ? 2.4 : 1);
  let raf = 0, last = performance.now();

  const result = () => {
    try { return evalExtinguisher(seq, { fire: large ? 'large' : 'small' }).result; }
    catch (err) { console.error('[extinguisher] could not evaluate', seq, err); return 'incomplete'; }
  };
  const finish = () => { if (S.closed) return; S.closed = true; cancelAnimationFrame(raf); m.close({ sequence: [...seq], result: result() }); };
  const m = modal({ className: 'mg-ext', label: '소화기', onKey });

  // --- layout ---
  const cv = el('canvas', { width: 640, height: 360, class: 'mg-ext-canvas', role: 'img' });
  const feedback = el('p', { class: 'mg-feedback', 'aria-live': 'polite' });
  const dangerBar = el('span', { class: 'mg-bar-fill' }), dangerText = el('b');
  const danger = el('div', { class: 'mg-meter mg-danger', hidden: practice }, el('small', {}, el('span', { 'aria-hidden': 'true' }, '▲ '), large ? '버틸 수 있는 시간' : '불이 번지기까지'), dangerText, el('span', { class: 'mg-bar', 'aria-hidden': 'true' }, dangerBar));
  const actions = el('div', { class: 'mg-ext-actions' });
  const helpBox = el('div', { class: 'mg-ext-help' });
  const retreatBtn = el('button', { type: 'button', class: 'mg-btn mg-retreat' }, el('span', { 'aria-hidden': 'true' }, '⤺ '), practice ? '연습 그만하기' : '물러나 알린다 (계단 · 119)');
  retreatBtn.addEventListener('click', () => (practice ? finish() : retreat()));

  m.root.append(
    header(practice ? '소화기 · 연습' : '소화기', practice ? '사용 순서 익히기' : large ? '천장까지 닿는 불길' : '작은 불', mode, 'mg-ext-title'),
    practice ? el('p', { class: 'mg-why' }, '연습입니다. 실제 불은 없습니다.') : null,
    el('div', { class: 'mg-ext-grid' },
      el('div', { class: 'mg-ext-stage' }, cv, danger),
      el('div', { class: 'mg-ext-side' }, helpBox, actions, feedback, retreatBtn)),
    el('p', { class: 'mg-keys' }, 'Tab으로 고르기 · Enter/Space 누르기 · 손잡이는 길게 누르기 · 쓸 때는 ←/→ 번갈아 · Esc 물러나기'));
  m.root.setAttribute('aria-labelledby', 'mg-ext-title');

  // --- decision ---
  function renderDecide() {
    const rec = large ? 'retreat' : 'fight';
    actions.replaceChildren(el('p', { class: 'mg-q' }, '어떻게 할까?'), ...choices.filter(c => c.id === 'fight').map(c => {
      const b = el('button', { type: 'button', class: `mg-btn mg-choice${mode === 'guided' && c.id === rec ? ' is-next' : ''}`, 'data-autofocus': true },
        el('b', {}, c.label), mode === 'guided' ? factText(c.facts).map(t => el('small', {}, t)) : null);
      b.addEventListener('click', () => { seq.push('fight'); S.phase = 'act'; renderAct(); say('소화기를 들고 불 앞에 섰다.'); });
      return b;
    }));
    const r = choices.find(c => c.id === 'retreat');
    if (mode === 'guided' && r) retreatBtn.replaceChildren(el('span', { 'aria-hidden': 'true' }, '⤺ '), el('b', {}, r.label), ...factText(r.facts).map(t => el('small', {}, t)));
    retreatBtn.classList.toggle('is-next', mode === 'guided' && rec === 'retreat');
    helpBox.replaceChildren(ladderHelp(mode === 'guided' ? 'free' : mode, () => choices.map(c => el('p', {}, el('b', {}, `${c.label}: `), factText(c.facts).join(' ')))) || '');
  }

  // --- acting ---
  const order = [...steps].sort((a, b) => hash(a.id) - hash(b.id));
  const btns = new Map();
  function renderAct() {
    retreatBtn.classList.remove('is-next');
    if (!practice) retreatBtn.replaceChildren(el('span', { 'aria-hidden': 'true' }, '⤺ '), '물러나 알린다 (계단 · 119)');
    actions.replaceChildren(el('p', { class: 'mg-q' }, '순서대로 누르세요'), el('div', { class: 'mg-steps' }, order.map((s) => {
      const b = el('button', { type: 'button', class: 'mg-btn mg-step-btn', 'data-step': s.id }, el('span', { class: 'mg-check', 'aria-hidden': 'true' }, '○'), el('span', {}, s.label),
        s.id === 'squeeze' ? el('small', { class: 'mg-holdtip' }, '길게 누르기') : s.id === 'sweep' ? el('small', { class: 'mg-holdtip' }, '←/→ 번갈아') : null);
      btns.set(s.id, b);
      if (s.id === 'squeeze') {
        b.addEventListener('pointerdown', (e) => { b.setPointerCapture?.(e.pointerId); startHold(); });
        b.addEventListener('pointerup', stopHold); b.addEventListener('pointercancel', stopHold); b.addEventListener('lostpointercapture', stopHold);
        b.addEventListener('keydown', (e) => { if ((e.key === ' ' || e.key === 'Enter') && !e.repeat) { e.preventDefault(); startHold(); } else if (e.key === ' ' || e.key === 'Enter') e.preventDefault(); });
        b.addEventListener('keyup', (e) => { if (e.key === ' ' || e.key === 'Enter') { e.preventDefault(); stopHold(); } });
        b.addEventListener('click', (e) => e.preventDefault());
      } else b.addEventListener('click', () => press(s.id));
      return b;
    })));
    btns.get(order[0].id)?.focus();
    refreshSteps();
  }

  const nextOp = () => OPS.find(id => !S.done.has(id));
  const expected = () => (!S.done.has('shout') && !S.done.size ? 'shout' : nextOp());
  function refreshSteps() {
    for (const [id, b] of btns) {
      const done = S.done.has(id);
      b.classList.toggle('is-done', done); b.classList.toggle('is-next', mode === 'guided' && !done && id === expected());
      b.querySelector('.mg-check').textContent = done ? '✓' : '○';
      b.setAttribute('aria-label', `${steps.find(s => s.id === id)?.label}${done ? ' (완료)' : ''}${id === 'squeeze' && !done ? ', 길게 누르기' : ''}${id === 'sweep' && S.done.has('squeeze') ? ', ←/→로 쓸기' : ''}`);
    }
    const nx = steps.find(s => s.id === expected());
    const make = () => nx ? [el('p', {}, el('b', {}, '다음: '), nx.label), ...factText(nx.facts).map(t => el('p', { class: 'mg-why' }, t))] : [];
    if (mode === 'guided') helpBox.replaceChildren(el('div', { class: 'mg-help', role: 'note' }, make()));
    else if (mode === 'hint') {
      const hb = el('button', { type: 'button', class: 'mg-btn mg-ghost' }, '힌트: 다음 순서');
      hb.addEventListener('click', () => hb.replaceWith(el('div', { class: 'mg-help', role: 'note' }, make())));
      helpBox.replaceChildren(hb);
    } else helpBox.replaceChildren();
  }

  function press(id) {
    if (S.phase !== 'act' || S.ended) return;
    if (id === 'shout') {
      if (S.done.has('shout')) return say('이미 알렸다.');
      S.done.add('shout'); seq.push('shout'); refreshSteps();
      return say('"불이야!" 소리에 이웃들이 문을 열고 내다본다.');
    }
    if (S.done.has(id) && id !== 'sweep') return say('이미 했다.');
    if (id === 'sweep' && S.done.has('squeeze')) return stroke(S.lastDir === 1 ? -1 : 1);
    if (id !== nextOp()) return mistake(MISTAKE[id]?.[nextOp()] || '순서가 맞지 않는다.');
    S.done.add(id); seq.push(id);
    if (id === 'pin') { S.pinOut = true; say('안전핀을 뽑았다.'); }
    if (id === 'aim') { S.aimed = true; say(large ? '노즐을 불 쪽으로 향했다. 열기가 얼굴까지 닿는다.' : '문을 등지고 노즐을 불 쪽으로 향했다.'); }
    refreshSteps();
  }
  function startHold() {
    if (S.phase !== 'act' || S.ended || S.done.has('squeeze')) return;
    if (nextOp() !== 'squeeze') return mistake(MISTAKE.squeeze[nextOp()] || '순서가 맞지 않는다.');
    S.holding = true;
  }
  function stopHold() {
    if (!S.holding) return;
    S.holding = false;
    if (!S.done.has('squeeze')) { S.hold = 0; btns.get('squeeze')?.style.setProperty('--p', '0'); say('손잡이를 끝까지 힘껏 쥐어야 한다. 길게 누르세요.'); }
  }
  function squeezed() {
    S.holding = false; S.done.add('squeeze'); seq.push('squeeze'); S.spraying = true;
    say('손잡이를 움켜쥐자 하얀 분말이 뿜어져 나온다. ←/→로 쓸듯이!');
    refreshSteps(); btns.get('sweep')?.focus();
  }
  function stroke(dir) {
    if (!S.done.has('squeeze') || S.ended) return;
    const alt = dir !== S.lastDir;
    S.lastDir = dir; S.sweepDir = dir;
    S.fireLv = Math.max(0, S.fireLv - (large ? 0.012 : alt ? 0.085 : 0.03));
    if (!alt && !large) say('한쪽으로만 뿌리고 있다. 좌우로 쓸듯이.');
    if (!large && S.fireLv <= 0.02) { S.fireLv = 0; seq.push('sweep'); S.done.add('sweep'); end('out'); }
  }
  function mistake(text) {
    S.danger = Math.min(1, S.danger + (practice ? 0 : 0.12)); S.shake = rm ? 0 : 0.35;
    say(text);
  }
  function retreat() { if (S.ended) return; seq.push('retreat'); finish(); }
  function say(text) { feedback.textContent = text; }

  function end(kind) {
    S.ended = true; S.spraying = kind === 'out' ? false : S.spraying;
    const recap = steps.map(s => el('li', { class: S.done.has(s.id) ? 'is-done' : '' }, el('span', { 'aria-hidden': 'true' }, S.done.has(s.id) ? '✓ ' : '○ '), s.label));
    const shoutFirst = seq.filter(id => id !== 'fight')[0] === 'shout';
    const body = {
      out: [el('h3', {}, practice ? '연습 완료' : '불이 꺼졌다'), el('ol', { class: 'mg-order' }, recap),
        !shoutFirst ? el('p', { class: 'mg-why' }, fact('fire_shout')?.text || '') : null, practice ? factText(['ext_1', 'ext_2', 'ext_3']).map(t => el('p', { class: 'mg-why' }, t)) : null],
      spread: [el('h3', {}, '불이 번졌다'), el('p', {}, '상자 더미로 옮겨 붙었다. 이제는 작은 불이 아니다.')],
      overwhelmed: [el('h3', {}, '더는 버틸 수 없다'), el('p', {}, '연기와 열기가 덮쳐 온다. 소화기 한 대로는 감당할 수 없는 불이다.')],
    }[kind];
    const ok = el('button', { type: 'button', class: 'mg-btn mg-primary', 'data-autofocus': true }, kind === 'out' ? '확인' : kind === 'spread' ? '계속' : '그래도 버틴다');
    ok.addEventListener('click', finish);
    actions.replaceChildren(el('div', { class: `mg-end is-${kind}` }, body, ok));
    helpBox.replaceChildren();
    retreatBtn.hidden = kind === 'out' || practice;
    announce(body[0].textContent);
    ok.focus();
  }

  function onKey(e) {
    if (e.key === 'Escape') { if (S.ended || practice) finish(); else retreat(); return true; }
    if (S.phase === 'act' && !S.ended && S.done.has('squeeze') && (e.key === 'ArrowLeft' || e.key === 'ArrowRight')) { stroke(e.key === 'ArrowLeft' ? -1 : 1); return true; }
    return false;
  }

  function loop(now) {
    const dt = Math.min(0.1, (now - last) / 1000); last = now;
    if (S.phase === 'act' && !S.ended) {
      S.danger = Math.min(1, S.danger + growPerS * dt);
      S.fireLv = Math.min(1, S.fireLv + growPerS * dt * 0.4);
      if (S.holding) {
        S.hold += dt; btns.get('squeeze')?.style.setProperty('--p', String(Math.min(1, S.hold / HOLD_S)));
        if (S.hold >= HOLD_S) squeezed();
      }
      if (S.danger >= 1) end(large ? 'overwhelmed' : 'spread');
    }
    S.sweepDir *= Math.pow(0.02, dt); S.shake = Math.max(0, S.shake - dt);
    dangerBar.style.width = `${S.danger * 100}%`;
    dangerBar.parentElement.dataset.state = S.danger > 0.75 ? 'over' : S.danger > 0.45 ? 'warn' : 'ok';
    dangerText.textContent = S.danger > 0.75 ? '곧 번짐' : S.danger > 0.45 ? '서둘러야 함' : '여유 있음';
    drawScene(cv, now / 1000, dt, S, { reduced: rm, large });
    cv.setAttribute('aria-label', `불길 ${S.fireLv > 0.66 ? '큼' : S.fireLv > 0.25 ? '중간' : S.fireLv > 0 ? '작음' : '꺼짐'} · 안전핀 ${S.pinOut ? '뽑음' : '꽂혀 있음'} · 노즐 ${S.aimed ? '불 쪽' : '내려 둠'}${S.spraying ? ' · 분사 중' : ''}`);
    if (!S.closed) raf = requestAnimationFrame(loop);
  }

  if (S.phase === 'decide') renderDecide(); else renderAct();
  raf = requestAnimationFrame(loop);
  return m.done;
}

function hash(s) { return [...s].reduce((a, c) => (a * 31 + c.charCodeAt(0)) % 1009, 7); }

// --- canvas scene: corridor wall and floor, parcel boxes, flames and smoke, the extinguisher and its powder ---
const nz = (a) => 0.5 + 0.5 * Math.sin(a) * Math.sin(a * 1.37 + 1.1);
const rrect = (g, x, y, w, h, r) => { g.beginPath(); g.roundRect ? g.roundRect(x, y, w, h, r) : g.rect(x, y, w, h); };

function drawScene(cv, t, dt, S, { reduced: rm, large }) {
  const g = cv.getContext('2d'), W = cv.width, H = cv.height, floorY = H * 0.74, ft = rm ? t * 0.25 : t;
  const fx = W * 0.76, fy = floorY - 52, lv = S.fireLv, flick = rm ? 1 : 0.82 + 0.18 * nz(ft * 9);
  g.save();
  if (S.shake > 0) g.translate(Math.sin(t * 90) * S.shake * 6, 0);
  let gr = g.createLinearGradient(0, 0, 0, floorY); gr.addColorStop(0, '#303438'); gr.addColorStop(1, '#23262a');
  g.fillStyle = gr; g.fillRect(0, 0, W, floorY);
  g.strokeStyle = 'rgba(255,255,255,.045)'; g.lineWidth = 2;
  for (let x = 120; x < W; x += 170) { g.beginPath(); g.moveTo(x, 0); g.lineTo(x, floorY); g.stroke(); }
  g.fillStyle = '#463c33'; g.fillRect(18, floorY - 236, 82, 236); g.fillStyle = '#5a4e42'; g.fillRect(25, floorY - 229, 68, 229);
  g.fillStyle = '#c9c2b4'; g.fillRect(80, floorY - 120, 8, 4); // door handle: the way out is behind you
  g.fillStyle = '#2e7d4f'; g.fillRect(34, floorY - 262, 50, 18); g.fillStyle = '#e9fff1'; g.font = "700 11px 'Pretendard','Noto Sans KR',sans-serif"; g.textAlign = 'center'; g.fillText('비상구', 59, floorY - 249);
  gr = g.createLinearGradient(0, floorY, 0, H); gr.addColorStop(0, '#3b342d'); gr.addColorStop(1, '#1b1814');
  g.fillStyle = gr; g.fillRect(0, floorY, W, H - floorY);
  g.strokeStyle = 'rgba(0,0,0,.35)'; g.lineWidth = 1;
  for (let i = 1; i < 6; i++) { const y = floorY + (H - floorY) * Math.pow(i / 6, 1.5); g.beginPath(); g.moveTo(0, y); g.lineTo(W, y); g.stroke(); }
  for (let i = -6; i <= 6; i++) { g.beginPath(); g.moveTo(W / 2 + i * 40, floorY); g.lineTo(W / 2 + i * 150, H); g.stroke(); }
  g.fillStyle = '#18191a'; g.fillRect(0, floorY - 9, W, 9);
  if (lv > 0) { // warm light thrown on wall and floor
    const rg = g.createRadialGradient(fx, fy, 10, fx, fy, 150 + lv * 300);
    rg.addColorStop(0, `rgba(255,150,60,${0.55 * lv * flick})`); rg.addColorStop(1, 'rgba(255,110,40,0)');
    g.globalCompositeOperation = 'lighter'; g.fillStyle = rg; g.fillRect(0, 0, W, H); g.globalCompositeOperation = 'source-over';
  }
  for (const [x, w, h, c, y0] of [[fx - 78, 88, 58, '#a3774a', 0], [fx + 6, 72, 74, '#b2834f', 0], [fx - 46, 66, 44, '#986d43', 58]]) {
    const y = floorY - y0 - h;
    g.fillStyle = c; g.fillRect(x, y, w, h); g.fillStyle = 'rgba(0,0,0,.18)'; g.fillRect(x + w * 0.7, y, w * 0.3, h);
    g.fillStyle = 'rgba(210,190,150,.55)'; g.fillRect(x + w / 2 - 5, y, 10, h); // packing tape
    g.fillStyle = '#f2efe6'; g.fillRect(x + 8, y + h * 0.45, 22, 13); // shipping label
  }
  if (S.done.has('sweep')) { g.fillStyle = 'rgba(236,236,230,.75)'; g.beginPath(); g.ellipse(fx, floorY + 6, 120, 14, 0, 0, Math.PI * 2); g.fill(); g.fillRect(fx - 80, fy - 30, 150, 8); }
  if (lv > 0) flames(g, fx, fy, lv, ft, large, rm);
  if (lv > 0.04) {
    const sh = 70 + lv * (large ? 210 : 140), sm = g.createLinearGradient(0, 0, 0, sh);
    sm.addColorStop(0, `rgba(18,18,20,${0.3 + lv * 0.5})`); sm.addColorStop(1, 'rgba(18,18,20,0)');
    g.fillStyle = sm; g.fillRect(0, 0, W, sh);
  }
  const tip = drawExt(g, 150, H - 20, S, ft, fx, fy);
  if (S.spraying && !rm || S.spraying && Math.random() < 0.4) {
    for (let i = 0; i < (rm ? 2 : 6); i++) {
      const a = tip.ang + (Math.random() - 0.5) * 0.35, v = 260 + Math.random() * 150;
      S.particles.push({ x: tip.x, y: tip.y, vx: Math.cos(a) * v, vy: Math.sin(a) * v, life: 0, max: 0.55 + Math.random() * 0.5, r: 3 });
    }
  }
  if (S.particles.length > 280) S.particles.splice(0, S.particles.length - 280);
  for (const p of S.particles) {
    p.x += p.vx * dt; p.y += p.vy * dt; p.vx *= 0.965; p.vy = p.vy * 0.965 + 25 * dt; p.life += dt; p.r += 46 * dt;
    g.fillStyle = `rgba(242,242,238,${Math.max(0, 0.32 * (1 - p.life / p.max))})`;
    g.beginPath(); g.arc(p.x, p.y, p.r, 0, Math.PI * 2); g.fill();
  }
  S.particles = S.particles.filter(p => p.life < p.max);
  g.restore();
}

function flames(g, cx, base, lv, t, large, rm) {
  const h = 46 + lv * (large ? 290 : 220), w = 34 + lv * 130, n = 9;
  g.globalCompositeOperation = 'lighter';
  for (let layer = 0; layer < 2; layer++) {
    for (let i = 0; i < n; i++) {
      const u = i / (n - 1) - 0.5, k = 1 - Math.abs(u) * 1.4;
      const hh = h * k * (0.62 + 0.38 * nz(t * 6 + i * 1.9)) * (layer ? 0.55 : 1), ww = (w / n) * (layer ? 0.9 : 1.6);
      const x = cx + u * w, sway = rm ? 0 : Math.sin(t * 4 + i) * ww * 0.6;
      const grd = g.createLinearGradient(0, base, 0, base - hh);
      grd.addColorStop(0, layer ? 'rgba(255,250,215,.9)' : 'rgba(255,170,60,.85)');
      grd.addColorStop(0.5, layer ? 'rgba(255,210,95,.6)' : 'rgba(255,98,28,.55)');
      grd.addColorStop(1, 'rgba(190,40,10,0)');
      g.fillStyle = grd; g.beginPath(); g.moveTo(x - ww, base);
      g.quadraticCurveTo(x - ww * 0.9, base - hh * 0.55, x + sway, base - hh);
      g.quadraticCurveTo(x + ww * 0.9, base - hh * 0.55, x + ww, base); g.closePath(); g.fill();
    }
  }
  if (!rm) for (let i = 0; i < 10 * lv; i++) { // embers
    const s = (t * 0.7 + i * 0.137) % 1, ex = cx + Math.sin(i * 7.3 + t) * w * 0.5, ey = base - s * h * 1.2;
    g.fillStyle = `rgba(255,190,90,${0.8 * (1 - s)})`; g.fillRect(ex, ey, 2.5, 2.5);
  }
  g.globalCompositeOperation = 'source-over';
}

function drawExt(g, x, y, S, t, tx, ty) {
  const bw = 46, bh = 122, top = y - bh;
  g.fillStyle = 'rgba(0,0,0,.4)'; g.beginPath(); g.ellipse(x, y + 2, 34, 6, 0, 0, Math.PI * 2); g.fill();
  const gr = g.createLinearGradient(x - bw / 2, 0, x + bw / 2, 0);
  gr.addColorStop(0, '#7a110b'); gr.addColorStop(0.32, '#e23a2c'); gr.addColorStop(0.5, '#ff7a66'); gr.addColorStop(0.62, '#c82b20'); gr.addColorStop(1, '#650e09');
  g.fillStyle = gr; rrect(g, x - bw / 2, top, bw, bh, 14); g.fill();
  g.fillStyle = '#f3f0e8'; g.fillRect(x - 17, top + 36, 34, 46);
  g.fillStyle = '#c42a1f'; g.font = "800 10px 'Pretendard','Noto Sans KR',sans-serif"; g.textAlign = 'center';
  g.fillText('분말', x, top + 54); g.fillText('소화기', x, top + 68);
  g.fillStyle = '#2a2c2f'; g.fillRect(x - 9, top - 16, 18, 18);
  g.fillStyle = '#e8e8e4'; g.beginPath(); g.arc(x + 14, top - 6, 7, 0, Math.PI * 2); g.fill();
  g.strokeStyle = '#2fa35a'; g.lineWidth = 2.5; g.beginPath(); g.arc(x + 14, top - 6, 5, Math.PI * 1.1, Math.PI * 1.6); g.stroke();
  g.strokeStyle = '#222'; g.lineWidth = 1.2; g.beginPath(); g.moveTo(x + 14, top - 6); g.lineTo(x + 11, top - 10); g.stroke(); // gauge needle in the green
  const sq = S.done.has('squeeze') ? 1 : Math.min(1, S.hold / HOLD_S);
  g.strokeStyle = '#141516'; g.lineWidth = 6; g.lineCap = 'round';
  g.beginPath(); g.moveTo(x - 6, top - 14); g.lineTo(x - 38, top - 18); g.stroke();
  g.beginPath(); g.moveTo(x - 6, top - 22); g.lineTo(x - 38, top - 38 + sq * 15); g.stroke();
  if (!S.pinOut) {
    g.strokeStyle = '#e9c23b'; g.lineWidth = 3; g.beginPath(); g.arc(x - 24, top - 31, 6, 0, Math.PI * 2); g.stroke();
    g.strokeStyle = '#c9c9c9'; g.lineWidth = 2; g.beginPath(); g.moveTo(x - 18, top - 30); g.lineTo(x - 4, top - 25); g.stroke();
  }
  let tipX, tipY, ang;
  if (S.aimed) { tipX = x + 128; tipY = top + 4; ang = Math.atan2(ty - tipY, tx - tipX) + S.sweepDir * 0.26 + (S.spraying ? Math.sin(t * 3) * 0.03 : 0); }
  else { tipX = x + 34; tipY = y - 16; ang = Math.PI * 0.45; }
  g.strokeStyle = '#111214'; g.lineWidth = 7;
  g.beginPath(); g.moveTo(x + 8, top - 8); g.bezierCurveTo(x + 50, top + 6, tipX - 70, tipY + 44, tipX - 22 * Math.cos(ang), tipY - 22 * Math.sin(ang)); g.stroke();
  g.save(); g.translate(tipX, tipY); g.rotate(ang);
  g.fillStyle = '#1c1d20'; g.beginPath(); g.moveTo(-24, -5); g.lineTo(0, -9); g.lineTo(0, 9); g.lineTo(-24, 5); g.closePath(); g.fill();
  g.restore();
  return { x: tipX, y: tipY, ang };
}

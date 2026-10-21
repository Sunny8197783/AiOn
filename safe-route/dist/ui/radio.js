// Portable radio: a backlit FM dial (drag, ←/→, Shift for fine steps), frequency readout, signal meter and
// a waveform that turns to static off-station. Tuned in, the broadcast arrives line by line as captions;
// '메모' keeps a line. Resolves { station, official, heard, noted: [{ text, kind }] }; official = an official line was heard.
import { el, put, modal, header, ladderHelp, pill, reduced } from './mini-dom.js';
import { radioSignal } from '../game/drills.js';

const MIN = 87.5, MAX = 108, LOCK = 0.75;
const r1 = (f) => Math.round(Math.min(MAX, Math.max(MIN, f)) * 10) / 10;
const word = (c) => c <= 0 ? '신호 없음' : c < 0.4 ? '약함' : c < LOCK ? '보통' : '잘 들림';
const lineSeconds = (s) => Math.min(5.5, Math.max(2.4, 1.2 + s.length * 0.055));

export async function radio({ mode = 'guided', stations = [], start = 89.5, battery = true, extraSeconds = 0 } = {}) {
  let freq = r1(Number(start) || 89.5), power = false, raf = 0, last = performance.now(), frameAcc = 0;
  let cur = null, lockT = 0, nextAt = 0.8, shownState = '', clar = 0, closed = false;
  const pos = new Map(), heard = [], noted = [], seen = new Set();
  const target = stations.find(s => s.kind === 'official') || stations[0];
  const rm = reduced();

  const best = () => {
    const n = new Map();
    for (const h of heard) n.set(h.station, (n.get(h.station) || 0) + 1);
    let top = null;
    for (const h of heard) if (!top || n.get(h.station) >= n.get(top)) top = h.station; // ties: the later one
    return top;
  };
  const finish = () => { closed = true; cancelAnimationFrame(raf); m.close({ station: best(), official: heard.some(h => h.station.kind === 'official'), heard: heard.map(h => h.text), noted: noted.map(n => ({ text: n.text, kind: n.station.kind })) }); };
  const m = modal({ className: 'mg-radio', label: '휴대용 라디오', onKey: (e) => { if (e.key === 'Escape') { finish(); return true; } } });

  // --- faceplate ---
  const dial = el('canvas', { width: 720, height: 112, class: 'mg-dial-canvas', 'aria-hidden': 'true' });
  const wave = el('canvas', { width: 360, height: 56, class: 'mg-wave', 'aria-hidden': 'true' });
  const slider = el('div', { class: 'mg-dial', role: 'slider', tabindex: 0, 'data-autofocus': true, 'aria-label': '주파수 다이얼', 'aria-valuemin': MIN, 'aria-valuemax': MAX, 'aria-disabled': 'true' }, dial);
  const readout = el('b', { class: 'mg-freq' }), sigBars = el('span', { class: 'mg-sig', 'aria-hidden': 'true' }, [1, 2, 3, 4, 5].map(() => el('i'))), sigText = el('small');
  const knob = el('span', { class: 'mg-knob', 'aria-hidden': 'true' });
  const powerLed = el('span', { class: 'mg-led', 'aria-hidden': 'true' });
  const status = el('p', { class: 'mg-radio-status' });
  const capWho = el('p', { class: 'mg-cap-who' }), capText = el('p', { class: 'mg-cap-text' });
  const caption = el('div', { class: 'mg-caption', 'aria-live': 'polite' }, capWho, capText);
  const transcript = el('ul', { class: 'mg-transcript', 'aria-label': '들은 내용' });
  const off = el('button', { type: 'button', class: 'mg-btn mg-primary' }, '라디오 끄기');
  off.addEventListener('click', finish);

  const setFreq = (f) => { if (!power) return; freq = r1(f); update(); };
  slider.addEventListener('keydown', (e) => {
    const fine = e.shiftKey ? 0.1 : 0.3;
    const step = { ArrowRight: fine, ArrowUp: fine, ArrowLeft: -fine, ArrowDown: -fine, PageUp: 1, PageDown: -1 }[e.key];
    if (step !== undefined) { e.preventDefault(); setFreq(freq + step); }
    else if (e.key === 'Home' || e.key === 'End') { e.preventDefault(); setFreq(e.key === 'Home' ? MIN : MAX); }
  });
  const fromX = (e) => { const r = slider.getBoundingClientRect(); const pad = r.width * (30 / 720); return MIN + ((e.clientX - r.left - pad) / (r.width - 2 * pad)) * (MAX - MIN); };
  slider.addEventListener('pointerdown', (e) => { if (!power) return; slider.setPointerCapture(e.pointerId); slider.focus(); setFreq(fromX(e)); });
  slider.addEventListener('pointermove', (e) => { if (slider.hasPointerCapture(e.pointerId)) setFreq(fromX(e)); });
  slider.addEventListener('wheel', (e) => { if (!power) return; e.preventDefault(); setFreq(freq + (e.deltaY < 0 ? 0.1 : -0.1)); }, { passive: false });

  function drawDial() {
    const g = dial.getContext('2d'), W = dial.width, H = dial.height, x = (f) => 30 + ((f - MIN) / (MAX - MIN)) * (W - 60);
    const bg = g.createLinearGradient(0, 0, 0, H);
    bg.addColorStop(0, power ? '#3b2a0d' : '#22211f'); bg.addColorStop(0.5, power ? '#5a4113' : '#2a2927'); bg.addColorStop(1, power ? '#2b1e09' : '#1c1b1a');
    g.fillStyle = bg; g.fillRect(0, 0, W, H);
    if (power) { const glow = g.createRadialGradient(x(freq), H * 0.55, 4, x(freq), H * 0.55, 220); glow.addColorStop(0, 'rgba(255,200,110,.28)'); glow.addColorStop(1, 'rgba(255,200,110,0)'); g.fillStyle = glow; g.fillRect(0, 0, W, H); }
    g.strokeStyle = power ? '#ffd79a' : '#77736c'; g.fillStyle = g.strokeStyle; g.lineWidth = 2;
    g.font = "600 19px 'Pretendard','Noto Sans KR',sans-serif"; g.textAlign = 'center';
    for (let i = 0; i <= 40; i++) {
      const f = 88 + i * 0.5, X = x(f), major = i % 4 === 0, mid = i % 2 === 0;
      g.beginPath(); g.moveTo(X, 16); g.lineTo(X, 16 + (major ? 24 : mid ? 15 : 8)); g.stroke();
      if (major) g.fillText(String(f), X, 64);
    }
    g.font = "700 15px 'Pretendard','Noto Sans KR',sans-serif"; g.textAlign = 'left'; g.fillText('FM', 8, 98); g.textAlign = 'right'; g.fillText('MHz', W - 8, 98);
    g.strokeStyle = 'rgba(255,255,255,.12)'; g.beginPath(); g.moveTo(30, 82); g.lineTo(W - 30, 82); g.stroke();
    g.save(); if (power) { g.shadowColor = '#ff5a3c'; g.shadowBlur = 12; }
    g.strokeStyle = '#ff4a35'; g.lineWidth = 3; g.beginPath(); g.moveTo(x(freq), 8); g.lineTo(x(freq), H - 8); g.stroke(); g.restore();
    const gl = g.createLinearGradient(0, 0, 0, H * 0.45); gl.addColorStop(0, 'rgba(255,255,255,.14)'); gl.addColorStop(1, 'rgba(255,255,255,0)');
    g.fillStyle = gl; g.fillRect(0, 0, W, H * 0.45); // glass reflection
  }

  function drawWave(t, clarity) {
    const g = wave.getContext('2d'), W = wave.width, H = wave.height;
    g.fillStyle = '#120e06'; g.fillRect(0, 0, W, H);
    if (!power) return;
    g.strokeStyle = '#ffc861'; g.lineWidth = 1.6; g.shadowColor = '#ffb23d'; g.shadowBlur = 6; g.beginPath();
    const voice = cur ? 1 : 0.25;
    for (let X = 0; X <= W; X += 3) {
      const v = clarity * voice * Math.sin(X * 0.09 + t * 9) * Math.sin(X * 0.017 + t * 2.1) * H * 0.34;
      const n = (Math.random() * 2 - 1) * (1 - clarity) * H * 0.4;
      X ? g.lineTo(X, H / 2 + v + n) : g.moveTo(X, H / 2 + v + n);
    }
    g.stroke(); g.shadowBlur = 0;
  }

  function addHeard(station, i) {
    const key = `${station.freq}:${i}`, text = station.lines[i];
    if (seen.has(key)) return;
    seen.add(key); heard.push({ station, text });
    const memo = el('button', { type: 'button', class: 'mg-btn mg-chip', 'aria-pressed': 'false', 'aria-label': `메모: ${text}` }, '✎ 메모');
    memo.addEventListener('click', () => {
      const i = noted.findIndex(n => n.text === text), on = i < 0;
      if (on) noted.push({ text, station }); else noted.splice(i, 1);
      memo.setAttribute('aria-pressed', String(on)); memo.textContent = on ? '✓ 메모함' : '✎ 메모';
    });
    transcript.append(el('li', {}, el('span', { class: 'mg-tr-who' }, `${station.freq}MHz`), el('span', { class: 'mg-tr-text' }, text), memo));
    transcript.scrollTop = transcript.scrollHeight;
  }

  function update() {
    const { station, clarity } = power ? radioSignal(freq, stations) : { station: null, clarity: 0 };
    readout.textContent = power ? freq.toFixed(1) : '--.-';
    [...sigBars.children].forEach((b, i) => b.classList.toggle('on', power && clarity * 5 > i + 0.01));
    sigText.textContent = power ? `신호 ${word(clarity)}` : '꺼짐';
    knob.style.transform = `rotate(${(freq - MIN) * 34}deg)`;
    const locked = power && station && clarity >= LOCK ? station : null;
    slider.setAttribute('aria-valuenow', String(freq));
    slider.setAttribute('aria-valuetext', `${freq.toFixed(1)}MHz, 신호 ${word(clarity)}${locked ? `, ${locked.name}` : ''}`);
    if (locked !== cur) { cur = locked; lockT = 0; nextAt = 0.8; }
    const state = !power ? 'off' : cur ? `on:${cur.freq}` : clarity > 0 ? 'near' : 'static';
    if (state !== shownState) {
      shownState = state;
      if (cur) { capWho.replaceChildren(); put(capWho, el('b', {}, cur.name), mode === 'guided' && (cur.kind === 'official' || cur.kind === 'rumor') ? pill(cur.kind) : null); capText.textContent = '…'; }
      else if (state === 'near') { capWho.replaceChildren(); capText.textContent = mode === 'free' ? '…지직… 말소리가 섞여 들린다…' : '…지직… 말소리가 섞여 들린다. 조금만 더 맞춰 보자.'; }
      else if (state === 'static') { capWho.replaceChildren(); capText.textContent = '치이이익— (잡음뿐이다)'; }
    }
    clar = clarity;
    drawDial();
  }

  function loop(now) {
    const dt = Math.min(0.1, (now - last) / 1000); last = now;
    if (closed) return;
    if (cur && cur.lines?.length) {
      lockT += dt;
      if (lockT >= nextAt) {
        const i = pos.get(cur) ?? 0;
        capText.textContent = cur.lines[i];
        addHeard(cur, i);
        pos.set(cur, (i + 1) % cur.lines.length);
        nextAt = lockT + lineSeconds(cur.lines[i]) + (i + 1 === cur.lines.length ? 2.5 : 0);
      }
    }
    frameAcc += dt;
    if (!rm || frameAcc > 0.16) { frameAcc = 0; drawWave(now / 1000, clar); }
    raf = requestAnimationFrame(loop);
  }
  const help = ladderHelp(mode, () => [el('p', {}, `다이얼을 천천히 돌려 보세요 — 목표 ${target ? `${target.freq}MHz` : '방송이 들리는 곳'}`), el('p', { class: 'mg-why' }, '잡음이 줄고 신호 막대가 차오르면 방송이 들립니다. Shift를 누른 채 돌리면 0.1MHz씩 움직입니다.')]);

  put(m.root,
    header('라디오', '휴대용 라디오', mode, 'mg-radio-title'),
    help,
    el('div', { class: 'mg-radio-body' },
      el('div', { class: 'mg-speaker', 'aria-hidden': 'true' }),
      el('div', { class: 'mg-radio-face' },
        el('div', { class: 'mg-lcd' }, powerLed, readout, el('small', {}, 'MHz'), el('span', { class: 'mg-sigbox' }, sigBars, sigText)),
        wave, slider, el('div', { class: 'mg-knobrow' }, el('small', {}, 'TUNING'), knob))),
    status, caption,
    el('h3', { class: 'mg-sub' }, '들은 내용'), transcript,
    el('footer', { class: 'mg-foot' }, el('p', { class: 'mg-keys' }, '다이얼에서 ←/→ 0.3MHz · Shift+←/→ 0.1MHz · PgUp/PgDn 1MHz · 끌어서 맞추기 · Esc 끄기'), el('div', { class: 'mg-actions' }, off)));
  m.root.setAttribute('aria-labelledby', 'mg-radio-title');

  const powerOn = () => { if (closed) return; power = true; powerLed.classList.add('on'); slider.removeAttribute('aria-disabled'); status.textContent = ''; update(); };
  update();
  if (!battery) {
    status.textContent = '건전지가 없어 켜지지 않는다.';
    capText.textContent = '';
  } else if (extraSeconds > 0) {
    const wait = rm ? 1.2 : Math.min(4, extraSeconds / 10);
    status.replaceChildren('어둠 속에서 건전지를 찾아 넣는 중… ', el('span', { class: 'mg-bar', 'aria-hidden': 'true' }, el('span', { class: 'mg-bar-fill mg-fill-anim', style: `animation-duration:${wait}s` })));
    setTimeout(powerOn, wait * 1000);
  } else powerOn();
  raf = requestAnimationFrame(loop);
  return m.done;
}

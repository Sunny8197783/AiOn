// Landing screen, pause menu, settings and the about/sources page.
import { el, modal, icon, btn, uiSound, reduceMotion } from './dom.js';

export const CHAPTERS = [
  ['프롤로그', '평소와 다른 화요일', 'D-3 화 17:30 · 한결물류 사무실', '확인된 사실, 추정, 소문을 나눕니다.'],
  ['1장', '준비', 'D-2 ~ D-1 · 한빛아파트와 동네', '여섯 칸의 시간으로 무엇을 준비할까요.'],
  ['2장', '정전과 경보', 'D-day 금 밤', '불이 꺼지고, 경보가 울립니다.'],
  ['3장', '버티기', 'D+1 ~ D+3', '정전과 단수, 그리고 이웃.'],
  ['4장', '남을까, 떠날까', 'D+6', '조건이 바뀌면 답도 바뀝니다.'],
  ['5장', '공식 대피', 'D+12', '가방 하나와 새로운 길.'],
  ['엔딩', '생존 기록', '', '도움이 된 준비와 놓친 정보.'],
];

// ---------- landing ----------
// landing({ hasSave }) -> Promise<'new'|'continue'|'settings'|'about'>
export function landing({ hasSave = false } = {}) {
  const canvas = el('canvas', { class: 'landing-art', 'aria-hidden': 'true' });
  const choose = (id) => () => { uiSound('confirm'); m.close(id); };
  const content = el('div', { class: 'landing-wrap' },
    canvas,
    el('div', { class: 'landing-shade', 'aria-hidden': 'true' }),
    el('main', { class: 'landing-main' },
      el('section', { class: 'landing-hero' },
        el('p', { class: 'landing-eyebrow' }, el('span', { class: 'live-dot', 'aria-hidden': 'true' }), '이야기 중심 생존 시뮬레이션 · 가상 동네 연무동'),
        el('h1', { class: 'landing-title' },
          el('span', { class: 'bracket', 'aria-hidden': 'true' }, '〈'), el('span', { class: 'title-ko' }, '경보 이전'), el('span', { class: 'bracket', 'aria-hidden': 'true' }, '〉'),
          el('span', { class: 'title-en', lang: 'en' }, 'Before the Siren')),
        el('p', { class: 'landing-pitch' }, '전쟁 직전의 준비가, 전쟁 이후의 선택지를 바꿉니다.'),
        el('p', { class: 'landing-sub' }, '사재기가 아니라 정보를 확인하고, 상황에 맞게 판단하고, 이웃과 함께 버티는 이야기입니다.'),
        el('ol', { class: 'landing-loop', 'aria-label': '게임의 흐름' },
          ['정보', '계획', '행동', '결과'].map((t, i) => el('li', {}, el('span', { class: 'loop-no', 'aria-hidden': 'true' }, String(i + 1)), t))),
        el('div', { class: 'landing-actions' },
          btn('새로 시작', { primary: true, iconName: 'play', className: 'big', 'data-autofocus': hasSave ? null : true, onclick: choose('new') }),
          hasSave ? btn('이어하기', { iconName: 'retry', className: 'big', 'data-autofocus': true, onclick: choose('continue') }) : null,
          btn('설정', { ghost: true, iconName: 'sliders', onclick: choose('settings') }),
          btn('출처와 만든 사람', { ghost: true, iconName: 'book', onclick: choose('about') }))),
      el('section', { class: 'landing-chapters', 'aria-label': '장 구성' },
        el('h2', {}, '이야기 구성'),
        el('ol', {}, CHAPTERS.map(([no, title, when, line]) => el('li', {},
          el('span', { class: 'ch-no' }, no),
          el('span', { class: 'ch-main' }, el('b', {}, title), when ? el('small', {}, when) : null, el('span', { class: 'ch-line' }, line))))))),
    el('footer', { class: 'landing-foot' },
      el('p', { class: 'fiction-note' }, icon('info'),
        el('span', {}, '이 게임의 동네·인물·회사·재난문자·방송은 모두 가상입니다. 실제 상황이 아닙니다. 안전 행동요령은 국민안전24 등 공식 자료에서 확인한 문장만 출처와 함께 인용합니다. 폭력이나 부상 장면은 나오지 않습니다.')),
      el('p', { class: 'keys-note' }, ...[['WASD', '이동'], ['E', '살피기'], ['Tab', '수첩'], ['Esc', '메뉴']].map(([k, t]) => el('span', {}, el('kbd', {}, k), t)))));
  const m = modal({ className: 'landing', label: '경보 이전 · 시작 화면', content });
  const stop = skyline(canvas);
  return m.done.finally(stop);
}

// Animated skyline: apartment slabs at dusk; windows go dark in a wave (blackout), emergency lights and
// stars come out, a siren's sound rings spread from a rooftop, then the town lights up again. ~18 s loop.
function skyline(canvas) {
  const g = canvas.getContext('2d');
  if (!g) return () => {};
  let rng = 0x9e3779b9;
  const rand = () => { rng ^= rng << 13; rng ^= rng >>> 17; rng ^= rng << 5; return ((rng >>> 0) % 100000) / 100000; };
  let W = 0, H = 0, layers = [], stars = [], poles = [], raf = 0, alive = true;
  const WARM = ['#ffd58a', '#ffe3ad', '#ffc56b', '#f6e7c8', '#bcd7ff'];

  function layout() {
    const dpr = Math.min(1.5, devicePixelRatio || 1);
    W = canvas.clientWidth || innerWidth; H = canvas.clientHeight || innerHeight;
    canvas.width = Math.round(W * dpr); canvas.height = Math.round(H * dpr);
    g.setTransform(dpr, 0, 0, dpr, 0, 0);
    rng = 0x9e3779b9;
    const ground = H * 0.86;
    layers = [];
    // Far city: low boxes with sparse tiny windows.
    const far = []; for (let x = -20; x < W + 40;) { const w = 30 + rand() * 70, h = H * (0.08 + rand() * 0.2); far.push(slab(x, ground - h, w, h, Math.max(2, Math.round(w / 9)), Math.round(h / 9), 0.25, 2)); x += w + rand() * 8; }
    // Apartment slabs (15 floors) with big building numbers.
    const mid = []; let num = 101;
    for (let x = -40 + rand() * 40; x < W + 60;) {
      const w = 110 + rand() * 90, h = Math.min(H * 0.62, H * (0.4 + rand() * 0.16)), cols = Math.round(w / 15);
      const s = slab(x, ground - h, w, h, cols, 15, 0.55, 4); s.label = String(num++); s.exit = true; mid.push(s); x += w + 30 + rand() * 70;
    }
    // Villas in front.
    const near = []; for (let x = -10; x < W + 40;) { const w = 60 + rand() * 60, h = H * (0.1 + rand() * 0.07); near.push(slab(x, ground - h, w, h, Math.round(w / 16), 4, 0.5, 5)); x += w + 6 + rand() * 20; }
    layers = [{ list: far, color: '#141a28' }, { list: mid, color: '#0f1420' }, { list: near, color: '#0a0e16' }];
    stars = Array.from({ length: 140 }, () => ({ x: rand() * W, y: rand() * H * 0.55, r: rand() * 1.2 + 0.2, k: rand() }));
    poles = []; for (let x = 60 + rand() * 80; x < W; x += 230 + rand() * 80) poles.push(x);
  }
  function slab(x, y, w, h, cols, rows, lit, pad) {
    const win = [];
    const cw = (w - pad * 2) / cols, rh = (h - pad * 2) / rows;
    for (let r = 0; r < rows; r++) for (let c = 0; c < cols; c++) {
      if (rand() > lit) continue;
      win.push({ x: x + pad + c * cw + cw * 0.18, y: y + pad + r * rh + rh * 0.22, w: cw * 0.64, h: rh * 0.5, col: WARM[Math.floor(rand() * WARM.length)], k: rand(), off: rand() });
    }
    return { x, y, w, h, win };
  }

  function frame(now) {
    if (!alive) return;
    const still = reduceMotion();
    const T = 18000, p = still ? 0.2 : (now % T) / T;
    // dark: 0 lit -> 1 blackout
    const wave = p < 0.42 ? -1 : p < 0.52 ? (p - 0.42) / 0.1 : p < 0.84 ? 2 : -2;
    const dark = p < 0.42 ? 0 : p < 0.52 ? (p - 0.42) / 0.1 : p < 0.84 ? 1 : 1 - (p - 0.84) / 0.16;
    const ground = H * 0.86;

    const sky = g.createLinearGradient(0, 0, 0, ground);
    sky.addColorStop(0, mix('#0d1530', '#05070f', dark));
    sky.addColorStop(0.55, mix('#2b2a4a', '#0b1020', dark));
    sky.addColorStop(1, mix('#8a4d3f', '#1a1626', dark));
    g.fillStyle = sky; g.fillRect(0, 0, W, H);

    for (const s of stars) { g.globalAlpha = (0.15 + dark * 0.85) * (0.6 + 0.4 * Math.sin(now / 900 + s.k * 20)); g.fillStyle = '#e8eefc'; g.fillRect(s.x, s.y, s.r, s.r); }
    g.globalAlpha = 1;
    // Moon with halo.
    const mx = W * 0.8, my = H * 0.17;
    const halo = g.createRadialGradient(mx, my, 4, mx, my, 90); halo.addColorStop(0, 'rgba(240,236,220,.35)'); halo.addColorStop(1, 'rgba(240,236,220,0)');
    g.fillStyle = halo; g.fillRect(mx - 90, my - 90, 180, 180);
    g.fillStyle = '#f1ecdd'; g.beginPath(); g.arc(mx, my, 15, 0, Math.PI * 2); g.fill();
    // City glow on the horizon fades in the blackout.
    const glow = g.createLinearGradient(0, ground - H * 0.3, 0, ground);
    glow.addColorStop(0, 'rgba(255,170,90,0)'); glow.addColorStop(1, `rgba(255,170,90,${0.22 * (1 - dark)})`);
    g.fillStyle = glow; g.fillRect(0, ground - H * 0.3, W, H * 0.3);

    layers.forEach((L, li) => {
      for (const b of L.list) {
        g.fillStyle = L.color; g.fillRect(b.x, b.y, b.w, b.h);
        if (li === 1) { g.fillStyle = '#161c2a'; g.fillRect(b.x + b.w * 0.42, b.y - 10, b.w * 0.16, 10); g.fillRect(b.x, b.y - 3, b.w, 3); }
        for (const w of b.win) {
          let on;
          if (wave === -1) on = w.off > 0.04 || Math.sin(now / 3000 + w.k * 50) > 0;
          else if (wave === -2) on = w.k < (p - 0.84) / 0.16;
          else if (wave === 2) on = false;
          else on = (w.x / W) > wave;
          if (on) { g.fillStyle = w.col; g.globalAlpha = li === 0 ? 0.55 : 0.92; g.fillRect(w.x, w.y, w.w, w.h); }
          else if (dark > 0.5 && w.k > 0.985) { g.fillStyle = '#f3f6ff'; g.globalAlpha = 0.5 + 0.5 * Math.sin(now / 400 + w.k * 99); g.fillRect(w.x + w.w * 0.3, w.y, w.w * 0.3, w.h * 0.6); }
        }
        g.globalAlpha = 1;
        if (b.label) {
          g.fillStyle = `rgba(220,226,236,${0.55 - dark * 0.35})`; g.font = `700 ${Math.round(b.w * 0.16)}px Pretendard, 'Noto Sans KR', 'Malgun Gothic', sans-serif`;
          g.textAlign = 'center'; g.fillText(b.label, b.x + b.w / 2, b.y + b.w * 0.2);
          if (b.exit && dark > 0.2) { g.fillStyle = `rgba(60,230,140,${dark})`; for (let f = 1; f < 15; f += 2) g.fillRect(b.x + b.w - 7, b.y + (b.h / 15) * f + 4, 3, 2); }
        }
      }
    });
    // Siren rings from the tallest rooftop while the town is dark.
    if (dark > 0.6 && !still) {
      const top = layers[1].list.reduce((a, b) => (b.y < a.y ? b : a), layers[1].list[0]);
      if (top) {
        const sx = top.x + top.w / 2, sy = top.y - 14;
        g.fillStyle = '#c9cfd8'; g.fillRect(sx - 2, sy, 4, 12); g.fillRect(sx - 7, sy - 6, 14, 6);
        for (let i = 0; i < 3; i++) {
          const k = ((now / 2600) + i / 3) % 1;
          g.strokeStyle = `rgba(242,179,61,${(1 - k) * 0.5 * dark})`; g.lineWidth = 1.5;
          g.beginPath(); g.arc(sx, sy - 3, 10 + k * Math.min(W, H) * 0.45, Math.PI * 1.05, Math.PI * 1.95); g.stroke();
        }
      }
    }
    // Street: ground, poles with sagging wires, street lamps (off in the blackout), passing headlights.
    g.fillStyle = '#07090e'; g.fillRect(0, ground, W, H - ground);
    g.strokeStyle = 'rgba(10,12,18,.95)'; g.lineWidth = 1.2;
    for (let i = 0; i < poles.length; i++) {
      const x = poles[i];
      g.fillStyle = '#05070b'; g.fillRect(x - 2.5, ground - H * 0.3, 5, H * 0.3); g.fillRect(x - 18, ground - H * 0.28, 36, 3);
      if (i + 1 < poles.length) for (const dy of [0, 7]) { const x2 = poles[i + 1], y = ground - H * 0.28 + dy; g.beginPath(); g.moveTo(x, y); g.quadraticCurveTo((x + x2) / 2, y + 22, x2, y); g.stroke(); }
      if (dark < 0.5) { const lamp = g.createRadialGradient(x + 16, ground - H * 0.27, 1, x + 16, ground - H * 0.27, 60); lamp.addColorStop(0, `rgba(255,214,150,${0.7 * (1 - dark * 2)})`); lamp.addColorStop(1, 'rgba(255,214,150,0)'); g.fillStyle = lamp; g.fillRect(x - 44, ground - H * 0.27 - 60, 120, 120); }
    }
    if (!still) for (let i = 0; i < 3; i++) {
      const k = ((now / (7000 + i * 2300)) + i * 0.37) % 1, x = i % 2 ? W * (1.1 - k * 1.2) : W * (k * 1.2 - 0.1), y = ground + (H - ground) * (0.35 + i * 0.18);
      const hl = g.createRadialGradient(x, y, 1, x, y, 26); hl.addColorStop(0, 'rgba(255,244,214,.9)'); hl.addColorStop(1, 'rgba(255,244,214,0)');
      g.fillStyle = hl; g.fillRect(x - 26, y - 26, 52, 52);
    }
    if (!still) raf = requestAnimationFrame(frame);
  }
  const mix = (a, b, k) => { const pa = parseInt(a.slice(1), 16), pb = parseInt(b.slice(1), 16); const c = (s) => Math.round(((pa >> s) & 255) * (1 - k) + ((pb >> s) & 255) * k); return `rgb(${c(16)},${c(8)},${c(0)})`; };
  const onResize = () => { layout(); if (reduceMotion()) frame(performance.now()); };
  addEventListener('resize', onResize);
  layout();
  raf = requestAnimationFrame(frame);
  return () => { alive = false; cancelAnimationFrame(raf); removeEventListener('resize', onResize); };
}

// ---------- pause ----------
// pause() -> Promise<'resume'|'settings'|'save'|'menu'>
export function pause() {
  const go = (id) => () => { uiSound('confirm'); m.close(id); };
  const confirmBox = el('div', { class: 'pause-confirm', hidden: true, role: 'group', 'aria-label': '메인 메뉴로 나가기 확인' },
    el('p', {}, '메인 메뉴로 나가면 마지막 체크포인트 이후의 진행은 사라집니다.'),
    el('div', { class: 'row' }, btn('나가기', { primary: true, iconName: 'exit', onclick: go('menu') }), btn('취소', { ghost: true, onclick: () => { confirmBox.hidden = true; menuBtn.focus(); } })));
  const menuBtn = btn('메인 메뉴로', { ghost: true, iconName: 'exit', onclick: () => { confirmBox.hidden = false; confirmBox.querySelector('.btn').focus(); } });
  const content = el('div', { class: 'pause-panel' },
    el('p', { class: 'eyebrow' }, '일시정지'),
    el('h2', {}, '잠시 멈춤'),
    el('div', { class: 'pause-actions' },
      btn('계속하기', { primary: true, iconName: 'play', 'data-autofocus': true, onclick: go('resume') }),
      btn('설정', { iconName: 'sliders', onclick: go('settings') }),
      btn('체크포인트 저장', { iconName: 'save', onclick: go('save') }),
      menuBtn),
    confirmBox,
    el('dl', { class: 'key-list', 'aria-label': '조작키' }, ...[['WASD · 방향키', '이동'], ['Shift', '달리기'], ['마우스 끌기 · Alt+방향키', '둘러보기'], ['E · Enter', '살피기 · 대화'], ['F', '손전등 · 휴대폰 불빛'], ['Tab', '수첩'], ['I', '소지품'], ['M', '지도'], ['V', '1인칭 / 3인칭'], ['Esc', '이 메뉴']]
      .flatMap(([k, t]) => [el('dt', {}, k), el('dd', {}, t)])));
  const m = modal({ className: 'pause', label: '일시정지 메뉴', content,
    onKey(e) { if (e.key === 'Escape' && !e.repeat) { e.preventDefault(); if (!confirmBox.hidden) { confirmBox.hidden = true; menuBtn.focus(); } else m.close('resume'); } } });
  return m.done;
}

// ---------- settings ----------
// settings(current) -> Promise<{ quality, volume, mute, captions, reduceMotion, textScale, view }>
export function settings(current = {}) {
  const cur = { quality: 'high', volume: 0.7, mute: false, captions: true, reduceMotion: false, textScale: 1, view: 'third', ...current };
  const root = document.documentElement, scale0 = root.style.getPropertyValue('--text-scale');
  const radios = (name, legend, options, value, note) => el('fieldset', { class: 'set-group' }, el('legend', {}, legend),
    el('div', { class: 'seg' }, options.map(([v, label, sub]) => el('label', {},
      el('input', { type: 'radio', name, value: String(v), checked: String(v) === String(value) || null }),
      el('span', {}, el('b', {}, label), sub ? el('small', {}, sub) : null)))),
    note ? el('p', { class: 'set-note' }, note) : null);
  const toggle = (name, label, sub, value) => el('label', { class: 'set-toggle' },
    el('input', { type: 'checkbox', name, role: 'switch', checked: value || null }),
    el('span', { class: 'switch', 'aria-hidden': 'true' }), el('span', {}, el('b', {}, label), sub ? el('small', {}, sub) : null));
  const volOut = el('output', { for: 'set-volume' }, `${Math.round(cur.volume * 100)}%`);
  const vol = el('input', { type: 'range', id: 'set-volume', name: 'volume', min: '0', max: '100', step: '5', value: String(Math.round(cur.volume * 100)), 'aria-valuetext': `${Math.round(cur.volume * 100)}%` });
  vol.addEventListener('input', () => { volOut.textContent = `${vol.value}%`; vol.setAttribute('aria-valuetext', `${vol.value}%`); });
  const form = el('form', { class: 'settings-form' },
    el('div', { class: 'set-cols' },
      el('div', {},
        radios('quality', '그래픽 품질', [['low', '낮음', '가벼운 노트북'], ['medium', '보통', '대부분의 PC'], ['high', '높음', '그림자·주변광']], cur.quality, '느려지면 게임이 한 번 자동으로 품질을 낮춥니다.'),
        radios('view', '시점', [['third', '3인칭', '어깨 너머'], ['first', '1인칭', '정민의 눈']], cur.view),
        radios('textScale', '글자 크기', [[0.9, '작게'], [1, '보통'], [1.15, '크게'], [1.3, '아주 크게']], cur.textScale)),
      el('div', {},
        el('fieldset', { class: 'set-group' }, el('legend', {}, '소리'),
          el('label', { class: 'set-range', for: 'set-volume' }, el('span', {}, icon('sound'), '전체 음량'), volOut), vol,
          toggle('mute', '소리 끄기', '사이렌과 경보음도 꺼집니다. 자막은 계속 나옵니다.', cur.mute)),
        el('fieldset', { class: 'set-group' }, el('legend', {}, '접근성'),
          toggle('captions', '자막 표시', '사이렌·방송·소리를 글로도 보여 줍니다.', cur.captions),
          toggle('reduceMotion', '움직임 줄이기', '화면 흔들림, 머리 흔들림, 깜빡임과 전환 효과를 줄입니다.', cur.reduceMotion)))),
    el('div', { class: 'set-actions' }, btn('취소', { ghost: true, onclick: () => cancel() }), el('button', { type: 'submit', class: 'btn primary' }, icon('check'), el('span', {}, '적용'))));
  form.addEventListener('change', (e) => { if (e.target.name === 'textScale') root.style.setProperty('--text-scale', e.target.value); });
  form.addEventListener('submit', (e) => {
    e.preventDefault();
    const f = new FormData(form);
    uiSound('confirm');
    m.close({ ...cur, quality: f.get('quality') || cur.quality, view: f.get('view') || cur.view, textScale: Number(f.get('textScale')) || 1,
      volume: Number(vol.value) / 100, mute: f.has('mute'), captions: f.has('captions'), reduceMotion: f.has('reduceMotion') });
  });
  const content = el('div', { class: 'settings-panel' }, el('p', { class: 'eyebrow' }, icon('sliders'), '설정'), el('h2', {}, '보기·소리·접근성'), form);
  const cancel = () => { if (scale0) root.style.setProperty('--text-scale', scale0); else root.style.removeProperty('--text-scale'); m.close({ ...cur }); };
  const m = modal({ className: 'settings', label: '설정', content, onKey(e) { if (e.key === 'Escape') { e.preventDefault(); cancel(); } } });
  form.querySelector('input:checked')?.focus();
  return m.done;
}

// ---------- about ----------
// about(sources) -> Promise. Credits, every source with a link, the fiction disclaimer.
export function about(sources = []) {
  const byPub = new Map();
  for (const s of sources || []) { if (!byPub.has(s.publisher)) byPub.set(s.publisher, []); byPub.get(s.publisher).push(s); }
  const content = el('div', { class: 'about-panel' },
    el('header', { class: 'nb-head' }, el('p', { class: 'eyebrow' }, icon('book'), '출처와 만든 사람'), el('h2', {}, '〈경보 이전〉에 대하여')),
    el('div', { class: 'about-scroll', tabindex: '0', 'aria-label': '내용' },
      el('section', { class: 'about-fiction' },
        el('h3', {}, icon('info'), '가상의 이야기입니다'),
        el('p', {}, '연무동, 한빛아파트, 한결물류, 바로편의점과 모든 등장인물·사건은 가상입니다. 실제 지역·기관·인물과 관계가 없습니다.'),
        el('p', {}, '게임 속 재난문자, 재난방송, 라디오 주파수, 통제 구간, 출국 제한 같은 조건은 이야기를 위해 만든 것입니다. 어떤 신호 하나가 실제 위기를 뜻하지는 않습니다.'),
        el('p', {}, '게임이 알려 주는 안전 행동요령은 아래 공식 자료에서 직접 확인한 문장만 인용합니다. 실제 상황에서는 재난방송, 재난문자, 국민안전24와 지자체 안내를 따르세요.')),
      el('section', { class: 'about-sources' },
        el('h3', {}, icon('official'), `참고한 공식 자료 ${(sources || []).length}건`),
        [...byPub].map(([pub, list]) => el('div', { class: 'src-group' }, el('h4', {}, pub),
          el('ul', {}, list.map((s) => el('li', {},
            s.url ? el('a', { href: s.url, target: '_blank', rel: 'noopener noreferrer' }, s.title) : el('span', {}, s.title),
            s.checkedAt ? el('small', {}, ` · 확인 ${s.checkedAt}`) : null)))))),
      el('section', { class: 'about-credits' },
        el('h3', {}, icon('people'), '만든 사람과 도구'),
        el('ul', {},
          el('li', {}, '기획·개발: 〈경보 이전〉 제작팀 (Claude Code와 함께 제작)'),
          el('li', {}, '3D 렌더링: Three.js r180 (MIT 라이선스)'),
          el('li', {}, '소리: 녹음 파일 없이 Web Audio로 직접 합성'),
          el('li', {}, '글꼴: 기기에 설치된 Pretendard · Noto Sans KR · 맑은 고딕')))),
    el('div', { class: 'set-actions' }, btn('닫기', { primary: true, 'data-autofocus': true, onclick: () => m.close() })));
  const m = modal({ className: 'about', label: '출처와 만든 사람', content, onKey(e) { if (e.key === 'Escape') { e.preventDefault(); m.close(); } } });
  return m.done;
}

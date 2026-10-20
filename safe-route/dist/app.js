import { createWorld, LOCATIONS, MAP_BUILDINGS, STARTS } from './world.js';
import { SOURCES, STORY, ENTRANCES, SEOYEON_CARD, MAX_NOTE, createRun, recordEvent, noteContext, composeNote, makeHandover, buildPins, closingLines, buildDebrief } from './learning.js';
import { createReread } from './notecheck.js';

const $ = (id) => document.getElementById(id);
const escape = (s) => String(s ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
let world, run, phase = 'landing', chapter = 0, target = null, snapshot, visited = new Set();
let paths = [[], []], lastPosition, toastTimer, audio, soundEnabled = false, reread = null;
const dialog = $('dialog');
const names = ['준', '서연'];
const PLACES = { notice: '현장 안내', gate: '동쪽 입구' };
const shortLabel = id => ENTRANCES[id]?.short || PLACES[id];
const reduceMotion = matchMedia('(prefers-reduced-motion: reduce)').matches;
// Story dialogs and the note composer close only through their own buttons.
const locked = () => phase === 'transition' || phase === 'compose';

function toast(message) {
  $('toast').textContent = message; $('toast').classList.add('visible');
  clearTimeout(toastTimer); toastTimer = setTimeout(() => $('toast').classList.remove('visible'), 4200);
}

function chime() {
  if (!soundEnabled) return;
  try {
    audio ??= new AudioContext();
    audio.resume();
    const o = audio.createOscillator(), g = audio.createGain();
    o.type = 'sine'; o.frequency.setValueAtTime(540, audio.currentTime); o.frequency.linearRampToValueAtTime(810, audio.currentTime + .12);
    g.gain.setValueAtTime(.035, audio.currentTime); g.gain.exponentialRampToValueAtTime(.001, audio.currentTime + .3);
    o.connect(g); g.connect(audio.destination); o.start(); o.stop(audio.currentTime + .3);
  } catch { soundEnabled = false; $('sound').textContent = '음소거'; }
}

function openDialog({eyebrow = 'CIVILIAN RELAY', title, body, buttons = [], after}) {
  world?.setActive(false);
  $('dialog-content').innerHTML = `<div class="dialog-eyebrow">${escape(eyebrow)}</div><h2 class="dialog-title" id="dialog-title">${escape(title)}</h2><div class="dialog-body">${body}</div><div class="dialog-actions"></div>`;
  const actions = $('dialog-content').querySelector('.dialog-actions');
  buttons.forEach((button, i) => {
    const element = document.createElement('button'); element.className = button.secondary ? 'secondary' : 'primary'; element.textContent = button.label;
    element.addEventListener('click', () => { dialog.close(); button.action?.(); resume(); });
    actions.append(element); if (!i) element.autofocus = true;
  });
  if (!dialog.open) dialog.showModal();
  after?.();
}
// Buttons resume directly; the close event covers Esc and may arrive after the next dialog opened.
function resume() { if (phase === 'play' && !dialog.open) { world?.setActive(true); $('world').focus({preventScroll:true}); } }
dialog.addEventListener('close', resume);

const events = (type, ch = chapter) => run?.events.some(e => e.type === type && e.details.chapter === ch);
const lastChoice = () => run?.events.filter(e => e.type === 'notice_choice').at(-1)?.details.route;
function setTarget(id) { target = id; world?.setTarget(id); }
function sourceLink(id) { const s=SOURCES.find(s=>s.id===id);return s?`<a href="${escape(s.url)}" target="_blank" rel="noopener noreferrer">근거: ${escape(s.title)} ↗</a>`:''; }

function updateObjectives() {
  if (!run) return;
  $('chapter-label').textContent = `CHAPTER 0${chapter + 1} / 02`;
  $('character-name').textContent = `${names[chapter]}의 대피`;
  $('character-description').textContent = chapter ? '보행 보조기를 쓰는 이웃 · 서쪽 골목에서 출발' : '혼자 사는 동네 주민 · 남쪽 큰길에서 출발';
  const read = events('notice_read');
  let title, copy, steps, goal;
  if (!chapter) {
    const seen = events('gate_observed');
    $('clock-label').textContent = seen ? STORY.gateSeenAt : STORY.alertAt;
    title = !read ? '현장 안내를 확인하세요' : !seen ? '대피시설 쪽으로 이동' : 'A 계단 또는 B 진출입로로 대피';
    copy = !read ? '앞쪽 청록색 안내판에 가까이 가서 E를 누르세요.' : !seen ? '가는 길의 표지를 살피세요. 위험을 확인하려고 돌아갈 필요는 없습니다.' : '동쪽 출입구는 닫혀 있습니다. 운영 중인 다른 출입구로 들어가세요.';
    steps = [[read, '현장 안내 확인'], [seen, '가는 길의 출입구 상황 파악'], [false, '대피 후 서연에게 기록 보내기']];
    goal = !read ? 'notice' : !seen ? 'gate' : null;
  } else {
    const chosen = lastChoice();
    $('clock-label').textContent = STORY.receivedAt;
    title = !read ? '지금의 안내부터 확인할게요' : '내가 정한 입구로 갈게요';
    copy = !read ? '안내판으로 가서 E. 준의 기록은 왼쪽 아래에 있어요.' : chosen ? `${ENTRANCES[chosen].label} 쪽으로 갑니다.` : '안내판의 입구 가운데 하나를 고르세요.';
    steps = [[true, '준의 기록 받기'], [true, '내 기준에 대어 보기'], [read, '지금 안내 확인'], [false, '내가 정한 입구로 대피']];
    goal = !read ? 'notice' : chosen || null;
  }
  $('objective-title').textContent = title; $('objective-copy').textContent = copy;
  $('steps').innerHTML = steps.map(([done, label]) => `<li class="${done ? 'done' : ''}">${label}</li>`).join('');
  setTarget(goal);
}

function begin() {
  run = createRun(); paths = [[], []]; chapter = 0;
  $('landing').hidden = true; $('results').hidden = true; $('hud').hidden = false;
  document.body.classList.add('playing'); startChapter(0);
}
function play() { phase = 'play'; world.setActive(true); $('world').focus(); chime(); }

function startChapter(index) {
  chapter = index; run.chapter = index; visited = new Set(); lastPosition = null;
  world.setChapter(index); world.setVisited([]); setView(index ? 'first' : 'third'); updateObjectives(); phase = 'transition';
  $('legacy-note').hidden = true;
  if (!index) {
    openDialog({eyebrow: 'CHAPTER 01 · 낯선 경보', title: '익숙한 동네에 공습경보가 울렸습니다.',
      body: `<div class="sms"><div class="sms-head">[위급재난문자] · 가상 훈련</div><p>${STORY.alertAt} 미사일 발사, 연무동지역 공습경보, 지하시설 또는 건물 내로 대피. EVACUATION, MISSILES ATTACK [연무구(가상)]</p></div><p>당신은 연무동 주민 <strong>준</strong>입니다. 공습경보 사이렌(1분 동안 오르내리는 소리)이 울립니다. 가까운 지정 대피시설로 대피하고, 방송과 현장 안내를 따릅니다.</p><p>대피한 뒤에는 같은 동 이웃 <strong>서연</strong>에게 메시지를 보냅니다. 무엇을 쓸지는 당신이 정합니다.</p><div class="callout">WASD / 방향키 이동 · 드래그로 시점 · V 1인칭/3인칭<br>표지 가까이에서 E · M 지도 · Esc 일시정지</div><div class="metadata"><span>공습경보 · 호우 없음 · 가상 훈련</span><span>시간 순위와 사망 점수 없음</span></div>`,
      buttons: [{label: '준으로 대피 시작 →', action: play}]});
  } else {
    openDialog({eyebrow: `CHAPTER 02 · ${STORY.receivedAt} · 연무동 서쪽 골목`, title: '같은 문장, 다른 사람.',
      body: `<p>4분 뒤, 같은 동에 사는 <strong>서연</strong>의 휴대폰에 준의 메시지가 도착했습니다. 이제 당신은 서연입니다.</p><div class="callout"><b>서연의 재난 대비 카드</b><ul class="card-list">${SEOYEON_CARD.map(line => `<li>${escape(line)}</li>`).join('')}</ul></div><div class="metadata"><span>보냄 ${STORY.sentAt} · 받음 ${STORY.receivedAt}</span><span>1인칭 시점 · V로 3인칭</span></div>`,
      buttons: [{label: '서연으로 시작 →', action: startReread}]});
  }
}

function markPlace(id) { visited.add(id); world.setVisited([...visited]); }
function readNotice() {
  if (!events('notice_read')) recordEvent(run, 'notice_read', {chapter});
  markPlace('notice'); updateObjectives();
  if (!chapter) {
    openDialog({eyebrow: `현장 안내 · ${STORY.alertAt} · 연무동 A–03`, title: '가까운 지정 대피시설로 이동하세요.',
      body: `<div class="metadata"><span>출처: 가상 현장 안내자</span><span>발표 시각: ${STORY.alertAt}</span></div><p>한빛 생활문화센터 지하주차장(지하 1층)이 지정 대피시설입니다.</p><ul class="card-list"><li>출입구 A · 계단</li><li>출입구 B · 지하주차장 진출입로 (차량 통제, 걸어서 대피 가능)</li><li>출입구 C · 경사로 (준비 중)</li></ul><div class="callout">추가 정보를 모으려고 대피를 미루지 않습니다.</div>`,
      buttons: [{label: '안내를 확인했습니다', action: chime}]});
    return;
  }
  const choose = route => () => {
    recordEvent(run, 'notice_choice', {chapter, route}); updateObjectives();
    toast(route === 'access' ? `준의 기록(${STORY.sentAt})에는 없던 입구 · ${STORY.accessOpenAt}에 열림` : '그 입구로 표시했어요. 가까이 가서 다시 판단할 수 있어요.');
  };
  openDialog({eyebrow: `현장 안내 · ${STORY.accessOpenAt} 갱신`, title: '지금 운영 중인 출입구',
    body: `<div class="metadata"><span>출처: 가상 현장 안내자</span><span>갱신 시각: ${STORY.accessOpenAt}</span><span>적용: 이 시설</span></div><ul class="card-list"><li>동쪽 출입구: 폐쇄 유지</li><li>A 계단: 운영</li><li>B 지하주차장 진출입로: 운영 · 차량 통제 · 내리막이 가파름</li><li>C 경사로: ${STORY.accessOpenAt} 개방 · 유도블록 설치 · 안내자 있음</li></ul><div class="callout">시설 안에서는 관리자의 지시와 안내를 따릅니다.</div><p>어느 입구로 갈지는 서연이 정합니다.</p>`,
    buttons: [{label: 'C로 갈게요', action: choose('access')}, {label: 'A로 갈게요', secondary: true, action: choose('stairs')}, {label: 'B로 갈게요', secondary: true, action: choose('ramp')}]});
}
function observeGate() {
  if (!events('gate_observed')) recordEvent(run, 'gate_observed', {chapter, via: 'sign', time: STORY.gateSeenAt});
  markPlace('gate'); updateObjectives();
  openDialog({eyebrow: '현재 위치에서 보이는 정보', title: '동쪽 출입구는 닫혀 있습니다.', body: '<p>통제 표지에는 <strong>“동쪽 출입구 폐쇄 · A 계단 / B 진출입로 이용”</strong>이라고 적혀 있습니다.</p><div class="callout">확인한 사실: 이 출입구가 닫혀 있음.<br>이 사실만으로 알 수 없는 것: 시설 전체의 운영 여부.</div><p>더 가까이 조사하지 않고 안내된 다른 출입구로 이동합니다.</p>', buttons: [{label: '다른 출입구로 이동', action: chime}]});
}

function enterShelter(route) {
  recordEvent(run, 'route_attempt', {chapter, route});
  if (!chapter) {
    if (!events('gate_observed')) recordEvent(run, 'gate_observed', {chapter: 0, via: 'notice_after', time: STORY.gateSeenAt});
    markPlace(route); recordEvent(run, 'chapter_complete', {chapter: 0, route});
    openCompose();
    return;
  }
  if (route === 'access') {
    markPlace(route); recordEvent(run, 'chapter_complete', {chapter: 1, route}); phase = 'transition'; setView('third');
    $('clock-label').textContent = STORY.arrivedAt;
    openDialog({eyebrow: `두 번째 대피 완료 · ${STORY.arrivedAt}`, title: '서연이 출입구 C로 대피했습니다.', body: `<p>시설 안에서는 관리자의 지시와 안내를 따르고, 방송을 계속 듣습니다.</p><p>${sourceLink('korea_alert')}</p>`, buttons: [{label: '선택의 흔적 보기 →', action: showResults}]});
    return;
  }
  // 서연 is free to walk up to A or B; up close she judges them against her own card.
  if (!events('notice_read')) recordEvent(run, 'notice_read', {chapter, via: 'guide_at_entrance'});
  openDialog({eyebrow: '서연의 판단', title: '가까이 와서 보니 내 카드 기준과 맞지 않아요.',
    body: `<p>${route === 'stairs' ? '계단은 혼자 내려가지 않아요. 보조기와 떨어지지도 않을 거예요.' : '이 내리막은 가팔라서 혼자 내려가지 않아요.'}</p><p>입구의 안내자가 <strong>C 경사로가 ${STORY.accessOpenAt}에 열렸다</strong>고 알려 줍니다. 두 선택 모두 서연이 고를 수 있습니다.</p>`,
    buttons: [{label: 'C 경사로로 계획 바꾸기', action: () => helpChoice('self')}, {label: '안내자에게 C까지 함께 걸어 달라고 요청하기', secondary: true, action: () => helpChoice('escort')}]});
}
function helpChoice(choice) {
  recordEvent(run, 'help_choice', {chapter, choice});
  recordEvent(run, 'notice_choice', {chapter, route: 'access'}); updateObjectives();
  if (choice === 'self') { toast('C 경사로로 계획을 바꿨어요.'); return; }
  openDialog({eyebrow: '도움 요청', title: '“어떻게 도와드릴까요?”', body: `<p>안내자가 먼저 묻습니다.</p><div class="callout">서연: “C까지 옆에서 같이 걸어 주세요. 보조기는 제가 밀게요.”</div><p>${sourceLink('etiquette')}</p>`,
    buttons: [{label: '함께 C로 이동', action: () => toast('안내자가 C까지 함께 걷습니다.')}]});
}

// 준 writes to 서연. Every chip label is the exact fragment that goes into the message.
function openCompose() {
  phase = 'compose';
  const ctx = noteContext(run);
  const chips = {what: null, time: false, route: false, unknowns: false, advice: null};
  const radio = (name, options) => options.map(([value, label]) => `<label class="pill"><input type="radio" name="${name}" value="${value}"><span>${escape(label)}</span></label>`).join('');
  const row = (legend, inner) => `<fieldset class="chips"><legend>${legend}</legend>${inner}</fieldset>`;
  openDialog({eyebrow: `서연(같은 동 이웃)에게 보내기 · ${STORY.sentAt}`, title: '무엇을 남길까요?',
    body: `<p>준은 대피시설에 들어왔습니다. 서연에게 보낼 메시지를 고르거나 직접 고쳐 쓰세요.</p>
      ${row('무엇을 봤나', radio('what', [['gate_closed', composeNote({what: 'gate_closed'}, ctx)], ['all_closed', composeNote({what: 'all_closed'}, ctx)]]))}
      ${row('언제', radio('time', [['true', `${STORY.gateSeenAt}에`], ['false', '시각 없이']]))}
      ${row('내가 쓴 길', radio('route', [['true', composeNote({route: true}, ctx)], ['false', '쓰지 않기']]))}
      ${row('모르는 것', `<label class="pill"><input type="checkbox" name="unknowns"><span>${escape(composeNote({unknowns: true}, ctx))}</span></label>`)}
      ${row('한마디', radio('advice', [['follow_me', composeNote({advice: 'follow_me'}, ctx)], ['direction', composeNote({advice: 'direction'}, ctx)], ['check_notice', composeNote({advice: 'check_notice'}, ctx)], ['', '없음']]))}
      <label class="note-label" for="note-text">보낼 문장 <small>최대 ${MAX_NOTE}자 · 직접 고칠 수 있어요</small></label>
      <textarea id="note-text" maxlength="${MAX_NOTE}" rows="3"></textarea>
      <div class="compose-actions"><button type="button" id="note-reset" class="secondary" hidden>칩으로 다시 쓰기</button><button type="button" id="note-send" class="primary" disabled>보내기 · ${STORY.sentAt} <span>↗</span></button></div>`,
    after: () => {
      const text = $('note-text'), send = $('note-send'), reset = $('note-reset');
      let edited = false;
      const refresh = () => {
        if (!edited) text.value = composeNote(chips, ctx);
        send.disabled = !(edited ? text.value.trim() : chips.what);
        reset.hidden = !edited;
      };
      $('dialog-content').querySelectorAll('input').forEach(input => input.addEventListener('change', () => {
        if (input.name === 'unknowns') chips.unknowns = input.checked;
        else if (input.name === 'time' || input.name === 'route') chips[input.name] = input.value === 'true';
        else chips[input.name] = input.value || null;
        refresh();
      }));
      text.addEventListener('input', () => { edited = true; refresh(); });
      reset.addEventListener('click', () => { edited = false; refresh(); });
      send.addEventListener('click', () => {
        makeHandover(run, {chips, text: text.value, edited});
        phase = 'transition'; dialog.close(); chime(); startChapter(1);
      });
      refresh();
    }});
}

// "다시 읽기": 서연 reads 준's sentence at her own eye height, then sees why it does not carry over.
const anchorOf = id => id === 'start0' ? STARTS[0] : id === 'start1' ? STARTS[1] : LOCATIONS.find(l => l.id === id);
function markedNote(h) {
  const spans = [...new Set(Object.values(h.claims.quotes))].map(q => [h.text.indexOf(q), q]).filter(([i]) => i >= 0).sort((a, b) => a[0] - b[0]);
  let html = '', at = 0, n = 0;
  for (const [i, q] of spans) {
    if (i < at) continue;
    html += `${escape(h.text.slice(at, i))}<mark class="claim">${escape(q)}<sup>${++n}</sup></mark>`;
    at = i + q.length;
  }
  return html + escape(h.text.slice(at));
}
function showNote(mode) {
  const h = run.handover, note = $('legacy-note');
  note.hidden = false; note.classList.toggle('phone', mode !== 'panel');
  $('legacy-meta').textContent = `준 · 같은 동 이웃 · 보냄 ${h.sentAt} · 받음 ${h.receivedAt}`;
  $('legacy-text').innerHTML = mode === 'marked' ? markedNote(h) : escape(h.text);
  $('legacy-compare').hidden = mode !== 'panel';
}
function caption(text, pin, last = false) {
  $('caption').hidden = false;
  $('caption').innerHTML = `<p>${escape(text)}</p>${pin ? `<p class="pin-lines"><span class="was">${escape(pin.was)}</span><span class="now">${escape(pin.now)}</span></p>` : ''}<button type="button" id="caption-next" class="primary">${last ? '서연으로 계속' : '다음'} <span>▸</span></button><small>E · Enter · Space로 넘기기 · Esc로 끝까지</small>`;
  $('caption-next').onclick = rereadNext;
  $('caption-next').focus({preventScroll: true});
}
function placePin(i) {
  const p = reread.pins[i];
  reread.fx.pin(i + 1, anchorOf(p.anchor), p.was, p.now);
  if (p.also) reread.fx.pin('✕', anchorOf(p.also.anchor), ENTRANCES[p.also.anchor].label, p.also.now, 1.1);
  if (p.line) reread.fx.link(anchorOf(p.anchor), anchorOf(p.line));
  return p;
}
function tickClock() {
  const times = ['14:12', '14:13', '14:14', '14:15', STORY.receivedAt];
  if (reduceMotion) { $('clock-label').textContent = STORY.receivedAt; return; }
  times.forEach((t, i) => setTimeout(() => { $('clock-label').textContent = t; }, i * 300));
}
const REREAD_STEPS = [
  () => { reread.fx.eye(-0.5); showNote('phone'); caption('당신이 방금 준으로서 쓴 문장입니다. 서연에게 닿은 것은 이 문장뿐입니다.'); },
  () => { showNote('marked'); caption(run.handover.kind === 'unparsed' ? '이 문장에서 입구·시각·범위를 찾지 못했어요.' : '문장을 나눠 봅니다. 밑줄 친 부분이 서연이 기댈 수 있는 정보입니다.'); },
  () => { $('legacy-note').hidden = true; world.setView('third'); reread.fx.crane(); reread.fx.showPath(paths[0]); caption('준이 실제로 걸은 길 — 당신은 알고 있지만, 서연은 보지 못했습니다.'); },
  () => caption('준이 고를 수 있던 입구는 둘 다 서연의 기준과 다릅니다.', placePin(0)),
  () => { tickClock(); caption('기록은 그때의 사실입니다. 지금은 4분 뒤입니다.', placePin(1)); },
  () => caption(closingLines(run.handover.kind).join(' '), placePin(2), true),
];
function startReread() {
  phase = 'reread';
  world.setActive(false);
  document.body.classList.add('cinematic');
  reread = {fx: createReread(world, {start: STARTS[1], reduceMotion}), index: -1, pins: buildPins(run)};
  rereadNext();
}
function rereadNext() {
  if (!reread) return;
  reread.index += 1;
  if (reread.index < REREAD_STEPS.length) { REREAD_STEPS[reread.index](); chime(); return; }
  recordEvent(run, 'reread_done', {chapter: 1, pins: reread.pins.map(p => p.factor)});
  endReread();
  setView('first'); $('clock-label').textContent = STORY.receivedAt;
  showNote('panel'); updateObjectives(); play();
}
function rereadSkip() { while (reread && reread.index < REREAD_STEPS.length - 1) rereadNext(); }
function endReread() {
  reread?.fx.dispose(); reread = null;
  document.body.classList.remove('cinematic'); $('caption').hidden = true;
}

function interact() {
  if (phase !== 'play' || dialog.open || !snapshot?.nearby) return;
  const id = snapshot.nearby.id;
  if (id === 'notice') readNotice(); else if (id === 'gate') observeGate(); else enterShelter(id);
}

function mapDraw(canvas, {player = false, showPaths = false, labels = true} = {}) {
  const ctx = canvas.getContext('2d'); const w=canvas.width,h=canvas.height;
  ctx.clearRect(0,0,w,h);
  const scale=Math.min((w-35)/75,(h-30)/87); const px=x=>w/2+x*scale, pz=z=>h/2+(z+2)*scale;
  ctx.strokeStyle='#afc5b214';ctx.lineWidth=.7;
  for(let x=-40;x<=40;x+=10){ctx.beginPath();ctx.moveTo(px(x),0);ctx.lineTo(px(x),h);ctx.stroke();}
  for(let z=-45;z<=45;z+=10){ctx.beginPath();ctx.moveTo(0,pz(z));ctx.lineTo(w,pz(z));ctx.stroke();}
  ctx.strokeStyle='#93ada324';ctx.lineWidth=8*scale;ctx.lineCap='square';
  ctx.beginPath();ctx.moveTo(px(0),pz(36));ctx.lineTo(px(0),pz(-28));ctx.stroke();
  ctx.beginPath();ctx.moveTo(px(-25),pz(0));ctx.lineTo(px(26),pz(0));ctx.stroke();
  ctx.beginPath();ctx.moveTo(px(-17),pz(-20));ctx.lineTo(px(18),pz(-20));ctx.stroke();
  ctx.fillStyle='#78958936';
  for(const b of MAP_BUILDINGS) ctx.fillRect(px(b.x-b.w/2),pz(b.z-b.d/2),b.w*scale,b.d*scale);
  if(showPaths){paths.forEach((path,i)=>{ctx.beginPath();ctx.strokeStyle=i?'#85c7bb':'#f6bb6e';ctx.lineWidth=2.4;ctx.lineJoin='round';path.forEach((p,n)=>n?ctx.lineTo(px(p.x),pz(p.z)):ctx.moveTo(px(p.x),pz(p.z)));ctx.stroke();});}
  LOCATIONS.filter(point=>showPaths||!point.chapters||point.chapters.includes(chapter)).forEach(point=>{
    const active = phase === 'play' && point.id === target;
    ctx.fillStyle=active?'#f6bb6e':point.id==='gate'?'#bd8f72':'#91bca6';
    ctx.save();ctx.translate(px(point.x),pz(point.z));ctx.rotate(Math.PI/4);ctx.fillRect(-2.5,-2.5,5,5);ctx.restore();
    const text = showPaths && point.id === 'access' ? `C 경사로 · ${STORY.accessOpenAt} 개방` : shortLabel(point.id);
    if(labels){ctx.font=`${Math.max(8,w/40)}px sans-serif`;ctx.textAlign=point.x<0?'right':'left';ctx.fillStyle=active?'#ffcc85':'#a7c7b4';ctx.fillText(text,px(point.x)+(point.x<0?-7:7),pz(point.z)-7);}
  });
  if(player && snapshot){ctx.beginPath();ctx.arc(px(snapshot.x),pz(snapshot.z),4,0,Math.PI*2);ctx.fillStyle='#ffd59a';ctx.fill();ctx.strokeStyle='#182e2a';ctx.lineWidth=2;ctx.stroke();}
  ctx.font='9px sans-serif';ctx.fillStyle='#bfd3c26e';ctx.textAlign='right';ctx.fillText('N ↑',w-9,14);
}

function onUpdate(value) {
  snapshot = value;
  if(phase!=='play')return;
  $('interaction').hidden = !value.nearby || dialog.open;
  if(value.nearby) $('interaction-text').textContent = `${shortLabel(value.nearby.id)} 확인`;
  if(!dialog.open && (!lastPosition || Math.hypot(value.x-lastPosition.x,value.z-lastPosition.z)>.8)){
    if(paths[chapter].length<1800) paths[chapter].push({x:+value.x.toFixed(2),z:+value.z.toFixed(2)});
    lastPosition={x:value.x,z:value.z};
  }
  mapDraw($('minimap'),{player:true});
}
function showMap() {
  if(phase!=='play'||dialog.open)return;
  openDialog({eyebrow:'가상 훈련 지도 · 연무동',title:'지금 내 조건에 맞는 길',body:'<canvas class="full-map" id="full-map" width="760" height="460" aria-label="현재 위치와 출입구 지도"></canvas><div class="legend"><span>● 현재 위치</span><span>◇ 출입구 / 안내판</span></div><p>이 지도는 가상 공간입니다. 표시된 위치가 실제 상황의 안전을 보장하지 않습니다.</p>',buttons:[{label:'이동으로 돌아가기'}],after:()=>mapDraw($('full-map'),{player:true})});
}

const STATUS = {helped: ['✓', '도움됨'], recheck: ['?', '다시 확인'], mismatch: ['✕', '맞지 않음'], missing: ['○', '기록에 없음']};
function showResults() {
  phase='results';world.setActive(false);$('hud').hidden=true;$('results').hidden=false;
  const result=buildDebrief(run);
  const rows=result.rows.map(r=>`<tr class="${r.status}"><td><em>${escape(r.label)}</em>${r.quote?`<br>“${escape(r.quote)}”`:''}</td><td><span class="status">${STATUS[r.status][0]} ${STATUS[r.status][1]}</span></td><td>${escape(r.why)}<br>${sourceLink(r.sourceId)}</td></tr>`).join('');
  const card=c=>`<article><h3>${escape(c.title)}</h3><p>${escape(c.body)}</p>${sourceLink(c.sourceId)}</article>`;
  $('results').innerHTML=`<div class="results-head"><div><div class="eyebrow">AFTER ACTION · 선택의 흔적</div><h1>당신의 문장은<br>서연에게 이렇게 읽혔습니다.</h1><p>${escape(result.summary)}</p></div><span class="mode-badge">규칙 기반 복기 · AI 미연결</span></div><div class="result-grid"><div class="route-card"><h2>같은 동네, 서로 다른 길</h2><canvas id="result-map" width="650" height="400" aria-label="준과 서연의 이동 경로 비교"></canvas><div class="legend"><span style="color:var(--amber)">━ 준의 경로</span><span style="color:var(--mint)">━ 서연의 경로</span></div><p>주민의 시각, 출발점, 몸의 조건이 달라지면 맞는 길도 달라집니다. 빠른 도착이나 우연한 성공으로 판단을 평가하지 않습니다.</p><div class="callout">보낸 문장 · ${escape(run.handover?.sentAt)}<br>${escape(run.handover?.text)}</div></div><div class="review-card"><h2>내 문장은 서연에게 어떻게 읽혔나</h2><table class="claim-table"><thead><tr><th>준의 문장</th><th>서연에게는</th><th>왜</th></tr></thead><tbody>${rows}</tbody></table>${result.improvements.map(s=>`<div class="callout">${escape(s)}</div>`).join('')}<div class="tag-row">${result.strengths.map(s=>`<span>✓ ${escape(s)}</span>`).join('')}</div>${result.cards.slice(0,3).map(card).join('')}<details><summary>근거 더 보기</summary>${result.cards.slice(3).map(card).join('')}</details></div></div><div class="result-actions"><button id="practice" class="primary">조건을 바꿔 한 번 더 <span>↗</span></button><button id="replay" class="secondary">처음부터 다시 플레이</button><button id="export" class="secondary">내 훈련 기록 내려받기</button></div><p class="results-foot">출입구 배치, C의 ${STORY.accessOpenAt} 개방, B의 보행 허용, 서연의 카드 문장은 가상 시나리오 설정입니다.<br>학습용 가상 시나리오이며 실제 생존 가능성을 계산하지 않습니다. 외부 AI는 연결되지 않았습니다.</p>`;
  mapDraw($('result-map'),{showPaths:true});$('practice').onclick=()=>showPractice(result.practice);$('replay').onclick=begin;$('export').onclick=exportRun;chime();
}
function showPractice(practice) {
  openDialog({eyebrow:'TRANSFER TRAINING · 다른 상황에 적용',title:practice.title,body:`<p>${escape(practice.prompt)}</p><div id="practice-options">${practice.options.map(o=>`<button class="choice" data-answer="${escape(o.id)}">${escape(o.label)}</button>`).join('')}</div><div id="practice-response" aria-live="polite"></div><p>${sourceLink(practice.sourceId)}</p>`,buttons:[{label:'복기로 돌아가기',secondary:true}],after:()=>{
    $('practice-options').querySelectorAll('button').forEach(button=>button.onclick=()=>{
      const option=practice.options.find(o=>o.id===button.dataset.answer);
      recordEvent(run,'practice_answer',{chapter,practice:practice.id,answer:option.id,correct:option.correct});
      $('practice-response').innerHTML=`<div class="practice-feedback"><strong>${option.correct?'✓ 상황에 맞는 판단입니다.':'다시 생각해 볼 수 있습니다.'}</strong><br>${escape(option.feedback)}</div>`;
      if(option.correct){$('practice-options').querySelectorAll('button').forEach(b=>b.disabled=true);$('practice').textContent='후속 훈련 완료 · 다시 보기';chime();}
    });
  }});
}
function exportRun(){
  const blob=new Blob([JSON.stringify({game:'이어지는 길',mode:'rule-based-prototype',run,paths},null,2)],{type:'application/json'});
  const url=URL.createObjectURL(blob),link=document.createElement('a');link.href=url;link.download='civilian-relay-training.json';link.click();setTimeout(()=>URL.revokeObjectURL(url),1000);
}
function showGuide(){
  openDialog({eyebrow:'FIELD GUIDE · 훈련 안내',title:'알고 있는 것을 행동으로.',body:`<p>두 주민을 이어서 플레이합니다. 준으로 대피한 뒤 서연에게 메시지를 보내고, 서연이 되어 그 메시지를 자신의 시각·자리·몸의 기준으로 다시 읽습니다.</p><div class="callout">WASD / 방향키: 이동 · Shift: 준 달리기 · V: 1인칭/3인칭<br>마우스 드래그 / J·L: 시점 회전<br>E: 안내 확인 · M: 지도 · Esc: 일시정지</div><p><strong>시나리오:</strong> 공습경보, 호우 없음, 지정 대피시설이 있는 가상 동네입니다. 실제로는 그때의 경보와 관계 기관·현장 안내를 따릅니다.</p><p>이번 버전의 복기는 <strong>규칙 기반</strong>입니다. AI 서비스는 연결되지 않았으며 외부 서버로 플레이 기록을 보내지 않습니다.</p><ul class="source-list">${SOURCES.map(s=>`<li><a href="${escape(s.url)}" target="_blank" rel="noopener noreferrer">${escape(s.title)} ↗</a></li>`).join('')}</ul>`,buttons:[{label:'확인했습니다'}]});
}
function pause(){
  if(phase!=='play'||dialog.open)return;
  openDialog({eyebrow:'PAUSED',title:'잠시 쉬어가세요.',body:'<p>훈련 시간이 멈췄습니다. 준비되면 이어서 진행하세요.</p>',buttons:[{label:'훈련 계속하기'},{label:'처음 화면으로',secondary:true,action:goHome}]});
}
function setView(mode){
  const current=world.setView(mode);
  $('view-label').textContent=current==='first'?'3인칭으로':'1인칭으로';
  $('view-toggle').setAttribute('aria-pressed',String(current==='first'));
  return current;
}
function toggleView(){
  if(phase!=='play'||dialog.open)return;
  const next=setView(world.getSnapshot().view==='first'?'third':'first');
  toast(next==='first'?'1인칭 시점 · 드래그로 둘러봅니다':'3인칭 시점 · 드래그로 카메라를 돌립니다');
}
function goHome(){
  phase='landing'; endReread(); if(dialog.open)dialog.close();
  world.setActive(false);world.setChapter(0);setView('third');world.setTarget(null);
  $('landing').hidden=false;$('hud').hidden=true;$('results').hidden=true;$('legacy-note').hidden=true;document.body.classList.remove('playing');mapDraw($('landing-map'),{labels:false});
}

$('start').onclick=begin;$('guide').onclick=showGuide;$('pause').onclick=pause;$('map-button').onclick=showMap;$('interact-button').onclick=interact;$('view-toggle').onclick=toggleView;
$('home').onclick=e=>{e.preventDefault();if(phase==='play'){openDialog({title:'훈련을 처음부터 시작할까요?',body:'<p>진행 중인 이동 기록은 초기화됩니다.</p>',buttons:[{label:'훈련 계속하기'},{label:'처음 화면으로',secondary:true,action:goHome}]});}else goHome();};
$('sound').onclick=()=>{soundEnabled=!soundEnabled;$('sound').textContent=soundEnabled?'소리 켜짐':'음소거';$('sound').setAttribute('aria-label',soundEnabled?'소리 끄기':'소리 켜기');if(soundEnabled)chime();};
window.addEventListener('keydown',e=>{
  // Keep the browser's own Esc close from undoing pause or closing story dialogs.
  if(e.code==='Escape'&&(!dialog.open||locked()))e.preventDefault();
  if(e.repeat||dialog.open)return;
  if(phase==='reread'){
    if(['KeyE','Enter','Space'].includes(e.code)){e.preventDefault();rereadNext();}
    if(e.code==='Escape')rereadSkip();
    return;
  }
  if(e.code==='KeyE'){e.preventDefault();interact();}if(e.code==='KeyM'){e.preventDefault();showMap();}if(e.code==='KeyV'){e.preventDefault();toggleView();}if(e.code==='Escape')pause();
});
document.addEventListener('visibilitychange',()=>{if(document.hidden && phase==='play'&&!dialog.open)pause();});
window.addEventListener('blur',()=>{if(phase==='play'&&!dialog.open)pause();});

try {
  world=createWorld($('world'),{onUpdate}); world.setActive(false);world.setTarget(null);
  mapDraw($('landing-map'),{labels:false});$('loading').hidden=true;
  if(matchMedia('(pointer:coarse)').matches)toast('이 버전은 키보드와 마우스가 있는 PC에서 플레이할 수 있습니다.');
} catch(error) {
  console.error(error);$('loading').innerHTML='<span class="loader-symbol">↗</span><span>3D 화면을 시작하지 못했습니다.</span><p style="font-size:12px;letter-spacing:0;text-align:center;padding:20px">WebGL을 지원하는 브라우저에서 하드웨어 가속을 켜고 다시 열어 주세요.</p><button class="secondary" onclick="location.reload()">다시 시도</button>';
}

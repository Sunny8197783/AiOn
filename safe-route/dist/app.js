import { createWorld, LOCATIONS, MAP_BUILDINGS } from './world.js';
import { SOURCES, createRun, recordEvent, makeHandover, buildDebrief } from './learning.js';

const $ = (id) => document.getElementById(id);
const escape = (s) => String(s ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
let world, run, phase = 'landing', chapter = 0, target = 'notice', snapshot, visited = new Set();
let paths = [[], []], lastPosition, toastTimer, audio, soundEnabled = false;
const dialog = $('dialog');
const names = ['준', '서연'];
const shortLabels = { notice: '현장 안내', gate: '동쪽 입구', stairs: '계단', ramp: '경사로' };

function toast(message) {
  $('toast').textContent = message; $('toast').classList.add('visible');
  clearTimeout(toastTimer); toastTimer = setTimeout(() => $('toast').classList.remove('visible'), 4200);
}

function chime() {
  if (!soundEnabled) return;
  try {
    audio ??= new (window.AudioContext || window.webkitAudioContext)();
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
    element.addEventListener('click', () => { if (button.close !== false) dialog.close(); button.action?.(); });
    actions.append(element); if (!i) element.autofocus = true;
  });
  if (!dialog.open) dialog.showModal();
  after?.();
}
dialog.addEventListener('close', () => { if (phase === 'play') { world?.setActive(true); $('world').focus({preventScroll:true}); } });

function events(type, currentChapter = chapter) {
  return run?.events.some(e => e.type === type && e.details.chapter === currentChapter);
}
function setTarget(id) { target = id; world?.setTarget(id); }

function updateObjectives() {
  if (!run) return;
  $('chapter-label').textContent = `CHAPTER 0${chapter + 1} / 02`;
  $('character-name').textContent = `${names[chapter]}의 대피`;
  $('character-description').textContent = chapter ? '보행 보조기를 사용하는 주민' : '혼자 이동하는 동네 주민';
  $('clock-label').textContent = chapter ? '14:16' : '14:08';
  const read = events('notice_read');
  const seen = run.events.some(e => e.type === 'gate_observed');
  let title, copy, steps;
  if (!chapter) {
    title = !read ? '현장 안내를 확인하세요' : !seen ? '안내된 동쪽 입구로 이동' : '이용 가능한 출입구로 대피';
    copy = !read ? '앞쪽 청록색 안내소에 가까이 가서 E를 누르세요.' : !seen ? '대피 경로의 표지와 주변 상황을 살피세요. 위험 확인을 위해 우회할 필요는 없습니다.' : '동쪽 입구는 닫혀 있습니다. 지도에 표시된 다른 출입구를 이용하세요.';
    steps = [[read, '현장 안내 확인'], [seen, '이동 중 출입구 상황 파악'], [false, '대피 후 확인한 정보 전달']];
    setTarget(!read ? 'notice' : !seen ? 'gate' : 'stairs');
  } else {
    title = !read ? '이어받은 정보와 현재 안내 비교' : '내가 이용할 수 있는 입구로';
    copy = !read ? '현장 안내소에서 지금 적용되는 안내를 확인하세요.' : '서연은 계단을 이용하기 어렵습니다. 현장 안내의 경사로 출입구를 찾아가세요.';
    steps = [[true, '준이 전달한 기록 받기'], [read, '최신 안내와 이동 조건 확인'], [false, '경사로로 대피 완료']];
    setTarget(!read ? 'notice' : 'ramp');
  }
  $('objective-title').textContent = title; $('objective-copy').textContent = copy;
  $('steps').innerHTML = steps.map(([done, label]) => `<li class="${done ? 'done' : ''}">${label}</li>`).join('');
  $('legacy-note').hidden = !chapter;
  if (chapter) $('legacy-text').textContent = run.handover?.text || '전달받은 기록이 없습니다.';
}

function begin() {
  run = createRun(); paths = [[], []]; chapter = 0;
  $('landing').hidden = true; $('results').hidden = true; $('hud').hidden = false;
  document.body.classList.add('playing'); startChapter(0);
}
function startChapter(index) {
  chapter = index; run.chapter = index; visited = new Set(); lastPosition = null;
  world.setChapter(index); world.setVisited([]); updateObjectives(); phase = 'transition';
  openDialog({
    eyebrow: `CHAPTER 0${index + 1} · ${index ? '이어받은 길' : '낯선 경보'}`,
    title: index ? '같은 길, 다른 조건.' : '익숙한 동네에 경보가 울렸습니다.',
    body: index
      ? `<p>준이 안전한 장소에서 전달한 기록이 현장 안내자를 통해 <strong>서연</strong>에게 도착했습니다. 통신은 여전히 불안정합니다.</p><div class="callout">${escape(run.handover?.text)}</div><p>서연은 보행 보조기를 사용해 <strong>계단을 이용하기 어렵습니다.</strong> 앞사람의 기록과 현재 안내를 함께 보고, 이동 가능한 입구를 선택하세요.</p><div class="metadata"><span>14:16 · 시간은 앞으로 흐릅니다</span><span>걷기 속도로 이동</span></div>`
      : `<p>당신은 연무동 주민 <strong>준</strong>입니다. 일반 공습경보가 발령됐고, 휴대폰 연결이 불안정합니다. 가까운 지정 대피시설로 이동하세요.</p><p>이동 중 보이는 안내를 살피고, 대피한 뒤 <strong>확인한 사실만</strong> 다음 사람에게 전달합니다.</p><div class="callout">WASD / 방향키로 이동 · 마우스 드래그로 시점 조절<br>표지 가까이에서 E · M 지도 · Esc 일시정지</div><div class="metadata"><span>일반 공습 · 침수 없음</span><span>시간 순위와 사망 점수 없음</span></div>`,
    buttons: [{label: index ? '서연으로 이어가기 →' : '준으로 대피 시작 →', action: () => { phase = 'play'; world.setActive(true); $('world').focus(); chime(); }}]
  });
}

function markPlace(id) { visited.add(id); world.setVisited([...visited]); }
function readNotice() {
  if (!events('notice_read')) recordEvent(run, 'notice_read', {chapter});
  markPlace('notice'); updateObjectives();
  openDialog({eyebrow: `현장 안내 · ${chapter ? '14:16' : '14:08'} · 연무동 A–03`, title: chapter ? '현재 운영 중인 출입구를 확인하세요.' : '가까운 지정 대피시설로 이동하세요.',
    body: chapter
      ? `<div class="metadata"><span>출처: 가상 현장 안내자</span><span>확인 시각: 14:16</span><span>적용 지역: 이 시설</span></div><p><strong>동쪽 출입구는 폐쇄</strong>되어 있습니다. 북쪽 시설의 <strong>계단 입구와 경사로 입구는 운영 중</strong>입니다.</p><p>계단 이용이 어려운 주민은 안내된 <strong>경사로 입구</strong>를 이용할 수 있습니다. 이 안내는 현재 훈련 상황의 정보입니다.</p><div class="callout">주민의 이전 기록은 지금의 운영 안내와 다를 수 있습니다. 위치뿐 아니라 시각과 이용 조건을 함께 확인하세요.</div>`
      : `<div class="metadata"><span>출처: 가상 현장 안내자</span><span>발표 시각: 14:08</span></div><p>일반 공습경보 상황입니다. 지도의 <strong>가까운 지정 대피시설</strong>로 이동하세요. 이 경로에서는 동쪽 출입구의 안내 표지가 보입니다.</p><p>입구가 막혀 있으면 현장 안내에 따라 다른 이용 가능한 출입구를 선택하세요. 현장 표지는 가까이 다가가 만질 필요가 없습니다.</p><div class="callout">실제 상황에서는 즉각적인 보호·대피가 우선입니다. 추가 정보를 모으기 위해 위험한 장소로 가지 않습니다.</div>`,
    buttons:[{label:'안내를 확인했습니다',action:chime}]
  });
}
function observeGate() {
  if (!run.events.some(e => e.type === 'gate_observed')) recordEvent(run, 'gate_observed', {chapter,source:'현장 출입 통제 표지',time:'14:10'});
  markPlace('gate'); updateObjectives();
  openDialog({eyebrow:'현재 위치에서 보이는 정보',title:'동쪽 출입구는 닫혀 있습니다.',body:'<p>통제 표지에는 <strong>“동쪽 출입구 폐쇄 · 계단/경사로 출입구 이용”</strong>이라고 적혀 있습니다.</p><div class="callout">확인한 사실: 이 출입구가 닫혀 있음.<br>이 사실만으로 알 수 없는 것: 시설 전체의 폐쇄 여부.</div><p>더 가까이 조사하지 않고 안내된 다른 출입구로 이동합니다.</p>',buttons:[{label:'다른 출입구로 이동',action:chime}]});
}
function enterShelter(route) {
  recordEvent(run, 'route_attempt', {chapter, route});
  if (chapter === 1 && route === 'stairs') {
    if (!events('notice_read')) recordEvent(run, 'notice_read', {chapter:1,source:'계단 입구의 보호된 대기 지점에서 현장 안내자'});
    updateObjectives();
    openDialog({eyebrow:'경로 재계획',title:'서연이 이용하기 어려운 계단입니다.',body:'<p>준이 이 길을 지나갔더라도 서연에게 적합한 경로라는 뜻은 아닙니다. 계단을 무리하게 오르거나 보행 보조기를 버리지 않습니다.</p><div class="callout">입구의 보호된 대기 지점에서 현장 안내를 확인합니다. 안내자는 현재 이용 가능한 경사로 입구를 알려줍니다.</div><p>필요한 도움은 당사자의 의사를 확인하고 요청합니다.</p>',buttons:[{label:'경사로로 계획 바꾸기',action:()=>toast('지도 오른쪽의 경사로 출입구를 확인하세요.')}]});
    return;
  }
  markPlace(route);
  if (chapter === 0) {
    phase = 'transition';
    const sawGate = run.events.some(e => e.type === 'gate_observed');
    if (!sawGate) recordEvent(run, 'gate_observed', {chapter,source:'대피 후 현장 안내문',time:'14:10'});
    // The button shows exactly the sentence 서연 will receive.
    const note = kind => escape(makeHandover(structuredClone(run), kind).text);
    openDialog({eyebrow:'첫 번째 대피 완료 · 정보 인계',title:'다음 사람에게 무엇을 남길까요?',
      body:`<p>준이 대피시설에 도착했습니다. ${sawGate ? '이동 중 확인한' : '대피 후 현장 안내문에서 확인한'} 사실은 <strong>“14:10, 동쪽 출입구 폐쇄”</strong>입니다. 안내자는 이 기록을 다음 주민에게 전달합니다.</p><p>전달할 기록을 선택하세요.</p><button class="choice" data-note="precise"><b>확인한 범위만 전달</b>${note('precise')}</button><button class="choice" data-note="generalized"><b>시설 전체로 확대해 전달</b>${note('generalized')}</button>`,
      after:()=> $('dialog-content').querySelectorAll('[data-note]').forEach(button=>button.addEventListener('click',()=>{
        makeHandover(run,button.dataset.note); recordEvent(run,'chapter_complete',{chapter:0,route});
        dialog.close(); chime(); startChapter(1);
      }))
    });
  } else {
    recordEvent(run,'chapter_complete',{chapter:1,route}); phase = 'transition';
    openDialog({eyebrow:'두 번째 대피 완료',title:'서로 다른 두 길이 이어졌습니다.',body:'<p>서연이 경사로를 통해 대피시설에 도착했습니다. 시설 안에서는 <strong>관리자와 안내방송의 지시에 따라 대기</strong>합니다.</p><p>이제 두 사람의 경로와 전달한 정보가 어떤 선택으로 이어졌는지 살펴봅니다.</p>',buttons:[{label:'선택의 흔적 보기 →',action:showResults}]});
  }
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
  for(const b of MAP_BUILDINGS || []) { if([b.x,b.z,b.w,b.d].every(Number.isFinite)) ctx.fillRect(px(b.x-b.w/2),pz(b.z-b.d/2),b.w*scale,b.d*scale); }
  if(showPaths){paths.forEach((path,i)=>{ctx.beginPath();ctx.strokeStyle=i?'#87d6c7':'#f6bc72';ctx.lineWidth=2.4;ctx.lineJoin='round';path.forEach((p,n)=>n?ctx.lineTo(px(p.x),pz(p.z)):ctx.moveTo(px(p.x),pz(p.z)));ctx.stroke();});}
  LOCATIONS.forEach(point=>{
    const active = phase === 'play' && point.id === target;
    ctx.fillStyle=active?'#f6bb6e':point.id==='gate'?'#bd8f72':'#91bca6';
    ctx.save();ctx.translate(px(point.x),pz(point.z));ctx.rotate(Math.PI/4);ctx.fillRect(-2.5,-2.5,5,5);ctx.restore();
    if(labels){ctx.font=`${Math.max(8,w/40)}px sans-serif`;ctx.textAlign=point.x<0?'right':'left';ctx.fillStyle=active?'#ffcc85':'#a7c7b4';ctx.fillText(shortLabels[point.id],px(point.x)+(point.x<0?-7:7),pz(point.z)-7);}
  });
  if(player && snapshot){ctx.beginPath();ctx.arc(px(snapshot.x),pz(snapshot.z),4,0,Math.PI*2);ctx.fillStyle='#ffd59a';ctx.fill();ctx.strokeStyle='#182e2a';ctx.lineWidth=2;ctx.stroke();}
  ctx.font='9px sans-serif';ctx.fillStyle='#bfd3c26e';ctx.textAlign='right';ctx.fillText('N ↑',w-9,14);
}

function onUpdate(value) {
  snapshot = value;
  if(phase==='play'){
    $('interaction').hidden = !value.nearby || dialog.open;
    if(value.nearby) $('interaction-text').textContent = `${shortLabels[value.nearby.id]} 확인`;
    if(!dialog.open && (!lastPosition || Math.hypot(value.x-lastPosition.x,value.z-lastPosition.z)>.8)){
      if(paths[chapter].length<1800) paths[chapter].push({x:+value.x.toFixed(2),z:+value.z.toFixed(2)});
      lastPosition={x:value.x,z:value.z};
    }
    mapDraw($('minimap'),{player:true});
    const large=$('full-map');if(large)mapDraw(large,{player:true,showPaths:true});
  }
}
function showMap() {
  if(phase!=='play'||dialog.open)return;
  openDialog({eyebrow:'가상 훈련 지도 · 연무동',title:'지금 내 조건에 맞는 길',body:'<canvas class="full-map" id="full-map" width="760" height="460" aria-label="현재 위치와 출입구 지도"></canvas><div class="legend"><span>● 현재 위치</span><span>◇ 출입구 / 안내소</span><span>선: 실제 이동 기록</span></div><p>이 지도는 가상 공간입니다. 표시된 위치가 실제 상황의 안전을 보장하지 않습니다.</p>',buttons:[{label:'이동으로 돌아가기'}],after:()=>mapDraw($('full-map'),{player:true,showPaths:true})});
}

function sourceLink(id) { const s=SOURCES.find(s=>s.id===id);return s?`<a href="${escape(s.url)}" target="_blank" rel="noopener noreferrer">근거: ${escape(s.title)} ↗</a>`:''; }
function showResults() {
  phase='results';world.setActive(false);$('hud').hidden=true;$('results').hidden=false;
  const result=buildDebrief(run);
  $('results').innerHTML=`<div class="results-head"><div><div class="eyebrow">AFTER ACTION · 선택의 흔적</div><h1>당신의 선택은<br>다음 사람에게 이어졌습니다.</h1><p>${escape(result.summary)}</p></div><span class="mode-badge">규칙 기반 복기 · AI 미연결</span></div><div class="result-grid"><div class="route-card"><h2>같은 동네, 서로 다른 경로</h2><canvas id="result-map" width="650" height="400" aria-label="준과 서연의 이동 경로 비교"></canvas><div class="legend"><span style="color:#f6bb6e">━ 준의 경로</span><span style="color:#85c7bb">━ 서연의 경로</span></div><p>주민의 이동 조건과 정보가 달라지면 적절한 경로도 달라집니다. 빠른 도착이나 우연한 성공으로 판단을 평가하지 않습니다.</p><div class="callout">전달한 기록<br>${escape(run.handover?.text)}</div></div><div class="review-card"><h2>기록을 바탕으로 돌아보기</h2>${result.improvements.map(s=>`<div class="callout">${escape(s)}</div>`).join('')}${result.cards.map(c=>`<article><h3>${escape(c.title)}</h3><p>${escape(c.body)}</p>${sourceLink(c.sourceId)}</article>`).join('')}<div class="tag-row">${result.strengths.slice(0,3).map(s=>`<span>✓ ${escape(s)}</span>`).join('')}</div></div></div><div class="result-actions"><button id="practice" class="primary">조건을 바꿔 한 번 더 <span>↗</span></button><button id="replay" class="secondary">처음부터 다시 플레이</button><button id="export" class="secondary">내 훈련 기록 내려받기</button></div><p class="results-foot">행동 기록으로 선택한 후속 훈련입니다. 외부 AI는 연결되지 않았습니다.<br>학습용 가상 시나리오이며 실제 생존 가능성을 계산하지 않습니다.</p>`;
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
  openDialog({eyebrow:'FIELD GUIDE · 훈련 안내',title:'알고 있는 것을 행동으로.',body:`<p>두 주민을 순서대로 플레이하며 경보 확인, 정보의 범위, 경로 재계획을 연습합니다. 첫 사람의 기록이 다음 사람에게 이어집니다.</p><div class="callout">WASD / 방향키: 이동 · Shift: 준 달리기<br>마우스 드래그 / J·L: 카메라 회전<br>E: 안내 확인 · M: 지도 · Esc: 일시정지</div><p><strong>첫 시나리오:</strong> 일반 공습, 침수 없음, 지정 시설의 이용 가능한 출입구가 있는 가상 동네입니다. 실제로는 현지의 최신 경보와 관계 기관 안내를 따릅니다.</p><p>이번 버전의 복기는 <strong>규칙 기반</strong>입니다. AI 서비스는 연결되지 않았으며 외부 서버로 플레이 기록을 보내지 않습니다.</p><ul class="source-list">${SOURCES.map(s=>`<li><a href="${escape(s.url)}" target="_blank" rel="noopener noreferrer">${escape(s.title)} ↗</a></li>`).join('')}</ul><p>해외 자료는 일반적인 학습 원칙에 참고했습니다. 해외 신고 번호와 지역별 제도를 한국의 절차로 적용하지 않습니다.</p>`,buttons:[{label:'확인했습니다'}]});
}
function pause(){
  if(phase!=='play'||dialog.open)return;
  openDialog({eyebrow:'PAUSED',title:'잠시 쉬어가세요.',body:'<p>훈련 시간이 멈췄습니다. 준비되면 이어서 진행하세요.</p>',buttons:[{label:'훈련 계속하기'},{label:'처음 화면으로',secondary:true,action:goHome}]});
}
function setView(mode){
  const current=world?.setView(mode)||'third';
  $('view-label').textContent=current==='first'?'3인칭으로':'1인칭으로';
  $('view-toggle').setAttribute('aria-pressed',String(current==='first'));
  return current;
}
function toggleView(){
  if(phase!=='play'||dialog.open)return;
  const next=setView(snapshot?.view==='first'?'third':'first');
  toast(next==='first'?'1인칭 시점 · 드래그로 둘러봅니다':'3인칭 시점 · 드래그로 카메라를 돌립니다');
}
function goHome(){phase='landing';world.setActive(false);world.setChapter(0);world.setTarget(null);$('landing').hidden=false;$('hud').hidden=true;$('results').hidden=true;document.body.classList.remove('playing');mapDraw($('landing-map'),{labels:false});}

$('start').onclick=begin;$('guide').onclick=showGuide;$('pause').onclick=pause;$('map-button').onclick=showMap;$('interact-button').onclick=interact;
$('home').onclick=e=>{e.preventDefault();if(phase==='play'){openDialog({title:'훈련을 처음부터 시작할까요?',body:'<p>진행 중인 이동 기록은 초기화됩니다.</p>',buttons:[{label:'훈련 계속하기'},{label:'처음 화면으로',secondary:true,action:goHome}]});}else goHome();};
$('sound').onclick=()=>{soundEnabled=!soundEnabled;$('sound').textContent=soundEnabled?'소리 켜짐':'음소거';$('sound').setAttribute('aria-label',soundEnabled?'소리 끄기':'소리 켜기');if(soundEnabled)chime();};
window.addEventListener('keydown',e=>{
  // Keep the browser's own Esc close from undoing pause or closing story dialogs.
  if(e.code==='Escape'&&(!dialog.open||phase==='transition'))e.preventDefault();
  if(e.repeat)return;if(dialog.open)return;
  if(e.code==='KeyE'){e.preventDefault();interact();}if(e.code==='KeyM'){e.preventDefault();showMap();}if(e.code==='KeyV'){e.preventDefault();toggleView();}if(e.code==='Escape')pause();
});
document.addEventListener('visibilitychange',()=>{if(document.hidden && phase==='play'&&!dialog.open)pause();});
window.addEventListener('blur',()=>{if(phase==='play'&&!dialog.open)pause();});

try {
  world=createWorld($('world'),{onUpdate}); world.setActive(false);world.setTarget(null);
  setView('third');$('view-toggle').onclick=toggleView;
  mapDraw($('landing-map'),{labels:false});$('loading').hidden=true;
  if(matchMedia('(pointer:coarse)').matches)toast('이 버전은 키보드와 마우스가 있는 PC에서 플레이할 수 있습니다.');
} catch(error) {
  console.error(error);$('loading').innerHTML='<span class="loader-symbol">↗</span><span>3D 화면을 시작하지 못했습니다.</span><p style="font-size:12px;letter-spacing:0;text-align:center;padding:20px">WebGL을 지원하는 브라우저에서 하드웨어 가속을 켜고 다시 열어 주세요.</p><button class="secondary" onclick="location.reload()">다시 시도</button>';
}

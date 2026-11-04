// 소식 가려듣기 (design M5) as it is played: a claim reaches 정민, the player names what kind of information it
// is with the notebook labels, then either acts on that judgement or checks once more (the radio, or a notice
// board reached on foot). The posts and the rules live in game/rumors.js; this file only stages them.
//
// hearClaim(ctx, id, { lead }) -> { pick, res, act: 'go'|'check', taken } (ctx.ui.talk: wrap it in ctx.modal)
//   taken = the player took the claim as true and acted on it (believe() ran; G.know.misled when it is false).
// boardWalk(ctx, title, body, read) -> roam until a notice board (aptNotice / centerNotice) has been read.
import { ladderStage, recordEncounter } from '../game/drills.js';
import { posts, judge, believe } from '../game/rumors.js';

// The four labels the notebook uses (state.js LABELS), the same wording as story/neighbors.js.
export const LABEL_CHOICES = [{ id: 'official', label: '공식 안내' }, { id: 'site', label: '현장에서 확인한 것' }, { id: 'neighbor', label: '이웃에게 들은 것' }, { id: 'rumor', label: '소문 · 확인 안 됨' }];
// Pure: labelling a claim 'rumor' sets it aside; any other label takes it as something to act on.
export const takesAsTrue = (pick) => pick !== 'rumor';
// Pure: acting on a claim taken as true is believing it.
export const believes = (pick, act) => act === 'go' && takesAsTrue(pick);

export async function hearClaim(ctx, id, { lead = [] } = {}) {
  const G = ctx.G, [claim] = posts(G, id);
  const phoneOn = G.phone.battery > 0; // without the phone the same post reaches 정민 by word of mouth
  const guided = ladderStage(G, 'info') !== 'free';
  const pick = await ctx.ui.talk({ speaker: phoneOn ? claim.from : '복도에서 들린 말', role: phoneOn ? claim.meta : '누가 확인했는지 모름',
    lines: [...(phoneOn ? lead : ['복도에서 누군가 큰 소리로 전하는 말이 들린다.']), `"${claim.text}"`,
      guided ? '누가 말했나? 언제 확인했나? 어디를 두고 한 말인가? 이 말은 어떤 정보일까?' : '이 말은 어떤 정보일까?'],
    choices: LABEL_CHOICES }) ?? 'rumor';
  const res = judge(G, id, pick);
  recordEncounter(G, 'info', res.result);
  ctx.note(`${claim.from}: ${claim.text}`, 'rumor');
  if (!res.ok) ctx.toast('다시 떠올려 보니, 누가 언제 확인한 말인지는 적혀 있지 않았다.', 'self', 4200);
  const act = await ctx.ui.talk({ speaker: '정민', role: '판단', lines: [takesAsTrue(pick) ? '이 말을 믿고 움직여도 될까?' : '확인되지 않은 말이다. 어떻게 할까?'],
    choices: [{ id: 'go', label: '이대로 움직인다', note: takesAsTrue(pick) ? '이 말대로 한다' : '이 말은 두고 내 판단대로 한다' },
      { id: 'check', label: '한 번 더 확인한다 (라디오·게시판)', note: '시간이 조금 든다' }] }) ?? 'check';
  const taken = believes(pick, act);
  if (taken) believe(G, id);
  return { pick, res, act, taken, claim };
}

// Walk out to a notice board. Power is off by then, so the stairs; the doors of 302호 still work.
export async function boardWalk(ctx, title, body, read) {
  ctx.objective(title, body, [{ text: '단지 게시판 또는 행정복지센터 게시판 읽기', done: false }]);
  const board = async () => { await read(); return 'read'; };
  await ctx.roam({ on: {
    homeExitDoor: async () => { await ctx.goto('apartment', 'corridor3F'); },
    homeDoor: async () => { await ctx.goto('apartment', 'homeEntry'); },
    aptNotice: board, centerNotice: board,
  } });
}

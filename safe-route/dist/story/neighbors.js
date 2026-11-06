// 3장 저녁의 이웃들: the shelter rumour judged by its source, a barter you can test before you agree,
// and a knock at the door at night. Each has more than one fair answer; saying no costs only the help.
import { recordEvent, consume, trade, usable, hhmm } from '../game/state.js';
import { ladderStage, recordEncounter } from '../game/drills.js';
import { posts, judge, believe, verify } from '../game/rumors.js';
import { CATALOG } from '../game/items.js';
import { fact } from '../game/facts.js';
import { radioScene, factCard } from './common.js';

const count = (G, id) => (G.items[id]?.home ?? 0) + (G.items[id]?.bag ?? 0);
const from = (G, id) => (G.items[id].home > 0 ? 'home' : 'bag');
const qty = (id, n) => `${CATALOG[id].name} ${n}${CATALOG[id].unit}`;
// The same four labels the notebook uses (state.js LABELS).
const LABEL_CHOICES = [{ id: 'official', label: '공식 안내' }, { id: 'site', label: '현장에서 확인한 것' }, { id: 'neighbor', label: '이웃에게 들은 것' }, { id: 'rumor', label: '소문 · 확인 안 됨' }];

// "생활문화센터 대피소 폐쇄" (game/rumors.js shelter_closed): sort the claim first, then decide what to do with it.
export async function storeRumor(ctx) {
  const G = ctx.G, [claim, site] = posts(G, 'shelter_closed');
  const stage = ladderStage(G, 'info');
  const label = await ctx.ui.talk({ speaker: '102동 주민', role: '바로편의점 앞', lines: [
    '여기 주인도 떠났나 봐요. 참, 그 얘기 들었어요?', `"${claim.text}"`, `(${claim.from} · ${claim.meta})`,
    stage === 'free' ? '이 말은 어떤 정보일까?' : '누가 말했나? 언제 확인했나? 어디를 두고 한 말인가? 이 말은 어떤 정보일까?'],
  choices: LABEL_CHOICES });
  const res = judge(G, 'shelter_closed', label);
  recordEncounter(G, 'info', res.result);
  ctx.note('SNS: 생활문화센터 대피소 폐쇄? (출처 불명)', 'rumor', 'store');
  if (!res.ok) ctx.toast('다시 떠올려 보니, 누가 언제 확인한 말인지는 듣지 못했다.', 'self', 4200);
  const pick = await ctx.ui.talk({ speaker: '102동 주민', role: '바로편의점 앞', lines: ['저도 확실한 건 모르겠어요. 어떡하죠?'],
    choices: [{ id: 'spread', label: '큰일이네요. 다른 이웃들한테도 알려야겠어요' }, { id: 'check', label: '어디서 나온 말인지 먼저 확인해 볼게요' }] });
  if (pick === 'spread') {
    believe(G, 'shelter_closed');
    recordEvent(G, 'rumor_spread', { id: 'shelter_closed' });
    // The correction reaches 정민 the same evening; in this chapter believing it changes no road.
    await ctx.ui.talk({ speaker: site.from, role: `${site.meta}`, lines: [site.text, '단톡방에 도는 글은 저도 봤어요. 그래서 직접 가 본 거예요.'] });
    G.know.misled = null;
    ctx.note('박 소장: 생활문화센터 출입구 A 열려 있음 (직접 다녀옴)', 'site');
    const fix = await ctx.ui.talk({ speaker: '정민', role: '바로편의점 앞', lines: ['아까 102동 주민에게 폐쇄됐다고 맞장구친 게 마음에 걸린다.'],
      choices: [{ id: 'fix', label: '102동 주민에게 아니라고 다시 알린다' }, { id: 'no', label: '그냥 둔다' }] });
    if (fix === 'fix') { ctx.pass(15); ctx.remember('rumor_corrected', { after: 'spread' }); ctx.toast('"아, 그래요? 나도 옆집에 말해 둘게요."', 'neighbor'); }
  } else {
    const shown = verify(G, 'shelter_closed');
    const heard = usable(G, 'radio') ? await radioScene(ctx, 'rumor') : null;
    const official = shown.find(p => p.label === 'official');
    await ctx.ui.talk({ speaker: heard ? '재난방송' : '행정복지센터 게시판(주민의 휴대폰 사진)', role: '공식 안내', lines: [`${official.text} (가상 안내)`] });
    ctx.note('생활문화센터 대피소 정상 운영 (공식 안내로 확인)', 'official');
    const tell = await ctx.ui.talk({ speaker: '102동 주민', role: '바로편의점 앞', lines: ['…뭐래요? 확인됐어요?'], choices: [{ id: 'tell', label: '102동 주민에게 확인한 내용을 알려 준다' }, { id: 'no', label: '그냥 간다' }] });
    if (tell === 'tell') { await ctx.ui.talk({ speaker: '102동 주민', role: '바로편의점 앞', lines: ['아, 정상 운영이에요? 다행이다. 저도 다른 데 퍼뜨릴 뻔했네요.'] }); ctx.remember('rumor_corrected', { after: 'check' }); }
  }
  await factCard(ctx, '소문을 만났을 때', ['info_rumor', 'shelter_rumor', 'info_harm']);
}

// 102동 주민 offers a battery pack for food. Testing it first shows it is a used pack (batteries_old).
const FOODS = [['bars', 3], ['canned', 1], ['ramen', 2], ['rice_instant', 2]];
export const offerFor = (G) => { const f = FOODS.find(([id, n]) => count(G, id) >= n); return f ? { [f[0]]: f[1] } : null; };
const oneFood = (G) => { const f = FOODS.find(([id]) => count(G, id) >= 1); return f ? { [f[0]]: 1 } : null; };
// '3봉으로' / '1개로': the particle follows the last syllable's final consonant (ㄹ takes 로).
const ro = (w) => { const c = w.charCodeAt(w.length - 1) - 0xac00, f = c >= 0 && c < 11172 ? c % 28 : 0; return w + (f === 0 || f === 8 ? '로' : '으로'); };
const cartLine = (cart) => Object.entries(cart).map(([id, n]) => qty(id, n)).join(', ');

export async function barter(ctx) {
  const G = ctx.G, offer = offerFor(G);
  let triedOld = false;
  const choices = () => [
    { id: 'test', label: '먼저 손전등에 넣어 봐도 될까요?', disabled: !count(G, 'flashlight'), reason: '손전등이 없다' },
    { id: 'take', label: offer ? `${ro(cartLine(offer))} 바꾼다` : '바꾼다', disabled: !offer, reason: '바꿀 만한 먹을거리가 없다' },
    ...(count(G, 'old_bars') && !triedOld ? [{ id: 'old', label: '작년 비상가방에서 나온 건빵을 내민다' }] : []),
    { id: 'no', label: '괜찮아요. 지금은 안 바꿀게요' },
  ];
  let pick = await ctx.ui.talk({ speaker: '102동 주민', role: '물물교환', lines: ['그런데 혹시 먹을 거 남는 거 있어요? 저한테 AA 건전지 한 팩이 있는데, 바꿀래요?', offer?.bars ? '건빵이나 에너지바 세 봉이면 돼요.' : '먹을 거 조금이면 돼요.'], choices: choices() });
  while (pick === 'old') {
    triedOld = true;
    await ctx.ui.talk({ speaker: '102동 주민', role: '봉지를 뒤집어 보며', lines: ['아, 이거 기한이 한참 지났네요. 이건 저도 못 받아요. 드시지도 마세요.', fact('bag_nocook').text] });
    recordEvent(G, 'barter_old_bars', {});
    pick = await ctx.ui.talk({ speaker: '102동 주민', role: '물물교환', lines: ['다른 걸로 바꿀래요?'], choices: choices() });
  }
  if (pick === 'test') {
    const weak = CATALOG.batteries_old.charge;
    recordEvent(G, 'barter_tested', { charge: weak });
    const small = oneFood(G);
    const p2 = await ctx.ui.talk({ speaker: '102동 주민', role: '손전등을 켜 보니', lines: ['손전등에 넣고 켜 보니 불빛이 금방 누렇게 흐려진다.', `"어? 쓰던 거였나 봐요. 저도 몰랐어요. 남은 게 ${weak}% 정도겠네요."`, small ? `"그럼 ${qty(Object.keys(small)[0], 1)}만 주세요. 라디오 잠깐 듣기엔 될 거예요."` : '"이건 그냥 두세요. 미안해요."'],
      choices: small ? [{ id: 'small', label: `${ro(qty(Object.keys(small)[0], 1))} 바꾼다` }, { id: 'no', label: '괜찮아요' }] : [{ id: 'no', label: '알겠어요' }] });
    if (p2 === 'small') { trade(G, small, { batteries_old: 1 }, 'trader'); ctx.toast(`쓰던 건전지 한 팩을 받았다. 남은 양은 ${weak}% 정도.`, 'self', 4200); }
    ctx.toast(fact('radio_batt').text, 'official', 5200);
    return;
  }
  if (pick === 'take') {
    trade(G, offer, { batteries_old: 1 }, 'trader');
    await ctx.ui.talk({ speaker: '정민', role: '집에 와서', lines: ['받은 건전지를 넣어 보니 불빛이 약하다. 쓰던 건전지였다. 그분도 몰랐던 것 같다.', fact('radio_batt').text] });
    return;
  }
  await ctx.ui.talk({ speaker: '102동 주민', role: '물물교환', lines: ['네, 그래요. 필요하면 102동 1층으로 와요.'] });
}

// A knock at night: someone asks for water. Giving, pointing to the water point, or not answering are all fair.
export async function knock(ctx) {
  const G = ctx.G;
  const water = ['water_500', 'water_2l'].find(id => count(G, id) > 0);
  const pick = await ctx.ui.talk({ speaker: '현관 너머', role: `D+${G.day} ${hhmm(G.minute)}`, lines: ['똑똑. 문 너머에서 낮은 목소리가 들린다.', '"밤늦게 죄송해요. 101동 사는 사람인데요, 물을 조금만 얻을 수 있을까요? 아이가 열이 나서요."'],
    choices: [{ id: 'give', label: '문을 조금 열고 물 한 병을 건넨다', disabled: !water, reason: '나눌 물이 없다' }, { id: 'info', label: '문 너머로 급수소 위치와 시간을 알려 준다' }, { id: 'none', label: '대답하지 않는다' }] });
  recordEvent(G, 'knock', { pick });
  if (pick === 'give') {
    consume(G, water, 1, from(G, water));
    G.karma = (G.karma ?? 0) + 1;
    recordEvent(G, 'help', { who: 'stranger', what: '밤에 문을 두드린 이웃에게 물 한 병을 건넸다' });
    await ctx.ui.talk({ speaker: '현관 너머', role: '101동 주민', lines: ['"고마워요. 정말로요."', '발소리가 계단 쪽으로 멀어진다.'] });
  } else if (pick === 'info') {
    recordEvent(G, 'help', { who: 'stranger', what: '밤에 문을 두드린 이웃에게 급수소 위치와 운영 시간을 알려 주었다' });
    await ctx.ui.talk({ speaker: '정민', role: '문 너머로', lines: ['"행정복지센터 마당에 비상급수시설이 있어요. 오전 9시 반부터 오후 5시까지예요. 물통을 가져가세요."', '"아, 몰랐어요. 내일 아침에 가 볼게요. 고마워요."'] });
    ctx.toast(fact('shelter_share').text, 'official', 5200);
  } else {
    await ctx.caption('잠시 뒤 발소리가 계단 쪽으로 멀어졌다.', null, 3000);
  }
}

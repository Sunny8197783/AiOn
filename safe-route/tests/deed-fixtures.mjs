// Shared by the deed and ending tests: events that satisfy each deed, and a run builder.
import { createGame, recordEvent } from '../dist/game/state.js';
import { DEEDS, remember } from '../dist/game/deeds.js';

// One hand-built event per event-derived deed (G.chapter set so encounters land in the right chapter).
export const RAW = {
  lead_honest: [['talk_lead', { pick: 'ok' }]],
  seoyeon_contact: [['neighbors_done', { seoyeon: true, manager: false, clerk: false }]],
  took_batteries: [['gift', { from: 'seoyeon', item: 'batteries_aa' }]],
  manager_contact: [['neighbors_done', { seoyeon: false, manager: true, clerk: false }]],
  clerk_contact: [['neighbors_done', { seoyeon: false, manager: false, clerk: true }]],
  card_neighbors: [['docs_done', { fields: ['neighbors'], where: 'bag' }]],
  card_meet: [['docs_done', { fields: ['meet'], where: 'bag' }]],
  card_family: [['docs_done', { fields: ['family'], where: 'bag' }]],
  hoard: [['hoard', {}]],
  candle_left: [['candle_left', {}]],
  elder_alert_knock: [['elder_choice', { pick: 'help', minute: 1206 }]],
  elder_alert_told: [['help', { who: 'manager', what: '정민의 말을 듣고 304호 어르신을 모시고 내려왔다' }]],
  handover_precise: [['handover_written', { kind: 'precise' }]],
  handover_partial: [['handover_written', { kind: 'partial' }]],
  handover_overgeneral: [['handover_written', { kind: 'generalized' }]],
  stairs_asked: [['help', { who: 'seoyeon', kind: 'ask', what: '?' }]],
  stairs_carried: [['help', { who: 'seoyeon', kind: 'carry', what: '?' }]],
  looter_gave: [['looter', { result: 'gave' }]],
  looter_escaped: [['looter', { result: 'escaped' }]],
  looter_fought: [['looter', { result: 'fought' }]],
  violence: [['violence', { how: 'punch', ko: false }]],
  water_for_seoyeon: [['water_fetched', { litres: 8, forSeoyeon: true }]],
  store_paid: [['buy', { cart: {}, total: 1, paid: 'cash' }]],
  rumor_checked: [['rumor_checked', { id: 'shelter_closed' }]],
  rumor_spread: [['rumor_spread', { id: 'shelter_closed' }]],
  knock_helped: [['knock', { pick: 'give' }]],
  fire_warned: [['fire_shout', {}]],
  fire_handled: [['fire_retreat', { size: 'small' }]],
  notes_exchange: [['help', { who: 'seoyeon', kind: 'memo', what: '?' }]],
  smoke_judged: [['smoke_choice', { pick: 'stay' }]],
  shelter_fire_judged: [['decision', { stayMove: 'move' }], ['encounter', { tool: 'extinguisher', result: 'retreat-ok' }, 'crossroads']],
  door_note: [['note_left', {}]],
  seoyeon_card_photo: [['help', { who: 'seoyeon', kind: 'card', what: '?' }]],
  seoyeon_waited: [['path', { path: 'seoyeon' }]],
  volunteered: [['path', { path: 'volunteer' }]],
  stayed: [['path', { path: 'stay' }]],
};

// A game that reached `path` after doing `ids` (event deeds from RAW, new-choice deeds via remember).
export function run(path, ids = [], extra = [], seed = 3) {
  const G = createGame({ seed });
  for (const id of ids) {
    G.chapter = DEEDS[id].ch;
    if (DEEDS[id].src === 'new') remember(G, id, id === 'rumor_corrected' ? { after: 'check' } : {});
    else if (id === 'signals_clear') for (let i = 0; i < 4; i++) recordEvent(G, 'signal_sorted', { id: `s${i}`, ok: true });
    else for (const [type, d, ch] of RAW[id]) { if (ch) G.chapter = ch; recordEvent(G, type, d); }
  }
  for (const [type, d] of extra) recordEvent(G, type, d);
  if (path) { G.chapter = 'evac'; G.day = 12; G.minute = 13 * 60; recordEvent(G, 'path', { path }); G.know.path = path; }
  return G;
}

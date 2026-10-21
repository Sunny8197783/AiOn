// 연무동 town level: buildTown(kit, people, fx) composes ground, buildings, trees and park, fences,
// street furniture, shop fronts, the shelter entrances, story set dressing and traffic, and owns the
// Level contract (walk, solids, points, spawns, NPCs, states, update, anchors) plus the crowd wiring.
import * as THREE from 'three';
import { PLACES, SPAWNS, CLOSURES } from '../game/townmap.js';
import { heightAt, WIND, yawTo } from './town-util.js';
import { buildGround } from './town-ground.js';
import { buildBuildings } from './town-buildings.js';
import { treeSpots, buildPark } from './town-park.js';
import { buildProps, shrubSpots } from './town-props.js';
import { buildFences } from './town-fences.js';
import { buildFronts } from './town-fronts.js';
import { buildShelterSite, SHELTER_HOLES, FACADE_HOLE, GUIDE_SPOTS } from './town-shelter.js';
import { buildEvents } from './town-events.js';
import { buildTraffic } from './town-traffic.js';
import { createTownCrowd, CROWD_MODES } from './town-crowd.js';

const GATE_VALUES = ['open', 'closed'];
const DEFAULT_GATES = { east: 'open', stairs: 'open', ramp: 'open', access: 'closed' };
// Notice-board text per `notices` state (facility and schedule facts of this fictional town only).
const NOTICES = {
  none: {
    shelterNotice: ['민방위 대피시설 안내', '연무 생활문화센터 지하주차장', '출입구: 동쪽 정문 · A 계단 · B 진출입로', 'C 경사로: 공사 중 (10월 말 개방 예정)'],
    aptNotice: ['한빛아파트 알림', '10/25(일) 단지 가을 대청소', '분리수거: 수요일 · 일요일', '관리사무소 031-000-0103'],
    centerNotice: ['연무동 소식', '가을 문화축제 10/31(토) 연무공원', '행정복지센터 운영 09:00–18:00'],
  },
  dday: {
    aptNotice: ['관리사무소 안내', '정전으로 엘리베이터 운행 중지', '대피소: 연무 생활문화센터 지하주차장'],
    centerNotice: ['연무동 행정복지센터', '대피소: 연무 생활문화센터 지하주차장', '현장 안내 요원 배치'],
  },
  water: {
    shelterNotice: ['민방위 대피시설 안내', '연무 생활문화센터 지하주차장', '운영 변경은 현장 안내판에 게시'],
    aptNotice: ['단수 안내', '단지 전체 단수 중', '비상급수: 행정복지센터 마당'],
    centerNotice: ['비상급수 안내', '행정복지센터 마당 비상급수시설', '운영 09:00–17:00 · 용기 지참'],
  },
  evac: {
    shelterNotice: ['대피소 안내', '주민 이동 지원 중', '임시 집결지: 행정복지센터 앞'],
    aptNotice: ['주민 이동 안내', '오늘 14:00 임시 집결지 (행정복지센터 앞)', '남는 주민은 관리사무소에 알려 주세요'],
    centerNotice: ['주민 이동 지원', '임시 집결지: 행정복지센터 앞', '14:00 버스 출발 · 1인 1가방 · 신분증 지참'],
  },
};
const ACCENT = { none: '#1f4e8c', dday: '#c42b25', water: '#1f5fa8', evac: '#c42b25' };

export async function buildTown(kit, people, fx) {
  const group = new THREE.Group(); group.name = 'town';
  const quality = kit.quality || 'high';

  const ground = buildGround(kit, { holes: SHELTER_HOLES });
  const buildings = buildBuildings(kit, { holes: { culture: FACADE_HOLE } });
  const spots = [...treeSpots(kit.rng(77)), ...shrubSpots(kit.rng(78))];
  const park = buildPark(kit, { spots });
  const props = buildProps(kit, { avoid: spots });
  const fences = buildFences(kit);
  const fronts = buildFronts(kit);
  const site = buildShelterSite(kit);
  const walk = [...ground.walk, ...site.walk, ...fronts.walk, ...park.walk];
  const events = buildEvents(kit, people, walk);
  const traffic = buildTraffic(kit);
  for (const m of [ground, buildings, park, props, fences, fronts, site, events, traffic]) group.add(m.group);
  const solids = [buildings, site, fronts, park, props, fences, events, traffic].flatMap(m => m.solids);
  const hy = (x, z) => heightAt(walk, x, z);

  // ---- points, spawns ----
  const R = { door: 1.8, shelter: 2.2, notice: 1.9, place: 2.4 };
  const points = Object.entries(PLACES).map(([id, p]) => ({ id, label: p.label, x: p.x, y: hy(p.x, p.z), z: p.z, r: id === 'assembly' ? 4.5 : R[p.kind] ?? 1.8, kind: p.kind, to: p.to }));
  points.push(...events.closurePoints);
  const spawn = (x, z, yaw) => ({ x, y: hy(x, z), z, yaw });
  const spawns = Object.fromEntries(Object.entries(SPAWNS).map(([k, s]) => [k, spawn(s.x, s.z, s.yaw)]));
  Object.assign(spawns, { // extra spawns (documented in the report)
    water: spawn(-80, -15.6, Math.PI), assembly: spawn(-57.2, -36, Math.PI / 2), shelterC: spawn(-100.6, 12.0, Math.PI / 2),
    east: spawn(-65.6, 31, Math.PI / 2), park: spawn(0, 9.2, Math.PI), crossing: spawn(0, -8.6, Math.PI),
  });

  // ---- NPCs ----
  const mk = (preset, seed, at) => {
    try {
      const p = people.createPerson({ preset, seed });
      p.group.position.set(at.x, hy(at.x, at.z), at.z); p.group.rotation.y = yawTo(at.face[0], at.face[1]);
      p.group.visible = false; group.add(p.group); p.setAnim?.('idle');
      return p;
    } catch (err) { console.error(`[town] NPC ${preset} failed`, err); return undefined; }
  };
  const npcs = {};
  npcs.guideEast = mk('guide', 61, GUIDE_SPOTS.guideEast);
  npcs.guideC = mk('guide', 62, GUIDE_SPOTS.guideC);
  if (events.guides[0]) npcs.closureGuide = events.guides[0];
  npcs.clerkOutside = mk('clerk', 63, { x: 39.4, z: 11.1, face: [0, -1] });
  for (const k of Object.keys(npcs)) if (!npcs[k]) delete npcs[k];

  const crowd = createTownCrowd({ people, group, walk, quality });

  // ---- states ----
  const state = { traffic: 'normal', crowd: 'none', closures: [], gates: { ...DEFAULT_GATES }, waterPoint: 'off', buses: 'off', notices: 'none', noticeText: {}, season: 'autumn' };
  function drawNotices() {
    const set = NOTICES[state.notices] || NOTICES.none, g = state.gates;
    for (const [id, bd] of Object.entries(fronts.boards)) {
      let lines = state.noticeText[id] || set[id] || NOTICES.none[id];
      if (!state.noticeText[id] && id === 'shelterNotice' && state.notices === 'dday') {
        lines = ['현장 안내', `동쪽 출입구 ${g.east === 'closed' ? '폐쇄' : '운영'}`, `A 계단 ${g.stairs === 'closed' ? '폐쇄' : '운영'}`,
          `B 진출입로 ${g.ramp === 'closed' ? '폐쇄' : '운영 (차량 통제)'}`, `C 경사로 ${g.access === 'open' ? '개방 · 안내 요원 있음' : '준비 중'}`];
      }
      bd.draw(String(lines[0] ?? ''), lines.slice(1), ACCENT[state.notices] || ACCENT.none);
    }
  }
  function refreshNpcs() {
    if (npcs.guideEast) npcs.guideEast.group.visible = state.gates.east === 'closed';
    if (npcs.guideC) npcs.guideC.group.visible = state.gates.access === 'open' && state.crowd !== 'none';
    if (npcs.clerkOutside) npcs.clerkOutside.group.visible = state.crowd === 'normal' || state.crowd === 'busy';
  }
  function setState(key, value) {
    switch (key) {
      case 'traffic': traffic.setMode(value); state.traffic = traffic.mode; break;
      case 'crowd': state.crowd = crowd.setMode(value); break;
      case 'closures': {
        state.closures = events.setClosures(Array.isArray(value) ? value : value ? [value] : []);
        for (const p of events.closurePoints) p.enabled = state.closures.includes(p.id.slice(8));
        crowd.setContext({ closures: state.closures });
        break;
      }
      case 'gates': {
        const next = { ...state.gates };
        for (const [k, v] of Object.entries(value || {})) {
          if (!(k in DEFAULT_GATES) || !GATE_VALUES.includes(v)) { console.error(`[town] gates.${k} = "${v}" invalid; use ${Object.keys(DEFAULT_GATES).join('/')} : open | closed`); continue; }
          next[k] = v;
        }
        state.gates = next; site.setGates(next); crowd.setContext({ gates: next }); drawNotices();
        break;
      }
      case 'waterPoint': if (!['on', 'off'].includes(value)) { console.error(`[town] waterPoint "${value}" invalid; use on | off`); return; } state.waterPoint = value; events.setWater(value === 'on'); break;
      case 'buses': if (!['on', 'off'].includes(value)) { console.error(`[town] buses "${value}" invalid; use on | off`); return; } state.buses = value; events.setBuses(value === 'on'); break;
      case 'notices': if (!NOTICES[value]) { console.error(`[town] notices "${value}" invalid; use ${Object.keys(NOTICES).join(' | ')}`); return; } state.notices = value; drawNotices(); break;
      case 'noticeText': {
        for (const [id, lines] of Object.entries(value || {})) {
          if (!fronts.boards[id]) { console.error(`[town] noticeText: unknown board "${id}"; use ${Object.keys(fronts.boards).join(', ')}`); continue; }
          if (lines == null) delete state.noticeText[id]; else state.noticeText[id] = [].concat(lines).map(String);
        }
        drawNotices();
        break;
      }
      case 'season': if (value !== 'autumn') console.warn(`[town] season "${value}" not built; the town is autumn only`); break;
      default: console.warn(`[town] unknown state "${key}"`); return;
    }
    refreshNpcs();
  }
  setState('traffic', 'normal'); setState('closures', []); setState('gates', DEFAULT_GATES); setState('waterPoint', 'off');
  setState('buses', 'off'); setState('notices', 'none'); setState('crowd', 'normal');

  // ---- per frame ----
  let rmT = 0, reduced = false, nt = 0;
  const localNpcs = ['guideEast', 'guideC', 'clerkOutside'].map(k => npcs[k]).filter(Boolean);
  function update(dt, t, ctx = {}) {
    if ((rmT -= dt) <= 0) {
      rmT = 1;
      reduced = !!(globalThis.document?.documentElement?.classList?.contains('reduce-motion') || globalThis.matchMedia?.('(prefers-reduced-motion: reduce)')?.matches);
    }
    WIND.uTime.value = t; WIND.uWind.value = reduced ? 0 : 1;
    traffic.update(dt, t, ctx);
    events.update(dt, t, ctx);
    crowd.update(dt, ctx);
    nt += dt;
    const pl = ctx.player;
    localNpcs.forEach((p, k) => {
      if (!p.group.visible) return;
      const dx = pl ? pl.x - p.group.position.x : 99, dz = pl ? pl.z - p.group.position.z : 99, d2 = dx * dx + dz * dz;
      if (p !== npcs.clerkOutside) {
        const near = d2 < 49, ph = (nt + k * 3.1) % 10;
        p.lookAt?.(near ? pl.x : null, near ? pl.z : null);
        p.setAnim?.(near ? 'talk' : ph < 3 ? 'point' : ph < 5 ? 'wave' : 'idle');
      }
      if (d2 < 6400) p.update?.(dt);
    });
  }

  return {
    id: 'town', group, env: 'day', walk, solids, points, spawns, npcs,
    states: {
      traffic: ['normal', 'light', 'none'], crowd: CROWD_MODES, closures: Object.keys(CLOSURES),
      gates: { east: ['open', 'closed'], stairs: ['open', 'closed'], ramp: ['open', 'closed'], access: ['closed', 'open'] },
      waterPoint: ['off', 'on'], buses: ['off', 'on'], notices: Object.keys(NOTICES), noticeText: [], season: ['autumn'],
    },
    setState, update,
    get state() { return { ...state, gates: { ...state.gates }, closures: [...state.closures] }; },
    anchors: {
      craneShelter: { x: -80, y: 110, z: 31, look: { x: -89, y: 0, z: 31 } },
      streetHome: { x: 66, y: 1.7, z: 21, look: { x: 78, y: 2.2, z: 29.6 } },
      assemblyView: { x: -52, y: 7, z: -24, look: { x: -64, y: 1.5, z: -40 } },
    },
  };
}

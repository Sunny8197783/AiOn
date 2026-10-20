// The fictional 연무동 town layout: one source of truth for the 3D town, the 2D map and route logic.
// Frame: x = east, z = south (north is -z), metres, ground y = 0. No three.js here (node-testable).

export const BOUNDS = { x0: -130, x1: 130, z0: -110, z1: 110 };
export const SIDEWALK = 4; // sidewalk width along every road edge, curb height 0.12

export const ROADS = [
  { id: 'yeonmu', name: '연무로', x0: -130, x1: 130, z0: -6, z1: 6, lanes: 4 },
  { id: 'munhwa', name: '문화로', x0: -54, x1: -46, z0: -110, z1: 110, lanes: 2 },
  { id: 'hanbit', name: '한빛로', x0: 46, x1: 54, z0: -110, z1: 110, lanes: 2 },
  { id: 'sijang', name: '시장길', x0: -46, x1: 46, z0: -64, z1: -56, lanes: 2 },
  { id: 'danji', name: '단지길', x0: -46, x1: 46, z0: 56, z1: 64, lanes: 2 },
];

// Zebra crossings: road id + centre coordinate along that road (x for E-W roads, z for N-S roads).
export const CROSSWALKS = [
  { road: 'yeonmu', at: -104 }, { road: 'yeonmu', at: -60 }, { road: 'yeonmu', at: 0 }, { road: 'yeonmu', at: 60 },
  { road: 'munhwa', at: -14 }, { road: 'munhwa', at: 14 }, { road: 'hanbit', at: -14 }, { road: 'hanbit', at: 14 },
  { road: 'sijang', at: 0 }, { road: 'danji', at: 0 },
];

// Building footprints. floors x 3 m unless h is given. door: which face has the main door.
export const BUILDINGS = [
  { id: 'apt103', name: '한빛아파트 103동', kind: 'apartment', x0: 64, x1: 112, z0: 30, z1: 42, floors: 15, label: '103', door: 'n' },
  { id: 'apt102', name: '한빛아파트 102동', kind: 'apartment', x0: 64, x1: 112, z0: 58, z1: 70, floors: 15, label: '102' },
  { id: 'apt101', name: '한빛아파트 101동', kind: 'apartment', x0: 64, x1: 112, z0: 86, z1: 98, floors: 15, label: '101' },
  { id: 'aptOffice', name: '한빛아파트 관리동', kind: 'civic', x0: 116, x1: 126, z0: 30, z1: 40, floors: 1 },
  { id: 'store', name: '바로편의점', kind: 'shop', x0: 30, x1: 42, z0: 12, z1: 22, floors: 1, door: 'n', sign: '바로편의점' },
  { id: 'shopsS', name: '상가', kind: 'shop', x0: 16, x1: 29, z0: 12, z1: 22, floors: 2, sign: '연무약국' },
  { id: 'mart', name: '연무마트', kind: 'mart', x0: -36, x1: 4, z0: -50, z1: -24, floors: 2, door: 's', sign: '연무마트' },
  { id: 'office', name: '한결물류 연무센터', kind: 'office', x0: 70, x1: 104, z0: -48, z1: -22, floors: 4, door: 's', sign: '한결물류' },
  { id: 'warehouse', name: '한결물류 창고', kind: 'warehouse', x0: 64, x1: 124, z0: -104, z1: -60, floors: 1, h: 10 },
  { id: 'center', name: '연무동 행정복지센터', kind: 'civic', x0: -104, x1: -74, z0: -50, z1: -28, floors: 3, door: 's', sign: '연무동 행정복지센터' },
  { id: 'cafe', name: '카페 쉼표', kind: 'shop', x0: -126, x1: -110, z0: -34, z1: -16, floors: 2, door: 's', sign: '카페 쉼표' },
  { id: 'culture', name: '연무 생활문화센터', kind: 'civic', x0: -112, x1: -70, z0: 16, z1: 46, floors: 3, door: 'e', sign: '연무 생활문화센터' },
  // Background blocks (not enterable)
  { id: 'n1', kind: 'shop', x0: -40, x1: -8, z0: -104, z1: -70, floors: 4 },
  { id: 'n2', kind: 'villa', x0: 0, x1: 40, z0: -104, z1: -70, floors: 4 },
  { id: 'nw1', kind: 'villa', x0: -126, x1: -66, z0: -104, z1: -62, floors: 4 },
  { id: 's1', kind: 'villa', x0: -40, x1: -4, z0: 70, z1: 104, floors: 4 },
  { id: 's2', kind: 'villa', x0: 4, x1: 40, z0: 70, z1: 104, floors: 5 },
  { id: 'sw1', kind: 'villa', x0: -126, x1: -62, z0: 60, z1: 104, floors: 4 },
];

// Open areas the town builder fills (park, lots, yards).
export const AREAS = [
  { id: 'park', name: '연무공원', kind: 'park', x0: -42, x1: 12, z0: 12, z1: 52 },
  { id: 'martLot', name: '마트 주차장', kind: 'parking', x0: -36, x1: 4, z0: -22, z1: -12 },
  { id: 'centerYard', name: '행정복지센터 마당', kind: 'plaza', x0: -104, x1: -60, z0: -26, z1: -12 },
  { id: 'aptGrounds', name: '한빛아파트 단지', kind: 'complex', x0: 58, x1: 128, z0: 12, z1: 104 },
  { id: 'officeLot', name: '물류센터 마당', kind: 'parking', x0: 58, x1: 128, z0: -56, z1: -12 },
];

// Interaction points in the town. kind: door (to another level), shelter, notice, npc, place.
export const PLACES = {
  home: { x: 78, z: 28.5, label: '103동 출입구', kind: 'door', to: 'apartment' },
  store: { x: 36, z: 10.6, label: '바로편의점', kind: 'door', to: 'store' },
  mart: { x: -16, z: -22.8, label: '연무마트 입구', kind: 'door', to: 'mart' },
  office: { x: 87, z: -20.8, label: '한결물류 연무센터', kind: 'door', to: 'office' },
  center: { x: -89, z: -26.8, label: '행정복지센터', kind: 'place' },
  water: { x: -80, z: -17, label: '비상급수시설', kind: 'place' },
  cafe: { x: -118, z: -14.8, label: '카페 쉼표', kind: 'place' },
  assembly: { x: -60, z: -36, label: '임시 집결지', kind: 'place' },
  shelterEast: { x: -68.6, z: 31, label: '동쪽 출입구', kind: 'shelter', to: 'shelter' },
  shelterA: { x: -86, z: 13.4, label: '출입구 A · 계단', kind: 'shelter', to: 'shelter' },
  shelterB: { x: -80, z: 50, label: '출입구 B · 지하주차장 진출입로', kind: 'shelter', to: 'shelter' },
  shelterC: { x: -104, z: 13.4, label: '출입구 C · 경사로', kind: 'shelter', to: 'shelter' },
  shelterNotice: { x: -94, z: 12, label: '현장 안내판', kind: 'notice' },
  aptNotice: { x: 72, z: 28.5, label: '단지 게시판', kind: 'notice' },
  centerNotice: { x: -96, z: -20, label: '행정복지센터 게시판', kind: 'notice' },
};

// Spawn points in the town (yaw 0 faces north / -z).
export const SPAWNS = {
  home: { x: 78, z: 26, yaw: 0 }, store: { x: 36, z: 8.6, yaw: 0 }, mart: { x: -16, z: -21, yaw: Math.PI },
  office: { x: 87, z: -18.5, yaw: Math.PI }, cafe: { x: -118, z: -12.5, yaw: Math.PI }, shelter: { x: -86, z: 10.8, yaw: Math.PI },
};

// Pedestrian network for the 2D map and route planning. Node ids are short.
export const NODES = {
  home: { x: 78, z: 26, label: '103동' }, gate: { x: 57, z: 20, label: '단지 정문' },
  I1: { x: -50, z: -60 }, I2: { x: 50, z: -60 }, I3: { x: -50, z: 0 }, I4: { x: 50, z: 0 }, I5: { x: -50, z: 60 }, I6: { x: 50, z: 60 },
  store: { x: 36, z: 9, label: '편의점' }, M: { x: 0, z: 0, label: '연무로 횡단보도' }, mart: { x: -16, z: -10, label: '연무마트' },
  office: { x: 87, z: -18, label: '한결물류' }, yard: { x: -66, z: -9, label: '행정복지센터 앞' }, water: { x: -80, z: -16, label: '비상급수시설' },
  assembly: { x: -58, z: -36, label: '임시 집결지' }, W: { x: -104, z: 0, label: '서쪽 횡단보도' }, cafe: { x: -118, z: -12, label: '카페' },
  S1: { x: -57, z: 31, label: '문화로' }, east: { x: -68.6, z: 31, label: '동쪽 출입구' },
  A: { x: -86, z: 11, label: 'A 계단' }, C: { x: -104, z: 11, label: 'C 경사로' }, B: { x: -80, z: 52, label: 'B 진출입로' },
  shelter: { x: -90, z: 31, label: '대피소(지하)' },
};

// [from, to, segment id, tags]. Segments are what closures switch off. Tags describe the way itself.
export const EDGES = [
  ['home', 'gate', 'aptPath', []], ['gate', 'I4', 'hanbitMid', []], ['gate', 'I6', 'hanbitS', []],
  ['I4', 'store', 'yeonmuE', []], ['store', 'M', 'yeonmuC1', []], ['M', 'I3', 'yeonmuC2', []], ['M', 'mart', 'martLot', []],
  ['I4', 'office', 'officePath', []], ['I2', 'I4', 'hanbitN', []], ['I1', 'I2', 'sijang', []], ['I1', 'assembly', 'munhwaN2', []],
  ['assembly', 'I3', 'munhwaN1', []], ['I3', 'yard', 'yeonmuW1', []], ['yard', 'water', 'yardPath', []], ['yard', 'W', 'yeonmuW2', []],
  ['W', 'cafe', 'cafePath', []], ['W', 'C', 'crossW', ['crossing']], ['I3', 'A', 'yeonmuW3', ['crossing']], ['A', 'C', 'cultureN', []],
  ['I3', 'S1', 'munhwaS1', []], ['S1', 'east', 'cultureE', []], ['S1', 'I5', 'munhwaS2', []], ['I5', 'B', 'cultureS', []],
  ['I5', 'I6', 'danji', []],
  // Shelter entrances. Which ones are open is game state (see SHELTER_ENTRANCES), not a closure.
  ['east', 'shelter', 'gateEast', ['level']], ['A', 'shelter', 'gateA', ['stairs']], ['B', 'shelter', 'gateB', ['steep']], ['C', 'shelter', 'gateC', ['ramp']],
];

export const SHELTER_ENTRANCES = {
  east: { node: 'east', place: 'shelterEast', segment: 'gateEast', label: '동쪽 출입구', way: 'level' },
  stairs: { node: 'A', place: 'shelterA', segment: 'gateA', label: '출입구 A · 계단', short: 'A 계단', way: 'stairs' },
  ramp: { node: 'B', place: 'shelterB', segment: 'gateB', label: '출입구 B · 지하주차장 진출입로', short: 'B 진출입로', way: 'steep' },
  access: { node: 'C', place: 'shelterC', segment: 'gateC', label: '출입구 C · 경사로', short: 'C 경사로', way: 'ramp' },
};

// Road closures: which segments they cut, and where the 3D barricades stand (rects the player cannot cross).
export const CLOSURES = {
  yeonmuCenter: { label: '연무로 중앙 구간 통제', segments: ['yeonmuC1', 'yeonmuC2'], barricades: [{ x0: 27, x1: 29, z0: -10, z1: 10 }, { x0: -45, x1: -43, z0: -10, z1: 10 }] },
  danji: { label: '단지길 통제', segments: ['danji'], barricades: [{ x0: -1, x1: 1, z0: 52, z1: 68 }] },
  sijang: { label: '시장길 통제', segments: ['sijang'], barricades: [{ x0: -1, x1: 1, z0: -68, z1: -52 }] },
  munhwaSouth: { label: '문화로 남쪽 통제', segments: ['munhwaS2'], barricades: [{ x0: -58, x1: -42, z0: 44, z1: 46 }] },
  munhwaNorth: { label: '문화로 북쪽 통제', segments: ['munhwaN1'], barricades: [{ x0: -58, x1: -42, z0: -20, z1: -18 }] },
};

export const dist = (a, b) => Math.hypot(a.x - b.x, a.z - b.z);

// Edge list with lengths, skipping closed segments and closed shelter gates.
export function graph({ closures = [], gates = { east: true, stairs: true, ramp: true, access: true } } = {}) {
  const cut = new Set(closures.flatMap(id => CLOSURES[id]?.segments || []));
  for (const [k, e] of Object.entries(SHELTER_ENTRANCES)) if (!gates[k]) cut.add(e.segment);
  const adj = Object.fromEntries(Object.keys(NODES).map(id => [id, []]));
  for (const [a, b, seg, tags] of EDGES) {
    if (cut.has(seg)) continue;
    const len = dist(NODES[a], NODES[b]);
    adj[a].push({ to: b, seg, tags, len }); adj[b].push({ to: a, seg, tags, len });
  }
  return adj;
}

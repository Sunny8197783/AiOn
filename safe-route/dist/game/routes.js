// Route planning over the townmap pedestrian graph. Pure logic, node-testable.
import { NODES, SHELTER_ENTRANCES, graph, dist } from './townmap.js';
import { bagLoad } from './state.js';

const SPEED = { walk: 1.3, walker: 0.9 }; // m/s
const STEP_TAGS = ['stairs', 'steep'];    // impassable with a rollator walker
const HEAVY_KG = 8;

// Dijkstra. adj is graph() output. tagCost multiplies an edge's length per tag (Infinity = never use).
export function shortestPath(adj, from, to, { avoidTags = [], tagCost = {} } = {}) {
  if (!adj?.[from] || !adj?.[to]) return null;
  const cost = { [from]: 0 }, prev = {}, done = new Set();
  for (;;) {
    let u = null;
    for (const n in cost) if (!done.has(n) && (u === null || cost[n] < cost[u])) u = n;
    if (u === null) return null;
    if (u === to) break;
    done.add(u);
    for (const e of adj[u]) {
      const tags = e.tags ?? [];
      if (done.has(e.to) || tags.some(t => avoidTags.includes(t))) continue;
      const c = cost[u] + e.len * tags.reduce((m, t) => m * (tagCost[t] ?? 1), 1);
      if (Number.isFinite(c) && !(c >= cost[e.to])) { cost[e.to] = c; prev[e.to] = { from: u, e }; }
    }
  }
  const nodes = [to], segments = [], tags = new Set();
  let length = 0;
  for (let n = to; n !== from; n = prev[n].from) {
    const { from: p, e } = prev[n];
    nodes.unshift(p);
    segments.unshift(e.seg);
    (e.tags ?? []).forEach(t => tags.add(t));
    length += e.len;
  }
  return { nodes, length: Math.round(length), segments, tags: [...tags] };
}

const without = (adj, segs) => segs.length
  ? Object.fromEntries(Object.entries(adj).map(([k, es]) => [k, es.filter(e => !segs.includes(e.seg))]))
  : adj;

// The route's segments, longest first.
const bySize = (r) => r.segments
  .map((seg, i) => [seg, dist(NODES[r.nodes[i]], NODES[r.nodes[i + 1]])])
  .sort((a, b) => b[1] - a[1]).map(([seg]) => seg);

// gates accepts booleans or the town level's 'open'|'closed'; a gate not mentioned is open.
const gateOpen = (v) => v !== false && v !== 'closed';

// Up to 3 distinct options: the shortest, a detour around the shortest's longest segment, and a
// step-free one (walk) or a second detour (walker). Each adds { kind, seconds, entrance }.
export function routeOptions(G, from, to, { closures = [], gates = {}, mobility = 'walk', carryingKg } = {}) {
  if (!SPEED[mobility]) throw new Error(`unknown mobility: ${mobility}`);
  const open = Object.fromEntries(Object.keys(SHELTER_ENTRANCES).map(k => [k, gateOpen(gates[k])]));
  const adj = graph({ closures, gates: open });
  const kg = carryingKg ?? (G ? bagLoad(G).weightKg : 0);
  const speed = SPEED[mobility] * (kg > HEAVY_KG ? 0.8 : 1);
  const base = mobility === 'walker' ? STEP_TAGS : [];
  const find = (cut = [], avoidTags = base) => shortestPath(without(adj, cut), from, to, { avoidTags });

  const opts = [];
  const add = (r, kind) => {
    if (!r || opts.some(o => o.segments.join() === r.segments.join())) return false;
    const entrance = Object.keys(SHELTER_ENTRANCES).find(k => r.segments.includes(SHELTER_ENTRANCES[k].segment)) ?? null;
    opts.push({ ...r, kind, seconds: Math.round(r.length / speed), entrance });
    return true;
  };
  if (!add(find(), 'shortest')) return [];
  for (const seg of bySize(opts[0])) if (add(find([seg]), 'detour')) break;
  if (mobility === 'walk') add(find([], STEP_TAGS), 'stepFree');
  if (opts.length < 3 && opts[1]) add(find([bySize(opts[0])[0], bySize(opts[1])[0]]), 'detour');
  return opts.slice(0, 3);
}

const TAG_NOTE = { stairs: '계단', steep: '가파른 진출입로', ramp: '경사로', crossing: '횡단보도' };

// '103동 → 단지 정문 → … → 대피소(지하) · 410m, 약 5분 · 계단·횡단보도 있음'
export function explain(option) {
  if (!option?.nodes?.length) return '갈 수 있는 길이 없습니다.';
  const names = option.nodes.map(n => NODES[n]?.label).filter(Boolean);
  const via = names.filter((n, i) => n !== names[i - 1]).join(' → ');
  const secs = option.seconds ?? option.length / SPEED.walk;
  const notes = (option.tags ?? []).map(t => TAG_NOTE[t]).filter(Boolean);
  return `${via} · ${option.length}m, 약 ${Math.max(1, Math.round(secs / 60))}분${notes.length ? ` · ${notes.join('·')} 있음` : ''}`;
}

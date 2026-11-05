// What a graphics tier changes in a built level (engine/core.js calls it at every level swap and tier change):
// which meshes cast shadows (small props stop casting on the lower tiers) and which point/spot lights may cast,
// at what map size. The level's own choices are kept in userData.q0, so a later, higher tier restores them.
// Skinned people are left to engine/crowd.js (distance LOD), and their props (userData.cast) to engine/people.js.
import { profileOf } from './quality.js';

const _s = { x: 1, y: 1, z: 1 };
function worldScale(o) { // largest axis scale of o in the world (no three.js import: plain matrix columns)
  const e = o.matrixWorld.elements;
  _s.x = Math.hypot(e[0], e[1], e[2]); _s.y = Math.hypot(e[4], e[5], e[6]); _s.z = Math.hypot(e[8], e[9], e[10]);
  return Math.max(_s.x, _s.y, _s.z);
}

export function applyTier(root, tier) {
  if (!root?.traverse) return;
  const P = profileOf(tier);
  let lights = 0;
  root.updateMatrixWorld?.(true);
  root.traverse((o) => {
    if (o.isPointLight || o.isSpotLight) {
      const q0 = (o.userData.q0 ??= { cast: o.castShadow, map: o.shadow?.mapSize?.x ?? 512 });
      if (!q0.cast) return;
      const on = lights < P.lightShadows;
      if (on) lights++;
      if (o.castShadow !== on) o.castShadow = on; // a change recompiles once (the swap is under the fade)
      const n = Math.min(q0.map, P.lightShadowMap || q0.map);
      if (on && o.shadow.mapSize.x !== n) { o.shadow.mapSize.set(n, n); o.shadow.map?.dispose(); o.shadow.map = null; }
      return;
    }
    if (!o.isMesh || o.isSkinnedMesh || o.userData.cast !== undefined) return;
    const q0 = (o.userData.q0 ??= { cast: o.castShadow });
    if (!q0.cast) return;
    if (!P.casterMin) { o.castShadow = true; return; }
    if (q0.size === undefined) { // bounding diameter in metres (an instanced mesh: one instance)
      const g = o.geometry;
      if (g && !g.boundingSphere) g.computeBoundingSphere();
      q0.size = 2 * (g?.boundingSphere?.radius ?? 0) * worldScale(o);
    }
    o.castShadow = q0.size >= P.casterMin;
  });
}

// Free what a level build dropped from the cache (its tier changed) holds: its crowds' registrations (L.dispose), its
// kit light-group entries (kit.forget), geometry, skeleton bone textures, instance buffers, light shadow maps, and the
// materials and textures no other live root uses (keep: the scene and the other built levels; kit-memoized ones that
// a later build picks up again are simply re-uploaded then). Render-target textures (environment, shadows) are never
// touched here. Geometry shared with another level is uploaded again when drawn.
export function disposeLevel(L, persons, { kit = null, keep = [] } = {}) {
  if (!L?.group) return;
  const objs = [], mine = new Set(), mats = new Set(), used = new Set();
  const each = (o, fn) => { for (const m of [].concat(o.material || [])) if (m) fn(m); };
  L.group.traverse((o) => { objs.push(o); mine.add(o); each(o, (m) => mats.add(m)); }); // before L.dispose detaches its people
  try { kit?.forget?.(L.group); } catch (err) { console.error(`[engine] kit.forget for level "${L.id}" failed`, err); }
  try { L.dispose?.(); } catch (err) { console.error(`[engine] level "${L.id}" dispose failed`, err); }
  for (const o of objs) {
    o.geometry?.dispose?.();
    if (o.isSkinnedMesh) o.skeleton?.dispose?.();
    if (o.isInstancedMesh || o.isLight) o.dispose?.(); // instance attributes / shadow map
  }
  for (const r of keep) if (r && r !== L.group) r.traverse?.((o) => { if (!mine.has(o)) each(o, (m) => used.add(m)); });
  const texOf = (m, out) => {
    for (const v of Object.values(m)) if (v?.isTexture) out.add(v);
    if (m.uniforms) for (const u of Object.values(m.uniforms)) if (u?.value?.isTexture) out.add(u.value);
    return out;
  };
  const usedTex = new Set();
  for (const m of used) texOf(m, usedTex);
  const freeTex = new Set();
  for (const m of mats) if (!used.has(m)) { texOf(m, freeTex); m.dispose(); }
  for (const t of freeTex) if (!usedTex.has(t) && !t.isRenderTargetTexture && !t.isCubeTexture && t.image) t.dispose();
  if (persons) for (const p of [...persons]) if (mine.has(p.group)) persons.delete(p);
}

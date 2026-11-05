import test from 'node:test';
import assert from 'node:assert/strict';
import * as THREE from 'three';
import { shelfLayout, slotX, TAG_W, TAG_NEAR, SP } from '../dist/ui/grab-layout.js';

// Same placement as ui/grab3d.js grabRoom: shelf plane at z 0, camera `dist` in front, 0.3 above the rows' middle.
function tagBoxes(kind, n, aspect, dist = 1.1) {
  const L = shelfLayout({ kind, n, fov: 70, aspect, dist });
  const mid = L.rows.reduce((a, b) => a + b, 0) / L.rows.length;
  const cam = new THREE.PerspectiveCamera(70, aspect, 0.05, 50);
  cam.position.set(0, mid + 0.3, dist); cam.lookAt(0, mid - 0.12, 0); cam.updateMatrixWorld();
  const tw = TAG_W * L.scale, th = 0.0975 * L.scale;
  const boxes = Array.from({ length: n }, (_, i) => {
    const { row, x } = slotX(L, i, n), y = L.rows[row] - 0.06, z = TAG_NEAR;
    const a = new THREE.Vector3(x - tw / 2, y - th / 2, z).project(cam), b = new THREE.Vector3(x + tw / 2, y + th / 2, z).project(cam);
    return [a.x, a.y, b.x, b.y];
  });
  return { L, boxes };
}

test('bag picker: up to 24 items, three distinct shelves, tags apart and on screen', () => {
  for (const aspect of [16 / 9, 4 / 3, 21 / 9]) for (const n of [6, 11, 14, 19, 24]) {
    const { L, boxes } = tagBoxes('bag', n, aspect);
    assert.equal(new Set(L.rows).size, L.rows.length, 'every row has its own height');
    assert.ok(L.sp <= SP && L.scale <= 1);
    for (let i = 0; i < boxes.length; i++) for (let j = i + 1; j < boxes.length; j++) {
      const [a, b] = [boxes[i], boxes[j]];
      assert.ok(!(a[0] < b[2] && b[0] < a[2] && a[1] < b[3] && b[1] < a[3]), `n ${n} aspect ${aspect.toFixed(2)}: tags ${i} and ${j} overlap`);
    }
    for (const [x0, , x1] of boxes) assert.ok(x0 >= -0.95 && x1 <= 0.95, `n ${n} aspect ${aspect.toFixed(2)}: a tag leaves the screen (${x0.toFixed(2)}..${x1.toFixed(2)})`);
  }
});

test('two-row bags and the shop keep their layout', () => {
  assert.deepEqual(shelfLayout({ kind: 'bag', n: 8 }).rows, [0.82, 0.5]);
  assert.deepEqual(shelfLayout({ kind: 'shop', n: 24 }), { rows: [1.42, 1.02, 0.62], perRow: 8, sp: SP, scale: 1 });
});

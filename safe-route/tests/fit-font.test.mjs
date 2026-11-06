import test from 'node:test';
import assert from 'node:assert/strict';
import { fitFont } from '../dist/engine/kit.js';

// A canvas stand-in whose text width is 0.9 em a character, with a little rounding like real glyph metrics.
function ctx() {
  const g = { font: '', measures: 0 };
  g.measureText = (t) => { g.measures++; const px = +/(\d+(?:\.\d+)?)px/.exec(g.font)[1]; return { width: Math.round(t.length * px * 0.9 * 10) / 10 }; };
  return g;
}
const px = (g) => +/(\d+(?:\.\d+)?)px/.exec(g.font)[1];

test('fitFont keeps a size that already fits, measuring once', () => {
  const g = ctx();
  assert.equal(fitFont(g, '가게', 400, 60, 8, 700), 60);
  assert.equal(px(g), 60); assert.equal(g.measures, 1);
  assert.match(g.font, /^700 60px /);
});

test('fitFont returns the largest size that fits, set on the context, in a few measures', () => {
  for (const [text, maxW] of [['연무동 행정복지센터', 420], ['바로편의점 24시 · 택배 · 복권', 512 * 0.84], ['한빛아파트 103동', 300]]) {
    const g = ctx(), s = fitFont(g, text, maxW, 160, 8, 800);
    assert.equal(px(g), s);
    assert.ok(g.measureText(text).width <= maxW, `${text} fits`);
    g.font = `800 ${s + 1}px x`;
    assert.ok(g.measureText(text).width > maxW, `${text}: one size larger would not fit`);
    assert.ok(g.measures <= 6, `${text}: ${g.measures} measures`);
  }
});

test('fitFont stops at the minimum size even when the text never fits', () => {
  const g = ctx();
  assert.equal(fitFont(g, '아주 긴 안내문이 들어가는 좁은 표지판입니다', 20, 104, 40, 900), 40);
  assert.equal(px(g), 40);
});

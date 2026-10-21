// Emergency contact card: a paper card on the desk. Tick a field to write it on the card, pick where the card
// is kept, and see what stays readable once the phone is dead. Resolves { card: { id: value }, where }.
import { el, put, modal, header, ladderHelp, reduced, announce } from './mini-dom.js';

const WHERE = { bag: ['배낭 앞주머니', '대피할 때 배낭과 함께 나갑니다.'], home: ['책상 서랍', '집 안에 있을 때만 꺼내 볼 수 있습니다.'] };

export async function docs({ mode = 'guided', fields = [], carry = ['bag', 'home'] } = {}) {
  const on = new Set();
  let where = carry.includes('bag') ? 'bag' : carry[0] || 'home';
  const result = () => ({ card: Object.fromEntries(fields.filter(f => on.has(f.id)).map(f => [f.id, f.value])), where });
  const m = modal({ className: `mg-docs is-${mode}`, label: '비상연락카드 쓰기', onKey: (e) => { if (e.key === 'Escape') { m.close(result()); return true; } } });
  const rm = reduced();

  const paper = el('ol', { class: 'mg-card-rows', 'aria-label': '카드에 적은 내용' });
  const phoneOnly = el('ul', { class: 'mg-phone-only' });
  const phoneCount = el('b');

  const list = el('fieldset', { class: 'mg-fields' }, el('legend', {}, '카드에 적을 것'), fields.map((f, i) => {
    const id = `mg-doc-${i}`;
    const box = el('input', { type: 'checkbox', id, 'data-autofocus': i === 0 });
    box.addEventListener('change', () => { box.checked ? on.add(f.id) : on.delete(f.id); render(f.id); announce(box.checked ? `${f.label}을(를) 적었다.` : `${f.label}을(를) 지웠다.`); });
    return el('div', { class: `mg-field${mode === 'guided' && f.recommended ? ' is-rec' : ''}` },
      box,
      el('label', { for: id },
        el('b', {}, f.label, mode === 'guided' && f.recommended ? el('span', { class: 'mg-badge is-rec' }, '★ 권장') : null),
        el('small', { class: 'mg-spec' }, f.value),
        f.why && mode !== 'free' ? el('small', { class: 'mg-tip' }, f.why) : null));
  }));

  function render(justWrote) {
    paper.replaceChildren(...fields.filter(f => on.has(f.id)).map(f => el('li', { class: f.id === justWrote && !rm ? 'is-new' : '' }, el('span', { class: 'mg-card-k' }, f.label), el('span', { class: 'mg-card-v' }, f.value))));
    if (!on.size) paper.append(el('li', { class: 'mg-empty' }, '아직 아무것도 적지 않았다.'));
    const left = fields.filter(f => !on.has(f.id));
    phoneCount.textContent = `${on.size}/${fields.length}개 항목`;
    phoneOnly.replaceChildren(...left.map(f => el('li', {}, el('span', { 'aria-hidden': 'true' }, '✕ '), f.label)));
    if (!left.length) phoneOnly.append(el('li', { class: 'is-ok' }, el('span', { 'aria-hidden': 'true' }, '✓ '), '모두 종이에 적었다.'));
  }

  const whereSet = el('fieldset', { class: 'mg-where' }, el('legend', {}, '카드를 둘 곳'), carry.filter(k => WHERE[k]).map(k => {
    const id = `mg-where-${k}`;
    const r = el('input', { type: 'radio', name: 'mg-where', id, value: k, checked: k === where });
    r.addEventListener('change', () => { if (r.checked) where = k; });
    return el('label', { class: 'mg-radio-opt', for: id }, r, el('span', {}, el('b', {}, WHERE[k][0]), mode !== 'free' ? el('small', {}, WHERE[k][1]) : null));
  }));

  const doneBtn = el('button', { type: 'button', class: 'mg-btn mg-primary' }, '카드 다 썼다');
  doneBtn.addEventListener('click', () => m.close(result()));
  const help = ladderHelp(mode, () => [el('p', {}, '휴대폰이 꺼져도 볼 수 있는 것은 종이에 적은 것뿐입니다. 꼭 필요한 연락처와 장소부터 적어 보세요.'),
    mode === 'hint' ? el('p', { class: 'mg-why' }, '각 항목 아래에 왜 필요한지가 함께 보입니다.') : null]);

  put(m.root,
    header('서류·연락처 정리', '비상연락카드', mode, 'mg-docs-title'),
    el('div', { class: 'mg-docs-grid' },
      el('div', { class: 'mg-docs-left' }, help, list, whereSet),
      el('div', { class: 'mg-desk' },
        el('article', { class: 'mg-paper', 'aria-label': '비상연락카드' },
          el('h3', {}, '비상연락카드'), el('p', { class: 'mg-paper-sub' }, '지갑 크기 종이 한 장 · 볼펜으로'), paper),
        el('section', { class: 'mg-deadphone', 'aria-label': '휴대폰이 꺼졌을 때' },
          el('div', { class: 'mg-phone-ico', 'aria-hidden': 'true' }, el('span', {}, '0%')),
          el('div', {}, el('p', {}, '휴대폰이 꺼져도 카드로 볼 수 있음: ', phoneCount),
            el('p', { class: 'mg-why' }, '휴대폰 속에만 있어 꺼지면 볼 수 없는 것:'), phoneOnly)))),
    el('footer', { class: 'mg-foot' }, el('p', { class: 'mg-keys' }, 'Tab 이동 · Space 적기/지우기 · ↑↓ 보관 장소 · Esc 닫기'), el('div', { class: 'mg-actions' }, doneBtn)));
  m.root.setAttribute('aria-labelledby', 'mg-docs-title');
  render();
  return m.done;
}

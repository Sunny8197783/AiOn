// Wall-calendar planner: time slots as tiles, actions as cards. Choosing shows the cost and what it gives up.
import { el, modal, icon, btn, uiSound } from './dom.js';

// planner({ day, slots: { total, used }, actions: [{ id, label, desc, cost, done, disabled, reason, icon }], hint }) -> Promise<actionId>
export function planner({ day = '', slots = { total: 6, used: 0 }, actions = [], hint = '' } = {}) {
  const total = Math.max(0, slots?.total ?? 6), used = Math.min(total, Math.max(0, slots?.used ?? 0));
  const left = total - used;
  let selected = null;

  const tiles = Array.from({ length: total }, (_, n) => el('span', { class: `slot${n < used ? ' used' : ''}` },
    el('span', { class: 'slot-no' }, `${n + 1}`), el('span', { class: 'slot-state' }, n < used ? '지남' : '')));
  const slotRow = el('div', { class: 'slot-row', role: 'img', 'aria-label': `준비 시간 ${total}칸 가운데 ${used}칸을 썼고 ${left}칸이 남았습니다` }, tiles);

  const detail = el('aside', { class: 'planner-detail', 'aria-live': 'polite' });
  const cards = actions.map((a) => {
    const blocked = a.done || a.disabled;
    const card = el('button', { type: 'button', class: `plan-card${a.done ? ' done' : ''}${a.disabled && !a.done ? ' blocked' : ''}`, 'data-id': a.id, 'aria-disabled': blocked ? 'true' : null },
      el('span', { class: 'plan-icon', 'aria-hidden': 'true' }, a.icon || '•'),
      el('span', { class: 'plan-main' },
        el('b', { class: 'plan-label' }, a.label),
        el('span', { class: 'plan-desc' }, a.desc || ''),
        a.done ? el('span', { class: 'plan-status ok' }, icon('check'), '이미 했음')
          : a.disabled ? el('span', { class: 'plan-status no' }, icon('lock'), a.reason || '지금은 할 수 없음') : null),
      el('span', { class: 'plan-cost', 'aria-label': `${a.cost}칸 필요` }, `${a.cost}칸`));
    card.addEventListener('mouseenter', () => preview(a));
    card.addEventListener('focus', () => preview(a));
    card.addEventListener('click', () => choose(a, card));
    return card;
  });
  const grid = el('div', { class: 'planner-grid', role: 'group', 'aria-label': '할 일' }, cards);
  grid.addEventListener('mouseleave', () => preview(selected));

  const board = el('div', { class: 'planner-board' },
    el('header', { class: 'planner-head' },
      el('p', { class: 'eyebrow' }, icon('calendar'), '준비 계획표'),
      btn('닫기', { ghost: true, className: 'planner-close', onclick: () => m.close(null) }),
      el('h2', {}, day),
      el('p', { class: 'planner-sub' }, `남은 칸 `, el('b', {}, `${left}`), ` / ${total} · 모든 일을 다 할 수는 없습니다.`)),
    slotRow,
    hint ? el('p', { class: 'planner-hint' }, icon('info'), el('span', {}, hint)) : null,
    el('div', { class: 'planner-body' }, grid, detail));

  const m = modal({ className: 'planner', label: `준비 계획표 · ${day}`, content: board, onKey });
  preview(null);

  function preview(a) {
    const cost = a && !a.done && !a.disabled ? a.cost : 0;
    tiles.forEach((t, n) => {
      const on = n >= used && n < used + cost;
      t.classList.toggle('preview', on);
      t.querySelector('.slot-state').textContent = n < used ? '지남' : on ? '사용' : '';
    });
    if (!selected || a === selected) renderDetail(a, a === selected && !!selected);
  }

  function renderDetail(a, confirming) {
    if (!a) { detail.replaceChildren(el('p', { class: 'muted' }, '할 일을 고르면 드는 시간과 대신 못 하게 되는 일을 보여 줍니다.')); return; }
    const after = left - a.cost;
    const lost = actions.filter((b) => b.id !== a.id && !b.done && !b.disabled && b.cost > after);
    const parts = [el('h3', {}, el('span', { 'aria-hidden': 'true' }, a.icon || ''), ' ', a.label), el('p', {}, a.desc || '')];
    if (a.done || a.disabled) {
      parts.push(el('p', { class: 'plan-status no' }, icon('lock'), a.done ? '이미 했습니다.' : (a.reason || '지금은 할 수 없습니다.')));
    } else {
      parts.push(el('dl', { class: 'plan-facts' },
        el('dt', {}, '드는 시간'), el('dd', {}, `${a.cost}칸`),
        el('dt', {}, '남는 칸'), el('dd', {}, after > 0 ? `${after}칸` : '없음 · 준비 시간이 끝납니다')));
      if (lost.length) parts.push(el('div', { class: 'plan-lost' }, el('b', {}, '이걸 하면 못 하게 되는 일'), el('ul', {}, lost.map((b) => el('li', {}, b.label)))));
      if (confirming) {
        parts.push(el('div', { class: 'plan-confirm' },
          btn('이 일을 한다', { primary: true, iconName: 'check', onclick: () => { uiSound('confirm'); m.close(a.id); } }),
          btn('다시 고르기', { ghost: true, onclick: () => back() })));
      }
    }
    detail.replaceChildren(...parts);
  }

  function choose(a, card) {
    if (a.done || a.disabled) { card.classList.remove('nope'); void card.offsetWidth; card.classList.add('nope'); uiSound('error'); selected = null; cards.forEach((c) => c.classList.remove('selected')); renderDetail(a, false); return; }
    selected = a;
    cards.forEach((c) => c.classList.toggle('selected', c === card));
    uiSound('select');
    preview(a);
    detail.querySelector('.plan-confirm .btn')?.focus();
  }
  function back() {
    const card = cards[actions.indexOf(selected)];
    selected = null;
    cards.forEach((c) => c.classList.remove('selected'));
    preview(null);
    card?.focus();
  }
  function onKey(e) {
    if (e.key === 'Escape') { e.preventDefault(); if (selected) back(); else m.close(null); return; }
    const at = cards.indexOf(document.activeElement);
    if (at < 0) return;
    const step = { ArrowRight: 1, ArrowDown: 1, ArrowLeft: -1, ArrowUp: -1 }[e.key];
    if (step) { e.preventDefault(); cards[(at + step + cards.length) % cards.length].focus(); }
  }
  // Start on the first card that can be chosen.
  (cards.find((c) => !c.hasAttribute('aria-disabled')) || cards[0])?.focus();
  return m.done;
}
